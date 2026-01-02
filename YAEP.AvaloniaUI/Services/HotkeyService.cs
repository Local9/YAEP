using Avalonia.Controls;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using YAEP.Interop;
using YAEP.Models;

namespace YAEP.Services
{
    /// <summary>
    /// Service for managing global hotkeys.
    /// </summary>
    public class HotkeyService
    {
        private const int HOTKEY_ID_BASE = 9000;
        private const int HOTKEY_ID_MAX = 9999;

        private const int ERROR_HOTKEY_ALREADY_REGISTERED = 1409;

        private const int INITIALIZATION_WAIT_ATTEMPTS = 100;
        private const int INITIALIZATION_WAIT_DELAY_MS = 100;
        private const int REGISTRATION_WAIT_ATTEMPTS = 200;
        private const int REGISTRATION_WAIT_DELAY_MS = 50;
        private const int THREAD_START_DELAY_MS = 100;
        private const int CLASS_RETRY_DELAY_MS = 10;
        private const int THREAD_JOIN_TIMEOUT_MS = 1000;

        private const uint VK_F1 = 0x70;
        private const uint VK_NUMPAD0 = 0x60;
        private const uint VK_SPACE = 0x20;
        private const uint VK_RETURN = 0x0D;
        private const uint VK_TAB = 0x09;
        private const uint VK_ESCAPE = 0x1B;
        private const uint VK_BACK = 0x08;
        private const uint VK_DELETE = 0x2E;
        private const uint VK_INSERT = 0x2D;
        private const uint VK_HOME = 0x24;
        private const uint VK_END = 0x23;
        private const uint VK_PRIOR = 0x21;
        private const uint VK_NEXT = 0x22;
        private const uint VK_UP = 0x26;
        private const uint VK_DOWN = 0x28;
        private const uint VK_LEFT = 0x25;
        private const uint VK_RIGHT = 0x27;

        private readonly DatabaseService _databaseService;
        private readonly IThumbnailWindowService _thumbnailWindowService;
        private IntPtr _windowHandle = IntPtr.Zero;
        private IntPtr _messageWindowHandle = IntPtr.Zero;
        private Thread? _messageLoopThread;
        private bool _isDisposed = false;
        private readonly object _lockObject = new object();
        private WndProc? _wndProcDelegate;
        private Dictionary<int, long> _hotkeyIdToGroupId = new Dictionary<int, long>();
        private Dictionary<long, int> _groupIdToForwardHotkeyId = new Dictionary<long, int>();
        private Dictionary<long, int> _groupIdToBackwardHotkeyId = new Dictionary<long, int>();
        private Dictionary<int, long> _hotkeyIdToProfileId = new Dictionary<int, long>();
        private Dictionary<long, int> _profileIdToHotkeyId = new Dictionary<long, int>();

        public HotkeyService(DatabaseService databaseService, IThumbnailWindowService thumbnailWindowService)
        {
            _databaseService = databaseService;
            _thumbnailWindowService = thumbnailWindowService;
        }

        /// <summary>
        /// Initializes the hotkey service with a window handle.
        /// </summary>
        [SupportedOSPlatform("windows")]
        public void Initialize(Window window)
        {
            if (window == null)
                return;

            SetupMessageHook();

            Task.Run(async () =>
            {
                int attempts = 0;
                IntPtr windowHandle = IntPtr.Zero;
                while (windowHandle == IntPtr.Zero && attempts < INITIALIZATION_WAIT_ATTEMPTS)
                {
                    await Task.Delay(INITIALIZATION_WAIT_DELAY_MS);
                    lock (_lockObject)
                    {
                        windowHandle = _windowHandle;
                    }
                    attempts++;
                }

                if (windowHandle != IntPtr.Zero)
                {
                    RegisterHotkeys();
                }
            });
        }

        [SupportedOSPlatform("windows")]
        private void SetupMessageHook()
        {
            lock (_lockObject)
            {
                if (_messageLoopThread != null && _messageLoopThread.IsAlive)
                    return;

                _messageLoopThread = new Thread(MessageLoopThread)
                {
                    IsBackground = true,
                    Name = "HotkeyMessageLoop"
                };

                try
                {
                    _messageLoopThread.SetApartmentState(ApartmentState.STA);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to set apartment state: {ex.Message}");
                }

                _messageLoopThread.Start();
                Thread.Sleep(THREAD_START_DELAY_MS);
            }
        }

        [SupportedOSPlatform("windows")]
        private void MessageLoopThread()
        {
            try
            {
                string className = "YAEP_HotkeyWindow_" + Guid.NewGuid().ToString("N");
                _wndProcDelegate = MessageWindowProc;

                IntPtr hInstance = User32NativeMethods.GetModuleHandle(null);

                WNDCLASSEX wc = default(WNDCLASSEX);
                wc.cbSize = (uint)Marshal.SizeOf(typeof(WNDCLASSEX));
                wc.style = User32NativeMethods.CS_HREDRAW | User32NativeMethods.CS_VREDRAW;
                wc.lpfnWndProc = _wndProcDelegate;
                wc.cbClsExtra = 0;
                wc.cbWndExtra = 0;
                wc.hInstance = hInstance;
                wc.hIcon = IntPtr.Zero;
                wc.hCursor = IntPtr.Zero;
                wc.hbrBackground = IntPtr.Zero;
                wc.lpszMenuName = null;
                wc.lpszClassName = className;
                wc.hIconSm = IntPtr.Zero;

                ushort atom = User32NativeMethods.RegisterClassEx(ref wc);
                int error = Marshal.GetLastWin32Error();

                if (atom == 0)
                {
                    string errorMessage = GetErrorMessage(error);

                    if (error == 0)
                    {
                        User32NativeMethods.UnregisterClass(className, hInstance);
                        Thread.Sleep(CLASS_RETRY_DELAY_MS);
                        atom = User32NativeMethods.RegisterClassEx(ref wc);
                        error = Marshal.GetLastWin32Error();

                        if (atom == 0)
                        {
                            Debug.WriteLine($"Failed to register window class after retry. Error: {error} (0x{error:X}) - {errorMessage}");
                            return;
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"Failed to register window class. Error: {error} (0x{error:X}) - {errorMessage}");
                        return;
                    }
                }

                _messageWindowHandle = User32NativeMethods.CreateWindowEx(
                    User32NativeMethods.WS_EX_NOACTIVATE,
                    className,
                    "YAEP Hotkey Window",
                    0,
                    0, 0, 0, 0,
                    User32NativeMethods.HWND_MESSAGE,
                    IntPtr.Zero,
                    User32NativeMethods.GetModuleHandle(null),
                    IntPtr.Zero);

                if (_messageWindowHandle == IntPtr.Zero)
                {
                    int createError = Marshal.GetLastWin32Error();
                    Debug.WriteLine($"Failed to create message-only window. Error: {createError} (0x{createError:X})");
                    User32NativeMethods.UnregisterClass(className, User32NativeMethods.GetModuleHandle(null));
                    return;
                }

                lock (_lockObject)
                {
                    _windowHandle = _messageWindowHandle;
                }

                MSG msg;
                while (!_isDisposed)
                {
                    int result = User32NativeMethods.GetMessage(
                        out msg,
                        _messageWindowHandle,
                        0,
                        0);

                    if (result <= 0)
                        break;

                    User32NativeMethods.TranslateMessage(ref msg);
                    User32NativeMethods.DispatchMessage(ref msg);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in hotkey message loop: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
            }
            finally
            {
                if (_messageWindowHandle != IntPtr.Zero)
                {
                    User32NativeMethods.DestroyWindow(_messageWindowHandle);
                    _messageWindowHandle = IntPtr.Zero;
                }
            }
        }

        [SupportedOSPlatform("windows")]
        private IntPtr MessageWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == User32NativeMethods.WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                lock (_lockObject)
                {
                    // Check if it's a profile hotkey first
                    if (_hotkeyIdToProfileId.TryGetValue(id, out long profileId))
                    {
                        Task.Run(() => SwitchProfile(profileId));
                    }
                    // Otherwise check if it's a group hotkey
                    else if (_hotkeyIdToGroupId.TryGetValue(id, out long groupId))
                    {
                        bool forward = _groupIdToForwardHotkeyId.ContainsKey(groupId) && _groupIdToForwardHotkeyId[groupId] == id;
                        Task.Run(() => CycleGroup(groupId, forward));
                    }
                }
                return IntPtr.Zero;
            }
            else if (msg == User32NativeMethods.WM_REGISTER_HOTKEYS)
            {
                try
                {
                    RegisterHotkeysInternal();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Exception in RegisterHotkeysInternal: {ex.Message}");
                    Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                    if (ex.InnerException != null)
                    {
                        Debug.WriteLine($"Inner exception: {ex.InnerException.Message}");
                    }
                }
                return IntPtr.Zero;
            }
            else if (msg == User32NativeMethods.WM_UNREGISTER_HOTKEYS)
            {
                try
                {
                    UnregisterHotkeysInternal();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Exception in UnregisterHotkeysInternal: {ex.Message}");
                    Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                    if (ex.InnerException != null)
                    {
                        Debug.WriteLine($"Inner exception: {ex.InnerException.Message}");
                    }
                }
                return IntPtr.Zero;
            }

            return User32NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
        }

        /// <summary>
        /// Registers hotkeys from all groups in the database.
        /// </summary>
        [SupportedOSPlatform("windows")]
        public void RegisterHotkeys()
        {
            SetupMessageHook();

            IntPtr windowHandle;
            lock (_lockObject)
            {
                windowHandle = _windowHandle;
            }

            if (windowHandle == IntPtr.Zero)
            {
                int attempts = 0;
                while (windowHandle == IntPtr.Zero && attempts < REGISTRATION_WAIT_ATTEMPTS)
                {
                    Thread.Sleep(REGISTRATION_WAIT_DELAY_MS);
                    lock (_lockObject)
                    {
                        windowHandle = _windowHandle;
                    }
                    attempts++;
                }

                if (windowHandle == IntPtr.Zero)
                    return;
            }

            if (!User32NativeMethods.PostMessage(windowHandle, User32NativeMethods.WM_REGISTER_HOTKEYS, IntPtr.Zero, IntPtr.Zero))
            {
                int postError = Marshal.GetLastWin32Error();
                Debug.WriteLine($"Failed to post message. Error: {postError} (0x{postError:X})");
            }
        }

        private void RegisterHotkeysInternal()
        {
            IntPtr windowHandle;
            lock (_lockObject)
            {
                windowHandle = _windowHandle;
            }

            if (windowHandle == IntPtr.Zero)
                return;

            UnregisterHotkeysInternal();

            // Register profile hotkeys first (all profiles, not just active)
            List<Profile> profiles = _databaseService.GetProfiles();
            int hotkeyId = HOTKEY_ID_BASE;

            foreach (Profile profile in profiles)
            {
                if (!string.IsNullOrWhiteSpace(profile.SwitchHotkey))
                {
                    if (TryParseHotkey(profile.SwitchHotkey, out int modifiers, out uint vk))
                    {
                        if (hotkeyId >= HOTKEY_ID_MAX)
                        {
                            Debug.WriteLine($"Warning: Maximum hotkey ID reached, cannot register more hotkeys");
                            break;
                        }

                        if (User32NativeMethods.RegisterHotKey(windowHandle, hotkeyId, modifiers, vk))
                        {
                            lock (_lockObject)
                            {
                                _hotkeyIdToProfileId[hotkeyId] = profile.Id;
                                _profileIdToHotkeyId[profile.Id] = hotkeyId;
                            }
                            hotkeyId++;
                        }
                        else
                        {
                            int regError = Marshal.GetLastWin32Error();
                            if (regError != ERROR_HOTKEY_ALREADY_REGISTERED)
                            {
                                string errorMsg = GetErrorMessage(regError);
                                Debug.WriteLine($"Failed to register hotkey for profile '{profile.Name}': {profile.SwitchHotkey}. Error: {regError} (0x{regError:X}) - {errorMsg}");
                            }
                        }
                    }
                }
            }

            // Register group hotkeys for the active profile
            Profile? activeProfile = _databaseService.GetActiveProfile() ?? _databaseService.CurrentProfile;
            if (activeProfile != null)
            {
                List<DatabaseService.ClientGroupWithMembers> groups = _databaseService.GetClientGroupsWithMembers(activeProfile.Id);

                foreach (DatabaseService.ClientGroupWithMembers groupWithMembers in groups)
                {
                    DatabaseService.ClientGroup group = groupWithMembers.Group;

                    if (!string.IsNullOrWhiteSpace(group.CycleForwardHotkey))
                    {
                        if (TryParseHotkey(group.CycleForwardHotkey, out int modifiers, out uint vk))
                        {
                            if (hotkeyId >= HOTKEY_ID_MAX)
                            {
                                Debug.WriteLine($"Warning: Maximum hotkey ID reached, cannot register more hotkeys");
                                break;
                            }

                            if (User32NativeMethods.RegisterHotKey(windowHandle, hotkeyId, modifiers, vk))
                            {
                                lock (_lockObject)
                                {
                                    _hotkeyIdToGroupId[hotkeyId] = group.Id;
                                    _groupIdToForwardHotkeyId[group.Id] = hotkeyId;
                                }
                                hotkeyId++;
                            }
                            else
                            {
                                int regError = Marshal.GetLastWin32Error();
                                if (regError != ERROR_HOTKEY_ALREADY_REGISTERED)
                                {
                                    string errorMsg = GetErrorMessage(regError);
                                    Debug.WriteLine($"Failed to register forward hotkey for group '{group.Name}': {group.CycleForwardHotkey}. Error: {regError} (0x{regError:X}) - {errorMsg}");
                                }
                            }
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(group.CycleBackwardHotkey))
                    {
                        if (TryParseHotkey(group.CycleBackwardHotkey, out int modifiers, out uint vk))
                        {
                            if (hotkeyId >= HOTKEY_ID_MAX)
                            {
                                Debug.WriteLine($"Warning: Maximum hotkey ID reached, cannot register more hotkeys");
                                break;
                            }

                            if (User32NativeMethods.RegisterHotKey(windowHandle, hotkeyId, modifiers, vk))
                            {
                                lock (_lockObject)
                                {
                                    _hotkeyIdToGroupId[hotkeyId] = group.Id;
                                    _groupIdToBackwardHotkeyId[group.Id] = hotkeyId;
                                }
                                hotkeyId++;
                            }
                            else
                            {
                                int regError = Marshal.GetLastWin32Error();
                                if (regError != ERROR_HOTKEY_ALREADY_REGISTERED)
                                {
                                    string errorMsg = GetErrorMessage(regError);
                                    Debug.WriteLine($"Failed to register backward hotkey for group '{group.Name}': {group.CycleBackwardHotkey}. Error: {regError} (0x{regError:X}) - {errorMsg}");
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Unregisters all hotkeys. Can be called from any thread.
        /// </summary>
        [SupportedOSPlatform("windows")]
        public void UnregisterHotkeys()
        {
            IntPtr windowHandle;
            lock (_lockObject)
            {
                windowHandle = _windowHandle;
            }

            if (windowHandle == IntPtr.Zero)
                return;

            if (!User32NativeMethods.PostMessage(windowHandle, User32NativeMethods.WM_UNREGISTER_HOTKEYS, IntPtr.Zero, IntPtr.Zero))
            {
                int postError = Marshal.GetLastWin32Error();
                Debug.WriteLine($"Failed to post message. Error: {postError} (0x{postError:X})");
            }
        }

        private void UnregisterHotkeysInternal()
        {
            IntPtr windowHandle;
            List<int> hotkeyIds;

            lock (_lockObject)
            {
                windowHandle = _windowHandle;
                hotkeyIds = new List<int>(_hotkeyIdToGroupId.Keys);
            }

            if (windowHandle == IntPtr.Zero)
                return;

            foreach (int hotkeyId in hotkeyIds)
            {
                User32NativeMethods.UnregisterHotKey(windowHandle, hotkeyId);
            }

            lock (_lockObject)
            {
                _hotkeyIdToGroupId.Clear();
                _groupIdToForwardHotkeyId.Clear();
                _groupIdToBackwardHotkeyId.Clear();
                _hotkeyIdToProfileId.Clear();
                _profileIdToHotkeyId.Clear();
            }
        }

        private void CycleGroup(long groupId, bool forward)
        {
            Profile? activeProfile = _databaseService.GetActiveProfile() ?? _databaseService.CurrentProfile;
            if (activeProfile == null)
                return;

            List<DatabaseService.ClientGroupWithMembers> groups = _databaseService.GetClientGroupsWithMembers(activeProfile.Id);
            DatabaseService.ClientGroupWithMembers? targetGroup = groups.FirstOrDefault(g => g.Group.Id == groupId);
            if (targetGroup == null)
                return;

            List<string> activeWindowTitles = _thumbnailWindowService.GetActiveThumbnailWindowTitles();
            HashSet<string> activeTitlesSet = new HashSet<string>(activeWindowTitles, StringComparer.OrdinalIgnoreCase);

            IntPtr currentForeground = User32NativeMethods.GetForegroundWindow();
            string? currentWindowTitle = null;

            try
            {
                Process[] processes = System.Diagnostics.Process.GetProcesses();
                foreach (Process process in processes)
                {
                    try
                    {
                        if (process.MainWindowHandle == currentForeground && !string.IsNullOrEmpty(process.MainWindowTitle))
                        {
                            currentWindowTitle = process.MainWindowTitle;
                            break;
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }

            List<string> allClientsInOrder = targetGroup.Members
                .OrderBy(m => m.DisplayOrder)
                .Select(m => m.WindowTitle)
                .Where(t => activeTitlesSet.Contains(t))
                .ToList();

            if (allClientsInOrder.Count == 0)
                return;

            int currentIndex = -1;
            if (!string.IsNullOrEmpty(currentWindowTitle))
            {
                currentIndex = allClientsInOrder.FindIndex(c =>
                    string.Equals(c, currentWindowTitle, StringComparison.OrdinalIgnoreCase));
            }

            if (currentIndex < 0)
            {
                currentIndex = forward ? -1 : allClientsInOrder.Count;
            }

            int nextIndex;
            if (forward)
            {
                nextIndex = (currentIndex + 1) % allClientsInOrder.Count;
            }
            else
            {
                nextIndex = currentIndex <= 0 ? allClientsInOrder.Count - 1 : currentIndex - 1;
            }

            string nextWindowTitle = allClientsInOrder[nextIndex];
            ActivateClientByWindowTitle(nextWindowTitle);
        }

        private void ActivateClientByWindowTitle(string windowTitle)
        {
            Process[] processes = System.Diagnostics.Process.GetProcesses();
            foreach (Process process in processes)
            {
                try
                {
                    if (process.MainWindowTitle == windowTitle && process.MainWindowHandle != IntPtr.Zero)
                    {
                        User32NativeMethods.SetForegroundWindow(process.MainWindowHandle);
                        User32NativeMethods.SetFocus(process.MainWindowHandle);

                        int style = User32NativeMethods.GetWindowLong(process.MainWindowHandle, InteropConstants.GWL_STYLE);
                        if ((style & InteropConstants.WS_MINIMIZE) == InteropConstants.WS_MINIMIZE)
                        {
                            User32NativeMethods.ShowWindowAsync(new HandleRef(null, process.MainWindowHandle), InteropConstants.SW_SHOWNORMAL);
                        }

                        break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error activating client '{windowTitle}': {ex.Message}");
                }
            }
        }

        [SupportedOSPlatform("windows")]
        private void SwitchProfile(long profileId)
        {
            Profile? profile = _databaseService.GetProfile(profileId);
            if (profile == null || profile.IsDeleted)
                return;

            Profile? activeProfile = _databaseService.GetActiveProfile();
            if (activeProfile?.Id == profileId)
                return;

            _databaseService.SetCurrentProfile(profileId);

            RegisterHotkeys();
        }

        private bool TryParseHotkey(string hotkeyString, out int modifiers, out uint vk)
        {
            modifiers = 0;
            vk = 0;

            if (string.IsNullOrWhiteSpace(hotkeyString))
                return false;

            string[] parts = hotkeyString.Split('+');

            if (parts.Length < 1)
                return false;

            int keyIndex = parts.Length - 1;
            if (parts.Length > 1)
            {
                for (int i = 0; i < parts.Length - 1; i++)
                {
                    string mod = parts[i].Trim().ToLower();
                    if (mod == "ctrl" || mod == "control")
                        modifiers |= User32NativeMethods.MOD_CONTROL;
                    else if (mod == "alt")
                        modifiers |= User32NativeMethods.MOD_ALT;
                    else if (mod == "shift")
                        modifiers |= User32NativeMethods.MOD_SHIFT;
                    else if (mod == "win" || mod == "windows")
                        modifiers |= User32NativeMethods.MOD_WIN;
                }
            }

            string keyPart = parts[keyIndex].Trim();

            if (keyPart.StartsWith("F", StringComparison.OrdinalIgnoreCase) && keyPart.Length > 1)
            {
                if (int.TryParse(keyPart.Substring(1), out int fNumber) && fNumber >= 1 && fNumber <= 24)
                {
                    vk = VK_F1 + (uint)(fNumber - 1);
                    return true;
                }
            }
            else if (keyPart.StartsWith("NumPad", StringComparison.OrdinalIgnoreCase) && keyPart.Length > 6)
            {
                string numPart = keyPart.Substring(6);
                if (int.TryParse(numPart, out int numPadNumber) && numPadNumber >= 0 && numPadNumber <= 9)
                {
                    vk = VK_NUMPAD0 + (uint)numPadNumber;
                    return true;
                }
            }
            else if (TryParseSpecialKey(keyPart, out uint specialVk))
            {
                vk = specialVk;
                return true;
            }
            else if (keyPart.Length == 1)
            {
                char keyChar = keyPart[0];
                if (keyChar >= 'A' && keyChar <= 'Z')
                {
                    vk = (uint)keyChar;
                    return true;
                }
                else if (keyChar >= '0' && keyChar <= '9')
                {
                    vk = (uint)keyChar;
                    return true;
                }
            }

            return false;
        }

        private bool TryParseSpecialKey(string keyName, out uint vk)
        {
            vk = 0;
            string upperKey = keyName.ToUpperInvariant();

            switch (upperKey)
            {
                case "SPACE":
                    vk = VK_SPACE;
                    return true;
                case "ENTER":
                    vk = VK_RETURN;
                    return true;
                case "TAB":
                    vk = VK_TAB;
                    return true;
                case "ESCAPE":
                case "ESC":
                    vk = VK_ESCAPE;
                    return true;
                case "BACKSPACE":
                case "BACK":
                    vk = VK_BACK;
                    return true;
                case "DELETE":
                case "DEL":
                    vk = VK_DELETE;
                    return true;
                case "INSERT":
                case "INS":
                    vk = VK_INSERT;
                    return true;
                case "HOME":
                    vk = VK_HOME;
                    return true;
                case "END":
                    vk = VK_END;
                    return true;
                case "PAGEUP":
                case "PGUP":
                    vk = VK_PRIOR;
                    return true;
                case "PAGEDOWN":
                case "PGDN":
                    vk = VK_NEXT;
                    return true;
                case "UP":
                    vk = VK_UP;
                    return true;
                case "DOWN":
                    vk = VK_DOWN;
                    return true;
                case "LEFT":
                    vk = VK_LEFT;
                    return true;
                case "RIGHT":
                    vk = VK_RIGHT;
                    return true;
                default:
                    return false;
            }
        }

        private string GetErrorMessage(int errorCode)
        {
            if (errorCode == 0)
                return "Unknown error";

            IntPtr buffer = IntPtr.Zero;
            uint result = User32NativeMethods.FormatMessage(
                User32NativeMethods.FORMAT_MESSAGE_ALLOCATE_BUFFER |
                User32NativeMethods.FORMAT_MESSAGE_FROM_SYSTEM |
                User32NativeMethods.FORMAT_MESSAGE_IGNORE_INSERTS,
                IntPtr.Zero,
                (uint)errorCode,
                0,
                out buffer,
                0,
                IntPtr.Zero);

            if (result > 0 && buffer != IntPtr.Zero)
            {
                string errorMsg = Marshal.PtrToStringUni(buffer) ?? "Unknown error";
                User32NativeMethods.LocalFree(buffer);
                return errorMsg;
            }

            return "Unknown error";
        }

        /// <summary>
        /// Cleans up resources.
        /// </summary>
        [SupportedOSPlatform("windows")]
        public void Dispose()
        {
            lock (_lockObject)
            {
                if (_isDisposed)
                    return;

                _isDisposed = true;
                UnregisterHotkeys();

                if (_messageWindowHandle != IntPtr.Zero)
                {
                    User32NativeMethods.PostMessage(_messageWindowHandle, User32NativeMethods.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
                }

                if (_messageLoopThread != null && _messageLoopThread.IsAlive)
                {
                    if (!_messageLoopThread.Join(THREAD_JOIN_TIMEOUT_MS))
                    {
                        Debug.WriteLine("Warning: Hotkey message loop thread did not exit gracefully");
                    }
                }

                if (_messageWindowHandle != IntPtr.Zero)
                {
                    User32NativeMethods.DestroyWindow(_messageWindowHandle);
                    _messageWindowHandle = IntPtr.Zero;
                }
            }
        }
    }
}

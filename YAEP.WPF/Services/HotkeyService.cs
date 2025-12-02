using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using YAEP.Interface;
using YAEP.Interop;

namespace YAEP.Services
{
    /// <summary>
    /// Service for managing global hotkeys.
    /// </summary>
    public class HotkeyService
    {
        private const int HOTKEY_ID_BASE = 9000;
        private const int HOTKEY_ID_MAX = 9999; // Support up to 500 groups (2 hotkeys each)

        private readonly DatabaseService _databaseService;
        private readonly IThumbnailWindowService _thumbnailWindowService;
        private HwndSource? _hwndSource;
        private IntPtr _windowHandle = IntPtr.Zero;
        private bool _isRegistered = false;
        private Dictionary<int, long> _hotkeyIdToGroupId = new Dictionary<int, long>(); // Maps hotkey ID to group ID
        private Dictionary<long, int> _groupIdToForwardHotkeyId = new Dictionary<long, int>(); // Maps group ID to forward hotkey ID
        private Dictionary<long, int> _groupIdToBackwardHotkeyId = new Dictionary<long, int>(); // Maps group ID to backward hotkey ID

        public HotkeyService(DatabaseService databaseService, IThumbnailWindowService thumbnailWindowService)
        {
            _databaseService = databaseService;
            _thumbnailWindowService = thumbnailWindowService;
        }

        /// <summary>
        /// Initializes the hotkey service with a window handle.
        /// </summary>
        public void Initialize(Window window)
        {
            if (window == null)
                return;

            _windowHandle = new WindowInteropHelper(window).Handle;
            if (_windowHandle == IntPtr.Zero)
            {
                window.Loaded += (s, e) =>
                {
                    _windowHandle = new WindowInteropHelper(window).Handle;
                    SetupMessageHook();
                    RegisterHotkeys();
                };
            }
            else
            {
                SetupMessageHook();
                RegisterHotkeys();
            }
        }

        /// <summary>
        /// Sets up the message hook for handling hotkey messages.
        /// </summary>
        private void SetupMessageHook()
        {
            if (_windowHandle == IntPtr.Zero)
                return;

            // Set up message hook
            HwndSource? source = HwndSource.FromHwnd(_windowHandle);
            if (source != null)
            {
                _hwndSource = source;
                _hwndSource.AddHook(WndProc);
            }
        }

        /// <summary>
        /// Registers hotkeys from all groups in the database.
        /// </summary>
        public void RegisterHotkeys()
        {
            if (_windowHandle == IntPtr.Zero)
                return;

            // Unregister existing hotkeys first
            UnregisterHotkeys();

            DatabaseService.Profile? activeProfile = _databaseService.GetActiveProfile() ?? _databaseService.CurrentProfile;
            if (activeProfile == null)
                return;

            List<DatabaseService.ClientGroupWithMembers> groups = _databaseService.GetClientGroupsWithMembers(activeProfile.Id);
            int hotkeyId = HOTKEY_ID_BASE;

            foreach (DatabaseService.ClientGroupWithMembers groupWithMembers in groups)
            {
                DatabaseService.ClientGroup group = groupWithMembers.Group;

                // Register forward hotkey for this group
                if (!string.IsNullOrWhiteSpace(group.CycleForwardHotkey))
                {
                    if (TryParseHotkey(group.CycleForwardHotkey, out int modifiers, out uint vk))
                    {
                        if (hotkeyId >= HOTKEY_ID_MAX)
                        {
                            Debug.WriteLine($"Warning: Maximum hotkey ID reached, cannot register more hotkeys");
                            break;
                        }

                        if (User32NativeMethods.RegisterHotKey(_windowHandle, hotkeyId, modifiers, vk))
                        {
                            _hotkeyIdToGroupId[hotkeyId] = group.Id;
                            _groupIdToForwardHotkeyId[group.Id] = hotkeyId;
                            _isRegistered = true;
                            Debug.WriteLine($"Registered forward hotkey for group '{group.Name}': {group.CycleForwardHotkey} (ID: {hotkeyId})");
                            hotkeyId++;
                        }
                        else
                        {
                            Debug.WriteLine($"Failed to register forward hotkey for group '{group.Name}': {group.CycleForwardHotkey}");
                        }
                    }
                }

                // Register backward hotkey for this group
                if (!string.IsNullOrWhiteSpace(group.CycleBackwardHotkey))
                {
                    if (TryParseHotkey(group.CycleBackwardHotkey, out int modifiers, out uint vk))
                    {
                        if (hotkeyId >= HOTKEY_ID_MAX)
                        {
                            Debug.WriteLine($"Warning: Maximum hotkey ID reached, cannot register more hotkeys");
                            break;
                        }

                        if (User32NativeMethods.RegisterHotKey(_windowHandle, hotkeyId, modifiers, vk))
                        {
                            _hotkeyIdToGroupId[hotkeyId] = group.Id;
                            _groupIdToBackwardHotkeyId[group.Id] = hotkeyId;
                            _isRegistered = true;
                            Debug.WriteLine($"Registered backward hotkey for group '{group.Name}': {group.CycleBackwardHotkey} (ID: {hotkeyId})");
                            hotkeyId++;
                        }
                        else
                        {
                            Debug.WriteLine($"Failed to register backward hotkey for group '{group.Name}': {group.CycleBackwardHotkey}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Unregisters all hotkeys.
        /// </summary>
        public void UnregisterHotkeys()
        {
            if (_windowHandle == IntPtr.Zero)
                return;

            // Unregister all hotkey IDs we've registered
            foreach (int hotkeyId in _hotkeyIdToGroupId.Keys)
            {
                User32NativeMethods.UnregisterHotKey(_windowHandle, hotkeyId);
            }

            _hotkeyIdToGroupId.Clear();
            _groupIdToForwardHotkeyId.Clear();
            _groupIdToBackwardHotkeyId.Clear();
            _isRegistered = false;
        }

        /// <summary>
        /// Window procedure hook to handle hotkey messages.
        /// </summary>
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == User32NativeMethods.WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (_hotkeyIdToGroupId.TryGetValue(id, out long groupId))
                {
                    // Determine if this is forward or backward
                    bool forward = _groupIdToForwardHotkeyId.ContainsKey(groupId) && _groupIdToForwardHotkeyId[groupId] == id;
                    CycleGroup(groupId, forward);
                    handled = true;
                }
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Cycles to the next/previous client in a specific group.
        /// </summary>
        private void CycleGroup(long groupId, bool forward)
        {
            DatabaseService.Profile? activeProfile = _databaseService.GetActiveProfile() ?? _databaseService.CurrentProfile;
            if (activeProfile == null)
                return;

            List<DatabaseService.ClientGroupWithMembers> groups = _databaseService.GetClientGroupsWithMembers(activeProfile.Id);
            DatabaseService.ClientGroupWithMembers? targetGroup = groups.FirstOrDefault(g => g.Group.Id == groupId);
            if (targetGroup == null)
                return;

            // Get all active window titles
            List<string> activeWindowTitles = _thumbnailWindowService.GetActiveThumbnailWindowTitles();
            HashSet<string> activeTitlesSet = new HashSet<string>(activeWindowTitles, StringComparer.OrdinalIgnoreCase);

            // Get current foreground window to determine which client is active
            IntPtr currentForeground = User32NativeMethods.GetForegroundWindow();
            string? currentWindowTitle = null;

            // Find the current window title by checking all processes
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
                        // Ignore errors accessing process properties
                    }
                }
            }
            catch
            {
                // Ignore errors
            }

            // Get all clients in order for this specific group only
            List<string> allClientsInOrder = targetGroup.Members
                .OrderBy(m => m.DisplayOrder)
                .Select(m => m.WindowTitle)
                .Where(t => activeTitlesSet.Contains(t))
                .ToList();

            if (allClientsInOrder.Count == 0)
                return;

            // Find current client index
            int currentIndex = -1;
            if (!string.IsNullOrEmpty(currentWindowTitle))
            {
                currentIndex = allClientsInOrder.FindIndex(c =>
                    string.Equals(c, currentWindowTitle, StringComparison.OrdinalIgnoreCase));
            }

            // If current window not found in list, start from beginning/end
            if (currentIndex < 0)
            {
                currentIndex = forward ? -1 : allClientsInOrder.Count;
            }

            // Determine next index
            int nextIndex;
            if (forward)
            {
                nextIndex = (currentIndex + 1) % allClientsInOrder.Count;
            }
            else
            {
                nextIndex = currentIndex <= 0 ? allClientsInOrder.Count - 1 : currentIndex - 1;
            }

            // Activate the next client
            string nextWindowTitle = allClientsInOrder[nextIndex];
            ActivateClientByWindowTitle(nextWindowTitle);
        }

        /// <summary>
        /// Activates a client by its window title.
        /// </summary>
        private void ActivateClientByWindowTitle(string windowTitle)
        {
            // Get all processes and find the one with matching window title
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

        /// <summary>
        /// Tries to parse a hotkey string (e.g., "Ctrl+Shift+F1" or "F1") into modifiers and virtual key code.
        /// </summary>
        private bool TryParseHotkey(string hotkeyString, out int modifiers, out uint vk)
        {
            modifiers = 0;
            vk = 0;

            if (string.IsNullOrWhiteSpace(hotkeyString))
                return false;

            string[] parts = hotkeyString.Split('+');

            // Support single keys (no modifiers) or combinations
            if (parts.Length < 1)
                return false;

            // Parse modifiers (if any)
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

            // Parse key
            string keyPart = parts[keyIndex].Trim().ToUpper();

            // Handle function keys
            if (keyPart.StartsWith("F") && keyPart.Length > 1)
            {
                if (int.TryParse(keyPart.Substring(1), out int fNumber) && fNumber >= 1 && fNumber <= 24)
                {
                    vk = (uint)(0x70 + fNumber - 1); // VK_F1 = 0x70
                    return true;
                }
            }
            // Handle regular keys
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

        /// <summary>
        /// Cleans up resources.
        /// </summary>
        public void Dispose()
        {
            UnregisterHotkeys();
            _hwndSource?.RemoveHook(WndProc);
            _hwndSource = null;
        }
    }
}


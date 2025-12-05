using Avalonia.Controls;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
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
        // private HwndSource? _hwndSource; // WPF-specific, needs Avalonia platform-specific implementation
        private IntPtr _windowHandle = IntPtr.Zero;
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

            // TODO: Get window handle for Avalonia - this requires platform-specific code
            // In Avalonia, we need to use platform-specific APIs to get the native window handle
            // For now, this is a placeholder that will need to be implemented
            try
            {
                // Try to get the window handle using Avalonia's platform abstraction
                // This will need platform-specific implementation
                Avalonia.Platform.IPlatformHandle? platformHandle = window.TryGetPlatformHandle();
                if (platformHandle != null)
                {
                    _windowHandle = platformHandle.Handle;
                    SetupMessageHook();
                    RegisterHotkeys();
                }
            }
            catch
            {
                // If we can't get the handle immediately, try when window is shown
                window.Opened += (s, e) =>
                {
                    try
                    {
                        Avalonia.Platform.IPlatformHandle? platformHandle = window.TryGetPlatformHandle();
                        if (platformHandle != null)
                        {
                            _windowHandle = platformHandle.Handle;
                            SetupMessageHook();
                            RegisterHotkeys();
                        }
                    }
                    catch
                    {
                        // Hotkey registration will fail silently if we can't get the handle
                    }
                };
            }
        }

        /// <summary>
        /// Sets up the message hook for handling hotkey messages.
        /// </summary>
        private void SetupMessageHook()
        {
            if (_windowHandle == IntPtr.Zero)
                return;

            // TODO: Implement message hook for Avalonia
            // In WPF, we used HwndSource.FromHwnd and AddHook
            // In Avalonia, we need to use platform-specific APIs
            // For now, this is commented out - hotkey functionality will need platform-specific implementation
            // HwndSource? source = HwndSource.FromHwnd(_windowHandle);
            // if (source != null)
            // {
            //     _hwndSource = source;
            //     _hwndSource.AddHook(WndProc);
            // }
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
            string keyPart = parts[keyIndex].Trim();

            // Handle function keys (F1-F24)
            if (keyPart.StartsWith("F", StringComparison.OrdinalIgnoreCase) && keyPart.Length > 1)
            {
                if (int.TryParse(keyPart.Substring(1), out int fNumber) && fNumber >= 1 && fNumber <= 24)
                {
                    vk = (uint)(0x70 + fNumber - 1); // VK_F1 = 0x70
                    return true;
                }
            }
            // Handle NumPad keys (NumPad0-NumPad9)
            else if (keyPart.StartsWith("NumPad", StringComparison.OrdinalIgnoreCase) && keyPart.Length > 6)
            {
                string numPart = keyPart.Substring(6);
                if (int.TryParse(numPart, out int numPadNumber) && numPadNumber >= 0 && numPadNumber <= 9)
                {
                    vk = (uint)(0x60 + numPadNumber); // VK_NUMPAD0 = 0x60
                    return true;
                }
            }
            // Handle special keys
            else if (TryParseSpecialKey(keyPart, out uint specialVk))
            {
                vk = specialVk;
                return true;
            }
            // Handle regular keys (single character)
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
        /// Tries to parse a special key string to its virtual key code.
        /// </summary>
        private bool TryParseSpecialKey(string keyName, out uint vk)
        {
            vk = 0;
            string upperKey = keyName.ToUpperInvariant();

            switch (upperKey)
            {
                case "SPACE":
                    vk = 0x20; // VK_SPACE
                    return true;
                case "ENTER":
                    vk = 0x0D; // VK_RETURN
                    return true;
                case "TAB":
                    vk = 0x09; // VK_TAB
                    return true;
                case "ESCAPE":
                case "ESC":
                    vk = 0x1B; // VK_ESCAPE
                    return true;
                case "BACKSPACE":
                case "BACK":
                    vk = 0x08; // VK_BACK
                    return true;
                case "DELETE":
                case "DEL":
                    vk = 0x2E; // VK_DELETE
                    return true;
                case "INSERT":
                case "INS":
                    vk = 0x2D; // VK_INSERT
                    return true;
                case "HOME":
                    vk = 0x24; // VK_HOME
                    return true;
                case "END":
                    vk = 0x23; // VK_END
                    return true;
                case "PAGEUP":
                case "PGUP":
                    vk = 0x21; // VK_PRIOR
                    return true;
                case "PAGEDOWN":
                case "PGDN":
                    vk = 0x22; // VK_NEXT
                    return true;
                case "UP":
                    vk = 0x26; // VK_UP
                    return true;
                case "DOWN":
                    vk = 0x28; // VK_DOWN
                    return true;
                case "LEFT":
                    vk = 0x25; // VK_LEFT
                    return true;
                case "RIGHT":
                    vk = 0x27; // VK_RIGHT
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Cleans up resources.
        /// </summary>
        public void Dispose()
        {
            UnregisterHotkeys();
            // _hwndSource?.RemoveHook(WndProc); // WPF-specific, commented out for Avalonia
            // _hwndSource = null;
        }
    }
}


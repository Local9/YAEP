using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using YAEP.Interop;

namespace YAEP.Services
{
    /// <summary>
    /// Service for managing a low-level keyboard hook to filter interfering keys when hotkeys are pressed.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class KeyboardHookService
    {
        private const uint VK_CONTROL = 0x11;
        private const uint VK_MENU = 0x12;
        private const uint VK_SHIFT = 0x10;
        private const uint VK_LWIN = 0x5B;
        private const uint VK_RWIN = 0x5C;

        private IntPtr _hookId = IntPtr.Zero;
        private LowLevelKeyboardProc? _hookProc;
        private readonly object _lockObject = new object();
        private HashSet<uint> _registeredHotkeyVKs = new HashSet<uint>();
        private HashSet<uint> _currentlyPressedKeys = new HashSet<uint>();
        private bool _isDisposed = false;

        /// <summary>
        /// Installs the low-level keyboard hook.
        /// </summary>
        /// <param name="threadId">The thread ID to install the hook on. Use 0 for global hook.</param>
        public void StartHook(uint threadId = 0)
        {
            lock (_lockObject)
            {
                if (_hookId != IntPtr.Zero || _isDisposed)
                    return;

                _hookProc = LowLevelKeyboardHookProc;
                IntPtr hMod = User32NativeMethods.GetModuleHandle(null);
                _hookId = User32NativeMethods.SetWindowsHookEx(
                    User32NativeMethods.WH_KEYBOARD_LL,
                    _hookProc,
                    hMod,
                    threadId);

                if (_hookId == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    Debug.WriteLine($"Failed to install keyboard hook. Error: {error} (0x{error:X})");
                }
            }
        }

        /// <summary>
        /// Removes the low-level keyboard hook.
        /// </summary>
        public void StopHook()
        {
            lock (_lockObject)
            {
                if (_hookId != IntPtr.Zero)
                {
                    User32NativeMethods.UnhookWindowsHookEx(_hookId);
                    _hookId = IntPtr.Zero;
                    _hookProc = null;
                }
            }
        }

        /// <summary>
        /// Sets the list of virtual key codes that are registered as hotkeys.
        /// </summary>
        /// <param name="vkCodes">List of virtual key codes registered as hotkeys.</param>
        public void SetRegisteredHotkeyVKs(List<uint> vkCodes)
        {
            lock (_lockObject)
            {
                _registeredHotkeyVKs = new HashSet<uint>(vkCodes);
            }
        }

        /// <summary>
        /// Low-level keyboard hook procedure.
        /// </summary>
        private IntPtr LowLevelKeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                uint message = (uint)wParam.ToInt32();
                KBDLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                uint vkCode = hookStruct.vkCode;

                lock (_lockObject)
                {
                    if (message == User32NativeMethods.WM_KEYDOWN || message == User32NativeMethods.WM_SYSKEYDOWN)
                    {
                        bool isRegisteredHotkey = _registeredHotkeyVKs.Contains(vkCode);

                        if (isRegisteredHotkey)
                        {
                            _currentlyPressedKeys.Add(vkCode);
                            return User32NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
                        }
                        else if (!IsModifierKey(vkCode))
                        {
                            if (_currentlyPressedKeys.Any(k => _registeredHotkeyVKs.Contains(k)))
                            {
                                _currentlyPressedKeys.Add(vkCode);
                                return (IntPtr)1;
                            }
                        }

                        _currentlyPressedKeys.Add(vkCode);
                    }
                    else if (message == User32NativeMethods.WM_KEYUP || message == User32NativeMethods.WM_SYSKEYUP)
                    {
                        _currentlyPressedKeys.Remove(vkCode);
                    }
                }
            }

            return User32NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private bool IsModifierKey(uint vk)
        {
            return vk == VK_CONTROL || vk == VK_MENU || vk == VK_SHIFT || vk == VK_LWIN || vk == VK_RWIN;
        }

        /// <summary>
        /// Cleans up resources.
        /// </summary>
        public void Dispose()
        {
            lock (_lockObject)
            {
                if (_isDisposed)
                    return;

                _isDisposed = true;
                StopHook();
                _registeredHotkeyVKs.Clear();
                _currentlyPressedKeys.Clear();
            }
        }
    }
}

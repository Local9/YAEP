using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using YAEP.Interop;

namespace YAEP.Services
{
    /// <summary>
    /// Service for managing a low-level keyboard hook to filter ignored keys.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class KeyboardHookService
    {
        private IntPtr _hookId = IntPtr.Zero;
        private LowLevelKeyboardProc? _hookProc;
        private readonly object _lockObject = new object();
        private HashSet<uint> _ignoredKeys = new HashSet<uint>();
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
        /// Sets the list of virtual key codes to ignore.
        /// </summary>
        /// <param name="vkCodes">List of virtual key codes to ignore.</param>
        public void SetIgnoredKeys(List<uint> vkCodes)
        {
            lock (_lockObject)
            {
                _ignoredKeys = new HashSet<uint>(vkCodes);
            }
        }

        /// <summary>
        /// Checks if a virtual key code is in the ignored list.
        /// </summary>
        /// <param name="vk">The virtual key code to check.</param>
        /// <returns>True if the key should be ignored, false otherwise.</returns>
        public bool IsKeyIgnored(uint vk)
        {
            lock (_lockObject)
            {
                return _ignoredKeys.Contains(vk);
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
                
                if (message == User32NativeMethods.WM_KEYDOWN || message == User32NativeMethods.WM_SYSKEYDOWN)
                {
                    KBDLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                    uint vkCode = hookStruct.vkCode;

                    lock (_lockObject)
                    {
                        if (_ignoredKeys.Contains(vkCode))
                        {
                            return (IntPtr)1;
                        }
                    }
                }
            }

            return User32NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
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
                _ignoredKeys.Clear();
            }
        }
    }
}

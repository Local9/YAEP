using System.Runtime.InteropServices;
using System.Threading;
using YAEP.Interop.Linux.X11;
using YAEP.Shared.Interfaces;

namespace YAEP.Interop.Linux.X11
{
    /// <summary>
    /// X11 implementation of IHotkeyService.
    /// </summary>
    public class X11HotkeyService : IHotkeyService
    {
        private IntPtr _display;
        private Thread? _eventLoopThread;
        private bool _isDisposed = false;

        public X11HotkeyService()
        {
            _display = X11NativeMethods.XOpenDisplay(null);
            if (_display == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to open X11 display");
            }
        }

        public void Initialize(IntPtr windowHandle)
        {
            // Start event loop thread for handling X11 events
            _eventLoopThread = new Thread(EventLoop)
            {
                IsBackground = true,
                Name = "X11HotkeyEventLoop"
            };
            _eventLoopThread.Start();
        }

        public void RegisterHotkeys()
        {
            // Register hotkeys using XGrabKey
            // TODO: Implement hotkey registration from database
        }

        public void UnregisterHotkeys()
        {
            // Unregister all hotkeys using XUngrabKey
            // TODO: Implement hotkey unregistration
        }

        public void ReloadHotkeyVKs()
        {
            // Reload and re-register hotkeys
            UnregisterHotkeys();
            RegisterHotkeys();
        }

        private void EventLoop()
        {
            while (!_isDisposed && _display != IntPtr.Zero)
            {
                if (X11NativeMethods.XPending(_display) > 0)
                {
                    X11NativeMethods.XNextEvent(_display, out XEvent evt);
                    if (evt.type == 2) // KeyPress
                    {
                        // Handle hotkey press
                        // TODO: Process hotkey events
                    }
                }
                Thread.Sleep(10); // Small delay to prevent CPU spinning
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            UnregisterHotkeys();

            if (_display != IntPtr.Zero)
            {
                X11NativeMethods.XCloseDisplay(_display);
                _display = IntPtr.Zero;
            }

            _eventLoopThread?.Join(1000);
        }
    }
}

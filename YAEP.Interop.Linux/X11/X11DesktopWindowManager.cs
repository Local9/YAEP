using System.Drawing;
using System.Runtime.InteropServices;
using YAEP.Interop.Linux.X11;
using YAEP.Shared.Enumerations;
using YAEP.Shared.Interfaces;

namespace YAEP.Interop.Linux.X11
{
    /// <summary>
    /// X11 implementation of IDesktopWindowManager.
    /// </summary>
    public class X11DesktopWindowManager : IDesktopWindowManager
    {
        private IntPtr _display;
        private uint _rootWindow;

        public bool IsCompositionEnabled { get; }

        public X11DesktopWindowManager()
        {
            _display = X11NativeMethods.XOpenDisplay(null);
            if (_display == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to open X11 display");
            }

            int screen = X11NativeMethods.XDefaultScreen(_display);
            _rootWindow = (uint)X11NativeMethods.XRootWindow(_display, screen);

            // X11 doesn't have DWM composition like Windows, so this is always false
            // However, compositors may provide similar functionality
            IsCompositionEnabled = false;
        }

        public IntPtr GetForegroundWindowHandle()
        {
            // Get active window using EWMH _NET_ACTIVE_WINDOW property
            // This is a simplified implementation - full implementation would use XGetWindowProperty
            return IntPtr.Zero; // TODO: Implement EWMH _NET_ACTIVE_WINDOW
        }

        public void ActivateWindow(IntPtr handle, AnimationStyle animation)
        {
            uint window = (uint)handle.ToInt64();
            X11NativeMethods.XRaiseWindow(_display, window);
            X11NativeMethods.XSetInputFocus(_display, window, X11NativeMethods.RevertToParent, X11NativeMethods.CurrentTime);
            X11NativeMethods.XFlush(_display);
        }

        public void MinimizeWindow(IntPtr handle, AnimationStyle animation, bool enableAnimation)
        {
            // X11 window minimization using EWMH _NET_WM_STATE
            // This requires setting the _NET_WM_STATE_HIDDEN atom
            // TODO: Implement full EWMH minimization
        }

        public void MoveWindow(IntPtr handle, double left, double top, double width, double height)
        {
            uint window = (uint)handle.ToInt64();
            X11NativeMethods.XMoveResizeWindow(_display, window, (int)left, (int)top, (uint)width, (uint)height);
            X11NativeMethods.XFlush(_display);
        }

        public void MaximizeWindow(IntPtr handle)
        {
            // X11 window maximization using EWMH _NET_WM_STATE
            // This requires setting the _NET_WM_STATE_MAXIMIZED_VERT and _NET_WM_STATE_MAXIMIZED_HORZ atoms
            // TODO: Implement full EWMH maximization
        }

        public (double Left, double Top, double Right, double Bottom) GetWindowPosition(IntPtr handle)
        {
            uint window = (uint)handle.ToInt64();
            X11NativeMethods.XGetGeometry(_display, window, out uint root, out int x, out int y, out uint width, out uint height, out uint border_width, out uint depth);
            return (x, y, x + (int)width, y + (int)height);
        }

        public bool IsWindowMaximized(IntPtr handle)
        {
            // Check EWMH _NET_WM_STATE for maximized state
            // TODO: Implement EWMH state checking
            return false;
        }

        public bool IsWindowMinimized(IntPtr handle)
        {
            // Check EWMH _NET_WM_STATE for minimized/hidden state
            // TODO: Implement EWMH state checking
            return false;
        }

        public IDesktopWindowManagerThumbnail GetLiveThumbnail(IntPtr destination, IntPtr source)
        {
            // X11 doesn't have native live thumbnails like Windows DWM
            // Would need to use compositor extensions or screenshots
            throw new NotSupportedException("Live thumbnails are not supported on X11. Use static thumbnails instead.");
        }

        public Image? GetStaticThumbnail(IntPtr source)
        {
            // Use X11 screenshot APIs or external tools
            // This is a placeholder - full implementation would use XGetImage or external screenshot tools
            return null; // TODO: Implement X11 screenshot
        }

        ~X11DesktopWindowManager()
        {
            if (_display != IntPtr.Zero)
            {
                X11NativeMethods.XCloseDisplay(_display);
            }
        }
    }
}

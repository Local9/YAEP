using System.Drawing;
using YAEP.Interop.Linux.Detection;
using YAEP.Interop.Linux.Wayland;
using YAEP.Interop.Linux.X11;
using YAEP.Shared.Enumerations;
using YAEP.Shared.Interfaces;

namespace YAEP.Interop.Linux.Services
{
    /// <summary>
    /// Unified Linux desktop window manager that delegates to X11 or Wayland implementation
    /// based on display server detection. Falls back to X11 if Wayland detection fails.
    /// </summary>
    public class LinuxDesktopWindowManager : IDesktopWindowManager
    {
        private readonly IDesktopWindowManager _implementation;

        public LinuxDesktopWindowManager()
        {
            DisplayServerType displayServer = DisplayServerDetector.Detect();

            try
            {
                if (displayServer == DisplayServerType.Wayland)
                {
                    _implementation = new WaylandDesktopWindowManager();
                }
                else
                {
                    _implementation = new X11DesktopWindowManager();
                }
            }
            catch
            {
                // Fallback to X11 if Wayland initialization fails
                _implementation = new X11DesktopWindowManager();
            }
        }

        public bool IsCompositionEnabled => _implementation.IsCompositionEnabled;

        public IntPtr GetForegroundWindowHandle() => _implementation.GetForegroundWindowHandle();

        public void ActivateWindow(IntPtr handle, AnimationStyle animation) =>
            _implementation.ActivateWindow(handle, animation);

        public void MinimizeWindow(IntPtr handle, AnimationStyle animation, bool enableAnimation) =>
            _implementation.MinimizeWindow(handle, animation, enableAnimation);

        public void MoveWindow(IntPtr handle, double left, double top, double width, double height) =>
            _implementation.MoveWindow(handle, left, top, width, height);

        public void MaximizeWindow(IntPtr handle) =>
            _implementation.MaximizeWindow(handle);

        public (double Left, double Top, double Right, double Bottom) GetWindowPosition(IntPtr handle) =>
            _implementation.GetWindowPosition(handle);

        public bool IsWindowMaximized(IntPtr handle) =>
            _implementation.IsWindowMaximized(handle);

        public bool IsWindowMinimized(IntPtr handle) =>
            _implementation.IsWindowMinimized(handle);

        public IDesktopWindowManagerThumbnail GetLiveThumbnail(IntPtr destination, IntPtr source) =>
            _implementation.GetLiveThumbnail(destination, source);

        public Image? GetStaticThumbnail(IntPtr source) =>
            _implementation.GetStaticThumbnail(source);
    }
}

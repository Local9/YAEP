using YAEP.Interop.Linux.Detection;
using YAEP.Interop.Linux.Wayland;
using YAEP.Interop.Linux.X11;
using YAEP.Shared.Interfaces;

namespace YAEP.Interop.Linux.Services
{
    /// <summary>
    /// Unified Linux hotkey service that delegates to X11 or Wayland implementation
    /// based on display server detection. Falls back to X11 if Wayland detection fails.
    /// </summary>
    public class LinuxHotkeyService : IHotkeyService
    {
        private readonly IHotkeyService _implementation;

        public LinuxHotkeyService()
        {
            DisplayServerType displayServer = DisplayServerDetector.Detect();

            try
            {
                if (displayServer == DisplayServerType.Wayland)
                {
                    _implementation = new WaylandHotkeyService();
                }
                else
                {
                    _implementation = new X11HotkeyService();
                }
            }
            catch
            {
                // Fallback to X11 if Wayland initialization fails
                _implementation = new X11HotkeyService();
            }
        }

        public void Initialize(IntPtr windowHandle) =>
            _implementation.Initialize(windowHandle);

        public void RegisterHotkeys() =>
            _implementation.RegisterHotkeys();

        public void UnregisterHotkeys() =>
            _implementation.UnregisterHotkeys();

        public void ReloadHotkeyVKs() =>
            _implementation.ReloadHotkeyVKs();

        public void Dispose() =>
            _implementation.Dispose();
    }
}

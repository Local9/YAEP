using YAEP.Interop.Linux.Services;
using YAEP.Interop.Windows.Services;
using YAEP.Shared.Interfaces;

namespace YAEP.Services
{
    /// <summary>
    /// Factory for creating platform-specific services at runtime.
    /// </summary>
    public static class PlatformServiceFactory
    {
        /// <summary>
        /// Creates a platform-specific desktop window manager.
        /// </summary>
        /// <returns>WindowsDesktopWindowManager on Windows, LinuxDesktopWindowManager on Linux.</returns>
        public static IDesktopWindowManager CreateDesktopWindowManager()
        {
            if (OperatingSystem.IsWindows())
            {
                return new WindowsDesktopWindowManager();
            }
            else if (OperatingSystem.IsLinux())
            {
                // LinuxDesktopWindowManager internally detects Wayland/X11
                // and falls back to X11 if Wayland is not available
                return new LinuxDesktopWindowManager();
            }
            throw new PlatformNotSupportedException($"Platform {Environment.OSVersion.Platform} is not supported");
        }

        /// <summary>
        /// Creates a platform-specific hotkey service.
        /// </summary>
        /// <param name="databaseService">Database service instance.</param>
        /// <param name="thumbnailWindowService">Thumbnail window service instance.</param>
        /// <returns>WindowsHotkeyService on Windows, LinuxHotkeyService on Linux.</returns>
        public static IHotkeyService CreateHotkeyService(DatabaseService databaseService, IThumbnailWindowService thumbnailWindowService)
        {
            if (OperatingSystem.IsWindows())
            {
                // Windows implementation will be created separately as it has additional dependencies
                // For now, return null and handle in App.axaml.cs
                return null!; // Will be handled by existing HotkeyService
            }
            else if (OperatingSystem.IsLinux())
            {
                // LinuxHotkeyService handles display server detection internally
                return new LinuxHotkeyService();
            }
            throw new PlatformNotSupportedException($"Platform {Environment.OSVersion.Platform} is not supported");
        }
    }
}

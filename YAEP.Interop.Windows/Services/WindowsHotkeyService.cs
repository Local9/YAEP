using System.Runtime.Versioning;
using YAEP.Shared.Interfaces;

namespace YAEP.Interop.Windows.Services
{
    /// <summary>
    /// Windows implementation of IHotkeyService.
    /// This is a placeholder - the actual Windows hotkey implementation
    /// is in YAEP.AvaloniaUI.Services.HotkeyService which has additional dependencies.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class WindowsHotkeyService : IHotkeyService
    {
        // Note: This is a stub implementation.
        // The actual Windows hotkey service (HotkeyService) is in YAEP.AvaloniaUI
        // and has dependencies on DatabaseService and IThumbnailWindowService.
        // For now, this interface is primarily used for Linux implementations.

        public void Initialize(IntPtr windowHandle)
        {
            // Stub - actual implementation is in YAEP.AvaloniaUI.Services.HotkeyService
            throw new NotImplementedException("Windows hotkey service is implemented in YAEP.AvaloniaUI.Services.HotkeyService");
        }

        public void RegisterHotkeys()
        {
            throw new NotImplementedException("Windows hotkey service is implemented in YAEP.AvaloniaUI.Services.HotkeyService");
        }

        public void UnregisterHotkeys()
        {
            throw new NotImplementedException("Windows hotkey service is implemented in YAEP.AvaloniaUI.Services.HotkeyService");
        }

        public void ReloadHotkeyVKs()
        {
            throw new NotImplementedException("Windows hotkey service is implemented in YAEP.AvaloniaUI.Services.HotkeyService");
        }

        public void Dispose()
        {
            // Nothing to dispose in stub
        }
    }
}

using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using System.Runtime.InteropServices;
using YAEP.Interop.Windows;

namespace YAEP.Services
{
    /// <summary>
    /// Service for getting monitor hardware information.
    /// </summary>
    public static class MonitorService
    {
        private static readonly List<MonitorHardwareInfo> _monitorCache = new();
        private static bool _cacheInitialized = false;

        // Display device state flags
        private const uint DISPLAY_DEVICE_ACTIVE = 0x00000001;

        /// <summary>
        /// Gets the Device Instance Path for a monitor based on its screen bounds.
        /// This is a stable identifier that persists across reboots.
        /// </summary>
        public static string GetHardwareIdForScreen(Screen screen)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Fallback for non-Windows: use index-based ID
                return $"DISPLAY{GetScreenIndex(screen)}";
            }

            InitializeMonitorCache();

            MonitorHardwareInfo? monitorInfo = FindMonitorByBounds(screen.Bounds);
            if (monitorInfo != null)
            {
                // Return Device Instance Path if available, otherwise fall back to device name
                return !string.IsNullOrEmpty(monitorInfo.DeviceInstancePath)
                    ? monitorInfo.DeviceInstancePath
                    : monitorInfo.DeviceName;
            }

            // Fallback: use index-based ID
            return $"DISPLAY{GetScreenIndex(screen)}";
        }

        /// <summary>
        /// Gets the display number (1-based) for a monitor.
        /// </summary>
        public static int GetDisplayNumberForScreen(Screen screen)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return GetScreenIndex(screen) + 1;
            }

            InitializeMonitorCache();

            MonitorHardwareInfo? monitorInfo = FindMonitorByBounds(screen.Bounds);
            if (monitorInfo != null)
            {
                int index = _monitorCache.IndexOf(monitorInfo);
                return index >= 0 ? index + 1 : GetScreenIndex(screen) + 1;
            }

            return GetScreenIndex(screen) + 1;
        }

        /// <summary>
        /// Finds a monitor in the cache by matching screen bounds.
        /// </summary>
        private static MonitorHardwareInfo? FindMonitorByBounds(PixelRect bounds)
        {
            return _monitorCache.FirstOrDefault(m =>
                m.Bounds.X == bounds.X &&
                m.Bounds.Y == bounds.Y &&
                m.Bounds.Width == bounds.Width &&
                m.Bounds.Height == bounds.Height);
        }

        /// <summary>
        /// Finds a screen by hardware ID, falling back to screen index, then primary screen.
        /// </summary>
        public static Screen? FindScreenBySettings(string? hardwareId, int screenIndex, Screens screens)
        {
            if (screens == null || screens.All.Count == 0)
                return null;

            Screen? targetScreen = null;

            // Try to match by hardware ID first
            if (!string.IsNullOrEmpty(hardwareId))
            {
                targetScreen = screens.All.FirstOrDefault(screen =>
                {
                    string screenHardwareId = GetHardwareIdForScreen(screen);
                    return screenHardwareId == hardwareId;
                });
            }

            // Fall back to screen index if hardware ID match failed
            if (targetScreen == null && screenIndex >= 0 && screenIndex < screens.All.Count)
            {
                targetScreen = screens.All[screenIndex];
            }

            // Final fallback to primary or first screen
            return targetScreen ?? screens.Primary ?? screens.All.FirstOrDefault();
        }

        /// <summary>
        /// Clears the monitor cache, forcing it to be reinitialized on next access.
        /// Call this when monitor configuration changes.
        /// </summary>
        public static void ClearCache()
        {
            _monitorCache.Clear();
            _cacheInitialized = false;
        }

        private static int GetScreenIndex(Screen screen)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                Avalonia.Controls.Screens? screens = desktop.MainWindow?.Screens;
                if (screens != null)
                {
                    for (int i = 0; i < screens.All.Count; i++)
                    {
                        if (screens.All[i] == screen)
                        {
                            return i;
                        }
                    }
                }
            }
            return 0;
        }

        private static void InitializeMonitorCache()
        {
            if (_cacheInitialized)
                return;

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _cacheInitialized = true;
                return;
            }

            _monitorCache.Clear();

            // First, enumerate monitors to get handles and bounds
            User32NativeMethods.MonitorEnumProc enumProc = (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
            {
                MONITORINFOEX mi = new MONITORINFOEX();
                mi.Size = Marshal.SizeOf(typeof(MONITORINFOEX));

                if (User32NativeMethods.GetMonitorInfo(hMonitor, ref mi))
                {
                    _monitorCache.Add(new MonitorHardwareInfo
                    {
                        Handle = hMonitor,
                        DeviceName = mi.DeviceName,
                        DeviceInstancePath = string.Empty, // Will be filled by EnumDisplayDevices
                        Bounds = new PixelRect(mi.Monitor.left, mi.Monitor.top,
                            mi.Monitor.right - mi.Monitor.left,
                            mi.Monitor.bottom - mi.Monitor.top)
                    });
                }
                return true;
            };

            User32NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, enumProc, IntPtr.Zero);

            // Now use EnumDisplayDevices to get Device Instance Paths
            try
            {
                // First, enumerate all display adapters
                uint adapterIndex = 0;
                while (true)
                {
                    DISPLAY_DEVICE adapterDevice = new DISPLAY_DEVICE();
                    adapterDevice.cb = Marshal.SizeOf(typeof(DISPLAY_DEVICE));

                    if (!User32NativeMethods.EnumDisplayDevices(null, adapterIndex, ref adapterDevice, 0))
                    {
                        break; // No more adapters
                    }

                    // Now enumerate monitors for this adapter
                    uint monitorIndex = 0;
                    while (true)
                    {
                        DISPLAY_DEVICE monitorDevice = new DISPLAY_DEVICE();
                        monitorDevice.cb = Marshal.SizeOf(typeof(DISPLAY_DEVICE));

                        if (!User32NativeMethods.EnumDisplayDevices(
                            adapterDevice.DeviceName,
                            monitorIndex,
                            ref monitorDevice,
                            User32NativeMethods.EDD_GET_DEVICE_INTERFACE_NAME))
                        {
                            break; // No more monitors for this adapter
                        }

                        // Check if this is an active monitor
                        if ((monitorDevice.StateFlags & DISPLAY_DEVICE_ACTIVE) != 0)
                        {
                            // Match this monitor with our cache by device name
                            // The DeviceName from GetMonitorInfo should match monitorDevice.DeviceName
                            MonitorHardwareInfo? monitorInfo = _monitorCache
                                .FirstOrDefault(m => m.DeviceName == monitorDevice.DeviceName &&
                                                    string.IsNullOrEmpty(m.DeviceInstancePath));

                            if (monitorInfo != null && !string.IsNullOrEmpty(monitorDevice.DeviceID))
                            {
                                // The DeviceID field contains the device interface name (Device Instance Path)
                                // This is a stable identifier that persists across reboots
                                monitorInfo.DeviceInstancePath = monitorDevice.DeviceID;
                            }
                        }

                        monitorIndex++;
                    }

                    adapterIndex++;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting Device Instance Paths: {ex.Message}");
            }

            _cacheInitialized = true;
        }

        private class MonitorHardwareInfo
        {
            public IntPtr Handle { get; set; }
            public string DeviceName { get; set; } = string.Empty;
            public string DeviceInstancePath { get; set; } = string.Empty;
            public PixelRect Bounds { get; set; }
        }
    }
}

using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using System.Runtime.InteropServices;
using YAEP.Interop;

namespace YAEP.Services
{
    /// <summary>
    /// Service for getting monitor hardware information.
    /// </summary>
    public static class MonitorService
    {
        private static readonly List<MonitorHardwareInfo> _monitorCache = new();
        private static bool _cacheInitialized = false;

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

            // Try to match by exact bounds
            foreach (MonitorHardwareInfo monitorInfo in _monitorCache)
            {
                if (monitorInfo.Bounds.X == screen.Bounds.X &&
                    monitorInfo.Bounds.Y == screen.Bounds.Y &&
                    monitorInfo.Bounds.Width == screen.Bounds.Width &&
                    monitorInfo.Bounds.Height == screen.Bounds.Height)
                {
                    // Return Device Instance Path if available, otherwise fall back to device name
                    return !string.IsNullOrEmpty(monitorInfo.DeviceInstancePath)
                        ? monitorInfo.DeviceInstancePath
                        : monitorInfo.DeviceName;
                }
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

            // Find matching monitor and return its display number
            for (int i = 0; i < _monitorCache.Count; i++)
            {
                MonitorHardwareInfo monitorInfo = _monitorCache[i];
                if (monitorInfo.Bounds.X == screen.Bounds.X &&
                    monitorInfo.Bounds.Y == screen.Bounds.Y &&
                    monitorInfo.Bounds.Width == screen.Bounds.Width &&
                    monitorInfo.Bounds.Height == screen.Bounds.Height)
                {
                    return i + 1;
                }
            }

            return GetScreenIndex(screen) + 1;
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
                        if ((monitorDevice.StateFlags & 0x00000001) != 0) // DISPLAY_DEVICE_ACTIVE
                        {
                            // Match this monitor with our cache by device name
                            // The DeviceName from GetMonitorInfo should match monitorDevice.DeviceName
                            foreach (MonitorHardwareInfo monitorInfo in _monitorCache)
                            {
                                if (monitorInfo.DeviceName == monitorDevice.DeviceName &&
                                    string.IsNullOrEmpty(monitorInfo.DeviceInstancePath))
                                {
                                    // The DeviceID field contains the device interface name (Device Instance Path)
                                    // This is a stable identifier that persists across reboots
                                    if (!string.IsNullOrEmpty(monitorDevice.DeviceID))
                                    {
                                        monitorInfo.DeviceInstancePath = monitorDevice.DeviceID;
                                    }
                                    break;
                                }
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

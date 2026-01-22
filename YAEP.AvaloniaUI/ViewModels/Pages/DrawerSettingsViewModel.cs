using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using System.Collections.ObjectModel;
using YAEP.Models;

namespace YAEP.ViewModels.Pages
{
    public partial class DrawerSettingsViewModel : ViewModelBase
    {
        private readonly DatabaseService _databaseService;
        private readonly DrawerWindowService? _drawerWindowService;
        private bool _isLoadingSettings = false;

        [ObservableProperty]
        private ObservableCollection<MonitorInfo> _availableMonitors = new();

        [ObservableProperty]
        private MonitorInfo? _selectedDrawerMonitor;

        [ObservableProperty]
        private DrawerSide _drawerSide = DrawerSide.Right;

        [ObservableProperty]
        private int _drawerWidth = 400;

        [ObservableProperty]
        private bool _isDrawerEnabled = false;

        public DrawerSettingsViewModel(DatabaseService databaseService, DrawerWindowService? drawerWindowService = null)
        {
            _databaseService = databaseService;
            _drawerWindowService = drawerWindowService;
        }

        public void OnNavigatedTo()
        {
            _isLoadingSettings = true;
            try
            {
                LoadDrawerSettings();
                LoadAvailableMonitors();
            }
            finally
            {
                _isLoadingSettings = false;
            }
        }

        public void OnNavigatedFrom()
        {
        }

        private void LoadDrawerSettings()
        {
            DrawerSettings settings = _databaseService.GetDrawerSettings();
            DrawerSide = settings.Side;
            DrawerWidth = settings.Width;
            IsDrawerEnabled = settings.IsEnabled;
        }

        private void LoadAvailableMonitors()
        {
            AvailableMonitors.Clear();

            try
            {
                Screens? screens = null;
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    screens = desktop.MainWindow?.Screens;
                }

                if (screens != null)
                {
                    Screen? primaryScreen = screens.Primary;
                    for (int i = 0; i < screens.All.Count; i++)
                    {
                        Screen screen = screens.All[i];
                        string hardwareId = MonitorService.GetHardwareIdForScreen(screen);
                        int displayNumber = MonitorService.GetDisplayNumberForScreen(screen);

                        MonitorInfo monitorInfo = new MonitorInfo
                        {
                            Screen = screen,
                            Name = $"Monitor {displayNumber}",
                            Bounds = screen.Bounds,
                            WorkingArea = screen.WorkingArea,
                            IsPrimary = screen == primaryScreen,
                            HardwareId = hardwareId,
                            DisplayNumber = displayNumber
                        };
                        AvailableMonitors.Add(monitorInfo);
                    }

                    DrawerSettings settings = _databaseService.GetDrawerSettings();
                    // Try to match by hardware ID first, then fall back to screen index
                    MonitorInfo? matchedMonitor = null;
                    if (!string.IsNullOrEmpty(settings.HardwareId))
                    {
                        matchedMonitor = AvailableMonitors.FirstOrDefault(m => m.HardwareId == settings.HardwareId);
                    }

                    if (matchedMonitor == null && settings.ScreenIndex >= 0 && settings.ScreenIndex < AvailableMonitors.Count)
                    {
                        matchedMonitor = AvailableMonitors[settings.ScreenIndex];
                    }

                    SelectedDrawerMonitor = matchedMonitor ??
                        AvailableMonitors.FirstOrDefault(m => m.IsPrimary) ??
                        AvailableMonitors.FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading monitors: {ex.Message}");
            }
        }

        partial void OnSelectedDrawerMonitorChanged(MonitorInfo? value)
        {
            if (!_isLoadingSettings && value != null)
            {
                int screenIndex = AvailableMonitors.IndexOf(value);
                if (screenIndex >= 0)
                {
                    SaveDrawerSettings();
                }
            }
        }

        partial void OnDrawerSideChanged(DrawerSide value)
        {
            if (!_isLoadingSettings)
            {
                SaveDrawerSettings();
            }
        }

        partial void OnDrawerWidthChanged(int value)
        {
            if (!_isLoadingSettings)
            {
                SaveDrawerSettings();
            }
        }

        partial void OnIsDrawerEnabledChanged(bool value)
        {
            if (!_isLoadingSettings)
            {
                SaveDrawerSettings();
            }
        }

        private void SaveDrawerSettings()
        {
            // If SelectedDrawerMonitor is null, try to load monitors first
            if (SelectedDrawerMonitor == null)
            {
                if (AvailableMonitors.Count == 0)
                {
                    LoadAvailableMonitors();
                }

                // If still null after loading, use first available monitor or default
                if (SelectedDrawerMonitor == null)
                {
                    if (AvailableMonitors.Count > 0)
                    {
                        SelectedDrawerMonitor = AvailableMonitors.FirstOrDefault(m => m.IsPrimary) ?? AvailableMonitors.FirstOrDefault();
                    }
                    else
                    {
                        // Can't save without a monitor, but we should still try to update the service
                        // Use default screen index 0
                        DrawerSettings defaultSettings = new DrawerSettings
                        {
                            ScreenIndex = 0,
                            HardwareId = string.Empty,
                            Side = DrawerSide,
                            Width = DrawerWidth,
                            Height = 600, // Default height
                            IsVisible = _drawerWindowService?.GetSettings().IsVisible ?? false,
                            IsEnabled = IsDrawerEnabled
                        };

                        _databaseService.SaveDrawerSettings(defaultSettings);
                        _drawerWindowService?.UpdateSettings(defaultSettings);
                        return;
                    }
                }
            }

            int screenIndex = AvailableMonitors.IndexOf(SelectedDrawerMonitor);
            if (screenIndex < 0)
            {
                // Fallback to screen index 0 if monitor not found
                screenIndex = 0;
            }

            int height = SelectedDrawerMonitor.WorkingArea.Height;

            DrawerSettings settings = new DrawerSettings
            {
                ScreenIndex = screenIndex,
                HardwareId = SelectedDrawerMonitor.HardwareId ?? string.Empty,
                Side = DrawerSide,
                Width = DrawerWidth,
                Height = height,
                IsVisible = _drawerWindowService?.GetSettings().IsVisible ?? false,
                IsEnabled = IsDrawerEnabled
            };

            _databaseService.SaveDrawerSettings(settings);
            _drawerWindowService?.UpdateSettings(settings);
        }
    }
}

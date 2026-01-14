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
            LoadDrawerSettings();
            LoadAvailableMonitors();
        }

        public void OnNavigatedFrom()
        {
        }

        private void LoadDrawerSettings()
        {
            _isLoadingSettings = true;
            try
            {
                DrawerSettings settings = _databaseService.GetDrawerSettings();
                DrawerSide = settings.Side;
                DrawerWidth = settings.Width;
                IsDrawerEnabled = settings.IsEnabled;
            }
            finally
            {
                _isLoadingSettings = false;
            }
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
                        MonitorInfo monitorInfo = new MonitorInfo
                        {
                            Screen = screen,
                            Name = $"Monitor {i + 1}",
                            Bounds = screen.Bounds,
                            WorkingArea = screen.WorkingArea,
                            IsPrimary = screen == primaryScreen
                        };
                        AvailableMonitors.Add(monitorInfo);
                    }

                    DrawerSettings settings = _databaseService.GetDrawerSettings();
                    if (settings.ScreenIndex >= 0 && settings.ScreenIndex < AvailableMonitors.Count)
                    {
                        SelectedDrawerMonitor = AvailableMonitors[settings.ScreenIndex];
                    }
                    else
                    {
                        SelectedDrawerMonitor = AvailableMonitors.FirstOrDefault(m => m.IsPrimary) ?? AvailableMonitors.FirstOrDefault();
                    }
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
            if (SelectedDrawerMonitor == null)
                return;

            int screenIndex = AvailableMonitors.IndexOf(SelectedDrawerMonitor);
            if (screenIndex < 0)
                return;

            int height = SelectedDrawerMonitor.WorkingArea.Height;

            DrawerSettings settings = new DrawerSettings
            {
                ScreenIndex = screenIndex,
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

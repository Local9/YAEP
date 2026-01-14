using Avalonia.Controls;
using Avalonia.Platform;
using YAEP.Models;
using YAEP.ViewModels.Windows;
using YAEP.Views.Windows;

namespace YAEP.Services
{
    public class DrawerWindowService
    {
        private readonly DatabaseService _databaseService;
        private DrawerWindow? _drawerWindow;
        private DrawerWindowViewModel? _viewModel;
        private DrawerIndicatorWindow? _indicatorWindow;

        public DrawerWindowService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        /// <summary>
        /// Initializes and shows the drawer window.
        /// </summary>
        public void Initialize()
        {
            try
            {
                DrawerSettings settings = _databaseService.GetDrawerSettings();
                settings = RecalculateHeight(settings);

                if (!settings.IsEnabled)
                {
                    return;
                }

                _viewModel = new DrawerWindowViewModel
                {
                    ScreenIndex = settings.ScreenIndex,
                    Side = settings.Side,
                    Width = settings.Width,
                    Height = settings.Height,
                    IsOpen = false
                };

                Dispatcher.UIThread.Post(() =>
                {
                    _drawerWindow = new DrawerWindow(_viewModel);
                    _drawerWindow.UpdateSettings(settings);

                    LoadMumbleLinks();

                    _drawerWindow.Show();

                    InitializeIndicator(settings);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing drawer window: {ex.Message}");
            }
        }

        /// <summary>
        /// Shows the drawer window (slides it in).
        /// </summary>
        public void Show()
        {
            if (_drawerWindow == null)
            {
                Initialize();
            }

            Dispatcher.UIThread.Post(() =>
            {
                if (_drawerWindow != null)
                {
                    LoadMumbleLinks();
                    _drawerWindow.SlideIn();
                }
            });
        }

        /// <summary>
        /// Hides the drawer window (slides it out).
        /// </summary>
        public void Hide()
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_drawerWindow != null)
                {
                    _drawerWindow.SlideOut();
                }
            });
        }

        /// <summary>
        /// Toggles the drawer window visibility.
        /// </summary>
        public void Toggle()
        {
            if (_drawerWindow == null)
            {
                Initialize();
            }

            Dispatcher.UIThread.Post(() =>
            {
                if (_drawerWindow != null)
                {
                    _drawerWindow.Toggle();
                    if (_viewModel != null)
                    {
                        UpdateVisibilitySetting(_viewModel.IsOpen);
                    }
                }
            });
        }

        /// <summary>
        /// Updates the drawer settings and refreshes the window.
        /// </summary>
        public void UpdateSettings(DrawerSettings settings)
        {
            settings = RecalculateHeight(settings);
            _databaseService.SaveDrawerSettings(settings);

            Dispatcher.UIThread.Post(() =>
            {
                if (!settings.IsEnabled)
                {
                    if (_drawerWindow != null)
                    {
                        _drawerWindow.Hide();
                    }
                    if (_indicatorWindow != null)
                    {
                        _indicatorWindow.Hide();
                    }
                    return;
                }

                if (_drawerWindow == null)
                {
                    Initialize();
                    return;
                }

                if (!_drawerWindow.IsVisible)
                {
                    _drawerWindow.Show();
                }
                if (_indicatorWindow != null && !_indicatorWindow.IsVisible)
                {
                    _indicatorWindow.Show();
                }

                _drawerWindow.UpdateSettings(settings);

                UpdateIndicator(settings);
            });
        }

        /// <summary>
        /// Recalculates the drawer height based on the selected monitor.
        /// </summary>
        private DrawerSettings RecalculateHeight(DrawerSettings settings)
        {
            try
            {
                if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                {
                    Screens? screens = desktop.MainWindow?.Screens;
                    if (screens != null)
                    {
                        Screen? targetScreen = null;
                        if (settings.ScreenIndex >= 0 && settings.ScreenIndex < screens.All.Count)
                        {
                            targetScreen = screens.All[settings.ScreenIndex];
                        }
                        else
                        {
                            targetScreen = screens.Primary ?? screens.All.FirstOrDefault();
                        }

                        if (targetScreen != null)
                        {
                            settings.Height = targetScreen.WorkingArea.Height;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error recalculating drawer height: {ex.Message}");
            }

            return settings;
        }

        /// <summary>
        /// Initializes the drawer indicator window.
        /// </summary>
        private void InitializeIndicator(DrawerSettings settings)
        {
            if (_indicatorWindow == null)
            {
                _indicatorWindow = new DrawerIndicatorWindow();
                _indicatorWindow.Hovered += IndicatorWindow_Hovered;
                _indicatorWindow.Show();
            }

            UpdateIndicator(settings);
        }

        /// <summary>
        /// Updates the indicator window position and size.
        /// </summary>
        private void UpdateIndicator(DrawerSettings settings)
        {
            if (_indicatorWindow != null)
            {
                _indicatorWindow.UpdatePosition(settings.Side, settings.ScreenIndex, settings.Width, settings.Height);
            }
            else if (settings != null)
            {
                InitializeIndicator(settings);
            }
        }

        /// <summary>
        /// Handles the indicator hover event to open the drawer.
        /// </summary>
        private void IndicatorWindow_Hovered(object? sender, EventArgs e)
        {
            if (_viewModel == null || !_viewModel.IsOpen)
            {
                Show();
            }
        }

        /// <summary>
        /// Gets the current drawer settings.
        /// </summary>
        public DrawerSettings GetSettings()
        {
            return _databaseService.GetDrawerSettings();
        }

        /// <summary>
        /// Updates the visibility setting in the database.
        /// </summary>
        private void UpdateVisibilitySetting(bool isVisible)
        {
            try
            {
                DrawerSettings settings = _databaseService.GetDrawerSettings();
                settings.IsVisible = isVisible;
                _databaseService.SaveDrawerSettings(settings);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating drawer visibility setting: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads mumble links into the drawer view model.
        /// </summary>
        private void LoadMumbleLinks()
        {
            try
            {
                if (_viewModel != null)
                {
                    List<MumbleLink> links = _databaseService.GetMumbleLinks();
                    _viewModel.MumbleLinks.Clear();
                    foreach (MumbleLink link in links)
                    {
                        _viewModel.MumbleLinks.Add(link);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading mumble links for drawer: {ex.Message}");
            }
        }

        /// <summary>
        /// Refreshes the mumble links in the drawer.
        /// </summary>
        public void RefreshMumbleLinks()
        {
            Dispatcher.UIThread.Post(() =>
            {
                LoadMumbleLinks();
            });
        }

        /// <summary>
        /// Shuts down the drawer window service.
        /// </summary>
        public void Shutdown()
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_drawerWindow != null)
                {
                    _drawerWindow.Close();
                    _drawerWindow = null;
                }

                if (_indicatorWindow != null)
                {
                    _indicatorWindow.Hovered -= IndicatorWindow_Hovered;
                    _indicatorWindow.Close();
                    _indicatorWindow = null;
                }

                _viewModel = null;
            });
        }
    }
}

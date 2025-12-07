using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform;
using System.Linq;
using System.Reflection;
using YAEP.Interface;
using YAEP.Services;
using YAEP.ViewModels;
using YAEP.ViewModels.Pages;
using YAEP.Views.Pages;

namespace YAEP.Views
{
    public partial class MainWindow : Window
    {
        private readonly DatabaseService? _databaseService;
        private readonly IThumbnailWindowService? _thumbnailWindowService;
        private readonly HotkeyService? _hotkeyService;
        private readonly Application? _application;
        private TrayIcon? _trayIcon;
        private object? _previousPage;

        public MainWindowViewModel? ViewModel { get; private set; }

        public MainWindow()
        {
            InitializeComponent();
        }

        public MainWindow(
            MainWindowViewModel viewModel,
            DatabaseService? databaseService = null,
            IThumbnailWindowService? thumbnailWindowService = null,
            HotkeyService? hotkeyService = null,
            Application? application = null)
        {
            ViewModel = viewModel;
            _databaseService = databaseService;
            _thumbnailWindowService = thumbnailWindowService;
            _hotkeyService = hotkeyService;
            _application = application;

            DataContext = viewModel;
            InitializeComponent();

            if (viewModel != null)
            {
                viewModel.PropertyChanged += ViewModel_PropertyChanged;
                
                if (viewModel.MenuItems.Count > 0)
                {
                    viewModel.SelectedMenuItem = viewModel.MenuItems[0];
                }
            }

            _thumbnailWindowService?.Start();
            _hotkeyService?.Initialize(this);

            this.Deactivated += MainWindow_Deactivated;
            this.Activated += MainWindow_Activated;
            this.Opened += MainWindow_Opened;
            this.Closing += MainWindow_Closing;
        }

        private void MainWindow_Opened(object? sender, EventArgs e)
        {
            InitializeTrayIcon();
        }

        private void InitializeTrayIcon()
        {
            if (_trayIcon != null)
            {
                UpdateTrayIconMenu();
                return;
            }

            try
            {
                WindowIcon? icon = this.Icon;
                
                if (icon == null)
                {
                    var assemblyName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
                    var iconUri = new Uri($"avares://{assemblyName}/Assets/yaep-icon.ico");
                    icon = new WindowIcon(AssetLoader.Open(iconUri));
                }

                _trayIcon = new TrayIcon
                {
                    Icon = icon,
                    ToolTipText = "YAEP - Yet Another EVE Preview",
                    IsVisible = true
                };

                UpdateTrayIconMenu();

                _trayIcon.Clicked += TrayIcon_Clicked;

                System.Diagnostics.Debug.WriteLine($"Tray icon initialized successfully. IsVisible: {_trayIcon.IsVisible}, Icon: {icon != null}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing tray icon: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        private void TrayIcon_Clicked(object? sender, EventArgs e)
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void UpdateTrayIconMenu()
        {
            if (_trayIcon == null) return;

            try
            {
                var menu = new NativeMenu();

                var showMenuItem = new NativeMenuItem("Show");
                showMenuItem.Click += ShowMenuItem_Click;
                menu.Add(showMenuItem);

                menu.Add(new NativeMenuItemSeparator());

                if (_databaseService != null)
                {
                    var profiles = _databaseService.GetProfiles();
                    foreach (var profile in profiles)
                    {
                        string header = profile.IsActive ? $"{profile.Name} (Active)" : profile.Name;
                        var profileMenuItem = new NativeMenuItem(header)
                        {
                            IsChecked = profile.IsActive
                        };

                        var profileCopy = profile;
                        profileMenuItem.Click += (s, args) =>
                        {
                            foreach (var item in menu.Items.OfType<NativeMenuItem>())
                            {
                                if (item.Header?.Contains("(Active)") == true)
                                {
                                    item.Header = item.Header.Replace(" (Active)", "");
                                    item.IsChecked = false;
                                }
                            }

                            profileMenuItem.Header = $"{profileCopy.Name} (Active)";
                            profileMenuItem.IsChecked = true;
                            _databaseService.SetCurrentProfile(profileCopy.Id);
                        };

                        menu.Add(profileMenuItem);
                    }
                }

                menu.Add(new NativeMenuItemSeparator());

                var exitMenuItem = new NativeMenuItem("Exit");
                exitMenuItem.Click += ExitMenuItem_Click;
                menu.Add(exitMenuItem);

                _trayIcon.Menu = menu;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error building tray context menu: {ex.Message}");
            }
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainWindowViewModel.SelectedMenuItem) && ViewModel != null)
            {
                NavigateToPage(ViewModel.SelectedMenuItem);
            }
        }

        private void NavigateToPage(NavigationItem? item)
        {
            if (item?.PageType == null || ViewModel == null)
                return;

            try
            {
                var previousPage = _previousPage ?? ViewModel.CurrentPage;
                var previousPageType = GetPageType(previousPage);

                if (previousPageType != null && previousPageType != item.PageType)
                {
                    HandlePageNavigationAway(previousPage, previousPageType, item.PageType);
                }

                var newPage = CreatePage(item.PageType);
                if (newPage != null)
                {
                    _previousPage = ViewModel.CurrentPage;
                    ViewModel.CurrentPage = newPage;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error navigating to page {item.PageType?.Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the page type from either a NavigationItem or a page instance.
        /// </summary>
        private Type? GetPageType(object? page)
        {
            if (page == null)
                return null;

            return page is NavigationItem navItem ? navItem.PageType : page.GetType();
        }

        /// <summary>
        /// Handles cleanup when navigating away from a page, including calling OnNavigatedFrom and resuming focus checks.
        /// </summary>
        private void HandlePageNavigationAway(object? previousPage, Type previousPageType, Type newPageType)
        {
            if (previousPageType == typeof(ThumbnailSettingsPage))
            {
                System.Diagnostics.Debug.WriteLine($"NavigateToPage: Leaving ThumbnailSettingsPage - Resuming focus checks");
                _thumbnailWindowService?.ResumeFocusCheckOnAllThumbnails();
            }

            if (previousPage != null && !(previousPage is NavigationItem))
            {
                CallOnNavigatedFrom(previousPage, previousPageType);
            }
        }

        /// <summary>
        /// Calls OnNavigatedFrom on a page's ViewModel using reflection or direct call.
        /// </summary>
        private void CallOnNavigatedFrom(object page, Type pageType)
        {
            try
            {
                if (page is ThumbnailSettingsPage thumbnailSettingsPage && thumbnailSettingsPage.ViewModel != null)
                {
                    thumbnailSettingsPage.ViewModel.OnNavigatedFrom();
                    return;
                }

                var viewModelProperty = pageType.GetProperty("ViewModel", BindingFlags.Public | BindingFlags.Instance);
                if (viewModelProperty?.GetValue(page) is { } viewModel)
                {
                    var onNavigatedFromMethod = viewModel.GetType().GetMethod("OnNavigatedFrom", BindingFlags.Public | BindingFlags.Instance);
                    onNavigatedFromMethod?.Invoke(viewModel, null);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error calling OnNavigatedFrom on {pageType.Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a page instance with its ViewModel based on the page type.
        /// </summary>
        private object? CreatePage(Type pageType)
        {
            return pageType switch
            {
                _ when pageType == typeof(ProfilesPage) => CreateProfilesPage(),
                _ when pageType == typeof(ThumbnailSettingsPage) => CreateThumbnailSettingsPage(),
                _ when pageType == typeof(ClientGroupingPage) => CreateClientGroupingPage(),
                _ when pageType == typeof(GridLayoutPage) => CreateGridLayoutPage(),
                _ when pageType == typeof(ProcessManagementPage) => CreateProcessManagementPage(),
                _ when pageType == typeof(SettingsPage) => CreateSettingsPage(),
                _ => null
            };
        }

        private object? CreateProfilesPage()
        {
            if (_databaseService == null)
                return null;

            var viewModel = new ProfilesViewModel(_databaseService);
            var page = new ProfilesPage(viewModel);
            viewModel.OnNavigatedTo();
            return page;
        }

        private object? CreateThumbnailSettingsPage()
        {
            if (_databaseService == null || _thumbnailWindowService == null)
                return null;

            var viewModel = new ThumbnailSettingsViewModel(_databaseService, _thumbnailWindowService);
            var page = new ThumbnailSettingsPage(viewModel);
            viewModel.OnNavigatedTo();
            return page;
        }

        private object? CreateClientGroupingPage()
        {
            if (_databaseService == null || _thumbnailWindowService == null)
                return null;

            var viewModel = new ClientGroupingViewModel(_databaseService, _thumbnailWindowService, _hotkeyService);
            var page = new ClientGroupingPage(viewModel);
            viewModel.OnNavigatedTo();
            return page;
        }

        private object? CreateGridLayoutPage()
        {
            if (_databaseService == null || _thumbnailWindowService == null)
                return null;

            var viewModel = new GridLayoutViewModel(_databaseService, _thumbnailWindowService);
            var page = new GridLayoutPage(viewModel);
            viewModel.OnNavigatedTo();
            return page;
        }

        private object? CreateProcessManagementPage()
        {
            if (_databaseService == null)
                return null;

            var viewModel = new ProcessManagementViewModel(_databaseService);
            var page = new ProcessManagementPage(viewModel);
            viewModel.OnNavigatedTo();
            return page;
        }

        private object? CreateSettingsPage()
        {
            if (_databaseService == null || _thumbnailWindowService == null || _application == null)
                return null;

            var viewModel = new SettingsViewModel(_databaseService, _thumbnailWindowService, _application);
            var page = new SettingsPage(viewModel);
            viewModel.OnNavigatedTo();
            return page;
        }

        private void MainWindow_WindowStateChanged(object? sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Minimized)
            {
                _thumbnailWindowService?.ResumeFocusCheckOnAllThumbnails();
            }
            else if (this.WindowState == WindowState.Normal || this.WindowState == WindowState.Maximized)
            {
                if (ViewModel?.SelectedMenuItem?.PageType == typeof(ThumbnailSettingsPage))
                {
                    _thumbnailWindowService?.SetFocusOnFirstThumbnail();
                }
            }
        }

        private void MainWindow_Deactivated(object? sender, EventArgs e)
        {
            if (ViewModel?.SelectedMenuItem?.PageType == typeof(ThumbnailSettingsPage))
            {
                _thumbnailWindowService?.ResumeFocusCheckOnAllThumbnails();
            }
        }

        private void MainWindow_Activated(object? sender, EventArgs e)
        {
            if (ViewModel?.SelectedMenuItem?.PageType == typeof(ThumbnailSettingsPage) && 
                ViewModel?.CurrentPage != null)
            {
                _thumbnailWindowService?.SetFocusOnFirstThumbnail();
            }
        }

        private void ShowMenuItem_Click(object? sender, EventArgs e)
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void ExitMenuItem_Click(object? sender, EventArgs e)
        {
            _trayIcon?.Dispose();
            _trayIcon = null;

            _thumbnailWindowService?.Stop();
            _hotkeyService?.Dispose();

            if (_application?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        }

        private void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
        }
    }
}


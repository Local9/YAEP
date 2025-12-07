using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform;
using System.Linq;
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

            // Set up navigation
            if (viewModel != null)
            {
                viewModel.PropertyChanged += ViewModel_PropertyChanged;
                
                // Select first menu item by default
                if (viewModel.MenuItems.Count > 0)
                {
                    viewModel.SelectedMenuItem = viewModel.MenuItems[0];
                }
            }

            // Start services if available
            _thumbnailWindowService?.Start();
            _hotkeyService?.Initialize(this);

            // Handle window state changes
            // Note: WindowStateChanged doesn't exist in Avalonia, use WindowState property change instead
            this.Deactivated += MainWindow_Deactivated;
            this.Activated += MainWindow_Activated;
            this.Opened += MainWindow_Opened;
            this.Closing += MainWindow_Closing;
        }

        private void MainWindow_Opened(object? sender, EventArgs e)
        {
            // Initialize tray icon after window is opened
            InitializeTrayIcon();
        }

        private void InitializeTrayIcon()
        {
            // Don't create a new tray icon if one already exists
            if (_trayIcon != null)
            {
                // Just update the menu in case profiles changed
                UpdateTrayIconMenu();
                return;
            }

            try
            {
                // Use the window's icon if available, otherwise load from assets
                WindowIcon? icon = this.Icon;
                
                if (icon == null)
                {
                    // Try to load the icon from assets
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

                // Create the menu
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
            // Show window on tray icon click
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

                // Show menu item
                var showMenuItem = new NativeMenuItem("Show");
                showMenuItem.Click += ShowMenuItem_Click;
                menu.Add(showMenuItem);

                menu.Add(new NativeMenuItemSeparator());

                // Add profile menu items
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

                        var profileCopy = profile; // Capture for closure
                        profileMenuItem.Click += (s, args) =>
                        {
                            // Update all profile items to reflect the new active state
                            foreach (var item in menu.Items.OfType<NativeMenuItem>())
                            {
                                if (item.Header?.Contains("(Active)") == true)
                                {
                                    item.Header = item.Header.Replace(" (Active)", "");
                                    item.IsChecked = false;
                                }
                            }

                            // Set the clicked profile as active
                            profileMenuItem.Header = $"{profileCopy.Name} (Active)";
                            profileMenuItem.IsChecked = true;
                            _databaseService.SetCurrentProfile(profileCopy.Id);

                            // Note: Native system tray menus in Avalonia close automatically on click
                            // and don't support keeping the menu open like WPF's StaysOpenOnClick.
                            // The menu will close, but the changes will be visible the next time it opens.
                            // To see the updated state, the user will need to reopen the menu.
                        };

                        menu.Add(profileMenuItem);
                    }
                }

                menu.Add(new NativeMenuItemSeparator());

                // Exit menu item
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
                object? page = null;

                // Create page instance with appropriate ViewModel
                if (item.PageType == typeof(ProfilesPage))
                {
                    if (_databaseService != null)
                    {
                        var viewModel = new ProfilesViewModel(_databaseService);
                        page = new ProfilesPage(viewModel);
                        viewModel.OnNavigatedTo();
                    }
                }
                else if (item.PageType == typeof(ThumbnailSettingsPage))
                {
                    if (_databaseService != null && _thumbnailWindowService != null)
                    {
                        var viewModel = new ThumbnailSettingsViewModel(_databaseService, _thumbnailWindowService);
                        page = new ThumbnailSettingsPage(viewModel);
                        viewModel.OnNavigatedTo();
                    }
                }
                else if (item.PageType == typeof(ClientGroupingPage))
                {
                    if (_databaseService != null && _thumbnailWindowService != null)
                    {
                        var viewModel = new ClientGroupingViewModel(_databaseService, _thumbnailWindowService, _hotkeyService);
                        page = new ClientGroupingPage(viewModel);
                        viewModel.OnNavigatedTo();
                    }
                }
                else if (item.PageType == typeof(GridLayoutPage))
                {
                    if (_databaseService != null && _thumbnailWindowService != null)
                    {
                        var viewModel = new GridLayoutViewModel(_databaseService, _thumbnailWindowService);
                        page = new GridLayoutPage(viewModel);
                        viewModel.OnNavigatedTo();
                    }
                }
                else if (item.PageType == typeof(ProcessManagementPage))
                {
                    if (_databaseService != null)
                    {
                        var viewModel = new ProcessManagementViewModel(_databaseService);
                        page = new ProcessManagementPage(viewModel);
                        viewModel.OnNavigatedTo();
                    }
                }
                else if (item.PageType == typeof(SettingsPage))
                {
                    if (_databaseService != null && _thumbnailWindowService != null && _application != null)
                    {
                        var viewModel = new SettingsViewModel(_databaseService, _thumbnailWindowService, _application);
                        page = new SettingsPage(viewModel);
                        viewModel.OnNavigatedTo();
                    }
                }

                if (page != null)
                {
                    ViewModel.CurrentPage = page;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error navigating to page {item.PageType?.Name}: {ex.Message}");
            }
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
            // Only set focus if we're already on the ThumbnailSettingsPage and not during navigation
            // This prevents double-calling SetFocusOnFirstThumbnail which causes flicker
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
            // Hide the window instead of closing it
            e.Cancel = true;
            Hide();
        }

        protected override void OnClosed(EventArgs e)
        {
            // Only dispose resources when actually closing (from Exit menu)
            // Don't dispose here since we're hiding instead of closing
            base.OnClosed(e);
        }
    }
}

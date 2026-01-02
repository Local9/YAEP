using Avalonia.Controls;
using Avalonia.Platform;
using SukiUI.Controls;
using System.Reflection;
using System.Runtime.Versioning;
using YAEP.Models;
using YAEP.ViewModels;
using YAEP.ViewModels.Pages;
using YAEP.Views.Pages;
using YAEP.Views.Windows;

namespace YAEP.Views
{
    public partial class MainWindow : SukiWindow
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

            // Hook into SukiSideMenu selection
            if (SideMenu != null)
            {
                SideMenu.PropertyChanged += SideMenu_PropertyChanged;
                // Select first item (Profiles page) and ensure it's displayed
                if (SideMenu.Items.Count > 0 && SideMenu.Items[0] is SukiSideMenuItem firstItem)
                {
                    SideMenu.SelectedItem = firstItem;
                    // Manually trigger page creation to ensure it displays on startup
                    HandleMenuItemSelection(firstItem);
                }
            }

            if (viewModel != null)
            {
                viewModel.PropertyChanged += ViewModel_PropertyChanged;
            }

            InitializeHotkeyService();

            this.Deactivated += MainWindow_Deactivated;
            this.Activated += MainWindow_Activated;
            this.Opened += MainWindow_Opened;
            this.Closing += MainWindow_Closing;
        }

        [SupportedOSPlatform("windows")]
        private void InitializeHotkeyService()
        {
            _hotkeyService?.Initialize(this);
        }

        private void MainWindow_Opened(object? sender, EventArgs e)
        {
            _thumbnailWindowService?.Start();

            InitializeTrayIcon();
            OpenMumbleLinksWindowIfNeeded();

            // Add click handler for Settings menu item (FooterContent items may not trigger PropertyChanged properly)
            if (SettingsMenuItem != null)
            {
                SettingsMenuItem.PointerPressed += SettingsMenuItem_PointerPressed;
            }

            // Ensure Profiles page is displayed on startup if not already shown
            if (SideMenu != null && SideMenu.SelectedItem is SukiSideMenuItem selectedItem)
            {
                // Check if the selected item's page content is empty
                ContentControl? pageContent = selectedItem.Name switch
                {
                    nameof(ProfilesMenuItem) => ProfilesPageContent,
                    nameof(ThumbnailSettingsMenuItem) => ThumbnailSettingsPageContent,
                    nameof(ClientGroupingMenuItem) => ClientGroupingPageContent,
                    nameof(GridLayoutMenuItem) => GridLayoutPageContent,
                    nameof(ProcessManagementMenuItem) => ProcessManagementPageContent,
                    nameof(MumbleLinksMenuItem) => MumbleLinksPageContent,
                    nameof(SettingsMenuItem) => SettingsPageContent,
                    _ => null
                };

                if (pageContent != null && pageContent.Content == null)
                {
                    HandleMenuItemSelection(selectedItem);
                }
            }
            else if (SideMenu != null && SideMenu.Items.Count > 0 && SideMenu.Items[0] is SukiSideMenuItem firstItem)
            {
                // Fallback: if nothing is selected, select and show the first item (Profiles)
                SideMenu.SelectedItem = firstItem;
                HandleMenuItemSelection(firstItem);
            }
        }

        private void SettingsMenuItem_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            if (SettingsMenuItem != null && SideMenu != null)
            {
                SideMenu.SelectedItem = SettingsMenuItem;
                HandleMenuItemSelection(SettingsMenuItem);
            }
        }

        private void OpenMumbleLinksWindowIfNeeded()
        {
            if (_databaseService == null)
                return;

            System.Collections.Generic.List<MumbleLink> selectedLinks = _databaseService.GetSelectedMumbleLinks();
            DatabaseService.MumbleLinksOverlaySettings settings = _databaseService.GetMumbleLinksOverlaySettings();

            if (selectedLinks.Count > 0)
            {
                MumbleLinksWindow? existingWindow = FindExistingMumbleLinksWindow();
                if (existingWindow != null)
                {
                    existingWindow.UpdateLinks(selectedLinks);
                    existingWindow.Topmost = settings.AlwaysOnTop;
                    existingWindow.Activate();
                    return;
                }

                MumbleLinksViewModel viewModel = new MumbleLinksViewModel(_databaseService);
                MumbleLinksWindow window = new MumbleLinksWindow(viewModel, selectedLinks);
                window.Topmost = settings.AlwaysOnTop;
                window.Show();
                window.Activate();
            }
        }

        private MumbleLinksWindow? FindExistingMumbleLinksWindow()
        {
            if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.Windows.OfType<MumbleLinksWindow>().FirstOrDefault();
            }
            return null;
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
                    string? assemblyName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
                    Uri iconUri = new Uri($"avares://{assemblyName}/Assets/yaep-icon.ico");
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
                NativeMenu menu = new NativeMenu();

                NativeMenuItem showMenuItem = new NativeMenuItem("Show");
                showMenuItem.Click += ShowMenuItem_Click;
                menu.Add(showMenuItem);

                menu.Add(new NativeMenuItemSeparator());

                if (_databaseService != null)
                {
                    System.Collections.Generic.List<Profile> profiles = _databaseService.GetProfiles();
                    foreach (Profile profile in profiles)
                    {
                        string header = profile.IsActive ? $"{profile.Name} (Active)" : profile.Name;
                        NativeMenuItem profileMenuItem = new NativeMenuItem(header)
                        {
                            IsChecked = profile.IsActive
                        };

                        Profile profileCopy = profile;
                        profileMenuItem.Click += (s, args) =>
                        {
                            foreach (NativeMenuItem item in menu.Items.OfType<NativeMenuItem>())
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

                NativeMenuItem exitMenuItem = new NativeMenuItem("Exit");
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
            // Selection is now handled by SukiSideMenu directly
        }

        private void SideMenu_PropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property.Name == "SelectedItem" && sender is SukiSideMenu sideMenu && sideMenu.SelectedItem is SukiSideMenuItem selectedItem)
            {
                HandleMenuItemSelection(selectedItem);
            }
        }

        private void HandleMenuItemSelection(SukiSideMenuItem menuItem)
        {
            if (menuItem == null)
                return;

            // Determine which page to create based on the menu item
            Type? pageType = menuItem.Name switch
            {
                nameof(ProfilesMenuItem) => typeof(ProfilesPage),
                nameof(ThumbnailSettingsMenuItem) => typeof(ThumbnailSettingsPage),
                nameof(ClientGroupingMenuItem) => typeof(ClientGroupingPage),
                nameof(GridLayoutMenuItem) => typeof(GridLayoutPage),
                nameof(ProcessManagementMenuItem) => typeof(ProcessManagementPage),
                nameof(MumbleLinksMenuItem) => typeof(MumbleLinksPage),
                nameof(SettingsMenuItem) => typeof(SettingsPage),
                _ => null
            };

            if (pageType == null)
                return;

            // Get the ContentControl for this menu item's PageContent
            ContentControl? pageContent = menuItem.Name switch
            {
                nameof(ProfilesMenuItem) => ProfilesPageContent,
                nameof(ThumbnailSettingsMenuItem) => ThumbnailSettingsPageContent,
                nameof(ClientGroupingMenuItem) => ClientGroupingPageContent,
                nameof(GridLayoutMenuItem) => GridLayoutPageContent,
                nameof(ProcessManagementMenuItem) => ProcessManagementPageContent,
                nameof(MumbleLinksMenuItem) => MumbleLinksPageContent,
                nameof(SettingsMenuItem) => SettingsPageContent,
                _ => null
            };

            if (pageContent == null)
                return;

            // Find the currently active page from all ContentControls
            object? previousPage = null;
            Type? previousPageType = null;

            // Check all ContentControls to find which one currently has content
            ContentControl[] allContentControls = new[]
            {
                ProfilesPageContent,
                ThumbnailSettingsPageContent,
                ClientGroupingPageContent,
                GridLayoutPageContent,
                ProcessManagementPageContent,
                MumbleLinksPageContent,
                SettingsPageContent
            };

            foreach (ContentControl? contentControl in allContentControls)
            {
                if (contentControl != null && contentControl.Content != null && contentControl != pageContent)
                {
                    previousPage = contentControl.Content;
                    previousPageType = previousPage.GetType();
                    break;
                }
            }

            // Handle navigation away from previous page if it's different from the new page
            if (previousPageType != null && previousPageType != pageType)
            {
                HandlePageNavigationAway(previousPage, previousPageType, pageType);
            }

            // Create and set the new page if it doesn't exist or is different
            if (pageContent.Content == null || previousPageType != pageType)
            {
                object? newPage = CreatePage(pageType);
                if (newPage != null)
                {
                    pageContent.Content = newPage;
                }
            }
        }

        private void NavigateToPage(NavigationItem? item)
        {
            if (item?.PageType == null || ViewModel == null)
                return;

            try
            {
                object? previousPage = _previousPage ?? ViewModel.CurrentPage;
                Type? previousPageType = GetPageType(previousPage);

                if (previousPageType != null && previousPageType != item.PageType)
                {
                    HandlePageNavigationAway(previousPage, previousPageType, item.PageType);
                }

                object? newPage = CreatePage(item.PageType);
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
                System.Diagnostics.Debug.WriteLine($"NavigateToPage: Leaving ThumbnailSettingsPage");
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

                PropertyInfo? viewModelProperty = pageType.GetProperty("ViewModel", BindingFlags.Public | BindingFlags.Instance);
                if (viewModelProperty?.GetValue(page) is { } viewModel)
                {
                    MethodInfo? onNavigatedFromMethod = viewModel.GetType().GetMethod("OnNavigatedFrom", BindingFlags.Public | BindingFlags.Instance);
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
                _ when pageType == typeof(MumbleLinksPage) => CreateMumbleLinksPage(),
                _ => null
            };
        }

        private object? CreateProfilesPage()
        {
            if (_databaseService == null)
                return null;

            ProfilesViewModel viewModel = new ProfilesViewModel(_databaseService, _hotkeyService);
            ProfilesPage page = new ProfilesPage(viewModel);
            viewModel.OnNavigatedTo();
            return page;
        }

        private object? CreateThumbnailSettingsPage()
        {
            if (_databaseService == null || _thumbnailWindowService == null)
                return null;

            ThumbnailSettingsViewModel viewModel = new ThumbnailSettingsViewModel(_databaseService, _thumbnailWindowService);
            ThumbnailSettingsPage page = new ThumbnailSettingsPage(viewModel);
            viewModel.OnNavigatedTo();
            return page;
        }

        private object? CreateClientGroupingPage()
        {
            if (_databaseService == null || _thumbnailWindowService == null)
                return null;

            ClientGroupingViewModel viewModel = new ClientGroupingViewModel(_databaseService, _thumbnailWindowService, _hotkeyService);
            ClientGroupingPage page = new ClientGroupingPage(viewModel);
            viewModel.OnNavigatedTo();
            return page;
        }

        private object? CreateGridLayoutPage()
        {
            if (_databaseService == null || _thumbnailWindowService == null)
                return null;

            GridLayoutViewModel viewModel = new GridLayoutViewModel(_databaseService, _thumbnailWindowService);
            GridLayoutPage page = new GridLayoutPage(viewModel);
            viewModel.OnNavigatedTo(this);
            return page;
        }

        private object? CreateProcessManagementPage()
        {
            if (_databaseService == null)
                return null;

            ProcessManagementViewModel viewModel = new ProcessManagementViewModel(_databaseService);
            ProcessManagementPage page = new ProcessManagementPage(viewModel);
            viewModel.OnNavigatedTo();
            return page;
        }

        private object? CreateSettingsPage()
        {
            if (_databaseService == null || _thumbnailWindowService == null || _application == null)
                return null;

            SettingsViewModel viewModel = new SettingsViewModel(_databaseService, _thumbnailWindowService, _application);
            SettingsPage page = new SettingsPage(viewModel);
            viewModel.OnNavigatedTo();
            return page;
        }

        private object? CreateMumbleLinksPage()
        {
            if (_databaseService == null)
                return null;

            MumbleLinksViewModel viewModel = new MumbleLinksViewModel(_databaseService);
            MumbleLinksPage page = new MumbleLinksPage(viewModel);
            viewModel.OnNavigatedTo();
            return page;
        }

        private void MainWindow_WindowStateChanged(object? sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Normal || this.WindowState == WindowState.Maximized)
            {
                if (ViewModel?.SelectedMenuItem?.PageType == typeof(ThumbnailSettingsPage))
                {
                    _thumbnailWindowService?.SetFocusOnFirstThumbnail();
                }
            }
        }

        private void MainWindow_Deactivated(object? sender, EventArgs e)
        {
            // Focus tracking is now handled automatically by the service
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


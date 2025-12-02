using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using YAEP.Interface;
using YAEP.Services;
using YAEP.ViewModels.Windows;
using TrayIconMenuItem = System.Windows.Controls.MenuItem;
using TrayIconSeparator = System.Windows.Controls.Separator;

namespace YAEP.Views.Windows
{
    public partial class MainWindow : INavigationWindow
    {
        public MainWindowViewModel ViewModel { get; }

        private readonly IThumbnailWindowService _thumbnailWindowService;
        private readonly HotkeyService _hotkeyService;
        private readonly DatabaseService _databaseService;

        public MainWindow(
            MainWindowViewModel viewModel,
            INavigationViewPageProvider navigationViewPageProvider,
            INavigationService navigationService,
            IThumbnailWindowService thumbnailWindowService,
            HotkeyService hotkeyService,
            DatabaseService databaseService
        )
        {
            ViewModel = viewModel;
            _thumbnailWindowService = thumbnailWindowService;
            _hotkeyService = hotkeyService;
            _databaseService = databaseService;
            DataContext = this;

            SystemThemeWatcher.Watch(this);

            InitializeComponent();
            SetPageService(navigationViewPageProvider);

            navigationService.SetNavigationControl(RootNavigation);

            _thumbnailWindowService.Start();
            _hotkeyService.Initialize(this);

            this.StateChanged += MainWindow_StateChanged;
            this.Deactivated += MainWindow_Deactivated;
            this.Activated += MainWindow_Activated;

            yaepTaskbarIcon.TrayContextMenuOpen += YaepTaskbarIcon_TrayContextMenuOpen;
        }

        private void YaepTaskbarIcon_TrayContextMenuOpen(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Windows.Controls.ContextMenu contextMenu = yaepTaskbarIcon.ContextMenu;

                if (contextMenu is null) return;

                contextMenu.Items.Clear();

                TrayIconMenuItem _showMenuItem = new TrayIconMenuItem
                {
                    Header = "Show"
                };

                TrayIconMenuItem _exitMenuItem = new TrayIconMenuItem
                {
                    Header = "Exit"
                };

                contextMenu.Items.Add(_showMenuItem);
                contextMenu.Items.Add(new TrayIconSeparator());

                _showMenuItem.Click += ShowMenuItem_Click;
                _exitMenuItem.Click += ExitMenuItem_Click;

                List<DatabaseService.Profile> profiles = _databaseService.GetProfiles();

                foreach (DatabaseService.Profile profile in profiles)
                {
                    string header = profile.IsActive ? $"{profile.Name} (Active)" : profile.Name;
                    TrayIconMenuItem profileMenuItem = new TrayIconMenuItem
                    {
                        Header = header,
                        StaysOpenOnClick = true,
                        IsChecked = profile.IsActive,
                    };
                    profileMenuItem.Click += (s, args) =>
                    {
                        foreach (TrayIconMenuItem item in contextMenu.Items.OfType<TrayIconMenuItem>())
                        {
                            string itemHeader = item.Header.ToString() ?? "";
                            if (itemHeader.Contains("(Active)"))
                            {
                                item.Header = itemHeader.Replace(" (Active)", "");
                                item.IsChecked = false;
                            }
                        }
                        profileMenuItem.Header = $"{profile.Name} (Active)";
                        _databaseService.SetCurrentProfile(profile.Id);
                    };
                    contextMenu.Items.Add(profileMenuItem);
                }

                contextMenu.Items.Add(new TrayIconSeparator());
                contextMenu.Items.Add(_exitMenuItem);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error building tray context menu: {ex.Message}");
            }
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (this.WindowState == System.Windows.WindowState.Minimized)
            {
                _thumbnailWindowService.ResumeFocusCheckOnAllThumbnails();
            }
            else if (this.WindowState == System.Windows.WindowState.Normal || this.WindowState == System.Windows.WindowState.Maximized)
            {
                INavigationViewItem? selectedItem = RootNavigation.SelectedItem;
                if (selectedItem is NavigationViewItem navItem && navItem.TargetPageType == typeof(Views.Pages.ThumbnailSettingsPage))
                {
                    _thumbnailWindowService.SetFocusOnFirstThumbnail();
                }
            }
        }

        private void MainWindow_Deactivated(object? sender, EventArgs e)
        {
            INavigationViewItem? selectedItem = RootNavigation.SelectedItem;
            if (selectedItem is NavigationViewItem navItem && navItem.TargetPageType == typeof(Views.Pages.ThumbnailSettingsPage))
            {
                _thumbnailWindowService.ResumeFocusCheckOnAllThumbnails();
            }
        }

        private void MainWindow_Activated(object? sender, EventArgs e)
        {
            INavigationViewItem? selectedItem = RootNavigation.SelectedItem;
            if (selectedItem is NavigationViewItem navItem && navItem.TargetPageType == typeof(Views.Pages.ThumbnailSettingsPage))
            {
                _thumbnailWindowService.SetFocusOnFirstThumbnail();
            }
        }

        #region INavigationWindow methods

        public INavigationView GetNavigation() => RootNavigation;

        public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);

        public void SetPageService(INavigationViewPageProvider navigationViewPageProvider) => RootNavigation.SetPageProviderService(navigationViewPageProvider);

        public void ShowWindow() => Show();

        public void CloseWindow() => Hide();

        #endregion INavigationWindow methods

        protected override void OnClosed(EventArgs e) => Hide();

        INavigationView INavigationWindow.GetNavigation()
        {
            throw new NotImplementedException();
        }

        public void SetServiceProvider(IServiceProvider serviceProvider)
        {
            throw new NotImplementedException();
        }

        private void ShowMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Show();
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            yaepTaskbarIcon.Dispose();

            _thumbnailWindowService?.Stop();
            _hotkeyService?.Dispose();

            base.OnClosed(e);

            Application.Current.Shutdown();
        }
    }
}

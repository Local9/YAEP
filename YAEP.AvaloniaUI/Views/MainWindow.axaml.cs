using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
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
            if (ViewModel?.SelectedMenuItem?.PageType == typeof(ThumbnailSettingsPage))
            {
                _thumbnailWindowService?.SetFocusOnFirstThumbnail();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _thumbnailWindowService?.Stop();
            _hotkeyService?.Dispose();
            base.OnClosed(e);
        }
    }
}

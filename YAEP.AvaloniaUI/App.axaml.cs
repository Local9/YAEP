using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using SukiUI;
using SukiUI.Enums;
using YAEP.ViewModels;
using YAEP.Views;
using YAEP.Views.Windows;

namespace YAEP
{
    public partial class App : Application
    {
        private static DatabaseService? _databaseService;
        private static IThumbnailWindowService? _thumbnailWindowService;
        private static HotkeyService? _hotkeyService;
        private static DrawerWindowService? _drawerWindowService;

        /// <summary>
        /// Gets the thumbnail window service instance.
        /// </summary>
        public static IThumbnailWindowService? ThumbnailWindowService => _thumbnailWindowService;

        /// <summary>
        /// Gets the drawer window service instance.
        /// </summary>
        public static DrawerWindowService? DrawerWindowService => _drawerWindowService;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                DisableAvaloniaDataAnnotationValidation();

                desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnExplicitShutdown;

                _databaseService = new DatabaseService();
                _thumbnailWindowService = new ThumbnailWindowService(_databaseService);
                _hotkeyService = new HotkeyService(_databaseService, _thumbnailWindowService);
                _drawerWindowService = new DrawerWindowService(_databaseService);

                LoadThemeSettings();

                MainWindowViewModel mainWindowViewModel = new MainWindowViewModel();

                desktop.MainWindow = new MainWindow(
                    mainWindowViewModel,
                    _databaseService,
                    _thumbnailWindowService,
                    _hotkeyService,
                    this,
                    _drawerWindowService);

                // Initialize drawer window service
                _drawerWindowService?.Initialize();

                _ = CheckForUpdatesAsync(desktop.MainWindow);
            }

            base.OnFrameworkInitializationCompleted();
        }

        /// <summary>
        /// Checks for updates on GitHub and shows a notification if a new version is available.
        /// </summary>
        private async Task CheckForUpdatesAsync(Avalonia.Controls.Window? mainWindow)
        {
            try
            {
                await Task.Delay(2000);

                using GitHubReleaseService releaseService = new GitHubReleaseService();
                GitHubReleaseInfo? releaseInfo = await releaseService.CheckForUpdateAsync();

                if (releaseInfo != null && mainWindow != null)
                {
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        UpdateNotificationWindow updateWindow = new UpdateNotificationWindow(releaseInfo);
                        await updateWindow.ShowDialog(mainWindow);
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking for updates: {ex.Message}");
            }
        }

        private void DisableAvaloniaDataAnnotationValidation()
        {
            DataAnnotationsValidationPlugin[] dataValidationPluginsToRemove =
                BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

            foreach (DataAnnotationsValidationPlugin? plugin in dataValidationPluginsToRemove)
            {
                BindingPlugins.DataValidators.Remove(plugin);
            }
        }

        private void LoadThemeSettings()
        {
            if (_databaseService == null)
                return;

            string? themeSetting = _databaseService.GetAppSetting("Theme");
            ThemeVariant savedTheme = ThemeVariant.Dark;
            if (!string.IsNullOrEmpty(themeSetting))
            {
                savedTheme = themeSetting switch
                {
                    "Light" => ThemeVariant.Light,
                    "Dark" => ThemeVariant.Dark,
                    _ => ThemeVariant.Dark
                };
            }
            SukiTheme.GetInstance().ChangeBaseTheme(savedTheme);

            string? colorSetting = _databaseService.GetAppSetting("ThemeColor");
            SukiColor savedColor = SukiColor.Red;
            if (!string.IsNullOrEmpty(colorSetting) && System.Enum.TryParse<SukiColor>(colorSetting, out SukiColor parsedColor))
            {
                savedColor = parsedColor;
            }

            SukiTheme? sukiTheme = this.Styles.OfType<SukiUI.SukiTheme>().FirstOrDefault();
            if (sukiTheme != null)
            {
                sukiTheme.ThemeColor = savedColor;
            }
        }
    }
}

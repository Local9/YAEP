using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using System.Linq;
using YAEP.Interface;
using YAEP.Services;
using YAEP.ViewModels;
using YAEP.Views;

namespace YAEP
{
    public partial class App : Application
    {
        private static DatabaseService? _databaseService;
        private static IThumbnailWindowService? _thumbnailWindowService;
        private static HotkeyService? _hotkeyService;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
                // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
                DisableAvaloniaDataAnnotationValidation();

                // Initialize services
                _databaseService = new DatabaseService();
                _thumbnailWindowService = new ThumbnailWindowService(_databaseService);
                _hotkeyService = new HotkeyService(_databaseService, _thumbnailWindowService);

                // Create MainWindowViewModel
                var mainWindowViewModel = new MainWindowViewModel();

                // Create MainWindow with services
                desktop.MainWindow = new MainWindow(
                    mainWindowViewModel,
                    _databaseService,
                    _thumbnailWindowService,
                    _hotkeyService,
                    this);
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void DisableAvaloniaDataAnnotationValidation()
        {
            // Get an array of plugins to remove
            var dataValidationPluginsToRemove =
                BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

            // remove each entry found
            foreach (var plugin in dataValidationPluginsToRemove)
            {
                BindingPlugins.DataValidators.Remove(plugin);
            }
        }
    }
}

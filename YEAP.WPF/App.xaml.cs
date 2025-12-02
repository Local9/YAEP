using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Windows.Threading;
using Wpf.Ui;
using Wpf.Ui.DependencyInjection;
using YAEP.Interface;
using YAEP.Services;
using YAEP.ViewModels.Pages;
using YAEP.ViewModels.Windows;
using YAEP.Views.Pages;
using YAEP.Views.Windows;

namespace YAEP
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        // The.NET Generic Host provides dependency injection, configuration, logging, and other services.
        // https://docs.microsoft.com/dotnet/core/extensions/generic-host
        // https://docs.microsoft.com/dotnet/core/extensions/dependency-injection
        // https://docs.microsoft.com/dotnet/core/extensions/configuration
        // https://docs.microsoft.com/dotnet/core/extensions/logging
        private static readonly IHost _host = Host
            .CreateDefaultBuilder()
            .ConfigureAppConfiguration(c =>
            {
                string? basePath = Path.GetDirectoryName(AppContext.BaseDirectory);

                if (basePath is null)
                    basePath = Directory.GetCurrentDirectory();

                c.SetBasePath(basePath);
            })
            .ConfigureServices((context, services) =>
            {
                services.AddNavigationViewPageProvider();

                services.AddHostedService<ApplicationHostService>();

                services.AddSingleton<IThemeService, ThemeService>();
                services.AddSingleton<ITaskBarService, TaskBarService>();
                services.AddSingleton<INavigationService, NavigationService>();
                services.AddSingleton<IThumbnailWindowService, ThumbnailWindowService>();
                services.AddSingleton<HotkeyService>();
                services.AddSingleton<DatabaseService>();
                services.AddSingleton<INavigationWindow, MainWindow>();
                services.AddSingleton<MainWindowViewModel>();

                services.AddSingleton<SettingsPage>();
                services.AddSingleton<SettingsViewModel>();
                services.AddSingleton<GridLayoutPage>();
                services.AddSingleton<GridLayoutViewModel>();
                services.AddSingleton<ClientGroupingPage>();
                services.AddSingleton<ClientGroupingViewModel>();
                services.AddSingleton<ThumbnailSettingsPage>();
                services.AddSingleton<ThumbnailSettingsViewModel>();
                services.AddSingleton<ProfilesPage>();
                services.AddSingleton<ProfilesViewModel>();
                services.AddSingleton<ProcessManagementPage>();
                services.AddSingleton<ProcessManagementViewModel>();
            }).Build();

        /// <summary>
        /// Gets services.
        /// </summary>
        public static IServiceProvider Services
        {
            get { return _host.Services; }
        }

        /// <summary>
        /// Occurs when the application is loading.
        /// </summary>
        private async void OnStartup(object sender, StartupEventArgs e)
        {
            await _host.StartAsync();
        }

        /// <summary>
        /// Occurs when the application is closing.
        /// </summary>
        private async void OnExit(object sender, ExitEventArgs e)
        {
            await _host.StopAsync();

            _host.Dispose();
        }

        /// <summary>
        /// Occurs when an exception is thrown by an application but not handled.
        /// </summary>
        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
        }
    }
}

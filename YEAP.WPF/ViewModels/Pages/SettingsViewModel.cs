using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Appearance;
using YAEP.Interface;
using YAEP.Services;

namespace YAEP.ViewModels.Pages
{
    public partial class SettingsViewModel : ObservableObject, INavigationAware
    {
        private readonly DatabaseService _databaseService;
        private readonly IThumbnailWindowService _thumbnailWindowService;
        private bool _isInitialized = false;
        private bool _isLoadingSettings = false;

        [ObservableProperty]
        private string _appVersion = String.Empty;

        [ObservableProperty]
        private ApplicationTheme _currentTheme = ApplicationTheme.Unknown;

        [ObservableProperty]
        private bool _thumbnailDraggingEnabled = true;

        [ObservableProperty]
        private bool _startHidden = false;

        public SettingsViewModel(DatabaseService databaseService, IThumbnailWindowService thumbnailWindowService)
        {
            _databaseService = databaseService;
            _thumbnailWindowService = thumbnailWindowService;
        }

        public Task OnNavigatedToAsync()
        {
            if (!_isInitialized)
                InitializeViewModel();

            LoadAppSettings();

            return Task.CompletedTask;
        }

        public Task OnNavigatedFromAsync()
        {
            return Task.CompletedTask;
        }

        private void InitializeViewModel()
        {
            CurrentTheme = ApplicationThemeManager.GetAppTheme();
            AppVersion = $"YAEP - Yet Another EVE Preview - {GetAssemblyVersion()}";

            _isInitialized = true;
        }

        private void LoadAppSettings()
        {
            _isLoadingSettings = true;
            try
            {
                ThumbnailDraggingEnabled = _databaseService.GetThumbnailDraggingEnabled();
                StartHidden = _databaseService.GetStartHidden();
            }
            finally
            {
                _isLoadingSettings = false;
            }
        }

        private string GetAssemblyVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString()
                ?? String.Empty;
        }

        [RelayCommand]
        private void OnChangeTheme(string parameter)
        {
            switch (parameter)
            {
                case "theme_light":
                    if (CurrentTheme == ApplicationTheme.Light)
                        break;

                    ApplicationThemeManager.Apply(ApplicationTheme.Light);
                    CurrentTheme = ApplicationTheme.Light;

                    break;

                default:
                    if (CurrentTheme == ApplicationTheme.Dark)
                        break;

                    ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                    CurrentTheme = ApplicationTheme.Dark;

                    break;
            }
        }


        partial void OnThumbnailDraggingEnabledChanged(bool value)
        {
            if (!_isLoadingSettings)
            {
                _databaseService.SetThumbnailDraggingEnabled(value);

                // Update all thumbnail windows to reflect the new setting
                _thumbnailWindowService.UpdateAllThumbnails();
            }
        }

        partial void OnStartHiddenChanged(bool value)
        {
            if (!_isLoadingSettings)
            {
                _databaseService.SetStartHidden(value);
            }
        }
    }
}

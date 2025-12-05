using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.Input;
using YAEP.Interface;
using YAEP.Services;

namespace YAEP.ViewModels.Pages
{
    public partial class SettingsViewModel : ViewModelBase
    {
        private readonly DatabaseService _databaseService;
        private readonly IThumbnailWindowService _thumbnailWindowService;
        private readonly Application _application;
        private bool _isInitialized = false;
        private bool _isLoadingSettings = false;

        [ObservableProperty]
        private string _appVersion = String.Empty;

        [ObservableProperty]
        private ThemeVariant _currentTheme = ThemeVariant.Default;

        [ObservableProperty]
        private bool _thumbnailDraggingEnabled = true;

        [ObservableProperty]
        private bool _startHidden = false;

        public SettingsViewModel(DatabaseService databaseService, IThumbnailWindowService thumbnailWindowService, Application application)
        {
            _databaseService = databaseService;
            _thumbnailWindowService = thumbnailWindowService;
            _application = application;
        }

        public void OnNavigatedTo()
        {
            if (!_isInitialized)
                InitializeViewModel();

            LoadAppSettings();
        }

        public void OnNavigatedFrom()
        {
        }

        private void InitializeViewModel()
        {
            CurrentTheme = _application.RequestedThemeVariant;
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
            ThemeVariant newTheme = parameter switch
            {
                "theme_light" => ThemeVariant.Light,
                "theme_dark" => ThemeVariant.Dark,
                _ => ThemeVariant.Default
            };

            if (CurrentTheme == newTheme)
                return;

            _application.RequestedThemeVariant = newTheme;
            CurrentTheme = newTheme;
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


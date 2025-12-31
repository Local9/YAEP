using Avalonia.Styling;
using SukiUI;
using SukiUI.Enums;

namespace YAEP.ViewModels.Pages
{
    public partial class SettingsViewModel(DatabaseService databaseService, IThumbnailWindowService thumbnailWindowService, Application application) : ViewModelBase
    {
        private readonly DatabaseService _databaseService = databaseService;
        private readonly IThumbnailWindowService _thumbnailWindowService = thumbnailWindowService;
        private readonly Application _application = application;
        private bool _isInitialized = false;
        private bool _isLoadingSettings = false;

        [ObservableProperty]
        private string _appVersion = String.Empty;

        [ObservableProperty]
        private string _libraries = String.Empty;

        [ObservableProperty]
        private ThemeVariant _currentTheme = ThemeVariant.Dark;

        [ObservableProperty]
        private SukiColor _currentThemeColor = SukiColor.Red;

        [ObservableProperty]
        private bool _thumbnailDraggingEnabled = true;

        [ObservableProperty]
        private bool _startHidden = false;

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
            AppVersion = $"YAEP - Yet Another EVE Preview - {GetAssemblyVersion()}";
            Libraries = "This product includes software developed by the following open source projects:\n" +
                        "- AvaloniaUI\n" +
                        "- SukiUI\n" +
                        "- SQLite\n" +
                        "- Lucide.Avalonia";

            _isInitialized = true;
        }

        private void LoadAppSettings()
        {
            _isLoadingSettings = true;
            try
            {
                ThumbnailDraggingEnabled = _databaseService.GetThumbnailDraggingEnabled();
                StartHidden = _databaseService.GetStartHidden();

                // Load theme setting
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
                CurrentTheme = savedTheme;
                SukiTheme.GetInstance().ChangeBaseTheme(savedTheme);

                // Load theme color setting
                string? colorSetting = _databaseService.GetAppSetting("ThemeColor");
                SukiColor savedColor = SukiColor.Red;
                if (!string.IsNullOrEmpty(colorSetting) && Enum.TryParse<SukiColor>(colorSetting, out SukiColor parsedColor))
                {
                    savedColor = parsedColor;
                }
                CurrentThemeColor = savedColor;
                SetSukiThemeColor(savedColor);
            }
            finally
            {
                _isLoadingSettings = false;
            }
        }

        private static string GetAssemblyVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString()
                ?? String.Empty;
        }

        partial void OnCurrentThemeChanged(ThemeVariant value)
        {
            if (!_isLoadingSettings)
            {
                SukiTheme.GetInstance().ChangeBaseTheme(value);
                _databaseService.SetAppSetting("Theme", value.ToString());
            }
        }

        private void SetSukiThemeColor(SukiColor color)
        {
            // Find SukiTheme in Application styles and set ThemeColor property
            SukiTheme? sukiTheme = _application.Styles.OfType<SukiTheme>().FirstOrDefault();
            if (sukiTheme != null)
            {
                sukiTheme.ThemeColor = color;
            }
        }

        partial void OnCurrentThemeColorChanged(SukiColor value)
        {
            if (!_isLoadingSettings)
            {
                SetSukiThemeColor(value);
                _databaseService.SetAppSetting("ThemeColor", value.ToString());
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


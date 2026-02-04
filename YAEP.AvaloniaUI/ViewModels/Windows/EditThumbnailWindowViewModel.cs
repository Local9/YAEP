using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using YAEP.Helpers;
using YAEP.Models;
using YAEP.Views.Windows;

namespace YAEP.ViewModels.Windows
{
    public partial class EditThumbnailWindowViewModel : ViewModelBase
    {
        private readonly DatabaseService _databaseService;
        private readonly IThumbnailWindowService _thumbnailWindowService;
        private readonly ThumbnailSetting _thumbnailSetting;
        private readonly Action? _onSettingsSaved;
        private EditThumbnailWindow? _window;
        private bool _isLoadingSettings = false;
        private bool _isCalculatingHeight = false;

        [ObservableProperty]
        private string _windowTitle;

        [ObservableProperty]
        private int _width;

        [ObservableProperty]
        private int _height;

        [ObservableProperty]
        private double _opacity;

        [ObservableProperty]
        private string _focusBorderColor = "#0078D4";

        [ObservableProperty]
        private int _focusBorderThickness = 3;

        [ObservableProperty]
        private WindowRatio _ratio = WindowRatio.None;

        /// <summary>
        /// Gets all available WindowRatio enum values for ComboBox binding.
        /// </summary>
        public Array WindowRatioValues => Enum.GetValues(typeof(WindowRatio));

        [ObservableProperty]
        private List<ThumbnailSetting> _availableThumbnails = new();

        [ObservableProperty]
        private ThumbnailSetting? _selectedThumbnailToCopy;

        /// <summary>
        /// Gets whether settings can be copied from the selected thumbnail.
        /// </summary>
        public bool CanCopySettings => SelectedThumbnailToCopy != null;

        partial void OnSelectedThumbnailToCopyChanged(ThumbnailSetting? value)
        {
            OnPropertyChanged(nameof(CanCopySettings));
        }

        private int _originalWidth;
        private int _originalHeight;
        private double _originalOpacity;
        private string _originalFocusBorderColor = "#0078D4";
        private int _originalFocusBorderThickness = 3;

        public EditThumbnailWindowViewModel(
            DatabaseService databaseService,
            IThumbnailWindowService thumbnailWindowService,
            ThumbnailSetting thumbnailSetting,
            Action? onSettingsSaved = null,
            EditThumbnailWindow? window = null)
        {
            _databaseService = databaseService;
            _thumbnailWindowService = thumbnailWindowService;
            _thumbnailSetting = thumbnailSetting;
            _onSettingsSaved = onSettingsSaved;
            _window = window;

            WindowTitle = thumbnailSetting.WindowTitle;
            Width = thumbnailSetting.Config.Width;
            Height = thumbnailSetting.Config.Height;
            Opacity = thumbnailSetting.Config.Opacity;
            FocusBorderColor = thumbnailSetting.Config.FocusBorderColor ?? "#0078D4";
            FocusBorderThickness = thumbnailSetting.Config.FocusBorderThickness;
            Ratio = WindowRatio.None;

            _originalWidth = thumbnailSetting.Config.Width;
            _originalHeight = thumbnailSetting.Config.Height;
            _originalOpacity = thumbnailSetting.Config.Opacity;
            _originalFocusBorderColor = thumbnailSetting.Config.FocusBorderColor ?? "#0078D4";
            _originalFocusBorderThickness = thumbnailSetting.Config.FocusBorderThickness;

            LoadAvailableThumbnails();
        }

        partial void OnWidthChanged(int value)
        {
            if (!_isLoadingSettings)
            {
                if (Ratio != WindowRatio.None && !_isCalculatingHeight)
                {
                    _isCalculatingHeight = true;
                    Height = CalculateHeightFromRatio(value, Ratio, Height);
                    _isCalculatingHeight = false;
                }

                _thumbnailWindowService.UpdateThumbnailSizeAndOpacityByWindowTitle(
                    WindowTitle,
                    value,
                    Height,
                    Opacity);
            }
        }

        partial void OnHeightChanged(int value)
        {
            if (!_isLoadingSettings)
            {
                if (Ratio != WindowRatio.None && !_isCalculatingHeight)
                {
                    int calculatedHeight = CalculateHeightFromRatio(Width, Ratio, value);
                    if (calculatedHeight != value)
                    {
                        _isCalculatingHeight = true;
                        Height = calculatedHeight;
                        _isCalculatingHeight = false;
                        return;
                    }
                }

                _thumbnailWindowService.UpdateThumbnailSizeAndOpacityByWindowTitle(
                    WindowTitle,
                    Width,
                    value,
                    Opacity);
            }
        }

        partial void OnOpacityChanged(double value)
        {
            if (!_isLoadingSettings)
            {
                _thumbnailWindowService.UpdateThumbnailSizeAndOpacityByWindowTitle(
                    WindowTitle,
                    Width,
                    Height,
                    value);
            }
        }

        partial void OnRatioChanged(WindowRatio value)
        {
            if (!_isLoadingSettings)
            {
                if (value != WindowRatio.None && !_isCalculatingHeight)
                {
                    _isCalculatingHeight = true;
                    Height = CalculateHeightFromRatio(Width, value, Height);
                    _isCalculatingHeight = false;
                }
            }
        }

        partial void OnFocusBorderColorChanged(string value)
        {
            if (!_isLoadingSettings)
            {
                _thumbnailWindowService.UpdateThumbnailBorderSettingsByWindowTitle(
                    WindowTitle,
                    value,
                    FocusBorderThickness);
            }
        }

        partial void OnFocusBorderThicknessChanged(int value)
        {
            if (!_isLoadingSettings)
            {
                _thumbnailWindowService.UpdateThumbnailBorderSettingsByWindowTitle(
                    WindowTitle,
                    FocusBorderColor,
                    value);
            }
        }

        [RelayCommand]
        private Task PickFocusBorderColor()
        {
            try
            {
                Avalonia.Media.Color currentColor;
                try
                {
                    currentColor = Avalonia.Media.Color.Parse(FocusBorderColor);
                }
                catch
                {
                    currentColor = Colors.Blue;
                }

                Dispatcher.UIThread.Post(async () =>
                {
                    ColorPickerWindow window = new ColorPickerWindow(currentColor);
                    Window? mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                        ? desktop.MainWindow
                        : null;

                    if (mainWindow != null)
                    {
                        await window.ShowDialog(mainWindow);
                    }
                    else
                    {
                        window.Show();
                    }

                    if (window.DialogResult == true)
                    {
                        Color color = window.SelectedColor;
                        FocusBorderColor = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error picking thumbnail border color: {ex.Message}");
            }
            return Task.CompletedTask;
        }

        [RelayCommand]
        private void Save()
        {
            if (string.IsNullOrWhiteSpace(WindowTitle))
                return;

            Profile? activeProfile = _databaseService.GetActiveProfile() ?? _databaseService.CurrentProfile;
            if (activeProfile != null)
            {
                ThumbnailConfig config = new ThumbnailConfig
                {
                    Width = Width,
                    Height = Height,
                    X = _thumbnailSetting.Config.X,
                    Y = _thumbnailSetting.Config.Y,
                    Opacity = Opacity,
                    FocusBorderColor = FocusBorderColor,
                    FocusBorderThickness = FocusBorderThickness,
                    ShowTitleOverlay = _thumbnailSetting.Config.ShowTitleOverlay
                };

                _databaseService.SaveThumbnailSettings(activeProfile.Id, WindowTitle, config);

                _thumbnailWindowService.UpdateThumbnailByWindowTitle(WindowTitle);

                _onSettingsSaved?.Invoke();

                Dispatcher.UIThread.Post(() =>
                {
                    _window?.Close();
                });
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            _thumbnailWindowService.UpdateThumbnailSizeAndOpacityByWindowTitle(
                WindowTitle,
                _originalWidth,
                _originalHeight,
                _originalOpacity);

            _thumbnailWindowService.UpdateThumbnailBorderSettingsByWindowTitle(
                WindowTitle,
                _originalFocusBorderColor,
                _originalFocusBorderThickness);

            Dispatcher.UIThread.Post(() =>
            {
                _window?.Close();
            });
        }

        /// <summary>
        /// Loads available thumbnails for the current profile (excluding the current thumbnail).
        /// </summary>
        private void LoadAvailableThumbnails()
        {
            Profile? activeProfile = _databaseService.GetActiveProfile() ?? _databaseService.CurrentProfile;
            if (activeProfile != null)
            {
                List<ThumbnailSetting> allThumbnails = _databaseService.GetAllThumbnailSettings(activeProfile.Id);
                AvailableThumbnails = allThumbnails
                    .Where(t => t.WindowTitle != _thumbnailSetting.WindowTitle)
                    .ToList();
            }
        }

        [RelayCommand]
        private void CopySettingsFromThumbnail()
        {
            if (SelectedThumbnailToCopy == null)
                return;

            _isLoadingSettings = true;

            Width = SelectedThumbnailToCopy.Config.Width;
            Height = SelectedThumbnailToCopy.Config.Height;
            Opacity = SelectedThumbnailToCopy.Config.Opacity;
            _thumbnailSetting.Config.X = SelectedThumbnailToCopy.Config.X;
            _thumbnailSetting.Config.Y = SelectedThumbnailToCopy.Config.Y;

            Profile? activeProfile = _databaseService.GetActiveProfile() ?? _databaseService.CurrentProfile;
            if (activeProfile != null)
            {
                ThumbnailConfig config = new ThumbnailConfig
                {
                    Width = Width,
                    Height = Height,
                    X = _thumbnailSetting.Config.X,
                    Y = _thumbnailSetting.Config.Y,
                    Opacity = Opacity,
                    FocusBorderColor = FocusBorderColor,
                    FocusBorderThickness = FocusBorderThickness,
                    ShowTitleOverlay = _thumbnailSetting.Config.ShowTitleOverlay
                };

                _databaseService.SaveThumbnailSettings(activeProfile.Id, WindowTitle, config);
            }

            _isLoadingSettings = false;

            _thumbnailWindowService.UpdateThumbnailByWindowTitle(WindowTitle);
        }

        /// <summary>
        /// Calculates height from width and aspect ratio.
        /// </summary>
        private int CalculateHeightFromRatio(int width, WindowRatio ratio, int currentHeight = 0)
        {
            return WindowRatioHelper.CalculateHeightFromRatio(width, ratio, currentHeight);
        }
    }
}


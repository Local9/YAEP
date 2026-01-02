using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using YAEP.Models;
using YAEP.Views.Windows;

namespace YAEP.ViewModels.Windows
{
    public partial class EditThumbnailWindowViewModel : ViewModelBase
    {
        private readonly DatabaseService _databaseService;
        private readonly IThumbnailWindowService _thumbnailWindowService;
        private readonly DatabaseService.ThumbnailSetting _thumbnailSetting;
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

        // Store original values for cancel functionality
        private int _originalWidth;
        private int _originalHeight;
        private double _originalOpacity;
        private string _originalFocusBorderColor = "#0078D4";
        private int _originalFocusBorderThickness = 3;

        public EditThumbnailWindowViewModel(
            DatabaseService databaseService,
            IThumbnailWindowService thumbnailWindowService,
            DatabaseService.ThumbnailSetting thumbnailSetting,
            Action? onSettingsSaved = null,
            EditThumbnailWindow? window = null)
        {
            _databaseService = databaseService;
            _thumbnailWindowService = thumbnailWindowService;
            _thumbnailSetting = thumbnailSetting;
            _onSettingsSaved = onSettingsSaved;
            _window = window;

            // Initialize properties from the setting
            WindowTitle = thumbnailSetting.WindowTitle;
            Width = thumbnailSetting.Config.Width;
            Height = thumbnailSetting.Config.Height;
            Opacity = thumbnailSetting.Config.Opacity;
            FocusBorderColor = thumbnailSetting.Config.FocusBorderColor ?? "#0078D4";
            FocusBorderThickness = thumbnailSetting.Config.FocusBorderThickness;
            Ratio = WindowRatio.None;

            // Store original values
            _originalWidth = thumbnailSetting.Config.Width;
            _originalHeight = thumbnailSetting.Config.Height;
            _originalOpacity = thumbnailSetting.Config.Opacity;
            _originalFocusBorderColor = thumbnailSetting.Config.FocusBorderColor ?? "#0078D4";
            _originalFocusBorderThickness = thumbnailSetting.Config.FocusBorderThickness;
        }

        partial void OnWidthChanged(int value)
        {
            if (!_isLoadingSettings)
            {
                // Recalculate height if ratio is set
                if (Ratio != WindowRatio.None && !_isCalculatingHeight)
                {
                    _isCalculatingHeight = true;
                    Height = CalculateHeightFromRatio(value, Ratio, Height);
                    _isCalculatingHeight = false;
                }

                // Update thumbnail live for preview (no database save until Save is clicked)
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
                // If ratio is set and we're not already calculating, ensure height matches ratio
                if (Ratio != WindowRatio.None && !_isCalculatingHeight)
                {
                    int calculatedHeight = CalculateHeightFromRatio(Width, Ratio, value);
                    if (calculatedHeight != value)
                    {
                        _isCalculatingHeight = true;
                        Height = calculatedHeight;
                        _isCalculatingHeight = false;
                        return; // Don't update yet, it will be updated when Height is set again
                    }
                }

                // Update thumbnail live for preview (no database save until Save is clicked)
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
                // Update thumbnail live for preview (no database save until Save is clicked)
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
                // Recalculate height when ratio changes
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
                // Update thumbnail border color live for preview
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
                // Update thumbnail border thickness live for preview
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
                // Convert hex string to Avalonia Color
                Avalonia.Media.Color currentColor;
                try
                {
                    currentColor = Avalonia.Media.Color.Parse(FocusBorderColor);
                }
                catch
                {
                    currentColor = Colors.Blue; // Default fallback
                }

                // Show color picker dialog
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
                        // Convert color to hex string
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
                // Get current X and Y from the existing setting (we don't edit these)
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

                // Update the thumbnail window if it exists
                _thumbnailWindowService.UpdateThumbnailByWindowTitle(WindowTitle);

                // Notify parent to reload settings
                _onSettingsSaved?.Invoke();

                // Close the window
                Dispatcher.UIThread.Post(() =>
                {
                    _window?.Close();
                });
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            // Restore thumbnail to original settings (live update)
            _thumbnailWindowService.UpdateThumbnailSizeAndOpacityByWindowTitle(
                WindowTitle,
                _originalWidth,
                _originalHeight,
                _originalOpacity);

            // Restore original border settings
            _thumbnailWindowService.UpdateThumbnailBorderSettingsByWindowTitle(
                WindowTitle,
                _originalFocusBorderColor,
                _originalFocusBorderThickness);

            // Close the window
            Dispatcher.UIThread.Post(() =>
            {
                _window?.Close();
            });
        }

        /// <summary>
        /// Calculates height from width and aspect ratio.
        /// </summary>
        private int CalculateHeightFromRatio(int width, WindowRatio ratio, int currentHeight = 300)
        {
            double aspectRatio = ratio switch
            {
                WindowRatio.Ratio21_9 => 21.0 / 9.0,
                WindowRatio.Ratio21_4 => 21.0 / 4.0,
                WindowRatio.Ratio16_9 => 16.0 / 9.0,
                WindowRatio.Ratio4_3 => 4.0 / 3.0,
                WindowRatio.Ratio1_1 => 1.0,
                _ => 0.0
            };

            if (aspectRatio == 0.0)
                return currentHeight; // Return current height if ratio is None

            // Calculate height: height = width / aspectRatio
            int calculatedHeight = (int)Math.Round(width / aspectRatio);

            // Clamp to valid range
            return Math.Clamp(calculatedHeight, 108, 540);
        }
    }
}


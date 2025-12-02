using System.Timers;
using System.Windows.Media;
using Wpf.Ui.Abstractions.Controls;
using YAEP.Interface;
using YAEP.Services;
using YAEP.Views.Windows;

namespace YAEP.ViewModels.Pages
{
    public partial class ThumbnailSettingsViewModel : ObservableObject, INavigationAware
    {
        private readonly DatabaseService _databaseService;
        private readonly IThumbnailWindowService _thumbnailWindowService;
        private bool _isInitialized = false;
        private bool _isLoadingSettings = false;
        private EditThumbnailWindow? _editThumbnailWindow;

        // Debounce timer for default settings updates
        private System.Timers.Timer? _defaultSettingsUpdateTimer;
        private const int DEBOUNCE_DELAY_MS = 300;

        [ObservableProperty]
        private DatabaseService.ThumbnailConfig? _defaultThumbnailConfig;

        [ObservableProperty]
        private List<DatabaseService.ThumbnailSetting> _thumbnailSettings = new();

        [ObservableProperty]
        private int _defaultWidth;

        [ObservableProperty]
        private int _defaultHeight;

        [ObservableProperty]
        private double _defaultOpacity;

        [ObservableProperty]
        private string _defaultFocusBorderColor = "#0078D4";

        [ObservableProperty]
        private int _defaultFocusBorderThickness = 3;

        [ObservableProperty]
        private bool _defaultShowTitleOverlay = true;

        [ObservableProperty]
        private WindowRatio _defaultRatio = WindowRatio.None;

        private bool _isCalculatingHeight = false;

        [ObservableProperty]
        private DatabaseService.ThumbnailSetting? _editingThumbnailSetting;

        [ObservableProperty]
        private string _editingThumbnailWindowTitle = String.Empty;

        [ObservableProperty]
        private int _editingThumbnailWidth;

        [ObservableProperty]
        private int _editingThumbnailHeight;

        [ObservableProperty]
        private double _editingThumbnailOpacity;

        // Store original values for cancel functionality
        private int _originalEditingThumbnailWidth;
        private int _originalEditingThumbnailHeight;
        private double _originalEditingThumbnailOpacity;

        public ThumbnailSettingsViewModel(DatabaseService databaseService, IThumbnailWindowService thumbnailWindowService)
        {
            _databaseService = databaseService;
            _thumbnailWindowService = thumbnailWindowService;

            // Initialize debounce timers
            _defaultSettingsUpdateTimer = new System.Timers.Timer(DEBOUNCE_DELAY_MS);
            _defaultSettingsUpdateTimer.Elapsed += OnDefaultSettingsUpdateTimerElapsed;
            _defaultSettingsUpdateTimer.AutoReset = false;

            // Note: _thumbnailSettingUpdateTimer is no longer needed since we update live
            // and only save to database on explicit Save button click

            // Subscribe to thumbnail service events
            _thumbnailWindowService.ThumbnailAdded += OnThumbnailAdded;
            _thumbnailWindowService.ThumbnailRemoved += OnThumbnailRemoved;
        }

        public Task OnNavigatedToAsync()
        {
            if (!_isInitialized)
                _isInitialized = true;

            LoadThumbnailSettings();

            // Set focus on the first thumbnail so user can see the border and color changes
            _thumbnailWindowService.SetFocusOnFirstThumbnail();

            return Task.CompletedTask;
        }

        public Task OnNavigatedFromAsync()
        {
            // Clean up timers when navigating away
            _defaultSettingsUpdateTimer?.Stop();

            // Resume focus checking on all thumbnails when leaving the page
            _thumbnailWindowService.ResumeFocusCheckOnAllThumbnails();

            return Task.CompletedTask;
        }

        private void OnThumbnailAdded(object? sender, YAEP.Interface.ThumbnailWindowChangedEventArgs e)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                // Refresh thumbnail settings when a new thumbnail is added
                LoadThumbnailSettings();
            });
        }

        private void OnThumbnailRemoved(object? sender, YAEP.Interface.ThumbnailWindowChangedEventArgs e)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                // Refresh thumbnail settings when a thumbnail is removed
                LoadThumbnailSettings();

                // Clear editing if the removed thumbnail was being edited
                if (EditingThumbnailSetting?.WindowTitle == e.WindowTitle)
                {
                    EditingThumbnailSetting = null;
                    EditingThumbnailWindowTitle = String.Empty;
                    _editThumbnailWindow?.Close();
                }
            });
        }

        private void LoadThumbnailSettings()
        {
            _isLoadingSettings = true;
            try
            {
                // Preserve the current ratio selection (it's UI-only, not stored in database)
                WindowRatio preservedRatio = DefaultRatio;

                DatabaseService.Profile? activeProfile = _databaseService.GetActiveProfile() ?? _databaseService.CurrentProfile;
                if (activeProfile != null)
                {
                    DefaultThumbnailConfig = _databaseService.GetThumbnailDefaultConfig(activeProfile.Id);
                    if (DefaultThumbnailConfig != null)
                    {
                        DefaultWidth = DefaultThumbnailConfig.Width;
                        DefaultHeight = DefaultThumbnailConfig.Height;
                        DefaultOpacity = DefaultThumbnailConfig.Opacity;
                        DefaultFocusBorderColor = DefaultThumbnailConfig.FocusBorderColor ?? "#0078D4";
                        DefaultFocusBorderThickness = DefaultThumbnailConfig.FocusBorderThickness;
                        DefaultShowTitleOverlay = DefaultThumbnailConfig.ShowTitleOverlay;
                        // Preserve the ratio selection (it's a UI-only feature, not stored in database)
                        DefaultRatio = preservedRatio;
                    }
                    ThumbnailSettings = _databaseService.GetAllThumbnailSettings(activeProfile.Id);
                }
                else
                {
                    DefaultThumbnailConfig = null;
                    ThumbnailSettings = new List<DatabaseService.ThumbnailSetting>();
                }
            }
            finally
            {
                _isLoadingSettings = false;
            }
        }

        partial void OnDefaultWidthChanged(int value)
        {
            if (!_isLoadingSettings)
            {
                // Recalculate height if ratio is set
                if (DefaultRatio != WindowRatio.None && !_isCalculatingHeight)
                {
                    _isCalculatingHeight = true;
                    DefaultHeight = CalculateHeightFromRatio(value, DefaultRatio);
                    _isCalculatingHeight = false;
                }

                // Update thumbnails immediately for live preview
                UpdateThumbnailSizeAndOpacity();
                // Also save to database with debounce
                DebounceDefaultSettingsUpdate();
            }
        }

        partial void OnDefaultHeightChanged(int value)
        {
            if (!_isLoadingSettings)
            {
                // If ratio is set and we're not already calculating, ensure height matches ratio
                if (DefaultRatio != WindowRatio.None && !_isCalculatingHeight)
                {
                    int calculatedHeight = CalculateHeightFromRatio(DefaultWidth, DefaultRatio);
                    if (calculatedHeight != value)
                    {
                        _isCalculatingHeight = true;
                        DefaultHeight = calculatedHeight;
                        _isCalculatingHeight = false;
                        return; // Don't update yet, it will be updated when DefaultHeight is set again
                    }
                }

                // Update thumbnails immediately for live preview
                UpdateThumbnailSizeAndOpacity();
                // Also save to database with debounce
                DebounceDefaultSettingsUpdate();
            }
        }

        partial void OnDefaultOpacityChanged(double value)
        {
            if (!_isLoadingSettings)
            {
                // Update thumbnails immediately for live preview
                UpdateThumbnailSizeAndOpacity();
                // Also save to database with debounce
                DebounceDefaultSettingsUpdate();
            }
        }

        partial void OnDefaultRatioChanged(WindowRatio value)
        {
            if (!_isLoadingSettings)
            {
                // Recalculate height when ratio changes
                if (value != WindowRatio.None && !_isCalculatingHeight)
                {
                    _isCalculatingHeight = true;
                    DefaultHeight = CalculateHeightFromRatio(DefaultWidth, value);
                    _isCalculatingHeight = false;
                }
                // Save to database with debounce
                DebounceDefaultSettingsUpdate();
            }
        }

        partial void OnDefaultFocusBorderColorChanged(string value)
        {
            System.Diagnostics.Debug.WriteLine($"OnDefaultFocusBorderColorChanged: value={value}, _isLoadingSettings={_isLoadingSettings}");
            if (!_isLoadingSettings)
            {
                // Update thumbnails immediately for live preview
                UpdateThumbnailBorderSettings();
                // Also save to database with debounce
                DebounceDefaultSettingsUpdate();
            }
        }

        partial void OnDefaultFocusBorderThicknessChanged(int value)
        {
            System.Diagnostics.Debug.WriteLine($"OnDefaultFocusBorderThicknessChanged: value={value}, _isLoadingSettings={_isLoadingSettings}");
            if (!_isLoadingSettings)
            {
                // Update thumbnails immediately for live preview
                UpdateThumbnailBorderSettings();
                // Also save to database with debounce
                DebounceDefaultSettingsUpdate();
            }
        }

        partial void OnDefaultShowTitleOverlayChanged(bool value)
        {
            if (!_isLoadingSettings)
            {
                // Update thumbnails immediately for live preview
                UpdateThumbnailTitleOverlay();
                // Also save to database with debounce
                DebounceDefaultSettingsUpdate();
            }
        }

        [RelayCommand]
        private void OnPickFocusBorderColor()
        {
            try
            {
                // Convert hex string to WPF Color
                Color currentColor;
                try
                {
                    currentColor = (Color)ColorConverter.ConvertFromString(DefaultFocusBorderColor);
                }
                catch
                {
                    currentColor = Colors.Blue; // Default fallback
                }

                // Create and show color picker window
                ColorPickerWindow colorPicker = new ColorPickerWindow(currentColor)
                {
                    Owner = System.Windows.Application.Current.MainWindow
                };

                colorPicker.ShowDialog();

                if (colorPicker.DialogResult == true)
                {
                    // Convert back to hex string
                    Color selectedColor = colorPicker.SelectedColor;
                    string newColor = $"#{selectedColor.R:X2}{selectedColor.G:X2}{selectedColor.B:X2}";

                    // Stop any pending debounce timer to avoid duplicate saves
                    _defaultSettingsUpdateTimer?.Stop();

                    // Temporarily set loading flag to prevent property change handler from triggering debounce
                    _isLoadingSettings = true;
                    DefaultFocusBorderColor = newColor;
                    _isLoadingSettings = false;

                    // Save immediately when OK is clicked (bypass debounce)
                    System.Diagnostics.Debug.WriteLine($"OnPickFocusBorderColor: OK clicked, saving immediately with Color={newColor}");
                    UpdateDefaultThumbnailConfig();
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Error picking color: {ex.Message}",
                    "Color Picker Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private void DebounceDefaultSettingsUpdate()
        {
            // Reset the timer - this will delay the update until the user stops moving the slider
            if (_defaultSettingsUpdateTimer == null)
            {
                System.Diagnostics.Debug.WriteLine("DebounceDefaultSettingsUpdate: Timer is null!");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"DebounceDefaultSettingsUpdate: Restarting timer (Color={DefaultFocusBorderColor}, Thickness={DefaultFocusBorderThickness})");
            _defaultSettingsUpdateTimer.Stop();
            _defaultSettingsUpdateTimer.Start();
        }

        private void OnDefaultSettingsUpdateTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            // Timer elapsed - user has stopped moving the slider, now update
            // Ensure we're on the UI thread
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                UpdateDefaultThumbnailConfig();
            });
        }

        private void UpdateDefaultThumbnailConfig()
        {
            DatabaseService.Profile? activeProfile = _databaseService.GetActiveProfile() ?? _databaseService.CurrentProfile;
            if (activeProfile != null && DefaultThumbnailConfig != null)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateDefaultThumbnailConfig: Updating for ProfileId {activeProfile.Id}, Color={DefaultFocusBorderColor}, Thickness={DefaultFocusBorderThickness}");

                // Get current X and Y from the existing config (we don't edit these)
                DatabaseService.ThumbnailConfig config = new DatabaseService.ThumbnailConfig
                {
                    Width = DefaultWidth,
                    Height = DefaultHeight,
                    X = DefaultThumbnailConfig.X,
                    Y = DefaultThumbnailConfig.Y,
                    Opacity = DefaultOpacity,
                    FocusBorderColor = DefaultFocusBorderColor,
                    FocusBorderThickness = DefaultFocusBorderThickness,
                    ShowTitleOverlay = DefaultShowTitleOverlay
                };

                _databaseService.SetThumbnailDefaultConfig(activeProfile.Id, config);

                // Update all existing thumbnail settings in the database with the new border settings
                _databaseService.UpdateAllThumbnailBorderSettings(activeProfile.Id, DefaultFocusBorderColor, DefaultFocusBorderThickness);

                // Update all existing thumbnail settings in the database with the new size and opacity (preserving positions)
                _databaseService.UpdateAllThumbnailSizeAndOpacity(activeProfile.Id, DefaultWidth, DefaultHeight, DefaultOpacity);

                // Update all existing thumbnail settings in the database with the new title overlay setting
                _databaseService.UpdateAllThumbnailTitleOverlay(activeProfile.Id, DefaultShowTitleOverlay);

                // Note: We don't call UpdateAllThumbnails() here because that would reload positions.
                // Instead, we've already updated the thumbnails live via UpdateThumbnailSizeAndOpacity() and UpdateThumbnailBorderSettings()

                // Update all thumbnail windows with the new title overlay setting
                _thumbnailWindowService.UpdateAllThumbnailsTitleOverlay(DefaultShowTitleOverlay);

                // Reload thumbnail settings to reflect the database changes
                LoadThumbnailSettings();

                // Update the config object (preserve reference to avoid breaking bindings)
                DefaultThumbnailConfig.Width = config.Width;
                DefaultThumbnailConfig.Height = config.Height;
                DefaultThumbnailConfig.Opacity = config.Opacity;
                DefaultThumbnailConfig.FocusBorderColor = config.FocusBorderColor;
                DefaultThumbnailConfig.FocusBorderThickness = config.FocusBorderThickness;
                DefaultThumbnailConfig.ShowTitleOverlay = config.ShowTitleOverlay;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"UpdateDefaultThumbnailConfig: Skipped - activeProfile={activeProfile != null}, DefaultThumbnailConfig={DefaultThumbnailConfig != null}");
            }
        }

        /// <summary>
        /// Updates border settings on all thumbnails immediately for live preview.
        /// </summary>
        private void UpdateThumbnailBorderSettings()
        {
            DatabaseService.Profile? activeProfile = _databaseService.GetActiveProfile() ?? _databaseService.CurrentProfile;
            if (activeProfile != null)
            {
                // Update all thumbnail windows with new border settings immediately
                _thumbnailWindowService.UpdateThumbnailBorderSettings(DefaultFocusBorderColor, DefaultFocusBorderThickness);
            }
        }

        /// <summary>
        /// Updates size and opacity on all thumbnails immediately for live preview.
        /// </summary>
        private void UpdateThumbnailSizeAndOpacity()
        {
            DatabaseService.Profile? activeProfile = _databaseService.GetActiveProfile() ?? _databaseService.CurrentProfile;
            if (activeProfile != null)
            {
                // Update all thumbnail windows with new size and opacity immediately
                _thumbnailWindowService.UpdateThumbnailSizeAndOpacity(DefaultWidth, DefaultHeight, DefaultOpacity);
            }
        }

        /// <summary>
        /// Calculates height from width and aspect ratio.
        /// </summary>
        private int CalculateHeightFromRatio(int width, WindowRatio ratio)
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
                return DefaultHeight; // Return current height if ratio is None

            // Calculate height: height = width / aspectRatio
            int calculatedHeight = (int)Math.Round(width / aspectRatio);

            // Clamp to valid range
            return Math.Clamp(calculatedHeight, 108, 540);
        }

        /// <summary>
        /// Updates title overlay visibility on all thumbnails immediately for live preview.
        /// </summary>
        private void UpdateThumbnailTitleOverlay()
        {
            DatabaseService.Profile? activeProfile = _databaseService.GetActiveProfile() ?? _databaseService.CurrentProfile;
            if (activeProfile != null)
            {
                // Update all thumbnail windows with new title overlay setting immediately
                _thumbnailWindowService.UpdateAllThumbnailsTitleOverlay(DefaultShowTitleOverlay);
            }
        }

        [RelayCommand]
        private void OnEditThumbnailSetting(DatabaseService.ThumbnailSetting? setting)
        {
            if (setting != null)
            {
                EditingThumbnailSetting = setting;
                EditingThumbnailWindowTitle = setting.WindowTitle;
                EditingThumbnailWidth = setting.Config.Width;
                EditingThumbnailHeight = setting.Config.Height;
                EditingThumbnailOpacity = setting.Config.Opacity;

                // Store original values for cancel functionality
                _originalEditingThumbnailWidth = setting.Config.Width;
                _originalEditingThumbnailHeight = setting.Config.Height;
                _originalEditingThumbnailOpacity = setting.Config.Opacity;

                ShowEditThumbnailWindow();
            }
        }

        private void ShowEditThumbnailWindow()
        {
            if (_editThumbnailWindow != null)
            {
                _editThumbnailWindow.Activate();
                return;
            }

            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                EditThumbnailWindow window = new EditThumbnailWindow(this);
                window.Owner = System.Windows.Application.Current.MainWindow;
                window.Closed += (s, e) =>
                {
                    _editThumbnailWindow = null;
                    // Cancel editing when window closes
                    if (EditingThumbnailSetting != null)
                    {
                        OnCancelEditThumbnailSetting();
                    }
                };
                _editThumbnailWindow = window;
                window.Show();
            });
        }

        partial void OnEditingThumbnailWidthChanged(int value)
        {
            if (!_isLoadingSettings && EditingThumbnailSetting != null && !string.IsNullOrWhiteSpace(EditingThumbnailWindowTitle))
            {
                // Update thumbnail live for preview (no database save until Save is clicked)
                _thumbnailWindowService.UpdateThumbnailSizeAndOpacityByWindowTitle(
                    EditingThumbnailWindowTitle,
                    value,
                    EditingThumbnailHeight,
                    EditingThumbnailOpacity);
            }
        }

        partial void OnEditingThumbnailHeightChanged(int value)
        {
            if (!_isLoadingSettings && EditingThumbnailSetting != null && !string.IsNullOrWhiteSpace(EditingThumbnailWindowTitle))
            {
                // Update thumbnail live for preview (no database save until Save is clicked)
                _thumbnailWindowService.UpdateThumbnailSizeAndOpacityByWindowTitle(
                    EditingThumbnailWindowTitle,
                    EditingThumbnailWidth,
                    value,
                    EditingThumbnailOpacity);
            }
        }

        partial void OnEditingThumbnailOpacityChanged(double value)
        {
            if (!_isLoadingSettings && EditingThumbnailSetting != null && !string.IsNullOrWhiteSpace(EditingThumbnailWindowTitle))
            {
                // Update thumbnail live for preview (no database save until Save is clicked)
                _thumbnailWindowService.UpdateThumbnailSizeAndOpacityByWindowTitle(
                    EditingThumbnailWindowTitle,
                    EditingThumbnailWidth,
                    EditingThumbnailHeight,
                    value);
            }
        }

        [RelayCommand]
        private void OnSaveEditThumbnailSetting()
        {
            if (EditingThumbnailSetting == null || string.IsNullOrWhiteSpace(EditingThumbnailWindowTitle))
                return;

            DatabaseService.Profile? activeProfile = _databaseService.GetActiveProfile() ?? _databaseService.CurrentProfile;
            if (activeProfile != null)
            {
                // Get current X and Y from the existing setting (we don't edit these)
                DatabaseService.ThumbnailConfig config = new DatabaseService.ThumbnailConfig
                {
                    Width = EditingThumbnailWidth,
                    Height = EditingThumbnailHeight,
                    X = EditingThumbnailSetting.Config.X,
                    Y = EditingThumbnailSetting.Config.Y,
                    Opacity = EditingThumbnailOpacity
                };

                _databaseService.SaveThumbnailSettings(activeProfile.Id, EditingThumbnailWindowTitle, config);

                // Update the thumbnail window if it exists
                _thumbnailWindowService.UpdateThumbnailByWindowTitle(EditingThumbnailWindowTitle);

                // Update the setting object directly to avoid reloading the entire list
                EditingThumbnailSetting.Config.Width = config.Width;
                EditingThumbnailSetting.Config.Height = config.Height;
                EditingThumbnailSetting.Config.Opacity = config.Opacity;

                // Notify that the collection has changed so the DataGrid updates
                OnPropertyChanged(nameof(ThumbnailSettings));

                // Clear editing state and close window
                EditingThumbnailSetting = null;
                EditingThumbnailWindowTitle = String.Empty;
                _editThumbnailWindow?.Close();
            }
        }

        [RelayCommand]
        private void OnCancelEditThumbnailSetting()
        {
            if (EditingThumbnailSetting != null && !string.IsNullOrWhiteSpace(EditingThumbnailWindowTitle))
            {
                // Restore thumbnail to original settings (live update)
                _thumbnailWindowService.UpdateThumbnailSizeAndOpacityByWindowTitle(
                    EditingThumbnailWindowTitle,
                    _originalEditingThumbnailWidth,
                    _originalEditingThumbnailHeight,
                    _originalEditingThumbnailOpacity);

                // Restore original values in the editing properties
                _isLoadingSettings = true;
                EditingThumbnailWidth = _originalEditingThumbnailWidth;
                EditingThumbnailHeight = _originalEditingThumbnailHeight;
                EditingThumbnailOpacity = _originalEditingThumbnailOpacity;
                _isLoadingSettings = false;

                // Update the setting object to reflect original values
                EditingThumbnailSetting.Config.Width = _originalEditingThumbnailWidth;
                EditingThumbnailSetting.Config.Height = _originalEditingThumbnailHeight;
                EditingThumbnailSetting.Config.Opacity = _originalEditingThumbnailOpacity;
                OnPropertyChanged(nameof(ThumbnailSettings));
            }

            EditingThumbnailSetting = null;
            EditingThumbnailWindowTitle = String.Empty;
            _editThumbnailWindow?.Close();
        }

        [RelayCommand]
        private void OnUpdateAllThumbnailsWithDefault()
        {
            DatabaseService.Profile? activeProfile = _databaseService.GetActiveProfile() ?? _databaseService.CurrentProfile;
            if (activeProfile == null)
                return;

            // Show confirmation dialog
            MessageBoxResult result = System.Windows.MessageBox.Show(
                "This will update all window-specific thumbnail settings with the default settings. This action cannot be undone.\n\nDo you want to continue?",
                "Update All Thumbnails",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                _databaseService.UpdateAllThumbnailSettingsWithDefault(activeProfile.Id);

                // Update all thumbnail windows to reflect the new settings
                _thumbnailWindowService.UpdateAllThumbnails();

                LoadThumbnailSettings();
            }
        }

        [RelayCommand]
        private void OnDeleteThumbnailSetting(DatabaseService.ThumbnailSetting? setting)
        {
            if (setting == null || string.IsNullOrWhiteSpace(setting.WindowTitle))
                return;

            DatabaseService.Profile? activeProfile = _databaseService.GetActiveProfile() ?? _databaseService.CurrentProfile;
            if (activeProfile == null)
                return;

            // Show confirmation dialog
            MessageBoxResult result = System.Windows.MessageBox.Show(
                $"Are you sure you want to delete thumbnail settings for '{setting.WindowTitle}'? This action cannot be undone.",
                "Delete Thumbnail Setting",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                _databaseService.DeleteThumbnailSettings(activeProfile.Id, setting.WindowTitle);

                // Cancel editing if we were editing this setting
                if (EditingThumbnailSetting?.WindowTitle == setting.WindowTitle)
                {
                    EditingThumbnailSetting = null;
                    EditingThumbnailWindowTitle = String.Empty;
                    _editThumbnailWindow?.Close();
                }

                LoadThumbnailSettings();
            }
        }
    }
}


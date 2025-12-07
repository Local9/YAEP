using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Timers;
using YAEP.Interface;
using YAEP.Services;
using YAEP.ViewModels.Windows;
using YAEP.Views.Windows;

namespace YAEP.ViewModels.Pages
{
    public partial class ThumbnailSettingsViewModel : ViewModelBase
    {
        private readonly DatabaseService _databaseService;
        private readonly IThumbnailWindowService _thumbnailWindowService;
        private bool _isInitialized = false;
        private bool _isLoadingSettings = false;

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

        /// <summary>
        /// Gets all available WindowRatio enum values for ComboBox binding.
        /// </summary>
        public Array WindowRatioValues => Enum.GetValues(typeof(WindowRatio));

        private bool _isCalculatingHeight = false;


        public ThumbnailSettingsViewModel(DatabaseService databaseService, IThumbnailWindowService thumbnailWindowService)
        {
            _databaseService = databaseService;
            _thumbnailWindowService = thumbnailWindowService;

            // Initialize debounce timers
            _defaultSettingsUpdateTimer = new System.Timers.Timer(DEBOUNCE_DELAY_MS);
            _defaultSettingsUpdateTimer.Elapsed += OnDefaultSettingsUpdateTimerElapsed;
            _defaultSettingsUpdateTimer.AutoReset = false;

            // Subscribe to thumbnail service events
            _thumbnailWindowService.ThumbnailAdded += OnThumbnailAdded;
            _thumbnailWindowService.ThumbnailRemoved += OnThumbnailRemoved;
        }

        public void OnNavigatedTo()
        {
            if (!_isInitialized)
                _isInitialized = true;

            // Stop any pending debounce timer before loading settings
            _defaultSettingsUpdateTimer?.Stop();

            // Load settings without triggering any thumbnail updates
            // This prevents flicker when opening the page
            LoadThumbnailSettings();

            // Ensure all thumbnail windows are visible before setting focus
            // This prevents windows from disappearing when navigating to the page
            Dispatcher.UIThread.Post(() =>
            {
                // Small delay using a timer to ensure windows are ready and not being updated
                System.Timers.Timer delayTimer = new System.Timers.Timer(300);
                delayTimer.Elapsed += (sender, e) =>
                {
                    delayTimer.Stop();
                    delayTimer.Dispose();
                    Dispatcher.UIThread.Post(() =>
                    {
                        // SetFocusOnFirstThumbnail will ensure all windows are shown and visible
                        _thumbnailWindowService.SetFocusOnFirstThumbnail();
                    });
                };
                delayTimer.AutoReset = false;
                delayTimer.Start();
            });
        }

        public void OnNavigatedFrom()
        {
            // Clean up timers when navigating away
            _defaultSettingsUpdateTimer?.Stop();

            // Resume focus checking on all thumbnails when leaving the page
            _thumbnailWindowService.ResumeFocusCheckOnAllThumbnails();
        }

        private void OnThumbnailAdded(object? sender, YAEP.Interface.ThumbnailWindowChangedEventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                // Refresh thumbnail settings when a new thumbnail is added
                LoadThumbnailSettings();
            });
        }

        private void OnThumbnailRemoved(object? sender, YAEP.Interface.ThumbnailWindowChangedEventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                // Refresh thumbnail settings when a thumbnail is removed
                LoadThumbnailSettings();
            });
        }

        private void LoadThumbnailSettings()
        {
            // Stop any pending debounce timer to prevent saving during load
            _defaultSettingsUpdateTimer?.Stop();
            
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
                        // Set properties while _isLoadingSettings is true to prevent triggering updates
                        DefaultWidth = DefaultThumbnailConfig.Width;
                        DefaultHeight = DefaultThumbnailConfig.Height;
                        DefaultOpacity = DefaultThumbnailConfig.Opacity;
                        DefaultFocusBorderColor = DefaultThumbnailConfig.FocusBorderColor ?? "#0078D4";
                        DefaultFocusBorderThickness = DefaultThumbnailConfig.FocusBorderThickness;
                        DefaultShowTitleOverlay = DefaultThumbnailConfig.ShowTitleOverlay;
                        // Preserve the ratio selection (it's a UI-only feature, not stored in database)
                        DefaultRatio = preservedRatio;
                    }
                    else
                    {
                        // If config is null, set safe default values to prevent 0 values from triggering updates
                        // These will only be set if they're currently 0 (initial state)
                        if (DefaultWidth == 0) DefaultWidth = 400;
                        if (DefaultHeight == 0) DefaultHeight = 300;
                        if (DefaultOpacity == 0) DefaultOpacity = 0.75;
                    }
                    ThumbnailSettings = _databaseService.GetAllThumbnailSettings(activeProfile.Id);
                }
                else
                {
                    DefaultThumbnailConfig = null;
                    // Set safe default values to prevent 0 values from triggering updates
                    if (DefaultWidth == 0) DefaultWidth = 400;
                    if (DefaultHeight == 0) DefaultHeight = 300;
                    if (DefaultOpacity == 0) DefaultOpacity = 0.75;
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
                    DefaultHeight = CalculateHeightFromRatio(value, DefaultRatio, DefaultHeight);
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
                    int calculatedHeight = CalculateHeightFromRatio(DefaultWidth, DefaultRatio, value);
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
                    DefaultHeight = CalculateHeightFromRatio(DefaultWidth, value, DefaultHeight);
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
        private async Task OnPickFocusBorderColor()
        {
            try
            {
                // Convert hex string to Avalonia Color
                Avalonia.Media.Color currentColor;
                try
                {
                    currentColor = Avalonia.Media.Color.Parse(DefaultFocusBorderColor);
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
                        DefaultFocusBorderColor = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error picking color: {ex.Message}");
                // TODO: Show error dialog using Avalonia's dialog system
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
            Dispatcher.UIThread.Post(() =>
            {
                UpdateDefaultThumbnailConfig();
            });
        }

        private void UpdateDefaultThumbnailConfig()
        {
            // Don't update if we're currently loading settings
            if (_isLoadingSettings)
            {
                System.Diagnostics.Debug.WriteLine("UpdateDefaultThumbnailConfig: Skipped - currently loading settings");
                return;
            }

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

                // Get existing thumbnail settings from database to preserve positions
                List<DatabaseService.ThumbnailSetting> existingSettings = _databaseService.GetAllThumbnailSettings(activeProfile.Id);

                // Also get cached settings from actual windows to get current positions
                Dictionary<string, DatabaseService.ThumbnailConfig> cachedSettings = _thumbnailWindowService.GetCachedThumbnailSettings();

                if (existingSettings.Count > 0)
                {
                    // Update each thumbnail individually, preserving positions
                    foreach (DatabaseService.ThumbnailSetting setting in existingSettings)
                    {
                        DatabaseService.ThumbnailConfig updatedConfig = new DatabaseService.ThumbnailConfig
                        {
                            Width = DefaultWidth,
                            Height = DefaultHeight,
                            X = setting.Config.X,
                            Y = setting.Config.Y,
                            Opacity = DefaultOpacity,
                            FocusBorderColor = DefaultFocusBorderColor,
                            FocusBorderThickness = DefaultFocusBorderThickness,
                            ShowTitleOverlay = DefaultShowTitleOverlay
                        };

                        // Save individual thumbnail settings with preserved position
                        _databaseService.SaveThumbnailSettings(activeProfile.Id, setting.WindowTitle, updatedConfig);
                        System.Diagnostics.Debug.WriteLine($"UpdateDefaultThumbnailConfig: Updated thumbnail '{setting.WindowTitle}' with preserved position X={setting.Config.X}, Y={setting.Config.Y}");
                    }
                }
                else
                {
                    // No existing settings, so nothing to update
                    System.Diagnostics.Debug.WriteLine("UpdateDefaultThumbnailConfig: No existing thumbnail settings found to update");
                }

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
            // Don't update during initialization or if values are invalid
            if (_isLoadingSettings || !_isInitialized)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateThumbnailSizeAndOpacity: Skipping update - loading settings or not initialized");
                return;
            }

            // Don't update if width, height, or opacity is 0 or invalid - this prevents windows from being resized to 0x0 or opacity 0
            if (DefaultWidth <= 0 || DefaultHeight <= 0 || DefaultOpacity <= 0)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateThumbnailSizeAndOpacity: Skipping update - invalid values: Width={DefaultWidth}, Height={DefaultHeight}, Opacity={DefaultOpacity}");
                return;
            }

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
                // Show edit thumbnail window
                Dispatcher.UIThread.Post(async () =>
                {
                    EditThumbnailWindow window = new EditThumbnailWindow();
                    
                    // Create the edit window ViewModel with window reference
                    var editViewModel = new EditThumbnailWindowViewModel(
                        _databaseService,
                        _thumbnailWindowService,
                        setting,
                        LoadThumbnailSettings, // Callback to reload settings after save
                        window);
                    
                    window.ViewModel = editViewModel;
                    window.DataContext = editViewModel;
                    
                    Window? mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                        ? desktop.MainWindow
                        : null;
                    if (mainWindow != null)
                    {
                        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                        await window.ShowDialog(mainWindow);
                    }
                    else
                    {
                        window.Show();
                    }
                });
            }
        }

        [RelayCommand]
        private async Task OnUpdateAllThumbnailsWithDefault()
        {
            DatabaseService.Profile? activeProfile = _databaseService.GetActiveProfile() ?? _databaseService.CurrentProfile;
            if (activeProfile == null)
                return;

            // TODO: Show confirmation dialog using Avalonia's dialog system
            // For now, proceed without confirmation
            _databaseService.UpdateAllThumbnailSettingsWithDefault(activeProfile.Id);

            // Update all thumbnail windows to reflect the new settings
            _thumbnailWindowService.UpdateAllThumbnails();

            LoadThumbnailSettings();
        }

    }
}


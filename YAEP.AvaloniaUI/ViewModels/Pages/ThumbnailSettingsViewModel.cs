using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

        private ColorOption? _selectedColorOption;

        public ColorOption? SelectedColorOption
        {
            get => _selectedColorOption;
            set
            {
                if (SetProperty(ref _selectedColorOption, value) && value != null && !_isLoadingSettings)
                {
                    DefaultFocusBorderColor = value.Value;
                }
            }
        }

        public List<ColorOption> PredefinedColors { get; } = new List<ColorOption>
        {
            new ColorOption { Name = "Blue", Value = "#0078D4" },
            new ColorOption { Name = "Green", Value = "#107C10" },
            new ColorOption { Name = "Red", Value = "#D13438" },
            new ColorOption { Name = "Orange", Value = "#FF8C00" },
            new ColorOption { Name = "Purple", Value = "#8764B8" },
            new ColorOption { Name = "Cyan", Value = "#00BCF2" },
            new ColorOption { Name = "Yellow", Value = "#FFB900" },
            new ColorOption { Name = "Bright Red", Value = "#E81123" },
            new ColorOption { Name = "Dark Purple", Value = "#5C2D91" },
            new ColorOption { Name = "Teal", Value = "#00B294" },
            new ColorOption { Name = "Black", Value = "#000000" },
            new ColorOption { Name = "White", Value = "#FFFFFF" }
        };

        public class ColorOption
        {
            public string Name { get; set; } = string.Empty;
            public string Value { get; set; } = string.Empty;
        }

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

            _defaultSettingsUpdateTimer = new System.Timers.Timer(DEBOUNCE_DELAY_MS);
            _defaultSettingsUpdateTimer.Elapsed += OnDefaultSettingsUpdateTimerElapsed;
            _defaultSettingsUpdateTimer.AutoReset = false;

            _thumbnailWindowService.ThumbnailAdded += OnThumbnailAdded;
            _thumbnailWindowService.ThumbnailRemoved += OnThumbnailRemoved;
        }

        public void OnNavigatedTo()
        {
            if (!_isInitialized)
                _isInitialized = true;

            _defaultSettingsUpdateTimer?.Stop();
            LoadThumbnailSettings();

            Dispatcher.UIThread.Post(() =>
            {
                System.Timers.Timer delayTimer = new System.Timers.Timer(300);
                delayTimer.Elapsed += (sender, e) =>
                {
                    delayTimer.Stop();
                    delayTimer.Dispose();
                    Dispatcher.UIThread.Post(() =>
                    {
                        _thumbnailWindowService.SetFocusOnFirstThumbnail();
                    });
                };
                delayTimer.AutoReset = false;
                delayTimer.Start();
            });
        }

        public void OnNavigatedFrom()
        {
            Debug.WriteLine("ThumbnailSettingsViewModel: OnNavigatedFrom called");

            _defaultSettingsUpdateTimer?.Stop();
            // Focus tracking is now handled automatically by the service
        }

        private void OnThumbnailAdded(object? sender, YAEP.Interface.ThumbnailWindowChangedEventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                LoadThumbnailSettings();
            });
        }

        private void OnThumbnailRemoved(object? sender, YAEP.Interface.ThumbnailWindowChangedEventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                LoadThumbnailSettings();
            });
        }

        private void LoadThumbnailSettings()
        {
            _defaultSettingsUpdateTimer?.Stop();

            _isLoadingSettings = true;
            try
            {
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
                        DefaultRatio = preservedRatio;
                    }
                    else
                    {
                        if (DefaultWidth == 0) DefaultWidth = 400;
                        if (DefaultHeight == 0) DefaultHeight = 300;
                        if (DefaultOpacity == 0) DefaultOpacity = 0.75;
                    }
                    ThumbnailSettings = _databaseService.GetAllThumbnailSettings(activeProfile.Id);
                }
                else
                {
                    DefaultThumbnailConfig = null;
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
                if (DefaultRatio != WindowRatio.None && !_isCalculatingHeight)
                {
                    _isCalculatingHeight = true;
                    DefaultHeight = CalculateHeightFromRatio(value, DefaultRatio, DefaultHeight);
                    _isCalculatingHeight = false;
                }

                UpdateThumbnailSizeAndOpacity();
                DebounceDefaultSettingsUpdate();
            }
        }

        partial void OnDefaultHeightChanged(int value)
        {
            if (!_isLoadingSettings)
            {
                if (DefaultRatio != WindowRatio.None && !_isCalculatingHeight)
                {
                    int calculatedHeight = CalculateHeightFromRatio(DefaultWidth, DefaultRatio, value);
                    if (calculatedHeight != value)
                    {
                        _isCalculatingHeight = true;
                        DefaultHeight = calculatedHeight;
                        _isCalculatingHeight = false;
                        return;
                    }
                }

                UpdateThumbnailSizeAndOpacity();
                DebounceDefaultSettingsUpdate();
            }
        }

        partial void OnDefaultOpacityChanged(double value)
        {
            if (!_isLoadingSettings)
            {
                UpdateThumbnailSizeAndOpacity();
                DebounceDefaultSettingsUpdate();
            }
        }

        partial void OnDefaultRatioChanged(WindowRatio value)
        {
            if (!_isLoadingSettings)
            {
                if (value != WindowRatio.None && !_isCalculatingHeight)
                {
                    _isCalculatingHeight = true;
                    DefaultHeight = CalculateHeightFromRatio(DefaultWidth, value, DefaultHeight);
                    _isCalculatingHeight = false;
                }
                DebounceDefaultSettingsUpdate();
            }
        }

        partial void OnDefaultFocusBorderColorChanged(string value)
        {
            System.Diagnostics.Debug.WriteLine($"OnDefaultFocusBorderColorChanged: value={value}, _isLoadingSettings={_isLoadingSettings}");
            
            // Update SelectedColorOption to match the current color value
            if (!_isLoadingSettings)
            {
                SelectedColorOption = PredefinedColors.FirstOrDefault(c => c.Value == value);
            }
            else
            {
                // During loading, update without triggering the setter logic
                _selectedColorOption = PredefinedColors.FirstOrDefault(c => c.Value == value);
                OnPropertyChanged(nameof(SelectedColorOption));
            }
            if (!_isLoadingSettings)
            {
                UpdateThumbnailBorderSettings();
                DebounceDefaultSettingsUpdate();
            }
        }

        partial void OnDefaultFocusBorderThicknessChanged(int value)
        {
            System.Diagnostics.Debug.WriteLine($"OnDefaultFocusBorderThicknessChanged: value={value}, _isLoadingSettings={_isLoadingSettings}");
            if (!_isLoadingSettings)
            {
                UpdateThumbnailBorderSettings();
                DebounceDefaultSettingsUpdate();
            }
        }

        partial void OnDefaultShowTitleOverlayChanged(bool value)
        {
            if (!_isLoadingSettings)
            {
                UpdateThumbnailTitleOverlay();
                DebounceDefaultSettingsUpdate();
            }
        }

        [RelayCommand]
        private Task OnPickFocusBorderColor()
        {
            try
            {
                Avalonia.Media.Color currentColor;
                try
                {
                    currentColor = Avalonia.Media.Color.Parse(DefaultFocusBorderColor);
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
                        DefaultFocusBorderColor = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error picking color: {ex.Message}");
            }
            return Task.CompletedTask;
        }

        private void DebounceDefaultSettingsUpdate()
        {
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
            Dispatcher.UIThread.Post(() =>
            {
                UpdateDefaultThumbnailConfig();
            });
        }

        private void UpdateDefaultThumbnailConfig()
        {
            if (_isLoadingSettings)
            {
                System.Diagnostics.Debug.WriteLine("UpdateDefaultThumbnailConfig: Skipped - currently loading settings");
                return;
            }

            DatabaseService.Profile? activeProfile = _databaseService.GetActiveProfile() ?? _databaseService.CurrentProfile;
            if (activeProfile != null && DefaultThumbnailConfig != null)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateDefaultThumbnailConfig: Updating for ProfileId {activeProfile.Id}, Color={DefaultFocusBorderColor}, Thickness={DefaultFocusBorderThickness}");

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

                List<DatabaseService.ThumbnailSetting> existingSettings = _databaseService.GetAllThumbnailSettings(activeProfile.Id);
                Dictionary<string, DatabaseService.ThumbnailConfig> cachedSettings = _thumbnailWindowService.GetCachedThumbnailSettings();

                if (existingSettings.Count > 0)
                {
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

                        _databaseService.SaveThumbnailSettings(activeProfile.Id, setting.WindowTitle, updatedConfig);
                        System.Diagnostics.Debug.WriteLine($"UpdateDefaultThumbnailConfig: Updated thumbnail '{setting.WindowTitle}' with preserved position X={setting.Config.X}, Y={setting.Config.Y}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("UpdateDefaultThumbnailConfig: No existing thumbnail settings found to update");
                }

                _thumbnailWindowService.UpdateAllThumbnailsTitleOverlay(DefaultShowTitleOverlay);
                LoadThumbnailSettings();

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
                _thumbnailWindowService.UpdateThumbnailBorderSettings(DefaultFocusBorderColor, DefaultFocusBorderThickness);
            }
        }

        /// <summary>
        /// Updates size and opacity on all thumbnails immediately for live preview.
        /// </summary>
        private void UpdateThumbnailSizeAndOpacity()
        {
            if (_isLoadingSettings || !_isInitialized)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateThumbnailSizeAndOpacity: Skipping update - loading settings or not initialized");
                return;
            }

            if (DefaultWidth <= 0 || DefaultHeight <= 0 || DefaultOpacity <= 0)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateThumbnailSizeAndOpacity: Skipping update - invalid values: Width={DefaultWidth}, Height={DefaultHeight}, Opacity={DefaultOpacity}");
                return;
            }

            DatabaseService.Profile? activeProfile = _databaseService.GetActiveProfile() ?? _databaseService.CurrentProfile;
            if (activeProfile != null)
            {
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
                return currentHeight;

            int calculatedHeight = (int)Math.Round(width / aspectRatio);
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
                _thumbnailWindowService.UpdateAllThumbnailsTitleOverlay(DefaultShowTitleOverlay);
            }
        }

        [RelayCommand]
        private void OnEditThumbnailSetting(DatabaseService.ThumbnailSetting? setting)
        {
            if (setting != null)
            {
                Dispatcher.UIThread.Post(async () =>
                {
                    EditThumbnailWindow window = new EditThumbnailWindow();

                    EditThumbnailWindowViewModel editViewModel = new EditThumbnailWindowViewModel(
                        _databaseService,
                        _thumbnailWindowService,
                        setting,
                        LoadThumbnailSettings,
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
        private Task OnUpdateAllThumbnailsWithDefault()
        {
            DatabaseService.Profile? activeProfile = _databaseService.GetActiveProfile() ?? _databaseService.CurrentProfile;
            if (activeProfile == null)
                return Task.CompletedTask;

            _databaseService.UpdateAllThumbnailSettingsWithDefault(activeProfile.Id);
            _thumbnailWindowService.UpdateAllThumbnails();
            LoadThumbnailSettings();
            return Task.CompletedTask;
        }

    }
}


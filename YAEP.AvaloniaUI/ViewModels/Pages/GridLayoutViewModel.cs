using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using YAEP.Interface;
using YAEP.Services;
using YAEP.ViewModels;

namespace YAEP.ViewModels.Pages
{
    public partial class GridLayoutViewModel : ViewModelBase
    {
        private readonly DatabaseService _databaseService;
        private readonly IThumbnailWindowService _thumbnailWindowService;
        private bool _isInitialized = false;
        private bool _isCalculatingHeight = false;

        // Debounce timers for text input updates
        private System.Timers.Timer? _gridCellWidthTextTimer;
        private System.Timers.Timer? _gridCellHeightTextTimer;
        private System.Timers.Timer? _gridStartXTextTimer;
        private System.Timers.Timer? _gridStartYTextTimer;
        private System.Timers.Timer? _gridColumnsTextTimer;
        private const int DEBOUNCE_DELAY_MS = 500;

        // Timer for refreshing thumbnail settings to detect when thumbnails are moved
        private System.Timers.Timer? _refreshTimer;
        private const int REFRESH_INTERVAL_MS = 1000; // Refresh every second

        [ObservableProperty]
        private int _gridCellWidth = 400;

        [ObservableProperty]
        private int _gridCellHeight = 300;

        [ObservableProperty]
        private int _gridStartX = 100;

        [ObservableProperty]
        private int _gridStartY = 100;

        [ObservableProperty]
        private int _gridColumns = 3;

        [ObservableProperty]
        private WindowRatio _gridCellRatio = WindowRatio.None;

        /// <summary>
        /// Gets all available WindowRatio enum values for ComboBox binding.
        /// </summary>
        public Array WindowRatioValues => Enum.GetValues(typeof(WindowRatio));

        // String properties for direct text input
        [ObservableProperty]
        private string _gridCellWidthText = "400";

        [ObservableProperty]
        private string _gridCellHeightText = "300";

        [ObservableProperty]
        private string _gridStartXText = "100";

        [ObservableProperty]
        private string _gridStartYText = "100";

        [ObservableProperty]
        private string _gridColumnsText = "3";

        [ObservableProperty]
        private List<DatabaseService.ThumbnailSetting> _thumbnailSettings = new();

        [ObservableProperty]
        private ObservableCollection<GridLayoutItem> _gridPreview = new();

        [ObservableProperty]
        private DatabaseService.ThumbnailSetting? _selectedThumbnailForStartPosition;

        public GridLayoutViewModel(DatabaseService databaseService, IThumbnailWindowService thumbnailWindowService)
        {
            _databaseService = databaseService;
            _thumbnailWindowService = thumbnailWindowService;

            // Initialize debounce timers
            _gridCellWidthTextTimer = new System.Timers.Timer(DEBOUNCE_DELAY_MS);
            _gridCellWidthTextTimer.Elapsed += OnGridCellWidthTextTimerElapsed;
            _gridCellWidthTextTimer.AutoReset = false;

            _gridCellHeightTextTimer = new System.Timers.Timer(DEBOUNCE_DELAY_MS);
            _gridCellHeightTextTimer.Elapsed += OnGridCellHeightTextTimerElapsed;
            _gridCellHeightTextTimer.AutoReset = false;

            _gridStartXTextTimer = new System.Timers.Timer(DEBOUNCE_DELAY_MS);
            _gridStartXTextTimer.Elapsed += OnGridStartXTextTimerElapsed;
            _gridStartXTextTimer.AutoReset = false;

            _gridStartYTextTimer = new System.Timers.Timer(DEBOUNCE_DELAY_MS);
            _gridStartYTextTimer.Elapsed += OnGridStartYTextTimerElapsed;
            _gridStartYTextTimer.AutoReset = false;

            _gridColumnsTextTimer = new System.Timers.Timer(DEBOUNCE_DELAY_MS);
            _gridColumnsTextTimer.Elapsed += OnGridColumnsTextTimerElapsed;
            _gridColumnsTextTimer.AutoReset = false;

            // Subscribe to thumbnail service events
            _thumbnailWindowService.ThumbnailAdded += OnThumbnailAdded;
            _thumbnailWindowService.ThumbnailRemoved += OnThumbnailRemoved;
        }

        public void OnNavigatedTo()
        {
            if (!_isInitialized)
                InitializeViewModel();

            LoadThumbnailSettings();
            UpdateGridPreview();

            // Start refresh timer to detect when thumbnails are moved (position/size changes)
            StartRefreshTimer();
        }

        public void OnNavigatedFrom()
        {
            // Clean up timers when navigating away
            _gridCellWidthTextTimer?.Stop();
            _gridCellHeightTextTimer?.Stop();
            _gridStartXTextTimer?.Stop();
            _gridStartYTextTimer?.Stop();
            _gridColumnsTextTimer?.Stop();
            _refreshTimer?.Stop();
        }

        private void OnThumbnailAdded(object? sender, YAEP.Interface.ThumbnailWindowChangedEventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                // Reload thumbnail settings when a new thumbnail is added
                LoadThumbnailSettings();
                UpdateGridPreview();
            });
        }

        private void OnThumbnailRemoved(object? sender, YAEP.Interface.ThumbnailWindowChangedEventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                // Reload thumbnail settings when a thumbnail is removed
                LoadThumbnailSettings();
                UpdateGridPreview();
            });
        }

        private void StartRefreshTimer()
        {
            if (_refreshTimer == null)
            {
                _refreshTimer = new System.Timers.Timer(REFRESH_INTERVAL_MS);
                _refreshTimer.Elapsed += OnRefreshTimerElapsed;
                _refreshTimer.AutoReset = true;
            }
            _refreshTimer.Start();
        }

        private void OnRefreshTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                RefreshThumbnailSettingsIfChanged();
            });
        }

        private void RefreshThumbnailSettingsIfChanged()
        {
            DatabaseService.Profile? activeProfile = _databaseService.GetActiveProfile() ?? _databaseService.CurrentProfile;
            if (activeProfile == null)
                return;

            // Get active thumbnail window titles
            List<string> activeWindowTitles = _thumbnailWindowService.GetActiveThumbnailWindowTitles();
            HashSet<string> activeTitlesSet = new HashSet<string>(activeWindowTitles, StringComparer.OrdinalIgnoreCase);

            List<DatabaseService.ThumbnailSetting> currentSettings = _databaseService.GetAllThumbnailSettings(activeProfile.Id)
                .Where(s => ShouldIncludeWindowTitle(s.WindowTitle) && activeTitlesSet.Contains(s.WindowTitle))
                .ToList();

            // Check if settings have changed by comparing counts and positions
            bool hasChanged = false;
            if (ThumbnailSettings.Count != currentSettings.Count)
            {
                hasChanged = true;
            }
            else
            {
                // Compare positions and sizes
                Dictionary<string, DatabaseService.ThumbnailSetting> currentDict = currentSettings.ToDictionary(s => s.WindowTitle);
                foreach (DatabaseService.ThumbnailSetting existing in ThumbnailSettings)
                {
                    if (currentDict.TryGetValue(existing.WindowTitle, out DatabaseService.ThumbnailSetting? current))
                    {
                        if (existing.Config.X != current.Config.X ||
                            existing.Config.Y != current.Config.Y ||
                            existing.Config.Width != current.Config.Width ||
                            existing.Config.Height != current.Config.Height ||
                            existing.Config.Opacity != current.Config.Opacity)
                        {
                            hasChanged = true;
                            break;
                        }
                    }
                    else
                    {
                        hasChanged = true;
                        break;
                    }
                }
            }

            if (hasChanged)
            {
                // Preserve the selected thumbnail if it still exists
                string? selectedTitle = SelectedThumbnailForStartPosition?.WindowTitle;

                // Update the settings
                ThumbnailSettings = currentSettings;

                // Restore selection if it still exists
                if (!string.IsNullOrEmpty(selectedTitle))
                {
                    SelectedThumbnailForStartPosition = ThumbnailSettings
                        .FirstOrDefault(s => s.WindowTitle == selectedTitle);
                }

                // Update grid preview to reflect new positions
                UpdateGridPreview();
            }
        }

        private void InitializeViewModel()
        {
            _isInitialized = true;
        }

        private void LoadThumbnailSettings()
        {
            // Preserve the selected thumbnail if it exists
            string? selectedTitle = SelectedThumbnailForStartPosition?.WindowTitle;

            DatabaseService.Profile? activeProfile = _databaseService.GetActiveProfile() ?? _databaseService.CurrentProfile;
            if (activeProfile != null)
            {
                List<DatabaseService.ThumbnailSetting> allSettings = _databaseService.GetAllThumbnailSettings(activeProfile.Id);

                // Get active thumbnail window titles
                List<string> activeWindowTitles = _thumbnailWindowService.GetActiveThumbnailWindowTitles();
                HashSet<string> activeTitlesSet = new HashSet<string>(activeWindowTitles, StringComparer.OrdinalIgnoreCase);

                // Filter: only include "EVE - CharacterName" format AND only active thumbnails
                ThumbnailSettings = allSettings
                    .Where(s => ShouldIncludeWindowTitle(s.WindowTitle) && activeTitlesSet.Contains(s.WindowTitle))
                    .ToList();
                System.Diagnostics.Debug.WriteLine($"LoadThumbnailSettings: Loaded {ThumbnailSettings.Count} active thumbnail setting(s) for profile {activeProfile.Id} ({activeProfile.Name}) (filtered from {allSettings.Count} total, {activeWindowTitles.Count} active)");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("LoadThumbnailSettings: No active profile found");
                ThumbnailSettings = new List<DatabaseService.ThumbnailSetting>();
            }

            // Restore selection if it still exists
            if (!string.IsNullOrEmpty(selectedTitle))
            {
                SelectedThumbnailForStartPosition = ThumbnailSettings
                    .FirstOrDefault(s => s.WindowTitle == selectedTitle);
            }
        }

        private bool ShouldIncludeWindowTitle(string windowTitle)
        {
            if (string.IsNullOrWhiteSpace(windowTitle))
                return false;

            // Exclude "EVE" exactly (case-insensitive) - only include windows with "EVE -" format
            if (windowTitle.Equals("EVE", StringComparison.OrdinalIgnoreCase))
                return false;

            // Include all other window titles (including "EVE - CharacterName" format)
            return true;
        }

        partial void OnGridCellWidthChanged(int value)
        {
            GridCellWidthText = value.ToString();

            // Recalculate height if ratio is set
            if (GridCellRatio != WindowRatio.None && !_isCalculatingHeight)
            {
                _isCalculatingHeight = true;
                GridCellHeight = CalculateHeightFromRatio(value, GridCellRatio);
                _isCalculatingHeight = false;
            }

            UpdateGridPreview();
        }

        partial void OnGridCellHeightChanged(int value)
        {
            GridCellHeightText = value.ToString();

            // If ratio is set and we're not already calculating, ensure height matches ratio
            if (GridCellRatio != WindowRatio.None && !_isCalculatingHeight)
            {
                int calculatedHeight = CalculateHeightFromRatio(GridCellWidth, GridCellRatio);
                if (calculatedHeight != value)
                {
                    _isCalculatingHeight = true;
                    GridCellHeight = calculatedHeight;
                    _isCalculatingHeight = false;
                    return; // Don't update preview yet, it will be updated when GridCellHeight is set again
                }
            }

            UpdateGridPreview();
        }

        partial void OnGridStartXChanged(int value)
        {
            GridStartXText = value.ToString();
            UpdateGridPreview();
        }

        partial void OnGridStartYChanged(int value)
        {
            GridStartYText = value.ToString();
            UpdateGridPreview();
        }

        partial void OnGridColumnsChanged(int value)
        {
            GridColumnsText = value.ToString();
            UpdateGridPreview();
        }

        partial void OnGridCellRatioChanged(WindowRatio value)
        {
            // Recalculate height when ratio changes
            if (value != WindowRatio.None && !_isCalculatingHeight)
            {
                _isCalculatingHeight = true;
                GridCellHeight = CalculateHeightFromRatio(GridCellWidth, value);
                _isCalculatingHeight = false;
            }
            UpdateGridPreview();
        }

        partial void OnGridCellWidthTextChanged(string value)
        {
            // Debounce the update
            _gridCellWidthTextTimer?.Stop();
            _gridCellWidthTextTimer?.Start();
        }

        partial void OnGridCellHeightTextChanged(string value)
        {
            // Only allow manual height changes if ratio is None
            if (GridCellRatio == WindowRatio.None)
            {
                // Debounce the update
                _gridCellHeightTextTimer?.Stop();
                _gridCellHeightTextTimer?.Start();
            }
        }

        partial void OnGridStartXTextChanged(string value)
        {
            // Debounce the update
            _gridStartXTextTimer?.Stop();
            _gridStartXTextTimer?.Start();
        }

        partial void OnGridStartYTextChanged(string value)
        {
            // Debounce the update
            _gridStartYTextTimer?.Stop();
            _gridStartYTextTimer?.Start();
        }

        partial void OnGridColumnsTextChanged(string value)
        {
            // Debounce the update
            _gridColumnsTextTimer?.Stop();
            _gridColumnsTextTimer?.Start();
        }

        private void OnGridCellWidthTextTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                ProcessTextInput(GridCellWidthText, 192, 960, value => GridCellWidth = value, value => GridCellWidthText = value.ToString());
            });
        }

        private void OnGridCellHeightTextTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                // Only process if ratio is None (manual height entry)
                if (GridCellRatio == WindowRatio.None)
                {
                    ProcessTextInput(GridCellHeightText, 108, 540, value => GridCellHeight = value, value => GridCellHeightText = value.ToString());
                }
            });
        }

        private void OnGridStartXTextTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                ProcessTextInput(GridStartXText, -10_000, 31_000, value => GridStartX = value, value => GridStartXText = value.ToString());
            });
        }

        private void OnGridStartYTextTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                ProcessTextInput(GridStartYText, -10_000, 31_000, value => GridStartY = value, value => GridStartYText = value.ToString());
            });
        }

        private void OnGridColumnsTextTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                ProcessTextInput(GridColumnsText, 1, 10, value => GridColumns = value, value => GridColumnsText = value.ToString());
            });
        }

        private void ProcessTextInput(string textValue, int min, int max, Action<int> setValue, Action<int> updateText)
        {
            if (string.IsNullOrWhiteSpace(textValue))
                return;

            if (int.TryParse(textValue, out int parsedValue))
            {
                int clampedValue = Math.Clamp(parsedValue, min, max);
                setValue(clampedValue);
                // Update text if value was clamped
                if (clampedValue != parsedValue)
                {
                    updateText(clampedValue);
                }
            }
        }

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
                return GridCellHeight; // Return current height if ratio is None

            // Calculate height: height = width / aspectRatio
            int calculatedHeight = (int)Math.Round(width / aspectRatio);

            // Clamp to valid range
            return Math.Clamp(calculatedHeight, 108, 540);
        }

        partial void OnThumbnailSettingsChanged(List<DatabaseService.ThumbnailSetting> value)
        {
            UpdateGridPreview();
        }

        partial void OnSelectedThumbnailForStartPositionChanged(DatabaseService.ThumbnailSetting? value)
        {
            if (value != null && value.Config != null)
            {
                GridStartX = value.Config.X;
                GridStartY = value.Config.Y;
                GridCellWidth = value.Config.Width;
                GridCellHeight = value.Config.Height;
            }
        }

        private void UpdateGridPreview()
        {
            if (ThumbnailSettings == null || ThumbnailSettings.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("UpdateGridPreview: No thumbnail settings available");
                GridPreview.Clear();
                return;
            }

            System.Diagnostics.Debug.WriteLine($"UpdateGridPreview: Processing {ThumbnailSettings.Count} thumbnail setting(s)");

            // Get DisplayOrder from ClientGroupMembers for the active profile
            DatabaseService.Profile? activeProfile = _databaseService.GetActiveProfile() ?? _databaseService.CurrentProfile;
            Dictionary<string, (int GroupDisplayOrder, int MemberDisplayOrder)> groupMemberOrdering = activeProfile != null
                ? _databaseService.GetAllClientGroupMembersForProfile(activeProfile.Id)
                : new Dictionary<string, (int GroupDisplayOrder, int MemberDisplayOrder)>(StringComparer.OrdinalIgnoreCase);

            // Order by: GroupDisplayOrder, then MemberDisplayOrder, then Config.X for ungrouped items
            List<DatabaseService.ThumbnailSetting> orderedSettings = ThumbnailSettings.OrderBy(t =>
            {
                if (groupMemberOrdering.TryGetValue(t.WindowTitle, out (int GroupDisplayOrder, int MemberDisplayOrder) ordering))
                {
                    // Return a tuple that will sort by GroupDisplayOrder first, then MemberDisplayOrder
                    return (ordering.GroupDisplayOrder, ordering.MemberDisplayOrder);
                }
                else
                {
                    // Ungrouped items come after grouped items, sorted by Config.X
                    return (int.MaxValue, t.Config.X);
                }
            }).ToList();

            // Calculate grid positions
            GridPreview.Clear();
            int row = 0;
            int col = 0;

            foreach (DatabaseService.ThumbnailSetting? setting in orderedSettings)
            {
                int x = GridStartX + (col * GridCellWidth);
                int y = GridStartY + (row * GridCellHeight);

                GridPreview.Add(new GridLayoutItem
                {
                    WindowTitle = setting.WindowTitle,
                    CurrentX = setting.Config.X,
                    CurrentY = setting.Config.Y,
                    CurrentWidth = setting.Config.Width,
                    CurrentHeight = setting.Config.Height,
                    NewX = x,
                    NewY = y,
                    NewWidth = GridCellWidth,
                    NewHeight = GridCellHeight,
                    Opacity = setting.Config.Opacity,
                    FocusBorderColor = setting.Config.FocusBorderColor ?? "#0078D4",
                    FocusBorderThickness = setting.Config.FocusBorderThickness,
                    ShowTitleOverlay = setting.Config.ShowTitleOverlay,
                    Row = row,
                    Column = col
                });

                col++;
                if (col >= GridColumns)
                {
                    col = 0;
                    row++;
                }
            }
        }

        [RelayCommand]
        private Task OnApplyGridLayout()
        {
            if (GridPreview == null || GridPreview.Count == 0)
            {
                // TODO: Show message dialog using Avalonia's dialog system
                System.Diagnostics.Debug.WriteLine("No thumbnails to arrange in grid layout.");
                return Task.CompletedTask;
            }

            DatabaseService.Profile? activeProfile = _databaseService.GetActiveProfile() ?? _databaseService.CurrentProfile;
            if (activeProfile == null)
            {
                // TODO: Show message dialog using Avalonia's dialog system
                System.Diagnostics.Debug.WriteLine("No active profile found. Please select a profile in Settings.");
                return Task.CompletedTask;
            }

            // TODO: Show confirmation dialog using Avalonia's dialog system
            // For now, proceed without confirmation

            // Get active thumbnail window titles to ensure we only apply to active thumbnails
            List<string> activeWindowTitles = _thumbnailWindowService.GetActiveThumbnailWindowTitles();
            HashSet<string> activeTitlesSet = new HashSet<string>(activeWindowTitles, StringComparer.OrdinalIgnoreCase);

            // Pause monitoring to prevent interference during grid application
            _thumbnailWindowService.PauseMonitoring();

            try
            {
                int successCount = 0;
                int errorCount = 0;
                string? errorMessage = null;

                // Apply grid layout only to active thumbnails
                foreach (GridLayoutItem item in GridPreview)
                {
                    // Skip if this thumbnail is not currently active
                    if (!activeTitlesSet.Contains(item.WindowTitle))
                    {
                        System.Diagnostics.Debug.WriteLine($"Skipping '{item.WindowTitle}' - thumbnail is not active");
                        continue;
                    }

                    try
                    {
                        // Use border color and thickness from GridLayoutItem (preserved from ThumbnailSettings)
                        DatabaseService.ThumbnailConfig config = new DatabaseService.ThumbnailConfig
                        {
                            Width = item.NewWidth,
                            Height = item.NewHeight,
                            X = item.NewX,
                            Y = item.NewY,
                            Opacity = item.Opacity, // Preserve opacity
                            // Preserve border color and thickness from GridLayoutItem
                            FocusBorderColor = item.FocusBorderColor ?? "#0078D4",
                            FocusBorderThickness = item.FocusBorderThickness,
                            // Preserve title overlay setting from GridLayoutItem
                            ShowTitleOverlay = item.ShowTitleOverlay
                        };

                        System.Diagnostics.Debug.WriteLine($"Saving grid layout for '{item.WindowTitle}': X={config.X}, Y={config.Y}, W={config.Width}, H={config.Height}");
                        _databaseService.SaveThumbnailSettings(activeProfile.Id, item.WindowTitle, config);

                        // Verify the save worked by reading it back
                        DatabaseService.ThumbnailConfig? savedConfig = _databaseService.GetThumbnailSettings(activeProfile.Id, item.WindowTitle);
                        if (savedConfig != null &&
                            savedConfig.X == config.X &&
                            savedConfig.Y == config.Y &&
                            savedConfig.Width == config.Width &&
                            savedConfig.Height == config.Height)
                        {
                            System.Diagnostics.Debug.WriteLine($"Verified: Settings saved correctly for '{item.WindowTitle}'");
                            successCount++;
                        }
                        else
                        {
                            errorCount++;
                            errorMessage = $"Settings for '{item.WindowTitle}' were not saved correctly (verification failed)";
                            System.Diagnostics.Debug.WriteLine(errorMessage);
                        }
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        errorMessage = $"Error updating '{item.WindowTitle}': {ex.Message}";
                        System.Diagnostics.Debug.WriteLine(errorMessage);
                        System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                    }
                }

                // Update all thumbnail windows to reflect the new settings
                try
                {
                    _thumbnailWindowService.UpdateAllThumbnails();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error updating thumbnail windows: {ex.Message}");
                }

                // Reload settings to refresh the preview
                LoadThumbnailSettings();
                UpdateGridPreview();

                // TODO: Show result message using Avalonia's dialog system
                System.Diagnostics.Debug.WriteLine($"Applied grid layout to {successCount} thumbnail(s). Errors: {errorCount}");
            }
            finally
            {
                // Always resume monitoring, even if there was an error
                _thumbnailWindowService.ResumeMonitoring();
            }
            return Task.CompletedTask;
        }

        public class GridLayoutItem
        {
            public string WindowTitle { get; set; } = string.Empty;
            public int CurrentX { get; set; }
            public int CurrentY { get; set; }
            public int CurrentWidth { get; set; }
            public int CurrentHeight { get; set; }
            public int NewX { get; set; }
            public int NewY { get; set; }
            public int NewWidth { get; set; }
            public int NewHeight { get; set; }
            public double Opacity { get; set; }
            public string FocusBorderColor { get; set; } = "#0078D4";
            public int FocusBorderThickness { get; set; } = 3;
            public bool ShowTitleOverlay { get; set; } = true;
            public int Row { get; set; }
            public int Column { get; set; }
        }
    }
}


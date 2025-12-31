using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using YAEP.Interface;
using YAEP.Models;
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

        private System.Timers.Timer? _gridCellWidthTextTimer;
        private System.Timers.Timer? _gridCellHeightTextTimer;
        private System.Timers.Timer? _gridStartXTextTimer;
        private System.Timers.Timer? _gridStartYTextTimer;
        private System.Timers.Timer? _gridColumnsTextTimer;
        private const int DEBOUNCE_DELAY_MS = 500;

        private System.Timers.Timer? _refreshTimer;
        private const int REFRESH_INTERVAL_MS = 1000;

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

        public Array WindowRatioValues => Enum.GetValues(typeof(WindowRatio));

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

        [ObservableProperty]
        private ObservableCollection<MonitorInfo> _availableMonitors = new();

        [ObservableProperty]
        private MonitorInfo? _selectedMonitor;

        public GridLayoutViewModel(DatabaseService databaseService, IThumbnailWindowService thumbnailWindowService)
        {
            _databaseService = databaseService;
            _thumbnailWindowService = thumbnailWindowService;

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

            _thumbnailWindowService.ThumbnailAdded += OnThumbnailAdded;
            _thumbnailWindowService.ThumbnailRemoved += OnThumbnailRemoved;
        }

        public void OnNavigatedTo(Window? window = null)
        {
            if (!_isInitialized)
                InitializeViewModel();

            LoadAvailableMonitors(window);

            LoadThumbnailSettings();
            UpdateGridPreview();

            StartRefreshTimer();
        }

        public void OnNavigatedFrom()
        {
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
                LoadThumbnailSettings();
                UpdateGridPreview();
            });
        }

        private void OnThumbnailRemoved(object? sender, YAEP.Interface.ThumbnailWindowChangedEventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
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

            List<string> activeWindowTitles = _thumbnailWindowService.GetActiveThumbnailWindowTitles();
            HashSet<string> activeTitlesSet = new HashSet<string>(activeWindowTitles, StringComparer.OrdinalIgnoreCase);

            List<DatabaseService.ThumbnailSetting> currentSettings = _databaseService.GetAllThumbnailSettings(activeProfile.Id)
                    .Where(s => ShouldIncludeWindowTitle(s.WindowTitle) && activeTitlesSet.Contains(s.WindowTitle))
                .ToList();

            bool hasChanged = false;
            if (ThumbnailSettings.Count != currentSettings.Count)
            {
                hasChanged = true;
            }
            else
            {
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
                string? selectedTitle = SelectedThumbnailForStartPosition?.WindowTitle;

                ThumbnailSettings = currentSettings;

                if (!string.IsNullOrEmpty(selectedTitle))
                {
                    SelectedThumbnailForStartPosition = ThumbnailSettings
                        .FirstOrDefault(s => s.WindowTitle == selectedTitle);
                }

                UpdateGridPreview();
            }
        }

        private void InitializeViewModel()
        {
            _isInitialized = true;
        }

        private void LoadAvailableMonitors(Window? window)
        {
            AvailableMonitors.Clear();

            try
            {
                Screens? screens = null;
                if (window != null)
                {
                    screens = window.Screens;
                }
                else
                {
                    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        screens = desktop.MainWindow?.Screens;
                    }
                }

                if (screens != null)
                {
                    Screen? primaryScreen = screens.Primary;
                    foreach (Screen screen in screens.All)
                    {
                        MonitorInfo monitorInfo = new MonitorInfo
                        {
                            Screen = screen,
                            Name = $"Monitor {AvailableMonitors.Count + 1}",
                            Bounds = screen.Bounds,
                            WorkingArea = screen.WorkingArea,
                            IsPrimary = screen == primaryScreen
                        };
                        AvailableMonitors.Add(monitorInfo);
                    }

                    SelectedMonitor = AvailableMonitors.FirstOrDefault(m => m.IsPrimary) ?? AvailableMonitors.FirstOrDefault();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Warning: Could not access screens, using default monitor");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading monitors: {ex.Message}");
            }
        }

        private void LoadThumbnailSettings()
        {
            string? selectedTitle = SelectedThumbnailForStartPosition?.WindowTitle;

            DatabaseService.Profile? activeProfile = _databaseService.GetActiveProfile() ?? _databaseService.CurrentProfile;
            if (activeProfile != null)
            {
                List<DatabaseService.ThumbnailSetting> allSettings = _databaseService.GetAllThumbnailSettings(activeProfile.Id);

                List<string> activeWindowTitles = _thumbnailWindowService.GetActiveThumbnailWindowTitles();
                HashSet<string> activeTitlesSet = new HashSet<string>(activeWindowTitles, StringComparer.OrdinalIgnoreCase);

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

            if (windowTitle.Equals("EVE", StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        partial void OnGridCellWidthChanged(int value)
        {
            GridCellWidthText = value.ToString();

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

            if (GridCellRatio != WindowRatio.None && !_isCalculatingHeight)
            {
                int calculatedHeight = CalculateHeightFromRatio(GridCellWidth, GridCellRatio);
                if (calculatedHeight != value)
                {
                    _isCalculatingHeight = true;
                    GridCellHeight = calculatedHeight;
                    _isCalculatingHeight = false;
                    return;
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
            _gridCellWidthTextTimer?.Stop();
            _gridCellWidthTextTimer?.Start();
        }

        partial void OnGridCellHeightTextChanged(string value)
        {
            if (GridCellRatio == WindowRatio.None)
            {
                _gridCellHeightTextTimer?.Stop();
                _gridCellHeightTextTimer?.Start();
            }
        }

        partial void OnGridStartXTextChanged(string value)
        {
            _gridStartXTextTimer?.Stop();
            _gridStartXTextTimer?.Start();
        }

        partial void OnGridStartYTextChanged(string value)
        {
            _gridStartYTextTimer?.Stop();
            _gridStartYTextTimer?.Start();
        }

        partial void OnGridColumnsTextChanged(string value)
        {
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
                return GridCellHeight;

            int calculatedHeight = (int)Math.Round(width / aspectRatio);

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
                if (SelectedMonitor != null)
                {
                    GridStartX = value.Config.X - SelectedMonitor.Bounds.X;
                    GridStartY = value.Config.Y - SelectedMonitor.Bounds.Y;
                }
                else
                {
                    GridStartX = value.Config.X;
                    GridStartY = value.Config.Y;
                }
                GridCellWidth = value.Config.Width;
                GridCellHeight = value.Config.Height;
            }
        }

        partial void OnSelectedMonitorChanged(MonitorInfo? value)
        {
            UpdateGridPreview();
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

            DatabaseService.Profile? activeProfile = _databaseService.GetActiveProfile() ?? _databaseService.CurrentProfile;
            Dictionary<string, (int GroupDisplayOrder, int MemberDisplayOrder)> groupMemberOrdering = activeProfile != null
                ? _databaseService.GetAllClientGroupMembersForProfile(activeProfile.Id)
                : new Dictionary<string, (int GroupDisplayOrder, int MemberDisplayOrder)>(StringComparer.OrdinalIgnoreCase);

            List<DatabaseService.ThumbnailSetting> orderedSettings = ThumbnailSettings.OrderBy(t =>
            {
                if (groupMemberOrdering.TryGetValue(t.WindowTitle, out (int GroupDisplayOrder, int MemberDisplayOrder) ordering))
                {
                    return (ordering.GroupDisplayOrder, ordering.MemberDisplayOrder);
                }
                else
                {
                    return (int.MaxValue, t.Config.X);
                }
            }).ToList();

            GridPreview.Clear();
            int row = 0;
            int col = 0;

            int monitorOffsetX = SelectedMonitor?.Bounds.X ?? 0;
            int monitorOffsetY = SelectedMonitor?.Bounds.Y ?? 0;

            foreach (DatabaseService.ThumbnailSetting? setting in orderedSettings)
            {
                int relativeX = GridStartX + (col * GridCellWidth);
                int relativeY = GridStartY + (row * GridCellHeight);
                int x = monitorOffsetX + relativeX;
                int y = monitorOffsetY + relativeY;

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

            List<string> activeWindowTitles = _thumbnailWindowService.GetActiveThumbnailWindowTitles();
            HashSet<string> activeTitlesSet = new HashSet<string>(activeWindowTitles, StringComparer.OrdinalIgnoreCase);

            _thumbnailWindowService.PauseMonitoring();

            try
            {
                int successCount = 0;
                int errorCount = 0;
                string? errorMessage = null;

                foreach (GridLayoutItem item in GridPreview)
                {
                    if (!activeTitlesSet.Contains(item.WindowTitle))
                    {
                        System.Diagnostics.Debug.WriteLine($"Skipping '{item.WindowTitle}' - thumbnail is not active");
                        continue;
                    }

                    try
                    {
                        DatabaseService.ThumbnailConfig config = new DatabaseService.ThumbnailConfig
                        {
                            Width = item.NewWidth,
                            Height = item.NewHeight,
                            X = item.NewX,
                            Y = item.NewY,
                            Opacity = item.Opacity,
                            FocusBorderColor = item.FocusBorderColor ?? "#0078D4",
                            FocusBorderThickness = item.FocusBorderThickness,
                            ShowTitleOverlay = item.ShowTitleOverlay
                        };

                        System.Diagnostics.Debug.WriteLine($"Saving grid layout for '{item.WindowTitle}': X={config.X}, Y={config.Y}, W={config.Width}, H={config.Height}");
                        _databaseService.SaveThumbnailSettings(activeProfile.Id, item.WindowTitle, config);

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

                try
                {
                    _thumbnailWindowService.UpdateAllThumbnails();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error updating thumbnail windows: {ex.Message}");
                }

                LoadThumbnailSettings();
                UpdateGridPreview();

                System.Diagnostics.Debug.WriteLine($"Applied grid layout to {successCount} thumbnail(s). Errors: {errorCount}");
            }
            finally
            {
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


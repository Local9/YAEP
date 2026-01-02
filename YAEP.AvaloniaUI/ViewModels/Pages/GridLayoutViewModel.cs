using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using System.Collections.ObjectModel;
using System.Timers;
using YAEP.Models;

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
        private List<DatabaseService.ThumbnailSetting> _allThumbnailSettings = new();

        [ObservableProperty]
        private ObservableCollection<GridLayoutItem> _gridPreview = new();

        [ObservableProperty]
        private DatabaseService.ThumbnailSetting? _selectedThumbnailForStartPosition;

        [ObservableProperty]
        private ObservableCollection<MonitorInfo> _availableMonitors = new();

        [ObservableProperty]
        private MonitorInfo? _selectedMonitor;

        [ObservableProperty]
        private List<ClientGroup> _availableGroups = new();

        [ObservableProperty]
        private ClientGroup? _selectedGroup;

        [ObservableProperty]
        private bool _onlyAffectActiveThumbnails = true;

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

            LoadGroups();
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
            Profile? activeProfile = _databaseService.GetActiveProfile() ?? _databaseService.CurrentProfile;
            if (activeProfile == null)
                return;

            // Reload all settings (no active filter)
            List<DatabaseService.ThumbnailSetting> allCurrentSettings = _databaseService.GetAllThumbnailSettings(activeProfile.Id)
                .Where(s => ShouldIncludeWindowTitle(s.WindowTitle))
                .ToList();

            bool hasChanged = false;
            if (AllThumbnailSettings.Count != allCurrentSettings.Count)
            {
                hasChanged = true;
            }
            else
            {
                Dictionary<string, DatabaseService.ThumbnailSetting> currentDict = allCurrentSettings.ToDictionary(s => s.WindowTitle);
                foreach (DatabaseService.ThumbnailSetting existing in AllThumbnailSettings)
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
                LoadThumbnailSettings();
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

        private void LoadGroups()
        {
            Profile? activeProfile = _databaseService.GetActiveProfile() ?? _databaseService.CurrentProfile;
            if (activeProfile != null)
            {
                AvailableGroups = _databaseService.GetClientGroups(activeProfile.Id);
                System.Diagnostics.Debug.WriteLine($"LoadGroups: Loaded {AvailableGroups.Count} group(s) for profile {activeProfile.Id} ({activeProfile.Name})");
            }
            else
            {
                AvailableGroups = new List<ClientGroup>();
                System.Diagnostics.Debug.WriteLine("LoadGroups: No active profile found");
            }
        }

        private void LoadThumbnailSettings()
        {
            string? selectedTitle = SelectedThumbnailForStartPosition?.WindowTitle;

            Profile? activeProfile = _databaseService.GetActiveProfile() ?? _databaseService.CurrentProfile;
            if (activeProfile != null)
            {
                // Load all thumbnail settings (for "Use Thumbnail" dropdown - always shows all)
                AllThumbnailSettings = _databaseService.GetAllThumbnailSettings(activeProfile.Id)
                    .Where(s => ShouldIncludeWindowTitle(s.WindowTitle))
                    .ToList();

                // Get active thumbnail window titles if filtering by active status
                HashSet<string>? activeTitlesSet = null;
                if (OnlyAffectActiveThumbnails)
                {
                    // Only work with currently active thumbnail windows
                    List<string> activeWindowTitles = _thumbnailWindowService.GetActiveThumbnailWindowTitles();
                    activeTitlesSet = new HashSet<string>(activeWindowTitles, StringComparer.OrdinalIgnoreCase);
                }

                // Filter by selected group if one is selected
                if (SelectedGroup != null)
                {
                    List<ClientGroupMember> groupMembers = _databaseService.GetClientGroupMembers(SelectedGroup.Id);
                    HashSet<string> groupWindowTitles = new HashSet<string>(groupMembers.Select(m => m.WindowTitle), StringComparer.OrdinalIgnoreCase);
                    
                    var filtered = AllThumbnailSettings
                        .Where(s => groupWindowTitles.Contains(s.WindowTitle));
                    
                    // When OnlyAffectActiveThumbnails is true, only include active thumbnail windows
                    if (OnlyAffectActiveThumbnails && activeTitlesSet != null)
                    {
                        filtered = filtered.Where(s => activeTitlesSet.Contains(s.WindowTitle));
                    }
                    
                    ThumbnailSettings = filtered.ToList();
                    
                    System.Diagnostics.Debug.WriteLine($"LoadThumbnailSettings: Loaded {ThumbnailSettings.Count} thumbnail setting(s) for group '{SelectedGroup.Name}' (filtered from {AllThumbnailSettings.Count} total, active only: {OnlyAffectActiveThumbnails})");
                }
                else
                {
                    // When OnlyAffectActiveThumbnails is true, only include active thumbnail windows
                    if (OnlyAffectActiveThumbnails && activeTitlesSet != null)
                    {
                        ThumbnailSettings = AllThumbnailSettings
                            .Where(s => activeTitlesSet.Contains(s.WindowTitle))
                            .ToList();
                    }
                    else
                    {
                        // Show all thumbnails if no group is selected and checkbox is unchecked
                        ThumbnailSettings = AllThumbnailSettings.ToList();
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"LoadThumbnailSettings: Loaded {ThumbnailSettings.Count} thumbnail setting(s) for profile {activeProfile.Id} ({activeProfile.Name}) (all groups, active only: {OnlyAffectActiveThumbnails})");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("LoadThumbnailSettings: No active profile found");
                ThumbnailSettings = new List<DatabaseService.ThumbnailSetting>();
                AllThumbnailSettings = new List<DatabaseService.ThumbnailSetting>();
            }

            if (!string.IsNullOrEmpty(selectedTitle))
            {
                SelectedThumbnailForStartPosition = AllThumbnailSettings
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

        partial void OnSelectedGroupChanged(ClientGroup? value)
        {
            LoadThumbnailSettings();
            UpdateGridPreview();
        }

        partial void OnOnlyAffectActiveThumbnailsChanged(bool value)
        {
            LoadThumbnailSettings();
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

            Profile? activeProfile = _databaseService.GetActiveProfile() ?? _databaseService.CurrentProfile;
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

        /// <summary>
        /// Checks all thumbnail windows and clamps their positions to screen bounds if they're outside monitor boundaries.
        /// This is called after grid layout is applied to ensure thumbnails are within valid screen bounds.
        /// </summary>
        private void CheckAndClampThumbnailBoundaries(long profileId)
        {
            try
            {
                List<YAEP.Views.Windows.ThumbnailWindow> allThumbnails = _thumbnailWindowService.GetAllThumbnailWindows();

                int clampedCount = Dispatcher.UIThread.InvokeAsync(() =>
                {
                    int count = 0;
                    foreach (YAEP.Views.Windows.ThumbnailWindow thumbnail in allThumbnails)
                    {
                        try
                        {
                            Avalonia.PixelPoint currentPosition = thumbnail.Position;
                            
                            if (!thumbnail.IsPositionValid(currentPosition.X, currentPosition.Y))
                            {
                                System.Diagnostics.Debug.WriteLine($"Thumbnail '{thumbnail.WindowTitle}' position ({currentPosition.X}, {currentPosition.Y}) is outside screen bounds, clamping to valid position");
                                
                                Avalonia.PixelPoint clampedPosition = thumbnail.ClampToScreenBounds(currentPosition.X, currentPosition.Y);
                                
                                thumbnail.Position = clampedPosition;
                                
                                ThumbnailConfig? currentConfig = _databaseService.GetThumbnailSettings(profileId, thumbnail.WindowTitle);
                                if (currentConfig != null)
                                {
                                    ThumbnailConfig correctedConfig = new ThumbnailConfig
                                    {
                                        Width = currentConfig.Width,
                                        Height = currentConfig.Height,
                                        X = clampedPosition.X,
                                        Y = clampedPosition.Y,
                                        Opacity = currentConfig.Opacity,
                                        FocusBorderColor = currentConfig.FocusBorderColor,
                                        FocusBorderThickness = currentConfig.FocusBorderThickness,
                                        ShowTitleOverlay = currentConfig.ShowTitleOverlay
                                    };
                                    
                                    _databaseService.SaveThumbnailSettings(profileId, thumbnail.WindowTitle, correctedConfig);
                                    count++;
                                    System.Diagnostics.Debug.WriteLine($"Clamped and saved corrected position for '{thumbnail.WindowTitle}': X={clampedPosition.X}, Y={clampedPosition.Y}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error checking/clamping boundary for thumbnail '{thumbnail.WindowTitle}': {ex.Message}");
                        }
                    }
                    
                    if (count > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"Boundary check: Clamped {count} thumbnail(s) to screen bounds after grid layout application");
                    }
                    
                    return count;
                }, DispatcherPriority.Normal).Result;

                if (clampedCount > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Boundary check completed: {clampedCount} thumbnail(s) were clamped to screen bounds");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in CheckAndClampThumbnailBoundaries: {ex.Message}");
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

            Profile? activeProfile = _databaseService.GetActiveProfile() ?? _databaseService.CurrentProfile;
            if (activeProfile == null)
            {
                // TODO: Show message dialog using Avalonia's dialog system
                System.Diagnostics.Debug.WriteLine("No active profile found. Please select a profile in Settings.");
                return Task.CompletedTask;
            }

            _thumbnailWindowService.PauseMonitoring();

            try
            {
                int successCount = 0;
                int errorCount = 0;
                string? errorMessage = null;

                // Get active window titles if we're only affecting active thumbnails
                HashSet<string>? activeTitlesSet = null;
                if (OnlyAffectActiveThumbnails)
                {
                    List<string> activeWindowTitles = _thumbnailWindowService.GetActiveThumbnailWindowTitles();
                    activeTitlesSet = new HashSet<string>(activeWindowTitles, StringComparer.OrdinalIgnoreCase);
                }

                foreach (GridLayoutItem item in GridPreview)
                {
                    // Skip inactive thumbnails if checkbox is checked
                    if (OnlyAffectActiveThumbnails && activeTitlesSet != null && !activeTitlesSet.Contains(item.WindowTitle))
                    {
                        System.Diagnostics.Debug.WriteLine($"Skipping '{item.WindowTitle}' - thumbnail is not active and 'Only Affect Active Thumbnails' is enabled");
                        continue;
                    }

                    try
                    {
                        ThumbnailConfig config = new ThumbnailConfig
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

                        ThumbnailConfig? savedConfig = _databaseService.GetThumbnailSettings(activeProfile.Id, item.WindowTitle);
                        if (savedConfig != null &&
                            savedConfig.X == config.X &&
                            savedConfig.Y == config.Y &&
                            savedConfig.Width == config.Width &&
                            savedConfig.Height == config.Height)
                        {
                            System.Diagnostics.Debug.WriteLine($"Verified: Settings saved correctly for '{item.WindowTitle}'");
                            successCount++;
                            
                            // Update thumbnail window if it's active
                            List<string> activeWindowTitles = _thumbnailWindowService.GetActiveThumbnailWindowTitles();
                            if (activeWindowTitles.Contains(item.WindowTitle, StringComparer.OrdinalIgnoreCase))
                            {
                                try
                                {
                                    _thumbnailWindowService.UpdateThumbnailByWindowTitle(item.WindowTitle);
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Warning: Could not update active thumbnail '{item.WindowTitle}': {ex.Message}");
                                }
                            }
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

                CheckAndClampThumbnailBoundaries(activeProfile.Id);

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


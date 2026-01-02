using System.Collections.Concurrent;
using System.ComponentModel;
using YAEP.Helpers;
using YAEP.Models;
using YAEP.Views.Windows;

namespace YAEP.Services
{
    public class ThumbnailWindowService : IThumbnailWindowService
    {
        private readonly DatabaseService _databaseService;
        private readonly ConcurrentDictionary<int, ThumbnailWindow> _thumbnailWindows;
        private readonly ConcurrentDictionary<string, ThumbnailConfig> _thumbnailSettingsCache;
        private System.Timers.Timer? _monitoringTimer;
        private System.Timers.Timer? _focusTrackingTimer;
        private readonly object _lockObject = new object();
        private bool _isRunning = false;
        private bool _isPaused = false;
        private ThumbnailWindow? _currentlyFocusedThumbnail;

        private const int MONITORING_INTERVAL_MS = 2000;
        private const int FOCUS_CHECK_INTERVAL_MS = 100;

        public event EventHandler<YAEP.Interface.ThumbnailWindowChangedEventArgs>? ThumbnailAdded;
        public event EventHandler<YAEP.Interface.ThumbnailWindowChangedEventArgs>? ThumbnailRemoved;

        public ThumbnailWindowService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
            _thumbnailWindows = new ConcurrentDictionary<int, ThumbnailWindow>();
            _thumbnailSettingsCache = new ConcurrentDictionary<string, ThumbnailConfig>();

            _databaseService.ProfileChanged += OnProfileChanged;
        }

        public void Start()
        {
            if (_isRunning)
                return;

            lock (_lockObject)
            {
                if (_isRunning)
                    return;

                _isRunning = true;

                ScanAndUpdateThumbnails();

                _monitoringTimer = new System.Timers.Timer(MONITORING_INTERVAL_MS);
                _monitoringTimer.Elapsed += (sender, e) => ScanAndUpdateThumbnails();
                _monitoringTimer.AutoReset = true;
                _monitoringTimer.Start();

                _focusTrackingTimer = new System.Timers.Timer(FOCUS_CHECK_INTERVAL_MS);
                _focusTrackingTimer.Elapsed += (sender, e) => CheckThumbnailFocus();
                _focusTrackingTimer.AutoReset = true;
                _focusTrackingTimer.Start();

                Debug.WriteLine("ThumbnailWindowService started");
            }
        }

        public void Stop()
        {
            if (!_isRunning)
                return;

            lock (_lockObject)
            {
                if (!_isRunning)
                    return;

                _isRunning = false;

                _monitoringTimer?.Stop();
                _monitoringTimer?.Dispose();
                _monitoringTimer = null;

                _focusTrackingTimer?.Stop();
                _focusTrackingTimer?.Dispose();
                _focusTrackingTimer = null;

                _currentlyFocusedThumbnail = null;

                foreach (KeyValuePair<int, ThumbnailWindow> kvp in _thumbnailWindows)
                {
                    try
                    {
                        ThumbnailWindow window = kvp.Value;
                        Dispatcher.UIThread.Post(() =>
                        {
                            window.CloseWindow();
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error closing thumbnail window for process {kvp.Key}: {ex.Message}");
                    }
                }

                _thumbnailWindows.Clear();

                _databaseService.ProfileChanged -= OnProfileChanged;

                Debug.WriteLine("ThumbnailWindowService stopped");
            }
        }

        private void ScanAndUpdateThumbnails()
        {
            if (_isPaused)
            {
                Debug.WriteLine("ThumbnailWindowService: Scanning paused, skipping update");
                return;
            }

            try
            {
                HashSet<int> trackedProcessIds = new HashSet<int>(_thumbnailWindows.Keys);

                Profile? currentProfile = _databaseService.CurrentProfile;
                if (currentProfile == null)
                {
                    Debug.WriteLine("No current profile found, skipping thumbnail scan");
                    return;
                }

                List<string> applicationsToPreview = _databaseService.GetProcessNames(currentProfile.Id);

                foreach (string applicationName in applicationsToPreview)
                {
                    try
                    {
                        if (!SecurityValidationHelper.IsValidProcessName(applicationName))
                        {
                            Debug.WriteLine($"Invalid process name from database: {applicationName}");
                            continue;
                        }

                        string processName = applicationName;
                        if (processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            processName = processName.Substring(0, processName.Length - 4);
                        }

                        Process[] processes = Process.GetProcessesByName(processName);

                        if (processes.Length == 0)
                            continue;

                        foreach (Process process in processes)
                        {
                            int processId = 0;

                            try
                            {
                                processId = process.Id;

                                bool hasExited;
                                try
                                {
                                    hasExited = process.HasExited;
                                }
                                catch (Win32Exception)
                                {
                                    process.Dispose();
                                    continue;
                                }

                                if (hasExited)
                                {
                                    RemoveThumbnailIfExists(processId);
                                    process.Dispose();
                                    continue;
                                }

                                IntPtr mainWindowHandle;
                                try
                                {
                                    mainWindowHandle = process.MainWindowHandle;
                                }
                                catch (Win32Exception)
                                {
                                    process.Dispose();
                                    continue;
                                }

                                if (mainWindowHandle == IntPtr.Zero)
                                {
                                    RemoveThumbnailIfExists(processId);
                                    process.Dispose();
                                    continue;
                                }

                                if (!_thumbnailWindows.ContainsKey(processId))
                                {
                                    CreateThumbnailForProcess(process);
                                }
                                else
                                {
                                    try
                                    {
                                        processName = process.ProcessName;
                                        trackedProcessIds.Remove(processId);
                                    }
                                    catch (InvalidOperationException)
                                    {
                                        RemoveThumbnailIfExists(processId);
                                    }
                                    catch (Win32Exception)
                                    {
                                        RemoveThumbnailIfExists(processId);
                                    }
                                }
                            }
                            catch (Win32Exception)
                            {
                                try
                                {
                                    process.Dispose();
                                }
                                catch
                                {
                                }
                            }
                            catch (InvalidOperationException)
                            {
                                try
                                {
                                    RemoveThumbnailIfExists(processId);
                                    process.Dispose();
                                }
                                catch
                                {
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Unexpected error processing {applicationName} (PID: {processId}): {ex.Message}");
                                try
                                {
                                    process?.Dispose();
                                }
                                catch
                                {
                                }
                            }
                        }
                    }
                    catch (Win32Exception)
                    {
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error scanning processes for application '{applicationName}': {ex.Message}");
                    }
                }

                foreach (int processId in trackedProcessIds)
                {
                    RemoveThumbnailIfExists(processId);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in ScanAndUpdateThumbnails: {ex.Message}");
            }
        }

        private void CreateThumbnailForProcess(Process process)
        {
            int processId = 0;
            try
            {
                processId = process.Id;

                IntPtr mainWindowHandle;
                try
                {
                    mainWindowHandle = process.MainWindowHandle;
                }
                catch (Win32Exception)
                {
                    return;
                }

                if (mainWindowHandle == IntPtr.Zero)
                {
                    return;
                }

                string windowTitle;
                try
                {
                    windowTitle = process.MainWindowTitle;
                }
                catch (Win32Exception)
                {
                    windowTitle = $"Process {processId}";
                }

                if (!ShouldCreateThumbnailForWindow(windowTitle))
                {
                    Debug.WriteLine($"Skipping thumbnail creation for window '{windowTitle}' (filtered out - not an active character window)");
                    return;
                }

                Debug.WriteLine($"Creating thumbnail for process (PID: {processId}), Window: {windowTitle}");

                Profile? currentProfile = _databaseService.CurrentProfile;
                if (currentProfile == null)
                {
                    Debug.WriteLine("No current profile found, cannot create thumbnail");
                    return;
                }

                Dispatcher.UIThread.Post(() =>
                {
                    ThumbnailWindow thumbnailWindow = new ThumbnailWindow(windowTitle, mainWindowHandle, _databaseService, currentProfile.Id);

                    thumbnailWindow.Topmost = true;
                    thumbnailWindow.ViewModel.IsAlwaysOnTop = true;

                    thumbnailWindow.Show();

                    if (_thumbnailWindows.TryAdd(processId, thumbnailWindow))
                    {
                        UpdateThumbnailSettingsCache(thumbnailWindow);

                        List<DatabaseService.ClientGroup> groups = _databaseService.GetClientGroups(currentProfile.Id);
                        DatabaseService.ClientGroup? defaultGroup = groups.OrderBy(g => g.Id).FirstOrDefault();
                        if (defaultGroup != null)
                        {
                            _databaseService.AddClientToGroup(defaultGroup.Id, windowTitle);
                        }

                        ThumbnailAdded?.Invoke(this, new YAEP.Interface.ThumbnailWindowChangedEventArgs { WindowTitle = windowTitle });
                    }

                    thumbnailWindow.Closed += (sender, e) =>
                    {
                        if (_thumbnailWindows.TryRemove(processId, out _))
                        {
                            _thumbnailSettingsCache.TryRemove(windowTitle, out _);
                            ThumbnailRemoved?.Invoke(this, new YAEP.Interface.ThumbnailWindowChangedEventArgs { WindowTitle = windowTitle });
                        }
                    };
                });
            }
            catch (Win32Exception)
            {
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unexpected error creating thumbnail for process (PID: {processId}): {ex.Message}");
            }
        }

        private void RemoveThumbnailIfExists(int processId)
        {
            if (_thumbnailWindows.TryRemove(processId, out ThumbnailWindow? thumbnailWindow))
            {
                try
                {
                    string windowTitle = thumbnailWindow.WindowTitle;
                    _thumbnailSettingsCache.TryRemove(windowTitle, out _);

                    // Clear focused thumbnail if it was the one being removed
                    if (_currentlyFocusedThumbnail == thumbnailWindow)
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            thumbnailWindow.ViewModel.IsFocused = false;
                        });
                        _currentlyFocusedThumbnail = null;
                    }

                    Dispatcher.UIThread.Post(() =>
                    {
                        thumbnailWindow.CloseWindow();
                    });
                    ThumbnailRemoved?.Invoke(this, new YAEP.Interface.ThumbnailWindowChangedEventArgs { WindowTitle = windowTitle });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error removing thumbnail for process ID {processId}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Determines if a thumbnail should be created for the given window title.
        /// Excludes "EVE" without hyphen - only includes "EVE - CharacterName" format.
        /// </summary>
        private bool ShouldCreateThumbnailForWindow(string windowTitle)
        {
            if (string.IsNullOrWhiteSpace(windowTitle))
                return false;

            if (windowTitle.Equals("EVE", StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        /// <summary>
        /// Handles profile change events by updating all existing thumbnails with new profile settings.
        /// </summary>
        private async void OnProfileChanged(object? sender, ProfileChangedEventArgs e)
        {
            if (e.NewProfile == null)
            {
                Debug.WriteLine("Profile changed to null, closing all thumbnails");
                foreach (KeyValuePair<int, ThumbnailWindow> kvp in _thumbnailWindows)
                {
                    try
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            kvp.Value.CloseWindow();
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error closing thumbnail window for process {kvp.Key}: {ex.Message}");
                    }
                }
                _thumbnailWindows.Clear();
                return;
            }

            Debug.WriteLine($"Profile changed from '{e.OldProfile?.Name}' to '{e.NewProfile.Name}', pausing service to update thumbnails");

            PauseMonitoring();

            try
            {
                Dispatcher.UIThread.Post(() =>
                {
                    foreach (KeyValuePair<int, ThumbnailWindow> kvp in _thumbnailWindows)
                    {
                        try
                        {
                            kvp.Value.UpdateProfile(e.NewProfile.Id);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error updating thumbnail window for process {kvp.Key} to new profile: {ex.Message}");
                        }
                    }
                });

                await Task.Delay(100);
                ResumeMonitoring();
                ScanAndUpdateThumbnails();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during profile change: {ex.Message}");
                await Task.Delay(100);
                ResumeMonitoring();
            }
        }

        /// <summary>
        /// Updates a thumbnail window with the specified window title to refresh its settings.
        /// </summary>
        /// <param name="windowTitle">The window title of the thumbnail to update.</param>
        public void UpdateThumbnailByWindowTitle(string windowTitle)
        {
            if (string.IsNullOrWhiteSpace(windowTitle))
                return;

            Dispatcher.UIThread.Post(() =>
            {
                foreach (KeyValuePair<int, ThumbnailWindow> kvp in _thumbnailWindows)
                {
                    try
                    {
                        if (kvp.Value.WindowTitle == windowTitle)
                        {
                            kvp.Value.RefreshSettings();
                            Debug.WriteLine($"Updated thumbnail window '{windowTitle}' with new settings");
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error updating thumbnail window '{windowTitle}': {ex.Message}");
                    }
                }
            });
        }

        /// <summary>
        /// Updates all thumbnail windows to refresh their settings.
        /// </summary>
        public void UpdateAllThumbnails()
        {
            Dispatcher.UIThread.Post(() =>
            {
                foreach (KeyValuePair<int, ThumbnailWindow> kvp in _thumbnailWindows)
                {
                    try
                    {
                        kvp.Value.RefreshSettings();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error updating thumbnail window (PID: {kvp.Key}): {ex.Message}");
                    }
                }
                Debug.WriteLine($"Updated {_thumbnailWindows.Count} thumbnail window(s) with new settings");
            });
        }

        /// <summary>
        /// Updates border settings on all thumbnail windows immediately for live preview.
        /// </summary>
        /// <param name="borderColor">The border color in hex format (e.g., "#0078D4").</param>
        /// <param name="borderThickness">The border thickness in pixels.</param>
        public void UpdateThumbnailBorderSettings(string borderColor, int borderThickness)
        {
            Dispatcher.UIThread.Post(() =>
            {
                foreach (KeyValuePair<int, ThumbnailWindow> kvp in _thumbnailWindows)
                {
                    try
                    {
                        kvp.Value.UpdateBorderSettings(borderColor, borderThickness);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error updating border settings for thumbnail window (PID: {kvp.Key}): {ex.Message}");
                    }
                }
            });
        }

        /// <summary>
        /// Updates border settings on a single thumbnail window by window title immediately for live preview.
        /// </summary>
        /// <param name="windowTitle">The window title of the thumbnail to update.</param>
        /// <param name="borderColor">The border color in hex format (e.g., "#0078D4").</param>
        /// <param name="borderThickness">The border thickness in pixels.</param>
        public void UpdateThumbnailBorderSettingsByWindowTitle(string windowTitle, string borderColor, int borderThickness)
        {
            if (string.IsNullOrWhiteSpace(windowTitle))
                return;

            Dispatcher.UIThread.Post(() =>
            {
                foreach (KeyValuePair<int, ThumbnailWindow> kvp in _thumbnailWindows)
                {
                    try
                    {
                        if (kvp.Value.WindowTitle == windowTitle)
                        {
                            kvp.Value.UpdateBorderSettings(borderColor, borderThickness);
                            Debug.WriteLine($"Updated border settings for thumbnail '{windowTitle}': Color={borderColor}, Thickness={borderThickness}");
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error updating border settings for thumbnail '{windowTitle}': {ex.Message}");
                    }
                }
            });
        }

        /// <summary>
        /// Pauses the monitoring timer to prevent interference during bulk operations (e.g., grid layout application).
        /// </summary>
        public void PauseMonitoring()
        {
            lock (_lockObject)
            {
                _isPaused = true;
                Debug.WriteLine("ThumbnailWindowService: Monitoring paused");
            }
        }

        /// <summary>
        /// Resumes the monitoring timer after a bulk operation.
        /// </summary>
        public void ResumeMonitoring()
        {
            lock (_lockObject)
            {
                _isPaused = false;
                Debug.WriteLine("ThumbnailWindowService: Monitoring resumed");
            }
        }

        /// <summary>
        /// Gets a list of window titles for all currently active thumbnail windows.
        /// </summary>
        /// <returns>List of window titles for active thumbnails.</returns>
        public List<string> GetActiveThumbnailWindowTitles()
        {
            List<string> windowTitles = new List<string>();

            foreach (KeyValuePair<int, ThumbnailWindow> kvp in _thumbnailWindows)
            {
                try
                {
                    windowTitles.Add(kvp.Value.WindowTitle);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error getting window title for process {kvp.Key}: {ex.Message}");
                }
            }

            return windowTitles;
        }

        /// <summary>
        /// Sets focus on the first available thumbnail window for preview purposes.
        /// </summary>
        public void SetFocusOnFirstThumbnail()
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_thumbnailWindows.Count > 0)
                {
                    ThumbnailWindow? firstThumbnail = _thumbnailWindows.Values.FirstOrDefault();
                    if (firstThumbnail != null)
                    {
                        try
                        {
                            firstThumbnail.Show();
                            firstThumbnail.Topmost = true;
                            firstThumbnail.ViewModel.IsAlwaysOnTop = true;
                            firstThumbnail.SetFocusPreview(true);
                            _currentlyFocusedThumbnail = firstThumbnail;
                            Debug.WriteLine($"Set focus preview on thumbnail '{firstThumbnail.WindowTitle}'");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error setting focus preview on thumbnail: {ex.Message}");
                        }
                    }
                }
                else
                {
                    Debug.WriteLine("SetFocusOnFirstThumbnail: No thumbnail windows available");
                }
            });
        }

        /// <summary>
        /// Checks which thumbnail (if any) currently has focus and updates focus states accordingly.
        /// Only updates focus when a thumbnail gets focus. If a non-thumbnail app gets focus,
        /// the last focused thumbnail keeps its border (QoL feature).
        /// </summary>
        private void CheckThumbnailFocus()
        {
            if (_isPaused || _thumbnailWindows.Count == 0)
                return;

            try
            {
                IntPtr foregroundWindow = YAEP.Interop.User32NativeMethods.GetForegroundWindow();
                if (foregroundWindow == IntPtr.Zero)
                    return;

                ThumbnailWindow? newlyFocusedThumbnail = null;

                // Check if any thumbnail's process is focused
                foreach (KeyValuePair<int, ThumbnailWindow> kvp in _thumbnailWindows)
                {
                    ThumbnailWindow thumbnail = kvp.Value;
                    if (thumbnail.ViewModel.ProcessHandle == IntPtr.Zero)
                        continue;

                    bool isFocused = false;

                    try
                    {
                        if (foregroundWindow == thumbnail.ViewModel.ProcessHandle)
                        {
                            isFocused = true;
                        }
                        else
                        {
                            uint foregroundProcessId = 0;
                            uint currentProcessId = 0;

                            YAEP.Interop.User32NativeMethods.GetWindowThreadProcessId(foregroundWindow, out foregroundProcessId);
                            YAEP.Interop.User32NativeMethods.GetWindowThreadProcessId(thumbnail.ViewModel.ProcessHandle, out currentProcessId);

                            isFocused = (foregroundProcessId != 0 && currentProcessId != 0 && foregroundProcessId == currentProcessId);
                        }

                        if (isFocused)
                        {
                            newlyFocusedThumbnail = thumbnail;
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error checking focus for thumbnail '{thumbnail.WindowTitle}': {ex.Message}");
                    }
                }

                // Update focus states
                Dispatcher.UIThread.Post(() =>
                {
                    if (newlyFocusedThumbnail != null)
                    {
                        // A thumbnail got focus - update it and clear others
                        if (_currentlyFocusedThumbnail != null && _currentlyFocusedThumbnail != newlyFocusedThumbnail)
                        {
                            _currentlyFocusedThumbnail.ViewModel.IsFocused = false;
                        }
                        newlyFocusedThumbnail.ViewModel.IsFocused = true;
                        _currentlyFocusedThumbnail = newlyFocusedThumbnail;
                    }
                    // If no thumbnail is focused (non-thumbnail app has focus), keep the current focused thumbnail's border
                    // This is the QoL feature - we don't clear the border when a non-thumbnail app gets focus
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in CheckThumbnailFocus: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the title overlay visibility on all thumbnail windows.
        /// </summary>
        /// <param name="showTitleOverlay">Whether to show the title overlay.</param>
        public void UpdateAllThumbnailsTitleOverlay(bool showTitleOverlay)
        {
            Dispatcher.UIThread.Post(() =>
            {
                foreach (KeyValuePair<int, ThumbnailWindow> kvp in _thumbnailWindows)
                {
                    try
                    {
                        kvp.Value.UpdateTitleOverlayVisibility(showTitleOverlay);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error updating title overlay for thumbnail (PID: {kvp.Key}): {ex.Message}");
                    }
                }
            });
        }

        /// <summary>
        /// Updates size and opacity on all thumbnail windows immediately for live preview.
        /// </summary>
        /// <param name="width">The width in pixels.</param>
        /// <param name="height">The height in pixels.</param>
        /// <param name="opacity">The opacity value (0.0 to 1.0).</param>
        public void UpdateThumbnailSizeAndOpacity(int width, int height, double opacity)
        {
            Dispatcher.UIThread.Post(() =>
            {
                foreach (KeyValuePair<int, ThumbnailWindow> kvp in _thumbnailWindows)
                {
                    try
                    {
                        kvp.Value.UpdateSizeAndOpacity(width, height, opacity);
                        UpdateThumbnailSettingsCache(kvp.Value);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error updating size and opacity for thumbnail (PID: {kvp.Key}): {ex.Message}");
                    }
                }
            });
        }

        /// <summary>
        /// Updates size and opacity on a single thumbnail window by window title immediately for live preview.
        /// </summary>
        /// <param name="windowTitle">The window title of the thumbnail to update.</param>
        /// <param name="width">The width in pixels.</param>
        /// <param name="height">The height in pixels.</param>
        /// <param name="opacity">The opacity value (0.0 to 1.0).</param>
        public void UpdateThumbnailSizeAndOpacityByWindowTitle(string windowTitle, int width, int height, double opacity)
        {
            if (string.IsNullOrWhiteSpace(windowTitle))
                return;

            Dispatcher.UIThread.Post(() =>
            {
                foreach (KeyValuePair<int, ThumbnailWindow> kvp in _thumbnailWindows)
                {
                    try
                    {
                        if (kvp.Value.WindowTitle == windowTitle)
                        {
                            kvp.Value.UpdateSizeAndOpacity(width, height, opacity);
                            UpdateThumbnailSettingsCache(kvp.Value);
                            Debug.WriteLine($"Updated size and opacity for thumbnail '{windowTitle}': Width={width}, Height={height}, Opacity={opacity}");
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error updating size and opacity for thumbnail '{windowTitle}': {ex.Message}");
                    }
                }
            });
        }

        /// <summary>
        /// Updates the cache with current settings from a thumbnail window.
        /// </summary>
        /// <param name="thumbnailWindow">The thumbnail window to cache settings from.</param>
        private void UpdateThumbnailSettingsCache(ThumbnailWindow thumbnailWindow)
        {
            try
            {
                string windowTitle = thumbnailWindow.WindowTitle;
                Avalonia.PixelPoint position = thumbnailWindow.Position;

                ThumbnailConfig cachedConfig = new ThumbnailConfig
                {
                    Width = (int)thumbnailWindow.Width,
                    Height = (int)thumbnailWindow.Height,
                    X = position.X,
                    Y = position.Y,
                    Opacity = thumbnailWindow.Opacity,
                    FocusBorderColor = thumbnailWindow.ViewModel.FocusBorderColor,
                    FocusBorderThickness = thumbnailWindow.ViewModel.FocusBorderThickness,
                    ShowTitleOverlay = thumbnailWindow.ViewModel.ShowTitleOverlay
                };

                _thumbnailSettingsCache.AddOrUpdate(windowTitle, cachedConfig, (key, oldValue) => cachedConfig);
                Debug.WriteLine($"Updated cache for thumbnail '{windowTitle}': X={position.X}, Y={position.Y}, Width={cachedConfig.Width}, Height={cachedConfig.Height}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating cache for thumbnail '{thumbnailWindow.WindowTitle}': {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the cached thumbnail settings for all current thumbnails.
        /// </summary>
        /// <returns>Dictionary of window titles to their cached settings.</returns>
        public Dictionary<string, ThumbnailConfig> GetCachedThumbnailSettings()
        {
            DispatcherOperation task = Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (KeyValuePair<int, ThumbnailWindow> kvp in _thumbnailWindows)
                {
                    UpdateThumbnailSettingsCache(kvp.Value);
                }
            }, DispatcherPriority.Normal);

            task.Wait(TimeSpan.FromMilliseconds(500));

            return new Dictionary<string, ThumbnailConfig>(_thumbnailSettingsCache);
        }

        /// <summary>
        /// Gets the cached thumbnail settings for a specific window title.
        /// </summary>
        /// <param name="windowTitle">The window title to get settings for.</param>
        /// <returns>The cached settings, or null if not found.</returns>
        public ThumbnailConfig? GetCachedThumbnailSettings(string windowTitle)
        {
            if (string.IsNullOrWhiteSpace(windowTitle))
                return null;

            foreach (KeyValuePair<int, ThumbnailWindow> kvp in _thumbnailWindows)
            {
                if (kvp.Value.WindowTitle == windowTitle)
                {
                    DispatcherOperation task = Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        UpdateThumbnailSettingsCache(kvp.Value);
                    }, DispatcherPriority.Normal);
                    task.Wait(TimeSpan.FromMilliseconds(500));
                    break;
                }
            }

            _thumbnailSettingsCache.TryGetValue(windowTitle, out ThumbnailConfig? config);
            return config;
        }

        /// <summary>
        /// Gets all currently active thumbnail windows.
        /// </summary>
        /// <returns>List of all active thumbnail windows.</returns>
        public List<ThumbnailWindow> GetAllThumbnailWindows()
        {
            return new List<ThumbnailWindow>(_thumbnailWindows.Values);
        }

        /// <summary>
        /// Starts a group drag operation, calculating relative positions of all thumbnails to the primary one.
        /// </summary>
        /// <param name="primaryWindow">The thumbnail window that is being dragged.</param>
        /// <returns>A dictionary mapping thumbnail windows to their relative positions, or null if group drag cannot be started.</returns>
        public Dictionary<ThumbnailWindow, Avalonia.PixelPoint>? StartGroupDrag(ThumbnailWindow primaryWindow)
        {
            if (primaryWindow == null)
                return null;

            try
            {
                List<ThumbnailWindow> allWindows = GetAllThumbnailWindows();
                Dictionary<ThumbnailWindow, Avalonia.PixelPoint> groupDragWindows = new Dictionary<ThumbnailWindow, Avalonia.PixelPoint>();

                Avalonia.PixelPoint primaryPosition;
                try
                {
                    primaryPosition = primaryWindow.Position;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error getting primary window position for group drag: {ex.Message}");
                    return null;
                }

                foreach (ThumbnailWindow window in allWindows)
                {
                    if (window != null && window != primaryWindow)
                    {
                        try
                        {
                            Avalonia.PixelPoint windowPosition = window.Position;
                            Avalonia.PixelPoint relativePos = new Avalonia.PixelPoint(
                                windowPosition.X - primaryPosition.X,
                                windowPosition.Y - primaryPosition.Y);
                            groupDragWindows[window] = relativePos;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error getting position for group drag window: {ex.Message}");
                        }
                    }
                }

                return groupDragWindows.Count > 0 ? groupDragWindows : null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error starting group drag: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Updates positions of all thumbnails in a group drag operation.
        /// Must be called from the UI thread for immediate updates during dragging.
        /// </summary>
        /// <param name="primaryWindow">The primary thumbnail window being dragged.</param>
        /// <param name="newPrimaryPosition">The new position of the primary window.</param>
        /// <param name="groupDragWindows">Dictionary of windows and their relative positions from the group drag start.</param>
        public void UpdateGroupDrag(ThumbnailWindow primaryWindow, Avalonia.PixelPoint newPrimaryPosition, Dictionary<ThumbnailWindow, Avalonia.PixelPoint> groupDragWindows)
        {
            if (primaryWindow == null || groupDragWindows == null)
                return;

            foreach (KeyValuePair<ThumbnailWindow, Avalonia.PixelPoint> kvp in groupDragWindows)
            {
                ThumbnailWindow window = kvp.Key;
                Avalonia.PixelPoint relativePos = kvp.Value;

                try
                {
                    if (window != null)
                    {
                        int groupX = newPrimaryPosition.X + relativePos.X;
                        int groupY = newPrimaryPosition.Y + relativePos.Y;

                        if (window.IsPositionValid(groupX, groupY))
                        {
                            Avalonia.PixelPoint groupNewPosition = new Avalonia.PixelPoint(groupX, groupY);
                            window.UpdatePositionAndLastKnown(groupNewPosition);
                        }
                        else
                        {
                            // Clamp to screen bounds if invalid
                            Avalonia.PixelPoint clampedPosition = window.ClampToScreenBounds(groupX, groupY);
                            window.UpdatePositionAndLastKnown(clampedPosition);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error moving group drag window: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Ends a group drag operation, saving positions for all thumbnails in the group.
        /// Must be called from the UI thread.
        /// </summary>
        /// <param name="groupDragWindows">Dictionary of windows that were part of the group drag.</param>
        public void EndGroupDrag(Dictionary<ThumbnailWindow, Avalonia.PixelPoint> groupDragWindows)
        {
            if (groupDragWindows == null)
                return;

            foreach (ThumbnailWindow window in groupDragWindows.Keys)
            {
                try
                {
                    if (window != null)
                    {
                        window.SaveSettings();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error saving group drag window settings: {ex.Message}");
                }
            }
        }
    }
}



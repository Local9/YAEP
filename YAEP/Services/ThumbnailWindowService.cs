using Avalonia.Threading;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using YAEP.Interface;
using YAEP.Views.Windows;

namespace YAEP.Services
{
    public class ThumbnailWindowService : IThumbnailWindowService
    {
        private readonly DatabaseService _databaseService;
        private readonly ConcurrentDictionary<int, ThumbnailWindow> _thumbnailWindows;
        private System.Timers.Timer? _monitoringTimer;
        private readonly object _lockObject = new object();
        private bool _isRunning = false;
        private bool _isPaused = false;

        private const int MONITORING_INTERVAL_MS = 2000;

        public event EventHandler<YAEP.Interface.ThumbnailWindowChangedEventArgs>? ThumbnailAdded;
        public event EventHandler<YAEP.Interface.ThumbnailWindowChangedEventArgs>? ThumbnailRemoved;

        public ThumbnailWindowService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
            _thumbnailWindows = new ConcurrentDictionary<int, ThumbnailWindow>();

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

                DatabaseService.Profile? currentProfile = _databaseService.CurrentProfile;
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

                                        if (_thumbnailWindows.TryGetValue(processId, out ThumbnailWindow? existingWindow) && currentProfile != null)
                                        {
                                            Dispatcher.UIThread.Post(() =>
                                            {
                                                existingWindow.UpdateProfile(currentProfile.Id);
                                            });
                                        }
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

                DatabaseService.Profile? currentProfile = _databaseService.CurrentProfile;
                if (currentProfile == null)
                {
                    Debug.WriteLine("No current profile found, cannot create thumbnail");
                    return;
                }

                Dispatcher.UIThread.Post(() =>
                {
                    ThumbnailWindow thumbnailWindow = new ThumbnailWindow(windowTitle, mainWindowHandle, _databaseService, currentProfile.Id);
                    thumbnailWindow.Show();

                    if (_thumbnailWindows.TryAdd(processId, thumbnailWindow))
                    {
                        ThumbnailAdded?.Invoke(this, new YAEP.Interface.ThumbnailWindowChangedEventArgs { WindowTitle = windowTitle });
                    }

                    thumbnailWindow.Closed += (sender, e) =>
                    {
                        if (_thumbnailWindows.TryRemove(processId, out _))
                        {
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

            Dispatcher.UIThread.Post(() =>
            {
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
            });

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
                    foreach (KeyValuePair<int, ThumbnailWindow> kvp in _thumbnailWindows)
                    {
                        try
                        {
                            kvp.Value.PauseFocusCheck();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error pausing focus check for thumbnail (PID: {kvp.Key}): {ex.Message}");
                        }
                    }

                    ThumbnailWindow? firstThumbnail = _thumbnailWindows.Values.FirstOrDefault();
                    if (firstThumbnail != null)
                    {
                        try
                        {
                            firstThumbnail.SetFocusPreview(true);
                            Debug.WriteLine($"Set focus preview on thumbnail '{firstThumbnail.WindowTitle}'");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error setting focus preview on thumbnail: {ex.Message}");
                        }
                    }
                }
            });
        }

        /// <summary>
        /// Resumes focus checking on all thumbnail windows.
        /// </summary>
        public void ResumeFocusCheckOnAllThumbnails()
        {
            Dispatcher.UIThread.Post(() =>
            {
                foreach (KeyValuePair<int, ThumbnailWindow> kvp in _thumbnailWindows)
                {
                    try
                    {
                        kvp.Value.ResumeFocusCheck();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error resuming focus check for thumbnail (PID: {kvp.Key}): {ex.Message}");
                    }
                }
            });
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
    }
}


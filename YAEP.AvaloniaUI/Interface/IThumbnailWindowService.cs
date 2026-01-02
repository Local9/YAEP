using YAEP.Models;

namespace YAEP.Interface
{
    /// <summary>
    /// Event arguments for thumbnail window changes.
    /// </summary>
    public class ThumbnailWindowChangedEventArgs : EventArgs
    {
        public string WindowTitle { get; set; } = string.Empty;
    }

    /// <summary>
    /// Service for managing thumbnail windows based on running processes.
    /// </summary>
    public interface IThumbnailWindowService
    {
        /// <summary>
        /// Event raised when a thumbnail window is added.
        /// </summary>
        event EventHandler<ThumbnailWindowChangedEventArgs>? ThumbnailAdded;

        /// <summary>
        /// Event raised when a thumbnail window is removed.
        /// </summary>
        event EventHandler<ThumbnailWindowChangedEventArgs>? ThumbnailRemoved;

        /// <summary>
        /// Starts monitoring processes and managing thumbnail windows.
        /// </summary>
        void Start();

        /// <summary>
        /// Stops monitoring processes and closes all thumbnail windows.
        /// </summary>
        void Stop();

        /// <summary>
        /// Updates a thumbnail window with the specified window title to refresh its settings.
        /// </summary>
        /// <param name="windowTitle">The window title of the thumbnail to update.</param>
        void UpdateThumbnailByWindowTitle(string windowTitle);

        /// <summary>
        /// Updates all thumbnail windows to refresh their settings.
        /// </summary>
        void UpdateAllThumbnails();

        /// <summary>
        /// Pauses the monitoring timer to prevent interference during bulk operations.
        /// </summary>
        void PauseMonitoring();

        /// <summary>
        /// Resumes the monitoring timer after a bulk operation.
        /// </summary>
        void ResumeMonitoring();

        /// <summary>
        /// Gets a list of window titles for all currently active thumbnail windows.
        /// </summary>
        /// <returns>List of window titles for active thumbnails.</returns>
        List<string> GetActiveThumbnailWindowTitles();

        /// <summary>
        /// Updates border settings on all thumbnail windows immediately for live preview.
        /// </summary>
        /// <param name="borderColor">The border color in hex format (e.g., "#0078D4").</param>
        /// <param name="borderThickness">The border thickness in pixels.</param>
        void UpdateThumbnailBorderSettings(string borderColor, int borderThickness);

        /// <summary>
        /// Updates border settings on a single thumbnail window by window title immediately for live preview.
        /// </summary>
        /// <param name="windowTitle">The window title of the thumbnail to update.</param>
        /// <param name="borderColor">The border color in hex format (e.g., "#0078D4").</param>
        /// <param name="borderThickness">The border thickness in pixels.</param>
        void UpdateThumbnailBorderSettingsByWindowTitle(string windowTitle, string borderColor, int borderThickness);

        /// <summary>
        /// Sets focus on the first available thumbnail window for preview purposes.
        /// </summary>
        void SetFocusOnFirstThumbnail();

        /// <summary>
        /// Updates the title overlay visibility on all thumbnail windows.
        /// </summary>
        /// <param name="showTitleOverlay">Whether to show the title overlay.</param>
        void UpdateAllThumbnailsTitleOverlay(bool showTitleOverlay);

        /// <summary>
        /// Updates size and opacity on all thumbnail windows immediately for live preview.
        /// </summary>
        /// <param name="width">The width in pixels.</param>
        /// <param name="height">The height in pixels.</param>
        /// <param name="opacity">The opacity value (0.0 to 1.0).</param>
        void UpdateThumbnailSizeAndOpacity(int width, int height, double opacity);

        /// <summary>
        /// Updates size and opacity on a single thumbnail window by window title immediately for live preview.
        /// </summary>
        /// <param name="windowTitle">The window title of the thumbnail to update.</param>
        /// <param name="width">The width in pixels.</param>
        /// <param name="height">The height in pixels.</param>
        /// <param name="opacity">The opacity value (0.0 to 1.0).</param>
        void UpdateThumbnailSizeAndOpacityByWindowTitle(string windowTitle, int width, int height, double opacity);

        /// <summary>
        /// Gets the cached thumbnail settings for all current thumbnails.
        /// </summary>
        /// <returns>Dictionary of window titles to their cached settings.</returns>
        Dictionary<string, ThumbnailConfig> GetCachedThumbnailSettings();

        /// <summary>
        /// Gets the cached thumbnail settings for a specific window title.
        /// </summary>
        /// <param name="windowTitle">The window title to get settings for.</param>
        /// <returns>The cached settings, or null if not found.</returns>
        ThumbnailConfig? GetCachedThumbnailSettings(string windowTitle);

        /// <summary>
        /// Gets all currently active thumbnail windows.
        /// </summary>
        /// <returns>List of all active thumbnail windows.</returns>
        List<YAEP.Views.Windows.ThumbnailWindow> GetAllThumbnailWindows();

        /// <summary>
        /// Starts a group drag operation, calculating relative positions of all thumbnails to the primary one.
        /// </summary>
        /// <param name="primaryWindow">The thumbnail window that is being dragged.</param>
        /// <returns>A dictionary mapping thumbnail windows to their relative positions, or null if group drag cannot be started.</returns>
        Dictionary<YAEP.Views.Windows.ThumbnailWindow, Avalonia.PixelPoint>? StartGroupDrag(YAEP.Views.Windows.ThumbnailWindow primaryWindow);

        /// <summary>
        /// Updates positions of all thumbnails in a group drag operation.
        /// </summary>
        /// <param name="primaryWindow">The primary thumbnail window being dragged.</param>
        /// <param name="newPrimaryPosition">The new position of the primary window.</param>
        /// <param name="groupDragWindows">Dictionary of windows and their relative positions from the group drag start.</param>
        void UpdateGroupDrag(YAEP.Views.Windows.ThumbnailWindow primaryWindow, Avalonia.PixelPoint newPrimaryPosition, Dictionary<YAEP.Views.Windows.ThumbnailWindow, Avalonia.PixelPoint> groupDragWindows);

        /// <summary>
        /// Ends a group drag operation, saving positions for all thumbnails in the group.
        /// </summary>
        /// <param name="groupDragWindows">Dictionary of windows that were part of the group drag.</param>
        void EndGroupDrag(Dictionary<YAEP.Views.Windows.ThumbnailWindow, Avalonia.PixelPoint> groupDragWindows);
    }
}


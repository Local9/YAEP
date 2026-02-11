namespace YAEP.Shared.Interfaces
{
    /// <summary>
    /// Service for managing global hotkeys across platforms.
    /// </summary>
    public interface IHotkeyService
    {
        /// <summary>
        /// Initializes the hotkey service with a window handle.
        /// </summary>
        /// <param name="windowHandle">The platform-specific window handle (IntPtr on Windows/Wayland, uint on X11).</param>
        void Initialize(IntPtr windowHandle);

        /// <summary>
        /// Registers hotkeys from all groups in the database.
        /// </summary>
        void RegisterHotkeys();

        /// <summary>
        /// Unregisters all hotkeys. Can be called from any thread.
        /// </summary>
        void UnregisterHotkeys();

        /// <summary>
        /// Reloads registered hotkey VKs and updates the keyboard hook.
        /// Call this when hotkeys are changed.
        /// </summary>
        void ReloadHotkeyVKs();

        /// <summary>
        /// Cleans up resources.
        /// </summary>
        void Dispose();
    }
}

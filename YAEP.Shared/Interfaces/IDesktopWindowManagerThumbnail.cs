namespace YAEP.Shared.Interfaces
{
    public interface IDesktopWindowManagerThumbnail
    {
        /// <summary>
        /// Registers a live thumbnail from the source window to be displayed in the destination window.
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="source"></param>
        void Register(IntPtr destination, IntPtr source);

        /// <summary>
        /// Unregisters the live thumbnail, removing it from the destination window.
        /// </summary>
        void Unregister();

        /// <summary>
        /// Moves and resizes the thumbnail to the specified rectangle.
        /// </summary>
        /// <param name="left"></param>
        /// <param name="top"></param>
        /// <param name="right"></param>
        /// <param name="bottom"></param>
        void Move(int left, int top, int right, int bottom);

        /// <summary>
        /// Sets the opacity of the thumbnail (0.0 to 1.0).
        /// </summary>
        /// <param name="opacity">Opacity value between 0.0 (transparent) and 1.0 (opaque).</param>
        void SetOpacity(double opacity);

        /// <summary>
        /// Updates the thumbnail properties to reflect any changes.
        /// </summary>
        void Update();
    }
}

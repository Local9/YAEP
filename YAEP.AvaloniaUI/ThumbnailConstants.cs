namespace YAEP
{
    /// <summary>
    /// Shared constants for thumbnail size bounds and default dimensions.
    /// Single source of truth for thumbnail layout and defaults.
    /// </summary>
    public static class ThumbnailConstants
    {
        // Size bounds (enforced for resize and ratio calculations)
        public const int MinThumbnailWidth = 192;
        public const int MaxThumbnailWidth = 960;
        public const int MinThumbnailHeight = 108;
        public const int MaxThumbnailHeight = 540;

        // Default position and dimensions
        public const int DefaultThumbnailX = 100;
        public const int DefaultThumbnailY = 100;
        public const int DefaultThumbnailWidth = 400;
        public const int DefaultThumbnailHeight = 300;
        public const double DefaultThumbnailOpacity = 0.75;

        // Default focus border
        public const string DefaultFocusBorderColor = "#0078D4";
        public const int DefaultFocusBorderThickness = 3;
    }
}

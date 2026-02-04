namespace YAEP.Helpers
{
    /// <summary>
    /// Shared logic for window aspect ratio and height-from-ratio calculations.
    /// Single source of truth for ratio-to-number mapping and thumbnail height bounds.
    /// </summary>
    public static class WindowRatioHelper
    {
        /// <summary>
        /// Returns the aspect ratio (width / height) for the given ratio, or 0.0 for None.
        /// </summary>
        public static double GetAspectRatio(WindowRatio ratio)
        {
            return ratio switch
            {
                WindowRatio.Ratio21_9 => 21.0 / 9.0,
                WindowRatio.Ratio21_4 => 21.0 / 4.0,
                WindowRatio.Ratio16_9 => 16.0 / 9.0,
                WindowRatio.Ratio4_3 => 4.0 / 3.0,
                WindowRatio.Ratio1_1 => 1.0,
                _ => 0.0
            };
        }

        /// <summary>
        /// Calculates height from width and aspect ratio, clamped to thumbnail bounds.
        /// </summary>
        /// <param name="width">Width in pixels.</param>
        /// <param name="ratio">Aspect ratio enum.</param>
        /// <param name="fallbackHeight">Returned when ratio is None or invalid.</param>
        /// <param name="minHeight">Minimum height (default from ThumbnailConstants).</param>
        /// <param name="maxHeight">Maximum height (default from ThumbnailConstants).</param>
        public static int CalculateHeightFromRatio(
            int width,
            WindowRatio ratio,
            int fallbackHeight = 0,
            int minHeight = 0,
            int maxHeight = 0)
        {
            if (minHeight == 0)
                minHeight = ThumbnailConstants.MinThumbnailHeight;
            if (maxHeight == 0)
                maxHeight = ThumbnailConstants.MaxThumbnailHeight;
            if (fallbackHeight == 0)
                fallbackHeight = ThumbnailConstants.DefaultThumbnailHeight;

            double aspectRatio = GetAspectRatio(ratio);
            if (aspectRatio == 0.0)
                return fallbackHeight;

            int calculatedHeight = (int)Math.Round(width / aspectRatio);
            return Math.Clamp(calculatedHeight, minHeight, maxHeight);
        }
    }
}

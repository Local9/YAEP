namespace YAEP.Models
{
    /// <summary>
    /// Represents thumbnail configuration settings.
    /// </summary>
    public class ThumbnailConfig
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public double Opacity { get; set; }
        public string FocusBorderColor { get; set; } = ThumbnailConstants.DefaultFocusBorderColor;
        public int FocusBorderThickness { get; set; } = ThumbnailConstants.DefaultFocusBorderThickness;
        public bool ShowTitleOverlay { get; set; } = true;
    }
}


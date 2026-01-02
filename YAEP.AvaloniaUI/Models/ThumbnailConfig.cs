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
        public string FocusBorderColor { get; set; } = "#0078D4";
        public int FocusBorderThickness { get; set; } = 3;
        public bool ShowTitleOverlay { get; set; } = true;
    }
}


namespace YAEP.Models
{
    /// <summary>
    /// Represents a thumbnail setting entry with window title.
    /// </summary>
    public class ThumbnailSetting
    {
        public string WindowTitle { get; set; } = string.Empty;
        public ThumbnailConfig Config { get; set; } = new();
    }
}


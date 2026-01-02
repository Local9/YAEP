namespace YAEP.Models
{
    /// <summary>
    /// Represents overlay window settings for Mumble links.
    /// </summary>
    public class MumbleLinksOverlaySettings
    {
        public bool AlwaysOnTop { get; set; } = true;
        public int X { get; set; } = 100;
        public int Y { get; set; } = 100;
        public int Width { get; set; } = 300;
        public int Height { get; set; } = 400;
    }
}


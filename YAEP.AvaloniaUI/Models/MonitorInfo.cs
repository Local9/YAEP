using Avalonia;
using Avalonia.Platform;

namespace YAEP.Models
{
    public class MonitorInfo
    {
        public Screen Screen { get; set; } = null!;
        public string Name { get; set; } = string.Empty;
        public PixelRect Bounds { get; set; }
        public PixelRect WorkingArea { get; set; }
        public bool IsPrimary { get; set; }

        public override string ToString()
        {
            string primary = IsPrimary ? " (Primary)" : "";
            return $"{Name}{primary} - {Bounds.Width}x{Bounds.Height} @ ({Bounds.X}, {Bounds.Y})";
        }
    }
}


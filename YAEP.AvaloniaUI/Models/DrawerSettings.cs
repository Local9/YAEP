namespace YAEP.Models
{
    public enum DrawerSide
    {
        Left,
        Right
    }

    public class DrawerSettings
    {
        public int ScreenIndex { get; set; } = 0;
        public DrawerSide Side { get; set; } = DrawerSide.Right;
        public int Width { get; set; } = 400;
        public int Height { get; set; } = 600;
        public bool IsVisible { get; set; } = false;
        public bool IsEnabled { get; set; } = false;
    }
}

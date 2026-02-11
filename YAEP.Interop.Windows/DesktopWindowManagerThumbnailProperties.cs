using System.Runtime.InteropServices;

namespace YAEP.Interop.Windows
{
    [StructLayout(LayoutKind.Sequential)]
    public class DesktopWindowManagerThumbnailProperties
    {
        public uint dwFlags;
        public Rect rcDestination;
        public Rect rcSource;
        public byte opacity;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fVisible;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fSourceClientAreaOnly;
    }
}

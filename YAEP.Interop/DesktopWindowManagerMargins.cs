using System.Runtime.InteropServices;

namespace YAEP.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public class DesktopWindowManagerMargins
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;

        public DesktopWindowManagerMargins(int left, int top, int right, int bottom)
        {
            cxLeftWidth = left;
            cyTopHeight = top;
            cxRightWidth = right;
            cyBottomHeight = bottom;
        }
    }
}

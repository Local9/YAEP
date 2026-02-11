using System.Runtime.InteropServices;

namespace YAEP.Interop.Windows
{
    [StructLayout(LayoutKind.Sequential)]
    public struct AnimationInfo
    {
        public uint cbSize;
        public int iMinAnimate;
    }
}

using System.Runtime.InteropServices;

namespace YAEP.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public struct AnimationInfo
    {
        public uint cbSize;
        public int iMinAnimate;
    }
}

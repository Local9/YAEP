using System.Drawing;
using System.Runtime.InteropServices;

namespace YAEP.Interop
{
    public static class DesktopWindowManagerNativeMethods
    {
        /// <summary>
        /// Enables the blur-behind effect on a window.
        /// </summary>
        /// <param name="hWnd"></param>
        /// <param name="pBlurBehind"></param>
        [DllImport("dwmapi.dll", PreserveSig = false)]
        public static extern void DwmEnableBlurBehindWindow(IntPtr hWnd, DesktopWindowManagerBlurBehind pBlurBehind);

        /// <summary>
        /// Extends the window frame into the client area.
        /// </summary>
        /// <param name="hWnd"></param>
        /// <param name="pMargins"></param>
        [DllImport("dwmapi.dll", PreserveSig = false)]
        public static extern void DwmExtendFrameIntoClientArea(IntPtr hWnd, DesktopWindowManagerMargins pMargins);

        /// <summary>
        /// Determines whether Desktop Window Manager (DWM) composition is enabled.
        /// </summary>
        /// <returns></returns>
        [DllImport("dwmapi.dll", PreserveSig = false)]
        public static extern bool DwmIsCompositionEnabled();

        /// <summary>
        /// Retrieves the current color used for DWM glass composition
        /// </summary>
        /// <param name="pcrColorization"></param>
        /// <param name="pfOpaqueBlend"></param>
        [DllImport("dwmapi.dll", PreserveSig = false)]
        public static extern void DwmGetColorizationColor(
            out int pcrColorization,
            [MarshalAs(UnmanagedType.Bool)] out bool pfOpaqueBlend);

        /// <summary>
        /// Enables or disables Desktop Window Manager (DWM) composition.
        /// </summary>
        /// <param name="bEnable"></param>
        [DllImport("dwmapi.dll", PreserveSig = false)]
        public static extern void DwmEnableComposition(bool bEnable);

        /// <summary>
        /// Registers a thumbnail relationship between a destination and source window.
        /// </summary>
        /// <param name="dest"></param>
        /// <param name="source"></param>
        /// <returns></returns>
        [DllImport("dwmapi.dll", PreserveSig = false)]
        public static extern IntPtr DwmRegisterThumbnail(IntPtr dest, IntPtr source);

        /// <summary>
        /// Unregisters a thumbnail relationship.
        /// </summary>
        /// <param name="hThumbnail"></param>
        [DllImport("dwmapi.dll", PreserveSig = false)]
        public static extern void DwmUnregisterThumbnail(IntPtr hThumbnail);

        /// <summary>
        /// Updates the properties of a thumbnail relationship.
        /// </summary>
        /// <param name="hThumbnail"></param>
        /// <param name="props"></param>
        [DllImport("dwmapi.dll", PreserveSig = false)]
        public static extern void DwmUpdateThumbnailProperties(IntPtr hThumbnail, DesktopWindowManagerThumbnailProperties props);

        /// <summary>
        /// Queries the source size of a thumbnail relationship.
        /// </summary>
        /// <param name="hThumbnail"></param>
        /// <param name="size"></param>
        [DllImport("dwmapi.dll", PreserveSig = false)]
        public static extern void DwmQueryThumbnailSourceSize(IntPtr hThumbnail, out Size size);
    }
}

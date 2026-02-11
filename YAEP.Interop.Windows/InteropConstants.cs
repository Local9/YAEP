namespace YAEP.Interop.Windows
{
    public static class InteropConstants
    {
        public const int GWL_STYLE = (-16);

        // Desktop Window Manager (TNP) Constants
        public const uint DWM_TNP_RECTDESTINATION = 0x00000001;
        public const uint DWM_TNP_RECTSOURCE = 0x00000002;
        public const uint DWM_TNP_OPACITY = 0x00000004;
        public const uint DWM_TNP_VISIBLE = 0x00000008;
        public const uint DWM_TNP_SOURCECLIENTAREAONLY = 0x00000010;

        // Window Styles
        public const UInt32 WS_MINIMIZE = 0x20000000;

        // Extended Window Styles
        public const int GWL_EXSTYLE = (-20);
        public const UInt32 WS_EX_TRANSPARENT = 0x00000020;
        public const UInt32 WS_EX_LAYERED = 0x00080000;
        public const UInt32 WS_EX_TOOLWINDOW = 0x00000080;

        // SetWindowPos flags
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public static readonly IntPtr HWND_TOP = new IntPtr(0);
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_SHOWWINDOW = 0x0040;

        // Window Messages
        public const int WM_SYSCOMMAND = 0x0112;

        // ShowWindow Commands
        public const int SW_SHOWNORMAL = 1;
        public const int SW_SHOWMAXIMIZED = 3;
        public const int SW_RESTORE = 9;

        // System Commands
        public const int SC_MINIMIZE = 0xf020;
    }
}

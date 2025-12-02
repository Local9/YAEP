namespace YAEP.Interop
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

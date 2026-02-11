using System.Runtime.InteropServices;

namespace YAEP.Interop.Linux.X11
{
    /// <summary>
    /// P/Invoke declarations for X11 (Xlib) APIs.
    /// </summary>
    public static class X11NativeMethods
    {
        private const string libX11 = "libX11.so.6";

        // Display connection
        [DllImport(libX11)]
        public static extern IntPtr XOpenDisplay(string? display_name);

        [DllImport(libX11)]
        public static extern int XCloseDisplay(IntPtr display);

        [DllImport(libX11)]
        public static extern int XDefaultScreen(IntPtr display);

        [DllImport(libX11)]
        public static extern IntPtr XRootWindow(IntPtr display, int screen_number);

        // Window management
        [DllImport(libX11)]
        public static extern int XRaiseWindow(IntPtr display, uint w);

        [DllImport(libX11)]
        public static extern int XSetInputFocus(IntPtr display, uint w, int revert_to, uint time);

        [DllImport(libX11)]
        public static extern int XMoveResizeWindow(IntPtr display, uint w, int x, int y, uint width, uint height);

        [DllImport(libX11)]
        public static extern int XGetWindowAttributes(IntPtr display, uint w, out XWindowAttributes attributes);

        [DllImport(libX11)]
        public static extern int XGetGeometry(IntPtr display, uint w, out uint root_return, out int x_return, out int y_return, out uint width_return, out uint height_return, out uint border_width_return, out uint depth_return);

        // Window properties (EWMH)
        [DllImport(libX11)]
        public static extern int XInternAtom(IntPtr display, string atom_name, bool only_if_exists);

        [DllImport(libX11)]
        public static extern int XGetWindowProperty(IntPtr display, uint w, int property, long long_offset, long long_length, bool delete, int req_type, out int actual_type_return, out int actual_format_return, out int nitems_return, out int bytes_after_return, out IntPtr prop_return);

        [DllImport(libX11)]
        public static extern int XChangeProperty(IntPtr display, uint w, int property, int type, int format, int mode, byte[] data, int nelements);

        // Keyboard
        [DllImport(libX11)]
        public static extern int XGrabKey(IntPtr display, int keycode, uint modifiers, uint grab_window, bool owner_events, int pointer_mode, int keyboard_mode);

        [DllImport(libX11)]
        public static extern int XUngrabKey(IntPtr display, int keycode, uint modifiers, uint grab_window);

        [DllImport(libX11)]
        public static extern int XKeycodeToKeysym(IntPtr display, int keycode, int index);

        [DllImport(libX11)]
        public static extern int XKeysymToKeycode(IntPtr display, int keysym);

        // Events
        [DllImport(libX11)]
        public static extern int XNextEvent(IntPtr display, out XEvent event_return);

        [DllImport(libX11)]
        public static extern int XPending(IntPtr display);

        [DllImport(libX11)]
        public static extern int XFlush(IntPtr display);

        // Window state
        public const int RevertToNone = 0;
        public const int RevertToPointerRoot = 1;
        public const int RevertToParent = 2;
        public const uint CurrentTime = 0;

        // Modifier masks
        public const uint ShiftMask = 1 << 0;
        public const uint LockMask = 1 << 1;
        public const uint ControlMask = 1 << 2;
        public const uint Mod1Mask = 1 << 3;  // Alt
        public const uint Mod4Mask = 1 << 6;  // Super/Windows key

        // Property modes
        public const int PropModeReplace = 0;
        public const int PropModePrepend = 1;
        public const int PropModeAppend = 2;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XWindowAttributes
    {
        public int x;
        public int y;
        public int width;
        public int height;
        public int border_width;
        public int depth;
        public IntPtr visual;
        public IntPtr root;
        public int @class;
        public int bit_gravity;
        public int win_gravity;
        public int backing_store;
        public ulong backing_planes;
        public ulong backing_pixel;
        public bool save_under;
        public IntPtr colormap;
        public bool map_installed;
        public int map_state;
        public long all_event_masks;
        public long your_event_mask;
        public long do_not_propagate_mask;
        public bool override_redirect;
        public IntPtr screen;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XEvent
    {
        public int type;
        public XKeyEvent key;
        // Other event types would be union members, but we'll use key for now
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XKeyEvent
    {
        public int type;
        public uint serial;
        public bool send_event;
        public IntPtr display;
        public uint window;
        public uint root;
        public uint subwindow;
        public uint time;
        public int x;
        public int y;
        public int x_root;
        public int y_root;
        public uint state;
        public uint keycode;
        public bool same_screen;
    }
}

using System.Runtime.InteropServices;

namespace YAEP.Interop.Linux.Wayland
{
    /// <summary>
    /// P/Invoke declarations for Wayland client APIs.
    /// Note: Wayland uses a protocol-based approach, so many operations require protocol objects.
    /// This is a simplified interface - full implementation would require Wayland protocol bindings.
    /// </summary>
    public static class WaylandNativeMethods
    {
        private const string libWaylandClient = "libwayland-client.so.0";

        // Wayland display connection
        [DllImport(libWaylandClient)]
        public static extern IntPtr wl_display_connect(string? name);

        [DllImport(libWaylandClient)]
        public static extern void wl_display_disconnect(IntPtr display);

        [DllImport(libWaylandClient)]
        public static extern int wl_display_dispatch(IntPtr display);

        [DllImport(libWaylandClient)]
        public static extern int wl_display_flush(IntPtr display);

        [DllImport(libWaylandClient)]
        public static extern int wl_display_roundtrip(IntPtr display);

        // Note: Wayland keyboard shortcuts and window management require protocol-specific implementations
        // which are typically handled through generated protocol bindings rather than direct P/Invoke
    }
}

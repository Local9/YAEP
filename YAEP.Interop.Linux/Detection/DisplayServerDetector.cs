namespace YAEP.Interop.Linux.Detection
{
    /// <summary>
    /// Detects the display server type (Wayland or X11) running on Linux.
    /// </summary>
    public static class DisplayServerDetector
    {
        private static DisplayServerType? _cachedType;

        /// <summary>
        /// Detects the display server type.
        /// Checks WAYLAND_DISPLAY and XDG_SESSION_TYPE environment variables.
        /// Falls back to X11 if detection is ambiguous.
        /// </summary>
        /// <returns>The detected display server type, or X11 as fallback.</returns>
        public static DisplayServerType Detect()
        {
            if (_cachedType.HasValue)
                return _cachedType.Value;

            // Check WAYLAND_DISPLAY environment variable (primary indicator)
            string? waylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
            if (!string.IsNullOrEmpty(waylandDisplay))
            {
                _cachedType = DisplayServerType.Wayland;
                return _cachedType.Value;
            }

            // Check XDG_SESSION_TYPE environment variable
            string? xdgSessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
            if (!string.IsNullOrEmpty(xdgSessionType))
            {
                if (xdgSessionType.Equals("wayland", StringComparison.OrdinalIgnoreCase))
                {
                    _cachedType = DisplayServerType.Wayland;
                    return _cachedType.Value;
                }
                if (xdgSessionType.Equals("x11", StringComparison.OrdinalIgnoreCase))
                {
                    _cachedType = DisplayServerType.X11;
                    return _cachedType.Value;
                }
            }

            // Check DISPLAY environment variable (X11 indicator)
            string? display = Environment.GetEnvironmentVariable("DISPLAY");
            if (!string.IsNullOrEmpty(display))
            {
                // If DISPLAY is set but WAYLAND_DISPLAY is not, prefer X11
                _cachedType = DisplayServerType.X11;
                return _cachedType.Value;
            }

            // Fallback to X11 if detection is ambiguous
            _cachedType = DisplayServerType.X11;
            return _cachedType.Value;
        }

        /// <summary>
        /// Clears the cached detection result.
        /// Useful for testing or if display server changes at runtime.
        /// </summary>
        public static void ClearCache()
        {
            _cachedType = null;
        }
    }
}

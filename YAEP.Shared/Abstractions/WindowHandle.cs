using System.Runtime.InteropServices;

namespace YAEP.Shared.Abstractions
{
    /// <summary>
    /// Platform-agnostic window handle wrapper.
    /// Can represent Windows IntPtr, X11 Window (uint), or Wayland surface (IntPtr).
    /// </summary>
    public struct WindowHandle
    {
        private readonly IntPtr _handle;
        private readonly uint _x11Window;
        private readonly bool _isX11;

        /// <summary>
        /// Creates a Windows window handle (IntPtr).
        /// </summary>
        public WindowHandle(IntPtr handle)
        {
            _handle = handle;
            _x11Window = 0;
            _isX11 = false;
        }

        /// <summary>
        /// Creates an X11 window handle (uint).
        /// </summary>
        public WindowHandle(uint x11Window)
        {
            _handle = IntPtr.Zero;
            _x11Window = x11Window;
            _isX11 = true;
        }

        /// <summary>
        /// Gets the handle as IntPtr (Windows or Wayland).
        /// </summary>
        public IntPtr AsIntPtr => _isX11 ? new IntPtr(_x11Window) : _handle;

        /// <summary>
        /// Gets the handle as X11 Window (uint).
        /// </summary>
        public uint AsX11Window => _isX11 ? _x11Window : (uint)_handle.ToInt64();

        /// <summary>
        /// Gets whether this is an X11 window handle.
        /// </summary>
        public bool IsX11 => _isX11;

        /// <summary>
        /// Gets whether this is a Windows/Wayland handle.
        /// </summary>
        public bool IsIntPtr => !_isX11;

        /// <summary>
        /// Implicit conversion from IntPtr.
        /// </summary>
        public static implicit operator WindowHandle(IntPtr handle) => new WindowHandle(handle);

        /// <summary>
        /// Implicit conversion to IntPtr.
        /// </summary>
        public static implicit operator IntPtr(WindowHandle handle) => handle.AsIntPtr;

        /// <summary>
        /// Implicit conversion from uint (X11 Window).
        /// </summary>
        public static implicit operator WindowHandle(uint x11Window) => new WindowHandle(x11Window);

        /// <summary>
        /// Implicit conversion to uint (X11 Window).
        /// </summary>
        public static implicit operator uint(WindowHandle handle) => handle.AsX11Window;

        public override bool Equals(object? obj)
        {
            if (obj is WindowHandle other)
            {
                if (_isX11 && other._isX11)
                    return _x11Window == other._x11Window;
                return _handle == other._handle;
            }
            return false;
        }

        public override int GetHashCode()
        {
            if (_isX11)
                return _x11Window.GetHashCode();
            return _handle.GetHashCode();
        }

        public static bool operator ==(WindowHandle left, WindowHandle right) => left.Equals(right);
        public static bool operator !=(WindowHandle left, WindowHandle right) => !left.Equals(right);
    }
}

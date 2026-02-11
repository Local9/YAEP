using YAEP.Interop.Windows;
using YAEP.Shared.Interfaces;

namespace YAEP.Interop.Windows.Services
{
    class WindowsDesktopWindowManagerThumbnail : IDesktopWindowManagerThumbnail
    {
        private readonly IDesktopWindowManager _desktopWindowManager;
        private IntPtr _handle;
        private DesktopWindowManagerThumbnailProperties _properties = new();

        public WindowsDesktopWindowManagerThumbnail(IDesktopWindowManager desktopWindowManager)
        {
            this._desktopWindowManager = desktopWindowManager;
            this._handle = IntPtr.Zero;
        }

        public void Register(IntPtr destination, IntPtr source)
        {
            // Initialize properties - rcDestination will be set by Move() call
            _properties.dwFlags = InteropConstants.DWM_TNP_OPACITY
                | InteropConstants.DWM_TNP_VISIBLE
                | InteropConstants.DWM_TNP_SOURCECLIENTAREAONLY;
            _properties.opacity = 255;
            _properties.fVisible = true;
            _properties.fSourceClientAreaOnly = true;
            // Initialize rcSource to empty - DWM will use full source window
            _properties.rcSource = new Rect(0, 0, 0, 0);
            // Initialize rcDestination to empty - will be set by Move() call
            _properties.rcDestination = new Rect(0, 0, 0, 0);

            if (!_desktopWindowManager.IsCompositionEnabled)
                return;

            try
            {
                _handle = DesktopWindowManagerNativeMethods.DwmRegisterThumbnail(destination, source);
                
                // Apply initial properties to make thumbnail visible
                // Note: DWM_TNP_RECTDESTINATION flag will be added when Move() is called
                if (_handle != IntPtr.Zero)
                {
                    DesktopWindowManagerNativeMethods.DwmUpdateThumbnailProperties(_handle, _properties);
                }
            }
            catch (ArgumentException)
            {
                // This exception is raised if the source client is already closed
                // Can happen on a really slow CPU's that the window is still being
                // listed in the process list yet it already cannot be used as
                // a thumbnail source
                _handle = IntPtr.Zero;
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                // This exception is raised if DWM is suddenly not available
                // (f.e. when switching between Windows user accounts)
                _handle = IntPtr.Zero;
            }
        }

        public void Unregister()
        {
            if ((!_desktopWindowManager.IsCompositionEnabled) || (_handle == IntPtr.Zero))
                return;

            try
            {
                DesktopWindowManagerNativeMethods.DwmUnregisterThumbnail(_handle);
            }
            catch (ArgumentException)
            {
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                // This exception is raised when DWM is not available for some reason
            }
        }

        public void Move(int left, int top, int right, int bottom)
        {
            _properties.rcDestination = new(left, top, right, bottom);
            // Ensure RECTDESTINATION flag is set when destination rectangle is specified
            _properties.dwFlags |= InteropConstants.DWM_TNP_RECTDESTINATION;
        }

        public void SetOpacity(double opacity)
        {
            opacity = Math.Max(0.0, Math.Min(1.0, opacity));
            _properties.opacity = (byte)(opacity * 255);
            _properties.dwFlags |= InteropConstants.DWM_TNP_OPACITY;
        }

        public void Update()
        {
            if ((!_desktopWindowManager.IsCompositionEnabled) || (_handle == IntPtr.Zero))
                return;

            try
            {
                DesktopWindowManagerNativeMethods.DwmUpdateThumbnailProperties(_handle, _properties);
            }
            catch (ArgumentException)
            {
                // This exception will be thrown if the EVE client disappears while this method is running
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                // This exception is raised when DWM is not available for some reason
            }
        }
    }
}

using System.Runtime.InteropServices;
using YAEP.Interop;

namespace YAEP.Models
{
    public class Thumbnail
    {
        private DesktopWindowManagerThumbnailProperties _props;
        private IntPtr _handle;

        private bool _isCompositionEnabled => DesktopWindowManagerNativeMethods.DwmIsCompositionEnabled();

        public Thumbnail()
        {
            _props = new DesktopWindowManagerThumbnailProperties();
            _handle = IntPtr.Zero;
        }

        public void Register(IntPtr destination, IntPtr source)
        {
            if (destination == IntPtr.Zero)
            {
                System.Diagnostics.Debug.WriteLine("Thumbnail.Register: Destination handle is IntPtr.Zero");
                return;
            }

            if (source == IntPtr.Zero)
            {
                System.Diagnostics.Debug.WriteLine("Thumbnail.Register: Source handle is IntPtr.Zero");
                return;
            }

            _props = new DesktopWindowManagerThumbnailProperties
            {
                dwFlags = InteropConstants.DWM_TNP_VISIBLE
                      | InteropConstants.DWM_TNP_OPACITY
                      | InteropConstants.DWM_TNP_RECTDESTINATION
                      | InteropConstants.DWM_TNP_SOURCECLIENTAREAONLY,
                opacity = 255,
                fVisible = true,
                fSourceClientAreaOnly = true
            };

            if (!_isCompositionEnabled)
            {
                System.Diagnostics.Debug.WriteLine("Thumbnail.Register: DWM composition is not enabled");
                return;
            }

            try
            {
                _handle = DesktopWindowManagerNativeMethods.DwmRegisterThumbnail(destination, source);
                if (_handle == IntPtr.Zero)
                {
                    System.Diagnostics.Debug.WriteLine("Thumbnail.Register: DwmRegisterThumbnail returned IntPtr.Zero");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Thumbnail.Register: Successfully registered thumbnail. Handle: {_handle}");
                }
            }
            catch (ArgumentException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Thumbnail.Register: ArgumentException - {ex.Message}");
                _handle = IntPtr.Zero;
            }
            catch (COMException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Thumbnail.Register: COMException - {ex.Message} (HRESULT: 0x{ex.ErrorCode:X8})");
                _handle = IntPtr.Zero;
            }
        }

        public void Unregister()
        {
            if (!_isCompositionEnabled || _handle == IntPtr.Zero)
                return;
            try
            {
                DesktopWindowManagerNativeMethods.DwmUnregisterThumbnail(_handle);
            }
            catch (ArgumentException)
            {
            }
            catch (COMException)
            {
            }
        }

        public void Move(int x, int y, int width, int height)
        {
            if (_handle == IntPtr.Zero)
                return;

            _props.rcDestination = new YAEP.Interop.Rect(x, y, x + width, y + height);
        }

        public void SetOpacity(double opacity)
        {
            opacity = Math.Max(0.0, Math.Min(1.0, opacity));
            _props.opacity = (byte)(opacity * 255);
            _props.dwFlags |= InteropConstants.DWM_TNP_OPACITY;
        }

        public void Update()
        {
            if (!_isCompositionEnabled || _handle == IntPtr.Zero)
                return;

            try
            {
                DesktopWindowManagerNativeMethods.DwmUpdateThumbnailProperties(_handle, _props);
            }
            catch (ArgumentException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Thumbnail.Update: ArgumentException - {ex.Message}");
            }
            catch (COMException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Thumbnail.Update: COMException - {ex.Message} (HRESULT: 0x{ex.ErrorCode:X8})");
            }
        }
    }
}

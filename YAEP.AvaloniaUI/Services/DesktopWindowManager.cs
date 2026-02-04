using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using YAEP.Interop;

namespace YAEP.Services
{
    public class DesktopWindowManager : IDesktopWindowManager
    {
        private const int WINDOW_SIZE_THRESHOLD = 300;
        private const int NO_ANIMATION = 0;

        public bool IsCompositionEnabled { get; }

        private AnimationInfo _animationParam = new();
        private int? _currentAnimationSetting = null;

        public DesktopWindowManager()
        {
            IsCompositionEnabled =
                ((Environment.OSVersion.Version.Major == 6) && (Environment.OSVersion.Version.Minor >= 2)) // Win 8 and Win 8.1
                || (Environment.OSVersion.Version.Major >= 10) // Win 10
                || DesktopWindowManagerNativeMethods.DwmIsCompositionEnabled(); // In case of Win 7 an API call is requiredWin 7
        }

        public IntPtr GetForegroundWindowHandle()
        {
            return User32NativeMethods.GetForegroundWindow();
        }

        public void ActivateWindow(IntPtr handle, AnimationStyle animation)
        {
            User32NativeMethods.SetForegroundWindow(handle);
            User32NativeMethods.SetFocus(handle);

            int style = User32NativeMethods.GetWindowLong(handle, InteropConstants.GWL_STYLE);

            if ((style & InteropConstants.WS_MINIMIZE) == InteropConstants.WS_MINIMIZE)
            {
                if (animation == AnimationStyle.OriginalAnimation)
                {
                    User32NativeMethods.ShowWindowAsync(new HandleRef(null, handle), InteropConstants.SW_RESTORE);
                }
                else
                {
                    TurnOffAnimation();
                    User32NativeMethods.ShowWindowAsync(new HandleRef(null, handle), InteropConstants.SW_SHOWNORMAL);
                    RestoreAnimation();
                }
            }
        }

        void TurnOffAnimation()
        {
            long currentAnimationSetup = User32NativeMethods.SystemParametersInfo(User32NativeMethods.SPI_GETANIMATION, (System.Int32)Marshal.SizeOf(typeof(AnimationInfo)), ref _animationParam, 0);

            if (_currentAnimationSetting == null)
                _currentAnimationSetting = _animationParam.iMinAnimate;

            if (_currentAnimationSetting != NO_ANIMATION)
            {
                _animationParam.iMinAnimate = NO_ANIMATION;
                User32NativeMethods.SystemParametersInfo(User32NativeMethods.SPI_SETANIMATION, (System.Int32)Marshal.SizeOf(typeof(AnimationInfo)), ref _animationParam, 0);
            }
        }

        void RestoreAnimation()
        {
            long currentAnimationSetup = User32NativeMethods.SystemParametersInfo(User32NativeMethods.SPI_GETANIMATION, (System.Int32)Marshal.SizeOf(typeof(AnimationInfo)), ref _animationParam, 0);

            if (_currentAnimationSetting == null)
                _currentAnimationSetting = _animationParam.iMinAnimate;

            if (_animationParam.iMinAnimate != (int)_currentAnimationSetting)
            {
                _animationParam.iMinAnimate = (int)_currentAnimationSetting;
                User32NativeMethods.SystemParametersInfo(User32NativeMethods.SPI_SETANIMATION, (System.Int32)Marshal.SizeOf(typeof(AnimationInfo)), ref _animationParam, 0);
            }
        }

        public void MinimizeWindow(IntPtr handle, AnimationStyle animation, bool enableAnimation)
        {
            if (animation == AnimationStyle.OriginalAnimation)
            {
                if (enableAnimation)
                    User32NativeMethods.SendMessage(handle, InteropConstants.WM_SYSCOMMAND, InteropConstants.SC_MINIMIZE, 0);
                else
                {
                    WindowPlacement placement = new();
                    placement.length = Marshal.SizeOf(typeof(WindowPlacement));
                    User32NativeMethods.GetWindowPlacement(handle, ref placement);
                    placement.showCmd = WindowPlacement.SW_MINIMIZE;
                    User32NativeMethods.SetWindowPlacement(handle, ref placement);
                }
            }
            else
            {
                TurnOffAnimation();
                User32NativeMethods.SendMessage(handle, InteropConstants.WM_SYSCOMMAND, InteropConstants.SC_MINIMIZE, 0);
                RestoreAnimation();
            }
        }
        public void MoveWindow(IntPtr handle, double left, double top, double width, double height) =>
            User32NativeMethods.MoveWindow(handle, (int)left, (int)top, (int)width, (int)height, true);

        public void MaximizeWindow(IntPtr handle) =>
            User32NativeMethods.ShowWindowAsync(new HandleRef(null, handle), InteropConstants.SW_SHOWMAXIMIZED);

        public (double Left, double Top, double Right, double Bottom) GetWindowPosition(IntPtr handle)
        {
            User32NativeMethods.GetWindowRect(handle, out YAEP.Interop.Rect windowRectangle);
            return (windowRectangle.Left, windowRectangle.Top, windowRectangle.Right, windowRectangle.Bottom);
        }

        public bool IsWindowMaximized(IntPtr handle) =>
            User32NativeMethods.IsZoomed(handle);

        public bool IsWindowMinimized(IntPtr handle) =>
            User32NativeMethods.IsIconic(handle);

        public IDesktopWindowManagerThumbnail GetLiveThumbnail(IntPtr destination, IntPtr source)
        {
            IDesktopWindowManagerThumbnail thumbnail = new DesktopWindowManagerThumbnail(this);
            thumbnail.Register(destination, source);

            return thumbnail;
        }

        [SupportedOSPlatform("windows")]
        public Image? GetStaticThumbnail(IntPtr source)
        {
            nint sourceContext = User32NativeMethods.GetDC(source);

            User32NativeMethods.GetClientRect(source, out YAEP.Interop.Rect windowRect);

            double width = windowRect.Right - windowRect.Left;
            double height = windowRect.Bottom - windowRect.Top;

            // Check if there is anything to make thumbnail of
            if ((width < WINDOW_SIZE_THRESHOLD) || (height < WINDOW_SIZE_THRESHOLD))
            {
                return null;
            }

            nint destContext = Gdi32NativeMethods.CreateCompatibleDC(sourceContext);
            nint bitmap = Gdi32NativeMethods.CreateCompatibleBitmap(sourceContext, (int)width, (int)height);

            nint oldBitmap = Gdi32NativeMethods.SelectObject(destContext, bitmap);
            Gdi32NativeMethods.BitBlt(destContext, 0, 0, (int)width, (int)height, sourceContext, 0, 0, Gdi32NativeMethods.SRCCOPY);
            Gdi32NativeMethods.SelectObject(destContext, oldBitmap);
            Gdi32NativeMethods.DeleteDC(destContext);
            User32NativeMethods.ReleaseDC(source, sourceContext);

            Image image = Image.FromHbitmap(bitmap);
            Gdi32NativeMethods.DeleteObject(bitmap);

            return image;
        }
    }
}

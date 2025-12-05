using System;
using System.Drawing;

namespace YAEP.Interface
{
    public interface IDesktopWindowManager
    {
        bool IsCompositionEnabled { get; }
        IntPtr GetForegroundWindowHandle();
        void ActivateWindow(IntPtr handle, AnimationStyle animation);
        // TODO: Linux
        // void ActivateWindow(IntPtr handle, string windowName);
        void MinimizeWindow(IntPtr handle, AnimationStyle animation, bool enableAnimation);
        void MoveWindow(IntPtr handle, double left, double top, double width, double height);
        void MaximizeWindow(IntPtr handle);
        (double Left, double Top, double Right, double Bottom) GetWindowPosition(IntPtr handle);
        bool IsWindowMaximized(IntPtr handle);
        bool IsWindowMinimized(IntPtr handle);
        IDesktopWindowManagerThumbnail GetLiveThumbnail(IntPtr destination, IntPtr source);
        Image? GetStaticThumbnail(IntPtr source);
    }
}

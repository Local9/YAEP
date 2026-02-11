using System;
using System.Drawing;
using YAEP.Shared.Enumerations;

namespace YAEP.Shared.Interfaces
{
    public interface IDesktopWindowManager
    {
        bool IsCompositionEnabled { get; }
        IntPtr GetForegroundWindowHandle();
        void ActivateWindow(IntPtr handle, AnimationStyle animation);
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

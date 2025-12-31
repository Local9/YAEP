using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform;
using YAEP.Interop;
using YAEP.ViewModels.Windows;

namespace YAEP.Views.Windows
{
    /// <summary>
    /// Window that displays the overlay (border and title) on top of the thumbnail window.
    /// </summary>
    public partial class ThumbnailOverlayWindow : Window
    {
        public ThumbnailWindowViewModel ViewModel { get; }
        private IntPtr? _thumbnailWindowHandle;

        public ThumbnailOverlayWindow()
        {
            ViewModel = new ThumbnailWindowViewModel(string.Empty);
            DataContext = this;
            InitializeComponent();
            ThumbnailOverlayControl.DataContext = ViewModel;
        }

        public ThumbnailOverlayWindow(ThumbnailWindowViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;
            InitializeComponent();
            ThumbnailOverlayControl.DataContext = ViewModel;

            this.Opened += ThumbnailOverlayWindow_Opened;
        }

        /// <summary>
        /// Sets the thumbnail window handle so the overlay can position itself above it.
        /// </summary>
        public void SetThumbnailWindowHandle(IntPtr handle)
        {
            _thumbnailWindowHandle = handle;
        }

        private void ThumbnailOverlayWindow_Opened(object? sender, EventArgs e)
        {
            MakeWindowClickThrough();
            BringToTop();
        }

        /// <summary>
        /// Makes the window click-through using Windows API extended window styles.
        /// </summary>
        private void MakeWindowClickThrough()
        {
            try
            {
                IPlatformHandle? platformHandle = this.TryGetPlatformHandle();
                if (platformHandle != null && platformHandle.Handle != IntPtr.Zero)
                {
                    int exStyle = User32NativeMethods.GetWindowLong(platformHandle.Handle, InteropConstants.GWL_EXSTYLE);
                    exStyle |= (int)(InteropConstants.WS_EX_LAYERED | InteropConstants.WS_EX_TRANSPARENT);
                    User32NativeMethods.SetWindowLong(platformHandle.Handle, InteropConstants.GWL_EXSTYLE, exStyle);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to make overlay window click-through: {ex.Message}");
            }
        }

        /// <summary>
        /// Brings the overlay window to the top using Windows API.
        /// </summary>
        public void BringToTop()
        {
            try
            {
                IPlatformHandle? platformHandle = this.TryGetPlatformHandle();
                if (platformHandle != null && platformHandle.Handle != IntPtr.Zero)
                {
                    IntPtr insertAfter = InteropConstants.HWND_TOPMOST;

                    if (_thumbnailWindowHandle.HasValue && _thumbnailWindowHandle.Value != IntPtr.Zero)
                    {
                        User32NativeMethods.SetWindowPos(
                            platformHandle.Handle,
                            _thumbnailWindowHandle.Value,
                            0, 0, 0, 0,
                            InteropConstants.SWP_NOMOVE | InteropConstants.SWP_NOSIZE | InteropConstants.SWP_NOACTIVATE | InteropConstants.SWP_SHOWWINDOW);
                    }

                    User32NativeMethods.SetWindowPos(
                        platformHandle.Handle,
                        InteropConstants.HWND_TOPMOST,
                        0, 0, 0, 0,
                        InteropConstants.SWP_NOMOVE | InteropConstants.SWP_NOSIZE | InteropConstants.SWP_NOACTIVATE | InteropConstants.SWP_SHOWWINDOW);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to bring overlay window to top: {ex.Message}");
            }
        }

        /// <summary>
        /// Synchronizes the position and size of this overlay window with the thumbnail window.
        /// </summary>
        public void SyncWithThumbnailWindow(Avalonia.PixelPoint position, double width, double height)
        {
            this.Position = position;
            this.Width = width;
            this.Height = height;
            BringToTop();
        }

        /// <summary>
        /// Handles pointer pressed events - events should pass through due to click-through window style.
        /// </summary>
        private void Window_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            e.Handled = false;
        }

        /// <summary>
        /// Handles pointer moved events - events should pass through due to click-through window style.
        /// </summary>
        private void Window_PointerMoved(object? sender, PointerEventArgs e)
        {
            e.Handled = false;
        }

        /// <summary>
        /// Handles pointer released events - events should pass through due to click-through window style.
        /// </summary>
        private void Window_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            e.Handled = false;
        }

        /// <summary>
        /// Handles pointer entered events - events should pass through due to click-through window style.
        /// </summary>
        private void Window_PointerEntered(object? sender, PointerEventArgs e)
        {
            e.Handled = false;
        }

        /// <summary>
        /// Handles pointer exited events - events should pass through due to click-through window style.
        /// </summary>
        private void Window_PointerExited(object? sender, PointerEventArgs e)
        {
            e.Handled = false;
        }
    }
}


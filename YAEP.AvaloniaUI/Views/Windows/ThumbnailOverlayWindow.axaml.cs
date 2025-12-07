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

        public ThumbnailOverlayWindow(ThumbnailWindowViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;
            InitializeComponent();

            // Make the window click-through after it's loaded
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
            // Make the window click-through using Windows API
            MakeWindowClickThrough();

            // Bring the window to the top
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
                    // Get current extended window style
                    int exStyle = User32NativeMethods.GetWindowLong(platformHandle.Handle, InteropConstants.GWL_EXSTYLE);

                    // Add WS_EX_LAYERED and WS_EX_TRANSPARENT styles
                    exStyle |= (int)(InteropConstants.WS_EX_LAYERED | InteropConstants.WS_EX_TRANSPARENT);

                    // Set the new extended window style
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
                    
                    // If we have the thumbnail window handle, position above it
                    // Otherwise use HWND_TOPMOST
                    if (_thumbnailWindowHandle.HasValue && _thumbnailWindowHandle.Value != IntPtr.Zero)
                    {
                        // Position the overlay window above the thumbnail window
                        // We'll use HWND_TOP to place it above the thumbnail, then make it topmost
                        User32NativeMethods.SetWindowPos(
                            platformHandle.Handle,
                            _thumbnailWindowHandle.Value,
                            0, 0, 0, 0,
                            InteropConstants.SWP_NOMOVE | InteropConstants.SWP_NOSIZE | InteropConstants.SWP_NOACTIVATE | InteropConstants.SWP_SHOWWINDOW);
                    }
                    
                    // Always ensure it's topmost
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

            // Ensure the overlay window stays on top after position/size changes
            BringToTop();
        }

        /// <summary>
        /// Handles pointer pressed events - events should pass through due to click-through window style.
        /// </summary>
        private void Window_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            // Events should pass through automatically with WS_EX_TRANSPARENT
            e.Handled = false;
        }

        /// <summary>
        /// Handles pointer moved events - events should pass through due to click-through window style.
        /// </summary>
        private void Window_PointerMoved(object? sender, PointerEventArgs e)
        {
            // Events should pass through automatically with WS_EX_TRANSPARENT
            e.Handled = false;
        }

        /// <summary>
        /// Handles pointer released events - events should pass through due to click-through window style.
        /// </summary>
        private void Window_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            // Events should pass through automatically with WS_EX_TRANSPARENT
            e.Handled = false;
        }

        /// <summary>
        /// Handles pointer entered events - events should pass through due to click-through window style.
        /// </summary>
        private void Window_PointerEntered(object? sender, PointerEventArgs e)
        {
            // Events should pass through automatically with WS_EX_TRANSPARENT
            e.Handled = false;
        }

        /// <summary>
        /// Handles pointer exited events - events should pass through due to click-through window style.
        /// </summary>
        private void Window_PointerExited(object? sender, PointerEventArgs e)
        {
            // Events should pass through automatically with WS_EX_TRANSPARENT
            e.Handled = false;
        }
    }
}


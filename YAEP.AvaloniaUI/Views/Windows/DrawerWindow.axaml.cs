using Avalonia.Controls;
using Avalonia.Platform;
using SukiUI.Controls;
using YAEP.Interop.Windows;
using YAEP.Models;
using YAEP.ViewModels.Windows;

namespace YAEP.Views.Windows
{
    public partial class DrawerWindow : SukiWindow
    {
        private DrawerWindowViewModel? _viewModel;
        private bool _isAnimating = false;

        public DrawerWindowViewModel? ViewModel => _viewModel;

        public DrawerWindow()
        {
            InitializeComponent();

            _viewModel = new DrawerWindowViewModel();
            DataContext = this;
        }

        public DrawerWindow(DrawerWindowViewModel viewModel)
        {
            _viewModel = viewModel;
            DataContext = this;
            InitializeComponent();

            this.Width = 0;

            this.Opened += DrawerWindow_Opened;
            this.Closing += DrawerWindow_Closing;
        }

        private void DrawerWindow_Opened(object? sender, EventArgs e)
        {
            HideFromAltTab();

            this.Width = 0;
            if (_viewModel != null)
            {
                _viewModel.IsOpen = false;
                _viewModel.Opacity = 0;
                UpdatePositionForWidth(0);
            }
        }

        private void DrawerWindow_Closing(object? sender, WindowClosingEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        /// <summary>
        /// Handles pointer exit from the drawer window - slides it out.
        /// Does not slide out when the server group ComboBox dropdown is open, so the user can select an item.
        /// </summary>
        private void DrawerWindow_PointerExited(object? sender, Avalonia.Input.PointerEventArgs e)
        {
            if (_viewModel == null || !_viewModel.IsOpen || _isAnimating)
                return;

            ComboBox? combo = this.FindControl<ComboBox>("ServerGroupComboBox");
            if (combo?.IsDropDownOpen == true)
                return;

            SlideOut();
        }

        /// <summary>
        /// Handles mumble link button click to open the link.
        /// </summary>
        private void MumbleLinkButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender is Avalonia.Controls.Button button && button.DataContext is MumbleLink link)
            {
                link.OpenLink();
            }
        }

        /// <summary>
        /// Hides the window from Alt+Tab switching using Windows API extended window styles.
        /// </summary>
        private void HideFromAltTab()
        {
            try
            {
                IPlatformHandle? platformHandle = this.TryGetPlatformHandle();
                if (platformHandle != null && platformHandle.Handle != IntPtr.Zero)
                {
                    int exStyle = User32NativeMethods.GetWindowLong(platformHandle.Handle, InteropConstants.GWL_EXSTYLE);
                    exStyle |= (int)InteropConstants.WS_EX_TOOLWINDOW;
                    User32NativeMethods.SetWindowLong(platformHandle.Handle, InteropConstants.GWL_EXSTYLE, exStyle);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to hide drawer window from Alt+Tab: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the window position based on screen and side settings.
        /// Uses current window width for positioning.
        /// </summary>
        public void UpdatePosition()
        {
            if (_viewModel == null)
                return;

            UpdatePositionForWidth(this.Width);
        }

        /// <summary>
        /// Calculates the window position based on screen bounds and side.
        /// For width-based animation, the window is always positioned at the edge.
        /// </summary>
        private Avalonia.PixelPoint CalculatePosition(Avalonia.PixelRect workingArea, DrawerSide side, double width, double height)
        {
            return side switch
            {
                DrawerSide.Left => new Avalonia.PixelPoint(
                    workingArea.X,
                    workingArea.Y + (workingArea.Height - (int)height) / 2),
                DrawerSide.Right => new Avalonia.PixelPoint(
                    workingArea.X + workingArea.Width - (int)width,
                    workingArea.Y + (workingArea.Height - (int)height) / 2),
                _ => new Avalonia.PixelPoint(workingArea.X, workingArea.Y)
            };
        }

        /// <summary>
        /// Slides the drawer window in (makes it visible) by expanding width.
        /// </summary>
        public void SlideIn()
        {
            if (_isAnimating || _viewModel == null)
                return;

            _isAnimating = true;
            _viewModel.IsOpen = true;

            if (!this.IsVisible)
            {
                this.Show();
            }

            this.Width = 0;
            UpdatePositionForWidth(0);

            if (_viewModel != null)
            {
                _viewModel.Opacity = 0;
            }

            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    if (_viewModel == null)
                    {
                        _isAnimating = false;
                        return;
                    }

                    if (_viewModel.Width <= 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"Warning: Drawer target width is {_viewModel.Width}, cannot animate");
                        _isAnimating = false;
                        return;
                    }

                    UpdatePositionForWidth(0);
                    _viewModel.Opacity = 1;

                    AnimateWidth(0, _viewModel.Width, () =>
                    {
                        _isAnimating = false;
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error sliding in drawer: {ex.Message}");
                    _isAnimating = false;
                }
            }, Avalonia.Threading.DispatcherPriority.Normal);
        }

        /// <summary>
        /// Slides the drawer window out (hides it) by collapsing width.
        /// </summary>
        public void SlideOut()
        {
            if (_isAnimating || _viewModel == null)
                return;

            _isAnimating = true;

            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    double currentWidth = this.Width;

                    AnimateWidth(currentWidth, 0, () =>
                    {
                        if (_viewModel != null)
                        {
                            _viewModel.IsOpen = false;
                            _viewModel.Opacity = 0;
                        }
                        _isAnimating = false;
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error sliding out drawer: {ex.Message}");
                    if (_viewModel != null)
                    {
                        _viewModel.IsOpen = false;
                        _viewModel.Opacity = 0;
                    }
                    this.Width = 0;
                    _isAnimating = false;
                }
            }, Avalonia.Threading.DispatcherPriority.Normal);
        }

        /// <summary>
        /// Animates the window width from start to target width.
        /// </summary>
        private void AnimateWidth(double startWidth, double targetWidth, Action? onComplete = null)
        {
            double deltaWidth = targetWidth - startWidth;

            if (Math.Abs(deltaWidth) < 1)
            {
                onComplete?.Invoke();
                _isAnimating = false;
                return;
            }

            const int DrawerAnimationSteps = 30;
            const int DrawerAnimationDelayMs = 10;
            int currentStep = 0;

            System.Timers.Timer? animationTimer = new System.Timers.Timer(DrawerAnimationDelayMs);
            animationTimer.Elapsed += (s, e) =>
            {
                currentStep++;
                double progress = (double)currentStep / DrawerAnimationSteps;
                progress = EaseOutCubic(progress);

                double newWidth = startWidth + deltaWidth * progress;
                double newOpacity = newWidth > 0 ? 1.0 : 0.0;

                Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        this.Width = newWidth;
                        if (_viewModel != null)
                        {
                            _viewModel.Opacity = newOpacity;
                            UpdatePositionForWidth(newWidth);
                        }
                    }
                    catch { }
                });

                if (currentStep >= DrawerAnimationSteps)
                {
                    animationTimer.Stop();
                    animationTimer.Dispose();
                    Dispatcher.UIThread.Post(() =>
                    {
                        this.Width = targetWidth;

                        if (_viewModel != null)
                        {
                            _viewModel.Opacity = targetWidth > 0 ? 1.0 : 0.0;
                            UpdatePositionForWidth(targetWidth);
                        }
                        _isAnimating = false;
                        onComplete?.Invoke();
                    });
                }
            };
            animationTimer.AutoReset = true;
            animationTimer.Start();
        }

        /// <summary>
        /// Updates the window position based on current width.
        /// For left side: position stays at left edge (X = workingArea.X)
        /// For right side: position moves left as width increases (X = workingArea.X + workingArea.Width - width)
        /// </summary>
        private void UpdatePositionForWidth(double width)
        {
            if (_viewModel == null)
                return;

            try
            {
                Screens? screens = this.Screens;
                if (screens == null || screens.All.Count == 0)
                    return;

                Screen? targetScreen = MonitorService.FindScreenBySettings(
                    _viewModel.HardwareId,
                    _viewModel.ScreenIndex,
                    screens);

                if (targetScreen == null)
                    return;

                Avalonia.PixelRect workingArea = targetScreen.WorkingArea;
                int x, y;

                if (_viewModel.Side == DrawerSide.Left)
                {
                    x = workingArea.X;
                }
                else
                {
                    x = workingArea.X + workingArea.Width - (int)width;
                }

                y = workingArea.Y + (workingArea.Height - (int)_viewModel.Height) / 2;

                this.Position = new Avalonia.PixelPoint(x, y);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating drawer position for width: {ex.Message}");
            }
        }

        /// <summary>
        /// Cubic ease-out function for smooth animation.
        /// </summary>
        private double EaseOutCubic(double t)
        {
            return 1 - Math.Pow(1 - t, 3);
        }

        /// <summary>
        /// Toggles the drawer visibility.
        /// </summary>
        public void Toggle()
        {
            if (_viewModel == null)
                return;

            if (_viewModel.IsOpen)
            {
                SlideOut();
            }
            else
            {
                SlideIn();
            }
        }

        /// <summary>
        /// Updates the drawer settings and repositions if needed.
        /// </summary>
        public void UpdateSettings(DrawerSettings settings)
        {
            if (_viewModel == null)
                return;

            int actualHeight = GetMonitorHeight(settings.HardwareId, settings.ScreenIndex);
            if (actualHeight > 0)
            {
                settings.Height = actualHeight;
            }

            _viewModel.ScreenIndex = settings.ScreenIndex;
            _viewModel.HardwareId = settings.HardwareId ?? string.Empty;
            _viewModel.Side = settings.Side;
            _viewModel.Width = settings.Width;
            _viewModel.Height = settings.Height;

            this.Height = settings.Height;

            if (_viewModel.IsOpen)
            {
                double currentWidth = this.Width;
                if (Math.Abs(currentWidth - settings.Width) > 1)
                {
                    AnimateWidth(currentWidth, settings.Width);
                }
                else
                {
                    UpdatePositionForWidth(this.Width);
                }
            }
            else
            {
                this.Width = 0;
                if (_viewModel != null)
                {
                    _viewModel.Opacity = 0;
                }
                UpdatePositionForWidth(0);
            }
        }

        /// <summary>
        /// Gets the height of the specified monitor.
        /// </summary>
        private int GetMonitorHeight(string hardwareId, int screenIndex)
        {
            try
            {
                Screens? screens = this.Screens;
                if (screens == null || screens.All.Count == 0)
                    return 0;

                Screen? targetScreen = MonitorService.FindScreenBySettings(hardwareId, screenIndex, screens);
                return targetScreen?.WorkingArea.Height ?? 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting monitor height: {ex.Message}");
                return 0;
            }
        }
    }
}

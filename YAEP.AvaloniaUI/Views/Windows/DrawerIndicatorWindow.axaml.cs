using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform;
using YAEP.Interop;
using YAEP.Models;

namespace YAEP.Views.Windows
{
    public partial class DrawerIndicatorWindow : Window
    {
        private double _indicatorWidth = 8;
        private double _indicatorHeight = 100;
        private DrawerSide _side = DrawerSide.Right;
        private int _screenIndex = 0;
        private System.Timers.Timer? _hoverTimer;
        private bool _isHovered = false;
        public event EventHandler? Hovered;

        public double IndicatorWidth
        {
            get => _indicatorWidth;
            set
            {
                _indicatorWidth = value;
                this.Width = value;
            }
        }

        public double IndicatorHeight
        {
            get => _indicatorHeight;
            set
            {
                _indicatorHeight = value;
                this.Height = value;
            }
        }

        public DrawerIndicatorWindow()
        {
            InitializeComponent();
            this.Opened += DrawerIndicatorWindow_Opened;
            this.Closing += DrawerIndicatorWindow_Closing;
        }

        private void DrawerIndicatorWindow_Opened(object? sender, EventArgs e)
        {
            HideFromAltTab();
            UpdatePosition();
        }

        private void DrawerIndicatorWindow_Closing(object? sender, WindowClosingEventArgs e)
        {
            e.Cancel = true;
            Hide();
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
                System.Diagnostics.Debug.WriteLine($"Failed to hide drawer indicator from Alt+Tab: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the indicator position based on screen and side settings.
        /// </summary>
        public void UpdatePosition(DrawerSide side, int screenIndex, double drawerWidth, double drawerHeight)
        {
            _side = side;
            _screenIndex = screenIndex;

            IndicatorWidth = 8;
            IndicatorHeight = drawerHeight;

            UpdatePosition();
        }

        private void UpdatePosition()
        {
            try
            {
                Screens? screens = this.Screens;
                if (screens == null || screens.All.Count == 0)
                    return;

                Screen? targetScreen = null;
                if (_screenIndex >= 0 && _screenIndex < screens.All.Count)
                {
                    targetScreen = screens.All[_screenIndex];
                }
                else
                {
                    targetScreen = screens.Primary ?? screens.All[0];
                }

                if (targetScreen == null)
                    return;

                Avalonia.PixelRect workingArea = targetScreen.WorkingArea;
                Avalonia.PixelPoint position = CalculatePosition(workingArea, _side, IndicatorWidth, IndicatorHeight);

                this.Position = position;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating drawer indicator position: {ex.Message}");
            }
        }

        /// <summary>
        /// Calculates the indicator position based on screen bounds and side.
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

        private void Border_PointerEntered(object? sender, PointerEventArgs e)
        {
            if (_isHovered)
                return;

            _isHovered = true;

            _hoverTimer?.Stop();
            _hoverTimer?.Dispose();
            _hoverTimer = new System.Timers.Timer(300);
            _hoverTimer.Elapsed += (s, args) =>
            {
                _hoverTimer.Stop();
                _hoverTimer.Dispose();
                _hoverTimer = null;
                Dispatcher.UIThread.Post(() =>
                {
                    Hovered?.Invoke(this, EventArgs.Empty);
                });
            };
            _hoverTimer.AutoReset = false;
            _hoverTimer.Start();

            if (sender is Border border)
            {
                border.Opacity = 0.7;
            }
        }

        private void Border_PointerExited(object? sender, PointerEventArgs e)
        {
            _isHovered = false;

            _hoverTimer?.Stop();
            _hoverTimer?.Dispose();
            _hoverTimer = null;

            if (sender is Border border)
            {
                border.Opacity = 0.3;
            }
        }
    }
}

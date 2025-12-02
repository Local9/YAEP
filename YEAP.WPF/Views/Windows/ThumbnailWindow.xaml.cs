using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Wpf.Ui.Appearance;
using YAEP.Interop;
using YAEP.Services;
using YAEP.ViewModels.Windows;

namespace YAEP.Views.Windows
{
    /// <summary>
    /// Interaction logic for ThumbnailWindow.xaml
    /// </summary>
    public partial class ThumbnailWindow : Window
    {
        private const int WINDOW_POSITION_THRESHOLD_LOW = -10_000;
        private const int WINDOW_POSITION_THRESHOLD_HIGH = 31_000;
        private const int WINDOW_SIZE_THRESHOLD = 10;

        private readonly DatabaseService _databaseService;
        private readonly string _windowTitle;
        private long _profileId;
        private volatile bool _isDragging = false;
        private System.Timers.Timer? _dragEndTimer;
        private System.Timers.Timer? _focusCheckTimer;
        private bool _isDraggingEnabled = true;
        private bool _focusCheckPaused = false;
        private Point _dragStartMousePosition;
        private Point _dragStartWindowPosition;
        private bool _isRightMouseButtonDown = false;
        private bool _isUpdatingProgrammatically = false;
        private string _focusBorderColor = "#0078D4";
        private int _focusBorderThickness = 3;

        public ThumbnailWindowViewModel ViewModel { get; }

        public ThumbnailWindow(string applicationTitle, IntPtr processHandle, DatabaseService databaseService, long profileId)
        {
            _databaseService = databaseService;
            _windowTitle = applicationTitle;
            _profileId = profileId;

            ViewModel = new ThumbnailWindowViewModel(applicationTitle);
            ViewModel.ProcessHandle = processHandle;

            DatabaseService.ThumbnailConfig config = _databaseService.GetThumbnailSettingsOrDefault(profileId, applicationTitle);
            ViewModel.IsAlwaysOnTop = true;
            ViewModel.Width = config.Width;
            ViewModel.Height = config.Height;
            _focusBorderColor = config.FocusBorderColor ?? "#0078D4";
            _focusBorderThickness = config.FocusBorderThickness;
            ViewModel.FocusBorderColor = _focusBorderColor;
            ViewModel.FocusBorderThickness = _focusBorderThickness;
            ViewModel.ShowTitleOverlay = config.ShowTitleOverlay;

            DataContext = this;

            SystemThemeWatcher.Watch(this);

            InitializeComponent();

            this.Opacity = 1.0;
            ViewModel.Opacity = config.Opacity;
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            this.Left = config.X;
            this.Top = config.Y;
            _isDraggingEnabled = _databaseService.GetThumbnailDraggingEnabled();

            this.Loaded += ThumbnailWindow_Loaded;
        }

        private void ThumbnailWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ThumbnailControl.SetProcessHandle(ViewModel.ProcessHandle);
            ThumbnailControl.SetOpacity(ViewModel.Opacity);
            StartFocusCheckTimer();
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.Opacity))
            {
                ThumbnailControl.SetOpacity(ViewModel.Opacity);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            ViewModel.PropertyChanged -= ViewModel_PropertyChanged;

            _dragEndTimer?.Stop();
            _dragEndTimer?.Dispose();
            _dragEndTimer = null;

            _focusCheckTimer?.Stop();
            _focusCheckTimer?.Dispose();
            _focusCheckTimer = null;

            SaveThumbnailSettings();
            ThumbnailControl.UnregisterThumbnail();
            base.OnClosed(e);
        }

        public void ShowWindow() => Show();
        public void CloseWindow() => Close();

        private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            bool isRightButton = e.RightButton == System.Windows.Input.MouseButtonState.Pressed;
            bool isLeftButton = e.LeftButton == System.Windows.Input.MouseButtonState.Pressed;

            if (e.ChangedButton == MouseButton.Right && _isDraggingEnabled)
            {
                _isRightMouseButtonDown = true;
                _isDragging = true;
                _dragStartMousePosition = this.PointToScreen(e.GetPosition(this));
                _dragStartWindowPosition = new Point(this.Left, this.Top);
                this.CaptureMouse();
                e.Handled = true;
            }
            else if (isLeftButton)
            {
                User32NativeMethods.SetForegroundWindow(ViewModel.ProcessHandle);
                User32NativeMethods.SetFocus(ViewModel.ProcessHandle);

                int style = User32NativeMethods.GetWindowLong(ViewModel.ProcessHandle, InteropConstants.GWL_STYLE);

                if ((style & InteropConstants.WS_MINIMIZE) == InteropConstants.WS_MINIMIZE)
                    User32NativeMethods.ShowWindowAsync(new HandleRef(null, ViewModel.ProcessHandle), InteropConstants.SW_SHOWNORMAL);
            }
        }

        private void Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isRightMouseButtonDown && _isDraggingEnabled && e.RightButton == MouseButtonState.Pressed)
            {
                Point currentScreenPosition = this.PointToScreen(e.GetPosition(this));

                Vector delta = currentScreenPosition - _dragStartMousePosition;

                double newLeft = _dragStartWindowPosition.X + delta.X;
                double newTop = _dragStartWindowPosition.Y + delta.Y;

                int left = (int)newLeft;
                int top = (int)newTop;

                if (!IsValidWindowPosition(left, top))
                {
                    left = 100;
                    top = 100;
                    newLeft = left;
                    newTop = top;
                }

                this.Left = newLeft;
                this.Top = newTop;
            }
        }

        private void Window_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Right && _isRightMouseButtonDown)
            {
                _isRightMouseButtonDown = false;
                this.ReleaseMouseCapture();

                SaveThumbnailSettings();

                Debug.WriteLine($"New window position: Left={this.Left}, Top={this.Top}");

                _dragEndTimer?.Stop();
                _dragEndTimer?.Dispose();

                _dragEndTimer = new System.Timers.Timer(100);
                _dragEndTimer.Elapsed += (s, args) =>
                {
                    _isDragging = false;
                    _dragEndTimer?.Stop();
                    _dragEndTimer?.Dispose();
                    _dragEndTimer = null;
                };
                _dragEndTimer.AutoReset = false;
                _dragEndTimer.Start();

                e.Handled = true;
            }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!IsValidWindowSize((int)e.NewSize.Width, (int)e.NewSize.Height))
            {
                this.Width = 400;
                this.Height = 300;
            }

            if (!_isUpdatingProgrammatically)
            {
                SaveThumbnailSettings();
            }

            Debug.WriteLine($"New window size: Width={e.NewSize.Width}, Height={e.NewSize.Height}");
        }

        /// <summary>
        /// Saves the current thumbnail settings to the database.
        /// </summary>
        private void SaveThumbnailSettings()
        {
            if (_isUpdatingProgrammatically)
            {
                Debug.WriteLine($"Skipping save for '{_windowTitle}' - programmatic update in progress");
                return;
            }

            try
            {
                DatabaseService.ThumbnailConfig config = new DatabaseService.ThumbnailConfig
                {
                    Width = (int)this.Width,
                    Height = (int)this.Height,
                    X = (int)this.Left,
                    Y = (int)this.Top,
                    Opacity = ViewModel.Opacity,
                    FocusBorderColor = ViewModel.FocusBorderColor,
                    FocusBorderThickness = ViewModel.FocusBorderThickness,
                    ShowTitleOverlay = ViewModel.ShowTitleOverlay
                };

                _databaseService.SaveThumbnailSettings(_profileId, _windowTitle, config);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving thumbnail settings: {ex.Message}");
            }
        }

        private bool IsValidWindowPosition(int left, int top)
        {
            return (left > WINDOW_POSITION_THRESHOLD_LOW)
                && (left < WINDOW_POSITION_THRESHOLD_HIGH)
                && (top > WINDOW_POSITION_THRESHOLD_LOW)
                && (top < WINDOW_POSITION_THRESHOLD_HIGH);
        }

        private bool IsValidWindowSize(int width, int height)
        {
            return (width > WINDOW_SIZE_THRESHOLD) && (height > WINDOW_SIZE_THRESHOLD);
        }

        /// <summary>
        /// Updates the thumbnail window with settings from a new profile.
        /// </summary>
        /// <param name="newProfileId">The ID of the new profile.</param>
        public void UpdateProfile(long newProfileId)
        {
            try
            {
                _profileId = newProfileId;
                DatabaseService.ThumbnailConfig config = _databaseService.GetThumbnailSettingsOrDefault(newProfileId, _windowTitle);

                this.Dispatcher.Invoke(() =>
                {
                    _isUpdatingProgrammatically = true;

                    try
                    {
                        ViewModel.Width = config.Width;
                        ViewModel.Height = config.Height;
                        this.Width = config.Width;
                        this.Height = config.Height;

                        ViewModel.Opacity = config.Opacity;
                        this.Opacity = 1.0;
                        _focusBorderColor = config.FocusBorderColor ?? "#0078D4";
                        _focusBorderThickness = config.FocusBorderThickness;
                        ViewModel.FocusBorderColor = _focusBorderColor;
                        ViewModel.FocusBorderThickness = _focusBorderThickness;
                        ViewModel.ShowTitleOverlay = config.ShowTitleOverlay;

                        ThumbnailControl.SetOpacity(config.Opacity);

                        if (!_isDragging)
                        {
                            this.Left = config.X;
                            this.Top = config.Y;
                        }
                    }
                    finally
                    {
                        _isUpdatingProgrammatically = false;
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating thumbnail window '{_windowTitle}' for profile {newProfileId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Refreshes the thumbnail window settings from the database for the current profile.
        /// </summary>
        public void RefreshSettings()
        {
            UpdateProfile(_profileId);

            _isDraggingEnabled = _databaseService.GetThumbnailDraggingEnabled();

            DatabaseService.ThumbnailConfig config = _databaseService.GetThumbnailSettingsOrDefault(_profileId, _windowTitle);
            _focusBorderColor = config.FocusBorderColor ?? "#0078D4";
            _focusBorderThickness = config.FocusBorderThickness;
            ViewModel.FocusBorderColor = _focusBorderColor;
            ViewModel.FocusBorderThickness = _focusBorderThickness;
            ViewModel.ShowTitleOverlay = config.ShowTitleOverlay;
        }

        /// <summary>
        /// Sets the focus state manually (for preview purposes).
        /// </summary>
        /// <param name="isFocused">Whether the thumbnail should appear focused.</param>
        public void SetFocusPreview(bool isFocused)
        {
            this.Dispatcher.Invoke(() =>
            {
                ViewModel.IsFocused = isFocused;
            });
        }

        /// <summary>
        /// Updates border settings immediately without reloading from database (for live preview).
        /// </summary>
        /// <param name="borderColor">The border color in hex format.</param>
        /// <param name="borderThickness">The border thickness in pixels.</param>
        public void UpdateBorderSettings(string borderColor, int borderThickness)
        {
            this.Dispatcher.Invoke(() =>
            {
                _focusBorderColor = borderColor ?? "#0078D4";
                _focusBorderThickness = borderThickness;
                ViewModel.FocusBorderColor = _focusBorderColor;
                ViewModel.FocusBorderThickness = _focusBorderThickness;

                Debug.WriteLine($"Updated border settings for '{_windowTitle}': Color={_focusBorderColor}, Thickness={_focusBorderThickness}");
            });
        }

        /// <summary>
        /// Gets the window title of this thumbnail window.
        /// </summary>
        public string WindowTitle => _windowTitle;

        /// <summary>
        /// Updates the title overlay visibility.
        /// </summary>
        /// <param name="showTitleOverlay">Whether to show the title overlay.</param>
        public void UpdateTitleOverlayVisibility(bool showTitleOverlay)
        {
            this.Dispatcher.Invoke(() =>
            {
                ViewModel.ShowTitleOverlay = showTitleOverlay;
            });
        }

        /// <summary>
        /// Updates size and opacity immediately without reloading from database (for live preview).
        /// </summary>
        /// <param name="width">The width in pixels.</param>
        /// <param name="height">The height in pixels.</param>
        /// <param name="opacity">The opacity value (0.0 to 1.0).</param>
        public void UpdateSizeAndOpacity(int width, int height, double opacity)
        {
            this.Dispatcher.Invoke(() =>
            {
                _isUpdatingProgrammatically = true;

                try
                {
                    ViewModel.Width = width;
                    ViewModel.Height = height;
                    this.Width = width;
                    this.Height = height;

                    ViewModel.Opacity = opacity;
                    ThumbnailControl.SetOpacity(opacity);

                    Debug.WriteLine($"Updated size and opacity for '{_windowTitle}': Width={width}, Height={height}, Opacity={opacity}");
                }
                finally
                {
                    _isUpdatingProgrammatically = false;
                }
            });
        }

        /// <summary>
        /// Starts a timer to periodically check if the process is focused.
        /// </summary>
        private void StartFocusCheckTimer()
        {
            _focusCheckTimer = new System.Timers.Timer(100);
            _focusCheckTimer.Elapsed += (sender, e) =>
            {
                this.Dispatcher.Invoke(() =>
                {
                    CheckFocus();
                });
            };
            _focusCheckTimer.AutoReset = true;
            _focusCheckTimer.Start();

            CheckFocus();
        }

        /// <summary>
        /// Checks if the process associated with this thumbnail is currently focused.
        /// </summary>
        private void CheckFocus()
        {
            if (_focusCheckPaused)
                return;

            if (ViewModel.ProcessHandle == IntPtr.Zero)
            {
                ViewModel.IsFocused = false;
                return;
            }

            try
            {
                IntPtr foregroundWindow = User32NativeMethods.GetForegroundWindow();
                bool isFocused = false;

                if (foregroundWindow != IntPtr.Zero)
                {
                    if (foregroundWindow == ViewModel.ProcessHandle)
                    {
                        isFocused = true;
                    }
                    else
                    {
                        uint foregroundProcessId = 0;
                        uint currentProcessId = 0;

                        User32NativeMethods.GetWindowThreadProcessId(foregroundWindow, out foregroundProcessId);
                        User32NativeMethods.GetWindowThreadProcessId(ViewModel.ProcessHandle, out currentProcessId);

                        isFocused = (foregroundProcessId != 0 && currentProcessId != 0 && foregroundProcessId == currentProcessId);
                    }
                }

                ViewModel.IsFocused = isFocused;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking focus for thumbnail '{_windowTitle}': {ex.Message}");
                ViewModel.IsFocused = false;
            }
        }

        /// <summary>
        /// Pauses the focus check timer (for preview purposes).
        /// </summary>
        public void PauseFocusCheck()
        {
            _focusCheckPaused = true;
        }

        /// <summary>
        /// Resumes the focus check timer.
        /// </summary>
        public void ResumeFocusCheck()
        {
            _focusCheckPaused = false;
            CheckFocus();
        }
    }
}

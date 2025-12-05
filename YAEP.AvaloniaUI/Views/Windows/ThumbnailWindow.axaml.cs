using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using YAEP.Interop;
using YAEP.Services;
using YAEP.ViewModels.Windows;

namespace YAEP.Views.Windows
{
    public partial class ThumbnailWindow : Window
    {
        private const int WINDOW_POSITION_THRESHOLD_LOW = -10_000;
        private const int WINDOW_POSITION_THRESHOLD_HIGH = 31_000;

        private readonly DatabaseService _databaseService;
        private readonly string _windowTitle;
        private long _profileId;
        private volatile bool _isDragging = false;
        private System.Timers.Timer? _dragEndTimer;
        private bool _isDraggingEnabled = true;
        private Avalonia.Point _dragStartMousePosition;
        private Avalonia.PixelPoint _dragStartWindowPosition;
        private bool _isRightMouseButtonDown = false;
        private bool _isUpdatingProgrammatically = false;

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
            ViewModel.FocusBorderColor = config.FocusBorderColor ?? "#0078D4";
            ViewModel.FocusBorderThickness = config.FocusBorderThickness;
            ViewModel.ShowTitleOverlay = config.ShowTitleOverlay;
            ViewModel.Opacity = config.Opacity;

            DataContext = this;
            InitializeComponent();

            this.Opacity = config.Opacity;
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            this.Position = new Avalonia.PixelPoint(config.X, config.Y);
            _isDraggingEnabled = _databaseService.GetThumbnailDraggingEnabled();

            // Initialize thumbnail when window is loaded
            this.Opened += ThumbnailWindow_Opened;
            this.Closed += ThumbnailWindow_Closed;
        }

        private void ThumbnailWindow_Closed(object? sender, EventArgs e)
        {
            _dragEndTimer?.Stop();
            _dragEndTimer?.Dispose();
            _dragEndTimer = null;

            SaveThumbnailSettings();
            if (ThumbnailControl != null)
            {
                ThumbnailControl.UnregisterThumbnail();
            }
        }

        private void ThumbnailWindow_Opened(object? sender, EventArgs e)
        {
            if (ThumbnailControl != null && ViewModel.ProcessHandle != IntPtr.Zero)
            {
                ThumbnailControl.SetProcessHandle(ViewModel.ProcessHandle);
                ThumbnailControl.SetOpacity(ViewModel.Opacity);
            }
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.Opacity))
            {
                this.Opacity = ViewModel.Opacity;
                if (ThumbnailControl != null)
                {
                    ThumbnailControl.SetOpacity(ViewModel.Opacity);
                }
            }
        }

        public void ShowWindow() => Show();
        public void CloseWindow() => Close();

        public string WindowTitle => _windowTitle;

        public void UpdateProfile(long newProfileId)
        {
            _profileId = newProfileId;
            DatabaseService.ThumbnailConfig config = _databaseService.GetThumbnailSettingsOrDefault(newProfileId, _windowTitle);

            Dispatcher.UIThread.Post(() =>
            {
                _isUpdatingProgrammatically = true;

                try
                {
                    ViewModel.Width = config.Width;
                    ViewModel.Height = config.Height;
                    this.Width = config.Width;
                    this.Height = config.Height;
                    ViewModel.Opacity = config.Opacity;
                    this.Opacity = config.Opacity;

                    if (!_isDragging)
                    {
                        this.Position = new Avalonia.PixelPoint(config.X, config.Y);
                    }
                }
                finally
                {
                    _isUpdatingProgrammatically = false;
                }
            });
        }

        public void RefreshSettings()
        {
            UpdateProfile(_profileId);
            _isDraggingEnabled = _databaseService.GetThumbnailDraggingEnabled();
        }

        public void SetFocusPreview(bool isFocused)
        {
            Dispatcher.UIThread.Post(() =>
            {
                ViewModel.IsFocused = isFocused;
            });
        }

        public void UpdateBorderSettings(string borderColor, int borderThickness)
        {
            Dispatcher.UIThread.Post(() =>
            {
                ViewModel.FocusBorderColor = borderColor ?? "#0078D4";
                ViewModel.FocusBorderThickness = borderThickness;
            });
        }

        public void UpdateTitleOverlayVisibility(bool showTitleOverlay)
        {
            Dispatcher.UIThread.Post(() =>
            {
                ViewModel.ShowTitleOverlay = showTitleOverlay;
            });
        }

        public void UpdateSizeAndOpacity(int width, int height, double opacity)
        {
            Dispatcher.UIThread.Post(() =>
            {
                ViewModel.Width = width;
                ViewModel.Height = height;
                this.Width = width;
                this.Height = height;
                ViewModel.Opacity = opacity;
                this.Opacity = opacity;
            });
        }

        public void PauseFocusCheck() { }
        public void ResumeFocusCheck() { }

        private void Window_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var properties = e.GetCurrentPoint(this).Properties;
            bool isRightButton = properties.IsRightButtonPressed;
            bool isLeftButton = properties.IsLeftButtonPressed;

            if (isRightButton && _isDraggingEnabled)
            {
                _isRightMouseButtonDown = true;
                _isDragging = true;
                var position = e.GetPosition(this);
                _dragStartMousePosition = position;
                _dragStartWindowPosition = this.Position;
                e.Pointer.Capture(this);
                e.Handled = true;
            }
            else if (isLeftButton && ViewModel.ProcessHandle != IntPtr.Zero)
            {
                // Activate the source window on left click
                User32NativeMethods.SetForegroundWindow(ViewModel.ProcessHandle);
                User32NativeMethods.SetFocus(ViewModel.ProcessHandle);

                int style = User32NativeMethods.GetWindowLong(
                    ViewModel.ProcessHandle,
                    InteropConstants.GWL_STYLE);
                if ((style & InteropConstants.WS_MINIMIZE) == InteropConstants.WS_MINIMIZE)
                {
                    User32NativeMethods.ShowWindowAsync(
                        new HandleRef(null, ViewModel.ProcessHandle),
                        InteropConstants.SW_SHOWNORMAL);
                }
            }
        }

        private void Window_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (_isRightMouseButtonDown && _isDraggingEnabled)
            {
                var properties = e.GetCurrentPoint(this).Properties;
                if (properties.IsRightButtonPressed)
                {
                    var currentPosition = e.GetPosition(this);
                    var delta = currentPosition - _dragStartMousePosition;

                    double newX = _dragStartWindowPosition.X + delta.X;
                    double newY = _dragStartWindowPosition.Y + delta.Y;

                    int x = (int)newX;
                    int y = (int)newY;

                    if (!IsValidWindowPosition(x, y))
                    {
                        x = 100;
                        y = 100;
                        newX = x;
                        newY = y;
                    }

                    this.Position = new Avalonia.PixelPoint(x, y);
                }
            }
        }

        private void Window_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (e.InitialPressMouseButton == MouseButton.Right && _isRightMouseButtonDown)
            {
                _isRightMouseButtonDown = false;
                e.Pointer.Capture(null);

                SaveThumbnailSettings();

                Debug.WriteLine($"New window position: X={this.Position.X}, Y={this.Position.Y}");

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

        private bool IsValidWindowPosition(int x, int y)
        {
            return (x > WINDOW_POSITION_THRESHOLD_LOW)
                && (x < WINDOW_POSITION_THRESHOLD_HIGH)
                && (y > WINDOW_POSITION_THRESHOLD_LOW)
                && (y < WINDOW_POSITION_THRESHOLD_HIGH);
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
                    X = this.Position.X,
                    Y = this.Position.Y,
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
    }
}


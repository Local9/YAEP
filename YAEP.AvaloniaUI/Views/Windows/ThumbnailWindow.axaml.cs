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
        private System.Timers.Timer? _positionTracker;
        private System.Timers.Timer? _focusCheckTimer;
        private bool _focusCheckPaused = false;
        private bool _isDraggingEnabled = true;
        private Avalonia.PixelPoint _dragStartMousePosition; // Initial mouse position in screen coordinates
        private Avalonia.PixelPoint _dragStartWindowPosition;
        private bool _isRightMouseButtonDown = false;
        private bool _isUpdatingProgrammatically = false;
        private Avalonia.PixelPoint? _initialPosition;
        private Avalonia.PixelPoint _lastKnownPosition; // Store last known position to save on close

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
                    
                    // Update border thickness on thumbnail control
                    if (ThumbnailControl != null)
                    {
                        ThumbnailControl.SetBorderThickness(config.FocusBorderThickness);
                    }
            ViewModel.Opacity = config.Opacity;
            
            // Set border thickness on thumbnail control after initialization
            this.Loaded += (s, e) =>
            {
                if (ThumbnailControl != null)
                {
                    ThumbnailControl.SetBorderThickness(ViewModel.FocusBorderThickness);
                }
            };

            DataContext = this;
            InitializeComponent();

            this.Width = config.Width;
            this.Height = config.Height;
            this.Opacity = config.Opacity;
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            // Store initial position to apply when window is opened
            _initialPosition = new Avalonia.PixelPoint(config.X, config.Y);
            _isDraggingEnabled = _databaseService.GetThumbnailDraggingEnabled();

            // Initialize thumbnail when window is loaded
            this.Opened += ThumbnailWindow_Opened;
            this.Closed += ThumbnailWindow_Closed;
            
            // Track position changes using a timer to periodically update last known position
            _positionTracker = new System.Timers.Timer(500); // Check every 500ms
            _positionTracker.Elapsed += (s, e) =>
            {
                if (!_isUpdatingProgrammatically && !_isDragging)
                {
                    try
                    {
                        var currentPosition = this.Position;
                        if (IsValidWindowPosition(currentPosition.X, currentPosition.Y))
                        {
                            _lastKnownPosition = currentPosition;
                        }
                    }
                    catch
                    {
                        // Window might be closing or disposed
                    }
                }
            };
            _positionTracker.AutoReset = true;
            _positionTracker.Start();
            
            // Also track when window is moved (if available)
            this.PositionChanged += (s, e) =>
            {
                if (!_isUpdatingProgrammatically)
                {
                    var newPosition = this.Position;
                    if (IsValidWindowPosition(newPosition.X, newPosition.Y))
                    {
                        _lastKnownPosition = newPosition;
                    }
                }
            };
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);
        }

        private void ThumbnailWindow_Closed(object? sender, EventArgs e)
        {
            _dragEndTimer?.Stop();
            _dragEndTimer?.Dispose();
            _dragEndTimer = null;

            _positionTracker?.Stop();
            _positionTracker?.Dispose();
            _positionTracker = null;

            _focusCheckTimer?.Stop();
            _focusCheckTimer?.Dispose();
            _focusCheckTimer = null;

            if (ThumbnailControl != null)
            {
                ThumbnailControl.UnregisterThumbnail();
            }
        }

        private void ThumbnailWindow_Opened(object? sender, EventArgs e)
        {
            // Apply saved position when window is opened
            if (_initialPosition.HasValue)
            {
                this.Position = _initialPosition.Value;
                _lastKnownPosition = _initialPosition.Value; // Store the initial position
                _initialPosition = null; // Clear after first use
            }
            else
            {
                // Store current position if no initial position was set
                _lastKnownPosition = this.Position;
            }

            if (ThumbnailControl != null && ViewModel.ProcessHandle != IntPtr.Zero)
            {
                ThumbnailControl.SetProcessHandle(ViewModel.ProcessHandle);
                ThumbnailControl.SetOpacity(ViewModel.Opacity);
                ThumbnailControl.SetBorderThickness(ViewModel.FocusBorderThickness);
                ThumbnailControl.SetIsFocused(ViewModel.IsFocused);
            }

            // Start focus checking timer
            StartFocusCheckTimer();
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.Opacity))
            {
                // Only update opacity if not hovering (hover takes precedence)
                if (!this.IsPointerOver)
                {
                    this.Opacity = ViewModel.Opacity;
                    if (ThumbnailControl != null)
                    {
                        ThumbnailControl.SetOpacity(ViewModel.Opacity);
                    }
                }
            }
            else if (e.PropertyName == nameof(ViewModel.FocusBorderThickness))
            {
                // Update border thickness on thumbnail control when it changes
                if (ThumbnailControl != null)
                {
                    ThumbnailControl.SetBorderThickness(ViewModel.FocusBorderThickness);
                }
            }
            else if (e.PropertyName == nameof(ViewModel.IsFocused))
            {
                // Update focus state on thumbnail control when it changes
                if (ThumbnailControl != null)
                {
                    ThumbnailControl.SetIsFocused(ViewModel.IsFocused);
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
                    // Only update opacity if not hovering (hover takes precedence)
                    if (!this.IsPointerOver)
                    {
                        this.Opacity = config.Opacity;
                        if (ThumbnailControl != null)
                        {
                            ThumbnailControl.SetOpacity(config.Opacity);
                        }
                    }

                    if (!_isDragging)
                    {
                        var newPosition = new Avalonia.PixelPoint(config.X, config.Y);
                        this.Position = newPosition;
                        _lastKnownPosition = newPosition; // Update last known position
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
                if (ThumbnailControl != null)
                {
                    ThumbnailControl.SetBorderThickness(borderThickness);
                }
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
            // Don't update if width or height is 0 or invalid - this prevents windows from being resized to 0x0
            if (width <= 0 || height <= 0)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateSizeAndOpacity: Skipping update for '{_windowTitle}' - invalid size: Width={width}, Height={height}");
                return;
            }

            Dispatcher.UIThread.Post(() =>
            {
                ViewModel.Width = width;
                ViewModel.Height = height;
                this.Width = width;
                this.Height = height;
                ViewModel.Opacity = opacity;
                // Only update opacity if not hovering (hover takes precedence)
                if (!this.IsPointerOver)
                {
                    this.Opacity = opacity;
                    if (ThumbnailControl != null)
                    {
                        ThumbnailControl.SetOpacity(opacity);
                    }
                }
            });
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
            // Immediately check focus when resuming
            CheckFocus();
        }

        /// <summary>
        /// Starts a timer to periodically check if the process is focused.
        /// </summary>
        private void StartFocusCheckTimer()
        {
            _focusCheckTimer = new System.Timers.Timer(100);
            _focusCheckTimer.Elapsed += (sender, e) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    CheckFocus();
                });
            };
            _focusCheckTimer.AutoReset = true;
            _focusCheckTimer.Start();

            // Check focus immediately
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

        private void Window_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var properties = e.GetCurrentPoint(this).Properties;
            bool isRightButton = properties.IsRightButtonPressed;
            bool isLeftButton = properties.IsLeftButtonPressed;

            if (isRightButton && _isDraggingEnabled)
            {
                _isRightMouseButtonDown = true;
                _isDragging = true;
                // Convert window-relative position to screen coordinates (like PointToScreen in WPF)
                var position = e.GetPosition(this);
                _dragStartMousePosition = new Avalonia.PixelPoint(
                    this.Position.X + (int)position.X,
                    this.Position.Y + (int)position.Y);
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
                    // Convert window-relative position to screen coordinates (like PointToScreen in WPF)
                    // Use current window position to convert to screen coordinates
                    var position = e.GetPosition(this);
                    var currentScreenPosition = new Avalonia.PixelPoint(
                        this.Position.X + (int)position.X,
                        this.Position.Y + (int)position.Y);

                    // Calculate delta in screen coordinates (exactly like WPF)
                    var deltaX = currentScreenPosition.X - _dragStartMousePosition.X;
                    var deltaY = currentScreenPosition.Y - _dragStartMousePosition.Y;

                    // Apply delta to initial window position (exactly like WPF)
                    double newX = _dragStartWindowPosition.X + deltaX;
                    double newY = _dragStartWindowPosition.Y + deltaY;

                    int x = (int)newX;
                    int y = (int)newY;

                    if (!IsValidWindowPosition(x, y))
                    {
                        x = 100;
                        y = 100;
                        newX = x;
                        newY = y;
                    }

                    var newPosition = new Avalonia.PixelPoint(x, y);
                    this.Position = newPosition;
                    _lastKnownPosition = newPosition; // Update last known position during drag
                }
            }
        }

        private void Window_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (e.InitialPressMouseButton == MouseButton.Right && _isRightMouseButtonDown)
            {
                _isRightMouseButtonDown = false;
                e.Pointer.Capture(null);

                // Update last known position before saving
                _lastKnownPosition = this.Position;

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

        private void Window_PointerEntered(object? sender, PointerEventArgs e)
        {
            // Set opacity to 1.0 when mouse enters
            this.Opacity = 1.0;
            if (ThumbnailControl != null)
            {
                ThumbnailControl.SetOpacity(1.0);
            }
        }

        private void Window_PointerExited(object? sender, PointerEventArgs e)
        {
            // Restore original opacity when mouse leaves
            this.Opacity = ViewModel.Opacity;
            if (ThumbnailControl != null)
            {
                ThumbnailControl.SetOpacity(ViewModel.Opacity);
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
                // Always use last known position - it's continuously updated via PropertyChanged
                Avalonia.PixelPoint positionToSave = _lastKnownPosition;
                
                // Try to get current position if window is still valid, otherwise use last known
                try
                {
                    var currentPosition = this.Position;
                    // Only use current position if it's valid (not 0,0 or invalid)
                    if (IsValidWindowPosition(currentPosition.X, currentPosition.Y))
                    {
                        positionToSave = currentPosition;
                        _lastKnownPosition = currentPosition; // Update last known position
                    }
                    else
                    {
                        Debug.WriteLine($"SaveThumbnailSettings: Current position invalid ({currentPosition.X}, {currentPosition.Y}), using last known: X={_lastKnownPosition.X}, Y={_lastKnownPosition.Y}");
                    }
                }
                catch (Exception ex)
                {
                    // Window might be closing, use last known position
                    Debug.WriteLine($"SaveThumbnailSettings: Error getting current position: {ex.Message}, using last known: X={_lastKnownPosition.X}, Y={_lastKnownPosition.Y}");
                }

                DatabaseService.ThumbnailConfig config = new DatabaseService.ThumbnailConfig
                {
                    Width = (int)this.Width,
                    Height = (int)this.Height,
                    X = positionToSave.X,
                    Y = positionToSave.Y,
                    Opacity = ViewModel.Opacity,
                    FocusBorderColor = ViewModel.FocusBorderColor,
                    FocusBorderThickness = ViewModel.FocusBorderThickness,
                    ShowTitleOverlay = ViewModel.ShowTitleOverlay
                };

                _databaseService.SaveThumbnailSettings(_profileId, _windowTitle, config);
                Debug.WriteLine($"Saved thumbnail settings for '{_windowTitle}': X={positionToSave.X}, Y={positionToSave.Y}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving thumbnail settings: {ex.Message}");
            }
        }
    }
}


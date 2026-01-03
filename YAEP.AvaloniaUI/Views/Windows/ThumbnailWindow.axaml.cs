using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform;
using System.Runtime.InteropServices;
using YAEP.Interop;
using YAEP.Models;
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
        private bool _isDraggingEnabled = true;
        private Avalonia.PixelPoint _dragStartMousePosition;
        private Avalonia.PixelPoint _dragStartWindowPosition;
        private bool _isRightMouseButtonDown = false;
        private bool _isUpdatingProgrammatically = false;
        private Avalonia.PixelPoint? _initialPosition;
        private Avalonia.PixelPoint _lastKnownPosition;
        private ThumbnailOverlayWindow? _overlayWindow;
        private bool _isGroupDragging = false;
        private Dictionary<ThumbnailWindow, Avalonia.PixelPoint>? _groupDragWindows;

        public ThumbnailWindowViewModel ViewModel { get; }

        public ThumbnailWindow()
        {
            _databaseService = null!;
            _windowTitle = string.Empty;
            _profileId = 0;
            ViewModel = new ThumbnailWindowViewModel(string.Empty);
            DataContext = this;
            InitializeComponent();
        }

        public ThumbnailWindow(string applicationTitle, IntPtr processHandle, DatabaseService databaseService, long profileId)
        {
            _databaseService = databaseService;
            _windowTitle = applicationTitle;
            _profileId = profileId;

            ViewModel = new ThumbnailWindowViewModel(applicationTitle);
            ViewModel.ProcessHandle = processHandle;

            ThumbnailConfig config = _databaseService.GetThumbnailSettingsOrDefault(profileId, applicationTitle);
            ViewModel.IsAlwaysOnTop = true;
            ViewModel.Width = config.Width;
            ViewModel.Height = config.Height;
            ViewModel.FocusBorderColor = config.FocusBorderColor ?? "#0078D4";
            ViewModel.FocusBorderThickness = config.FocusBorderThickness;
            ViewModel.ShowTitleOverlay = config.ShowTitleOverlay;
            ViewModel.Opacity = config.Opacity;

            DataContext = this;
            InitializeComponent();

            this.Width = config.Width;
            this.Height = config.Height;
            this.Opacity = config.Opacity;
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            _initialPosition = new Avalonia.PixelPoint(config.X, config.Y);
            _isDraggingEnabled = _databaseService.GetThumbnailDraggingEnabled();

            this.Opened += ThumbnailWindow_Opened;
            this.Closed += ThumbnailWindow_Closed;
            this.PositionChanged += ThumbnailWindow_PositionChanged;
            this.SizeChanged += ThumbnailWindow_SizeChanged;
            this.Activated += ThumbnailWindow_Activated;

            _positionTracker = new System.Timers.Timer(500);
            _positionTracker.Elapsed += (s, e) =>
            {
                if (!_isUpdatingProgrammatically && !_isDragging)
                {
                    try
                    {
                        Avalonia.PixelPoint currentPosition = this.Position;
                        if (IsValidWindowPosition(currentPosition.X, currentPosition.Y))
                        {
                            _lastKnownPosition = currentPosition;
                        }
                    }
                    catch
                    {
                    }
                }
            };
            _positionTracker.AutoReset = true;
            _positionTracker.Start();

            this.PositionChanged += (s, e) =>
            {
                if (!_isUpdatingProgrammatically)
                {
                    Avalonia.PixelPoint newPosition = this.Position;
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

            if (ThumbnailControl != null)
            {
                ThumbnailControl.UnregisterThumbnail();
            }

            if (_overlayWindow != null)
            {
                _overlayWindow.Close();
                _overlayWindow = null;
            }
        }

        private void ThumbnailWindow_Opened(object? sender, EventArgs e)
        {
            if (_initialPosition.HasValue)
            {
                Avalonia.PixelPoint initialPos = _initialPosition.Value;
                if (!IsValidWindowPosition(initialPos.X, initialPos.Y))
                {
                    Debug.WriteLine($"Initial position ({initialPos.X}, {initialPos.Y}) for '{_windowTitle}' is outside screen bounds, clamping to valid position");
                    initialPos = ClampToScreenBounds(initialPos.X, initialPos.Y);
                }
                this.Position = initialPos;
                _lastKnownPosition = initialPos;
                _initialPosition = null;
            }
            else
            {
                Avalonia.PixelPoint currentPos = this.Position;
                if (!IsValidWindowPosition(currentPos.X, currentPos.Y))
                {
                    Debug.WriteLine($"Current position ({currentPos.X}, {currentPos.Y}) for '{_windowTitle}' is outside screen bounds, clamping to valid position");
                    currentPos = ClampToScreenBounds(currentPos.X, currentPos.Y);
                    this.Position = currentPos;
                }
                _lastKnownPosition = currentPos;
            }

            if (ThumbnailControl != null && ViewModel.ProcessHandle != IntPtr.Zero)
            {
                ThumbnailControl.SetProcessHandle(ViewModel.ProcessHandle);
                ThumbnailControl.SetOpacity(ViewModel.Opacity);
            }

            CreateOverlayWindow();

            HideFromAltTab();

            Dispatcher.UIThread.Post(() =>
            {
                SaveThumbnailSettings();
            }, DispatcherPriority.Normal);
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
                System.Diagnostics.Debug.WriteLine($"Failed to hide thumbnail window from Alt+Tab: {ex.Message}");
            }
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.Opacity))
            {
                if (!this.IsPointerOver)
                {
                    this.Opacity = ViewModel.Opacity;
                    if (ThumbnailControl != null)
                    {
                        ThumbnailControl.SetOpacity(ViewModel.Opacity);
                    }
                }
            }
        }

        public void ShowWindow() => Show();
        public void CloseWindow() => Close();

        public string WindowTitle => _windowTitle;

        /// <summary>
        /// Updates the window position and last known position. Used by group drag operations.
        /// </summary>
        /// <param name="newPosition">The new position for the window.</param>
        public void UpdatePositionAndLastKnown(Avalonia.PixelPoint newPosition)
        {
            this.Position = newPosition;
            _lastKnownPosition = newPosition;
        }

        /// <summary>
        /// Saves the thumbnail settings. Public method for use by service classes.
        /// </summary>
        public void SaveSettings()
        {
            SaveThumbnailSettings();
        }

        /// <summary>
        /// Checks if a window position is valid. Public method for use by service classes.
        /// </summary>
        /// <param name="x">The X coordinate.</param>
        /// <param name="y">The Y coordinate.</param>
        /// <returns>True if the position is valid.</returns>
        public bool IsPositionValid(int x, int y)
        {
            return IsValidWindowPosition(x, y);
        }

        public void UpdateProfile(long newProfileId)
        {
            _profileId = newProfileId;
            ThumbnailConfig config = _databaseService.GetThumbnailSettingsOrDefault(newProfileId, _windowTitle);

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
                        Avalonia.PixelPoint newPosition = new Avalonia.PixelPoint(config.X, config.Y);
                        if (!_isUpdatingProgrammatically && !IsValidWindowPosition(newPosition.X, newPosition.Y))
                        {
                            Debug.WriteLine($"Profile update position ({newPosition.X}, {newPosition.Y}) for '{_windowTitle}' is outside screen bounds, clamping to valid position");
                            newPosition = ClampToScreenBounds(newPosition.X, newPosition.Y);
                        }
                        this.Position = newPosition;
                        _lastKnownPosition = newPosition;
                    }

                    SyncOverlayWindow();
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
                if (!this.IsPointerOver)
                {
                    this.Opacity = opacity;
                    if (ThumbnailControl != null)
                    {
                        ThumbnailControl.SetOpacity(opacity);
                    }
                }

                SyncOverlayWindow();
            });
        }


        private void Window_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            PointerPointProperties properties = e.GetCurrentPoint(this).Properties;
            bool isRightButton = properties.IsRightButtonPressed;
            bool isLeftButton = properties.IsLeftButtonPressed;
            bool isControlPressed = (e.KeyModifiers & KeyModifiers.Control) == KeyModifiers.Control;

            if (isRightButton && _isDraggingEnabled)
            {
                _isRightMouseButtonDown = true;
                _isDragging = true;
                Avalonia.Point position = e.GetPosition(this);
                _dragStartMousePosition = new Avalonia.PixelPoint(
                    this.Position.X + (int)position.X,
                    this.Position.Y + (int)position.Y);
                _dragStartWindowPosition = this.Position;

                _isGroupDragging = isControlPressed;
                _groupDragWindows = null;

                if (_isGroupDragging)
                {
                    Interface.IThumbnailWindowService? service = YAEP.App.ThumbnailWindowService;
                    if (service != null)
                    {
                        _groupDragWindows = service.StartGroupDrag(this);
                    }
                }

                e.Pointer.Capture(this);
                e.Handled = true;
            }
            else if (isLeftButton && ViewModel.ProcessHandle != IntPtr.Zero)
            {
                BringOverlayToTop();

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
                PointerPointProperties properties = e.GetCurrentPoint(this).Properties;
                if (properties.IsRightButtonPressed)
                {
                    Avalonia.Point position = e.GetPosition(this);
                    Avalonia.PixelPoint currentScreenPosition = new Avalonia.PixelPoint(
                        this.Position.X + (int)position.X,
                        this.Position.Y + (int)position.Y);

                    int deltaX = currentScreenPosition.X - _dragStartMousePosition.X;
                    int deltaY = currentScreenPosition.Y - _dragStartMousePosition.Y;

                    double newX = _dragStartWindowPosition.X + deltaX;
                    double newY = _dragStartWindowPosition.Y + deltaY;

                    int x = (int)newX;
                    int y = (int)newY;

                    if (!IsValidWindowPosition(x, y))
                    {
                        Debug.WriteLine($"Drag position ({x}, {y}) for '{_windowTitle}' is outside screen bounds, clamping to valid position");
                        Avalonia.PixelPoint clampedPosition = ClampToScreenBounds(x, y);
                        x = clampedPosition.X;
                        y = clampedPosition.Y;
                        newX = x;
                        newY = y;
                    }

                    Avalonia.PixelPoint newPosition = new Avalonia.PixelPoint(x, y);
                    this.Position = newPosition;
                    _lastKnownPosition = newPosition;

                    if (_isGroupDragging && _groupDragWindows != null)
                    {
                        Interface.IThumbnailWindowService? service = YAEP.App.ThumbnailWindowService;
                        if (service != null)
                        {
                            service.UpdateGroupDrag(this, newPosition, _groupDragWindows);
                        }
                    }
                }
            }
        }

        private void Window_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (e.InitialPressMouseButton == MouseButton.Right && _isRightMouseButtonDown)
            {
                _isRightMouseButtonDown = false;
                e.Pointer.Capture(null);

                _lastKnownPosition = this.Position;

                SaveThumbnailSettings();

                if (_isGroupDragging && _groupDragWindows != null)
                {
                    Interface.IThumbnailWindowService? service = YAEP.App.ThumbnailWindowService;
                    if (service != null)
                    {
                        service.EndGroupDrag(_groupDragWindows);
                    }
                }

                _isGroupDragging = false;
                _groupDragWindows = null;

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
            this.Opacity = 1.0;
            if (ThumbnailControl != null)
            {
                ThumbnailControl.SetOpacity(1.0);
            }
        }

        private void Window_PointerExited(object? sender, PointerEventArgs e)
        {
            this.Opacity = ViewModel.Opacity;
            if (ThumbnailControl != null)
            {
                ThumbnailControl.SetOpacity(ViewModel.Opacity);
            }
        }

        private void Window_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            bool isControlPressed = (e.KeyModifiers & KeyModifiers.Control) == KeyModifiers.Control;

            if (!isControlPressed)
                return;

            double currentWidth = this.Width;
            double currentHeight = this.Height;

            if (currentWidth <= 0 || currentHeight <= 0)
                return;

            double aspectRatio = currentWidth / currentHeight;
            double scaleFactor = e.Delta.Y > 0 ? 1.1 : 0.9;

            double newWidth = currentWidth * scaleFactor;
            double newHeight = newWidth / aspectRatio;

            if (newWidth > 960)
            {
                newWidth = 960;
                newHeight = newWidth / aspectRatio;
            }
            if (newHeight > 540)
            {
                newHeight = 540;
                newWidth = newHeight * aspectRatio;
            }
            if (newWidth < 192)
            {
                newWidth = 192;
                newHeight = newWidth / aspectRatio;
            }
            if (newHeight < 108)
            {
                newHeight = 108;
                newWidth = newHeight * aspectRatio;
            }

            int finalWidth = (int)Math.Round(newWidth);
            int finalHeight = (int)Math.Round(newHeight);

            if (finalWidth == (int)currentWidth && finalHeight == (int)currentHeight)
                return;

            ViewModel.Width = finalWidth;
            ViewModel.Height = finalHeight;
            this.Width = finalWidth;
            this.Height = finalHeight;

            SaveThumbnailSettings();

            e.Handled = true;
        }

        /// <summary>
        /// Checks if a window position is valid by verifying it's within screen bounds.
        /// Falls back to threshold check if screens are unavailable or called from non-UI thread.
        /// </summary>
        /// <param name="x">The X coordinate.</param>
        /// <param name="y">The Y coordinate.</param>
        /// <returns>True if the position is valid.</returns>
        private bool IsValidWindowPosition(int x, int y)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                return (x > WINDOW_POSITION_THRESHOLD_LOW)
                    && (x < WINDOW_POSITION_THRESHOLD_HIGH)
                    && (y > WINDOW_POSITION_THRESHOLD_LOW)
                    && (y < WINDOW_POSITION_THRESHOLD_HIGH);
            }

            try
            {
                Screens? screens = this.Screens;
                if (screens == null || screens.All.Count == 0)
                {
                    return (x > WINDOW_POSITION_THRESHOLD_LOW)
                        && (x < WINDOW_POSITION_THRESHOLD_HIGH)
                        && (y > WINDOW_POSITION_THRESHOLD_LOW)
                        && (y < WINDOW_POSITION_THRESHOLD_HIGH);
                }

                Avalonia.PixelPoint point = new Avalonia.PixelPoint(x, y);
                Screen? containingScreen = GetScreenContainingPoint(point, screens);

                if (containingScreen != null)
                {
                    Avalonia.PixelRect screenBounds = containingScreen.WorkingArea;
                    double windowWidth = this.Width;
                    double windowHeight = this.Height;

                    return (x >= screenBounds.X)
                        && (y >= screenBounds.Y)
                        && (x + windowWidth <= screenBounds.X + screenBounds.Width)
                        && (y + windowHeight <= screenBounds.Y + screenBounds.Height);
                }

                return false;
            }
            catch (InvalidOperationException)
            {
                return (x > WINDOW_POSITION_THRESHOLD_LOW)
                    && (x < WINDOW_POSITION_THRESHOLD_HIGH)
                    && (y > WINDOW_POSITION_THRESHOLD_LOW)
                    && (y < WINDOW_POSITION_THRESHOLD_HIGH);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error validating window position ({x}, {y}): {ex.Message}");
                return (x > WINDOW_POSITION_THRESHOLD_LOW)
                    && (x < WINDOW_POSITION_THRESHOLD_HIGH)
                    && (y > WINDOW_POSITION_THRESHOLD_LOW)
                    && (y < WINDOW_POSITION_THRESHOLD_HIGH);
            }
        }

        /// <summary>
        /// Gets the screen that contains the specified point.
        /// </summary>
        /// <param name="point">The point to check.</param>
        /// <param name="screens">The screens collection to search.</param>
        /// <returns>The screen containing the point, or null if not found.</returns>
        private Screen? GetScreenContainingPoint(Avalonia.PixelPoint point, Screens screens)
        {
            try
            {
                Screen? screen = screens.ScreenFromPoint(point);
                if (screen != null)
                    return screen;

                foreach (Screen screenItem in screens.All)
                {
                    if (screenItem.Bounds.Contains(point))
                    {
                        return screenItem;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error finding screen for point ({point.X}, {point.Y}): {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Clamps coordinates to ensure they're within screen bounds.
        /// If coordinates are outside all screens, clamps to the primary screen or first available screen.
        /// Must be called from UI thread.
        /// </summary>
        /// <param name="x">The X coordinate to clamp.</param>
        /// <param name="y">The Y coordinate to clamp.</param>
        /// <returns>A clamped PixelPoint that's guaranteed to be within screen bounds.</returns>
        public Avalonia.PixelPoint ClampToScreenBounds(int x, int y)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Debug.WriteLine("ClampToScreenBounds called from non-UI thread, using default position");
                return new Avalonia.PixelPoint(100, 100);
            }

            try
            {
                Screens? screens = this.Screens;
                if (screens == null || screens.All.Count == 0)
                {
                    Debug.WriteLine("No screens available, using default position (100, 100)");
                    return new Avalonia.PixelPoint(100, 100);
                }

                Avalonia.PixelPoint point = new Avalonia.PixelPoint(x, y);
                Screen? containingScreen = GetScreenContainingPoint(point, screens);

                if (containingScreen != null)
                {
                    Avalonia.PixelRect workingArea = containingScreen.WorkingArea;
                    double windowWidth = this.Width;
                    double windowHeight = this.Height;

                    int clampedX = x;
                    int clampedY = y;

                    if (clampedX < workingArea.X)
                        clampedX = workingArea.X;
                    else if (clampedX + windowWidth > workingArea.X + workingArea.Width)
                        clampedX = (int)(workingArea.X + workingArea.Width - windowWidth);

                    if (clampedY < workingArea.Y)
                        clampedY = workingArea.Y;
                    else if (clampedY + windowHeight > workingArea.Y + workingArea.Height)
                        clampedY = (int)(workingArea.Y + workingArea.Height - windowHeight);

                    if (clampedX < workingArea.X)
                        clampedX = workingArea.X;
                    if (clampedY < workingArea.Y)
                        clampedY = workingArea.Y;

                    return new Avalonia.PixelPoint(clampedX, clampedY);
                }

                Screen? targetScreen = screens.Primary ?? screens.All.FirstOrDefault();
                if (targetScreen != null)
                {
                    Avalonia.PixelRect workingArea = targetScreen.WorkingArea;
                    double windowWidth = this.Width;
                    double windowHeight = this.Height;

                    int clampedX = workingArea.X;
                    int clampedY = workingArea.Y;

                    Debug.WriteLine($"Position ({x}, {y}) outside all screens, clamped to primary screen: ({clampedX}, {clampedY})");
                    return new Avalonia.PixelPoint(clampedX, clampedY);
                }

                Debug.WriteLine("No screens available for clamping, using default position (100, 100)");
                return new Avalonia.PixelPoint(100, 100);
            }
            catch (InvalidOperationException)
            {
                Debug.WriteLine("ClampToScreenBounds: Invalid thread access, using default position");
                return new Avalonia.PixelPoint(100, 100);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error clamping coordinates ({x}, {y}): {ex.Message}");
                return new Avalonia.PixelPoint(100, 100);
            }
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
                Avalonia.PixelPoint positionToSave = _lastKnownPosition;

                try
                {
                    Avalonia.PixelPoint currentPosition = this.Position;
                    if (IsValidWindowPosition(currentPosition.X, currentPosition.Y))
                    {
                        positionToSave = currentPosition;
                        _lastKnownPosition = currentPosition;
                    }
                    else
                    {
                        // Position is invalid - clamp it before saving
                        Debug.WriteLine($"SaveThumbnailSettings: Current position invalid ({currentPosition.X}, {currentPosition.Y}), clamping to screen bounds");
                        positionToSave = ClampToScreenBounds(currentPosition.X, currentPosition.Y);
                        _lastKnownPosition = positionToSave;
                        // Update window position to the clamped value
                        this.Position = positionToSave;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"SaveThumbnailSettings: Error getting current position: {ex.Message}, using last known: X={_lastKnownPosition.X}, Y={_lastKnownPosition.Y}");
                }

                ThumbnailConfig config = new ThumbnailConfig
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

        /// <summary>
        /// Creates the overlay window that displays the border and title on top of the thumbnail.
        /// </summary>
        private void CreateOverlayWindow()
        {
            if (_overlayWindow != null)
            {
                _overlayWindow.Close();
            }

            _overlayWindow = new ThumbnailOverlayWindow(ViewModel);
            _overlayWindow.Topmost = true;

            try
            {
                Avalonia.Platform.IPlatformHandle? platformHandle = this.TryGetPlatformHandle();
                if (platformHandle != null && platformHandle.Handle != IntPtr.Zero)
                {
                    _overlayWindow.SetThumbnailWindowHandle(platformHandle.Handle);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set thumbnail window handle on overlay: {ex.Message}");
            }

            SyncOverlayWindow();

            _overlayWindow.Show();

            System.Timers.Timer timer = new System.Timers.Timer(100);
            timer.Elapsed += (s, e) =>
            {
                timer.Stop();
                timer.Dispose();
                Dispatcher.UIThread.Post(() =>
                {
                    if (_overlayWindow != null)
                    {
                        _overlayWindow.SyncWithThumbnailWindow(this.Position, this.Width, this.Height);
                    }
                });
            };
            timer.AutoReset = false;
            timer.Start();
        }

        /// <summary>
        /// Synchronizes the overlay window's position and size with the thumbnail window.
        /// </summary>
        private void SyncOverlayWindow()
        {
            if (_overlayWindow != null)
            {
                _overlayWindow.SyncWithThumbnailWindow(this.Position, this.Width, this.Height);
            }
        }

        /// <summary>
        /// Handles position changes to synchronize the overlay window.
        /// </summary>
        private void ThumbnailWindow_PositionChanged(object? sender, PixelPointEventArgs e)
        {
            if (!_isUpdatingProgrammatically)
            {
                SyncOverlayWindow();
            }
        }

        /// <summary>
        /// Handles size changes to synchronize the overlay window.
        /// </summary>
        private void ThumbnailWindow_SizeChanged(object? sender, SizeChangedEventArgs e)
        {
            SyncOverlayWindow();
        }

        /// <summary>
        /// Handles window activation to ensure overlay window stays on top.
        /// </summary>
        private void ThumbnailWindow_Activated(object? sender, EventArgs e)
        {
            BringOverlayToTop();
        }

        /// <summary>
        /// Brings the overlay window to the top.
        /// </summary>
        private void BringOverlayToTop()
        {
            if (_overlayWindow != null)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (_overlayWindow != null)
                    {
                        _overlayWindow.SyncWithThumbnailWindow(this.Position, this.Width, this.Height);
                        _overlayWindow.BringToTop();
                    }
                }, Avalonia.Threading.DispatcherPriority.Normal);
            }
        }
    }
}


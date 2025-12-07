using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using YAEP.Models;

namespace YAEP.Views.Windows
{
    /// <summary>
    /// Control that hosts a DWM thumbnail for live window preview.
    /// </summary>
    public partial class ThumbnailControl : UserControl
    {
        private Thumbnail? _thumbnail;
        private IntPtr _processHandle = IntPtr.Zero;
        private double _opacity = 1.0;
        private int _borderThickness = 0;
        private bool _isFocused = false;

        public ThumbnailControl()
        {
            InitializeComponent();
            this.Loaded += ThumbnailControl_Loaded;
            this.Unloaded += ThumbnailControl_Unloaded;
            this.SizeChanged += ThumbnailControl_SizeChanged;
        }

        private void ThumbnailControl_Loaded(object? sender, RoutedEventArgs e)
        {
            CreateLiveThumbnail();
        }

        private void ThumbnailControl_SizeChanged(object? sender, SizeChangedEventArgs e)
        {
            UpdateThumbnailSize();
        }

        /// <summary>
        /// Sets the source process handle for the thumbnail.
        /// </summary>
        public void SetProcessHandle(IntPtr processHandle)
        {
            _processHandle = processHandle;
            if (IsLoaded && _processHandle != IntPtr.Zero)
            {
                CreateLiveThumbnail();
            }
        }

        /// <summary>
        /// Sets the opacity of the thumbnail.
        /// </summary>
        public void SetOpacity(double opacity)
        {
            _opacity = opacity;
            if (_thumbnail != null)
            {
                _thumbnail.SetOpacity(_opacity);
                _thumbnail.Update();
            }
        }

        /// <summary>
        /// Sets the border thickness to account for when sizing the DWM thumbnail.
        /// </summary>
        public void SetBorderThickness(int borderThickness)
        {
            _borderThickness = borderThickness;
            UpdateThumbnailSize();
        }

        /// <summary>
        /// Sets whether the window is focused. When focused, the thumbnail is resized to show the border.
        /// </summary>
        public void SetIsFocused(bool isFocused)
        {
            _isFocused = isFocused;
            UpdateThumbnailSize();
        }

        /// <summary>
        /// Creates and registers the live thumbnail with DWM.
        /// </summary>
        private void CreateLiveThumbnail()
        {
            if (_processHandle == IntPtr.Zero)
            {
                Debug.WriteLine("ThumbnailControl: Source window handle is invalid (IntPtr.Zero)");
                return;
            }

            // Get the parent window handle (ThumbnailWindow)
            Window? parentWindow = this.GetVisualAncestors().OfType<Window>().FirstOrDefault();
            if (parentWindow == null)
            {
                Debug.WriteLine("ThumbnailControl: Parent window is null");
                return;
            }

            // Get the native window handle for Avalonia
            Avalonia.Platform.IPlatformHandle? platformHandle = parentWindow.TryGetPlatformHandle();
            if (platformHandle == null)
            {
                Debug.WriteLine(
                    "ThumbnailControl: Could not get platform handle for parent window");
                // Try again when the window is fully loaded
                parentWindow.Opened += (s, e) =>
                {
                    platformHandle = parentWindow.TryGetPlatformHandle();
                    if (platformHandle != null)
                    {
                        RegisterThumbnail(platformHandle.Handle);
                    }
                };
                return;
            }

            IntPtr destinationHandle = platformHandle.Handle;
            if (destinationHandle == IntPtr.Zero)
            {
                Debug.WriteLine(
                    "ThumbnailControl: Destination window handle is invalid (IntPtr.Zero)");
                return;
            }

            RegisterThumbnail(destinationHandle);
        }

        private void RegisterThumbnail(IntPtr destinationHandle)
        {
            // Unregister existing thumbnail if any
            if (_thumbnail != null)
            {
                _thumbnail.Unregister();
            }

            _thumbnail = new Thumbnail();
            _thumbnail.Register(destinationHandle, _processHandle);

            // Set thumbnail opacity
            _thumbnail.SetOpacity(_opacity);

            // Set initial size
            UpdateThumbnailSize();
        }

        /// <summary>
        /// Updates the thumbnail size to match the control size, accounting for border thickness when focused.
        /// </summary>
        private void UpdateThumbnailSize()
        {
            if (_thumbnail != null && this.Bounds.Width > 0 && this.Bounds.Height > 0)
            {
                int thumbnailX = 0;
                int thumbnailY = 0;
                int thumbnailWidth = (int)this.Bounds.Width;
                int thumbnailHeight = (int)this.Bounds.Height;

                // Only adjust size to leave space for the border when focused
                if (_isFocused && _borderThickness > 0)
                {
                    // Adjust size to leave space for the border on all sides
                    int borderOffset = _borderThickness * 2; // Border on both left/right and top/bottom
                    thumbnailWidth = Math.Max(1, (int)this.Bounds.Width - borderOffset);
                    thumbnailHeight = Math.Max(1, (int)this.Bounds.Height - borderOffset);
                    
                    // Position the thumbnail to be centered, accounting for the border
                    thumbnailX = _borderThickness;
                    thumbnailY = _borderThickness;
                }
                
                _thumbnail.Move(thumbnailX, thumbnailY, thumbnailWidth, thumbnailHeight);
                _thumbnail.Update();
            }
        }

        private void ThumbnailControl_Unloaded(object? sender, RoutedEventArgs e)
        {
            UnregisterThumbnail();
        }

        /// <summary>
        /// Unregisters the thumbnail from DWM.
        /// </summary>
        public void UnregisterThumbnail()
        {
            if (_thumbnail != null)
            {
                _thumbnail.Unregister();
                _thumbnail = null;
            }
        }
    }
}

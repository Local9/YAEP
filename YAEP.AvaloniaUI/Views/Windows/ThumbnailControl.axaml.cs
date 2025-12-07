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
        /// Updates the thumbnail size to match the control size.
        /// The thumbnail always fills the entire control since the border is handled by a separate overlay window.
        /// </summary>
        private void UpdateThumbnailSize()
        {
            if (_thumbnail != null && this.Bounds.Width > 0 && this.Bounds.Height > 0)
            {
                // Always fill the entire control - border is handled by separate overlay window
                _thumbnail.Move(0, 0, (int)this.Bounds.Width, (int)this.Bounds.Height);
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

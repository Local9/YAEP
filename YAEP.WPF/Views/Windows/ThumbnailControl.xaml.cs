using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Interop;
using YAEP.Models;

namespace YAEP.Views.Windows
{
    /// <summary>
    /// Interaction logic for ThumbnailControl.xaml
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

        private void ThumbnailControl_Loaded(object sender, RoutedEventArgs e)
        {
            CreateLiveThumbnail();
        }

        private void ThumbnailControl_SizeChanged(object sender, SizeChangedEventArgs e)
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
            Window? parentWindow = Window.GetWindow(this);
            if (parentWindow == null)
            {
                Debug.WriteLine("ThumbnailControl: Parent window is null");
                return;
            }

            IntPtr destinationHandle = new WindowInteropHelper(parentWindow).Handle;
            if (destinationHandle == IntPtr.Zero)
            {
                Debug.WriteLine("ThumbnailControl: Destination window handle is invalid (IntPtr.Zero)");
                return;
            }

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
        /// </summary>
        private void UpdateThumbnailSize()
        {
            if (_thumbnail != null && this.ActualWidth > 0 && this.ActualHeight > 0)
            {
                _thumbnail.Move(0, 0, (int)this.ActualWidth, (int)this.ActualHeight);
                _thumbnail.Update();
            }
        }

        private void ThumbnailControl_Unloaded(object sender, RoutedEventArgs e)
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


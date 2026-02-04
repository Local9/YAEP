using ThumbnailConstants = YAEP.ThumbnailConstants;

namespace YAEP.ViewModels.Windows
{
    public partial class ThumbnailWindowViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _applicationTitle = "YAEP - Thumbnail";

        [ObservableProperty]
        private bool _isAlwaysOnTop;

        [ObservableProperty]
        private int _height = ThumbnailConstants.DefaultThumbnailHeight;

        [ObservableProperty]
        private int _width = ThumbnailConstants.DefaultThumbnailWidth;

        [ObservableProperty]
        private double _opacity = 1.0;

        [ObservableProperty]
        private bool _isBorderless;

        [ObservableProperty]
        private bool _isFocused;

        [ObservableProperty]
        private string _focusBorderColor = ThumbnailConstants.DefaultFocusBorderColor;

        [ObservableProperty]
        private int _focusBorderThickness = ThumbnailConstants.DefaultFocusBorderThickness;

        [ObservableProperty]
        private string _displayTitle = string.Empty;

        [ObservableProperty]
        private bool _showTitleOverlay = true;

        public IntPtr ProcessHandle { get; set; }

        public ThumbnailWindowViewModel(string applicationTitle)
        {
            _applicationTitle = applicationTitle;
            // Remove EVE window title prefix from the window title for display
            _displayTitle = applicationTitle.StartsWith(YAEP.EveWindowTitleConstants.EveWindowTitlePrefix, StringComparison.OrdinalIgnoreCase)
                ? applicationTitle.Substring(6)
                : applicationTitle;
        }
    }
}


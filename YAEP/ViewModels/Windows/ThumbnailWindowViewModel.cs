using System;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using YAEP.ViewModels;

namespace YAEP.ViewModels.Windows
{
    public partial class ThumbnailWindowViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _applicationTitle = "YAEP - Thumbnail";

        [ObservableProperty]
        private bool _isAlwaysOnTop;

        [ObservableProperty]
        private int _height = 300;

        [ObservableProperty]
        private int _width = 400;

        [ObservableProperty]
        private double _opacity = 1.0;

        [ObservableProperty]
        private bool _isBorderless;

        [ObservableProperty]
        private bool _isFocused;

        [ObservableProperty]
        private string _focusBorderColor = "#0078D4";

        [ObservableProperty]
        private int _focusBorderThickness = 3;

        [ObservableProperty]
        private string _displayTitle = string.Empty;

        [ObservableProperty]
        private bool _showTitleOverlay = true;

        public IntPtr ProcessHandle { get; set; }

        public ThumbnailWindowViewModel(string applicationTitle)
        {
            _applicationTitle = applicationTitle;
            // Remove "EVE - " prefix from the window title for display
            _displayTitle = applicationTitle.StartsWith("EVE - ", StringComparison.OrdinalIgnoreCase)
                ? applicationTitle.Substring(6)
                : applicationTitle;
        }
    }
}


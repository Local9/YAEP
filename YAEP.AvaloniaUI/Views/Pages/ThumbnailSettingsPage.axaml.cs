using Avalonia.Controls;
using YAEP.ViewModels.Pages;

namespace YAEP.Views.Pages
{
    public partial class ThumbnailSettingsPage : UserControl
    {
        public ThumbnailSettingsViewModel ViewModel { get; }

        public ThumbnailSettingsPage()
        {
            InitializeComponent();
        }

        public ThumbnailSettingsPage(ThumbnailSettingsViewModel viewModel) : this()
        {
            ViewModel = viewModel;
            DataContext = viewModel;
        }
    }
}


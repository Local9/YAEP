using Avalonia.Controls;
using YAEP.ViewModels.Pages;

namespace YAEP.Views.Windows
{
    public partial class EditThumbnailWindow : Window
    {
        public ThumbnailSettingsViewModel ViewModel { get; }

        public EditThumbnailWindow(ThumbnailSettingsViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = viewModel;
            InitializeComponent();
        }
    }
}


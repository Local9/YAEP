using Wpf.Ui.Abstractions.Controls;
using YAEP.ViewModels.Pages;

namespace YAEP.Views.Pages
{
    public partial class ThumbnailSettingsPage : INavigableView<ThumbnailSettingsViewModel>
    {
        public ThumbnailSettingsViewModel ViewModel { get; }

        public ThumbnailSettingsPage(ThumbnailSettingsViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();
        }
    }
}


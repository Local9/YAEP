using SukiUI.Controls;
using YAEP.ViewModels.Pages;

namespace YAEP.Views.Windows
{
    public partial class EditMumbleLinkWindow : SukiWindow
    {
        public MumbleLinksViewModel ViewModel { get; }

        public EditMumbleLinkWindow(MumbleLinksViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = viewModel;
            InitializeComponent();
        }
    }
}


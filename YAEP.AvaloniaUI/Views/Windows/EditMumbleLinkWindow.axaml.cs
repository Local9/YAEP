using Avalonia.Controls;
using YAEP.ViewModels.Pages;

namespace YAEP.Views.Windows
{
    public partial class EditMumbleLinkWindow : Window
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


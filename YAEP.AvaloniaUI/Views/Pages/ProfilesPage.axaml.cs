using Avalonia.Controls;
using YAEP.ViewModels.Pages;

namespace YAEP.Views.Pages
{
    public partial class ProfilesPage : UserControl
    {
        public ProfilesViewModel ViewModel { get; } = null!;

        public ProfilesPage()
        {
            InitializeComponent();
        }

        public ProfilesPage(ProfilesViewModel viewModel) : this()
        {
            ViewModel = viewModel;
            DataContext = viewModel;
        }
    }
}


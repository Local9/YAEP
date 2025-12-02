using Wpf.Ui.Abstractions.Controls;
using YAEP.ViewModels.Pages;

namespace YAEP.Views.Pages
{
    public partial class ProfilesPage : INavigableView<ProfilesViewModel>
    {
        public ProfilesViewModel ViewModel { get; }

        public ProfilesPage(ProfilesViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();
        }
    }
}


using Avalonia.Controls;
using YAEP.ViewModels.Pages;

namespace YAEP.Views.Pages
{
    public partial class EveOnlineProfilesPage : UserControl
    {
        public EveOnlineProfilesViewModel ViewModel { get; } = null!;

        public EveOnlineProfilesPage()
        {
            InitializeComponent();
        }

        public EveOnlineProfilesPage(EveOnlineProfilesViewModel viewModel) : this()
        {
            ViewModel = viewModel;
            DataContext = viewModel;
        }
    }
}

using Avalonia.Controls;
using YAEP.ViewModels.Pages;

namespace YAEP.Views.Pages
{
    public partial class ClientGroupingPage : UserControl
    {
        public ClientGroupingViewModel ViewModel { get; }

        public ClientGroupingPage()
        {
            InitializeComponent();
        }

        public ClientGroupingPage(ClientGroupingViewModel viewModel) : this()
        {
            ViewModel = viewModel;
            DataContext = viewModel;
        }
    }
}


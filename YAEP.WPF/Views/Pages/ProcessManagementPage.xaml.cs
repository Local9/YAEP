using Wpf.Ui.Abstractions.Controls;
using YAEP.ViewModels.Pages;

namespace YAEP.Views.Pages
{
    public partial class ProcessManagementPage : INavigableView<ProcessManagementViewModel>
    {
        public ProcessManagementViewModel ViewModel { get; }

        public ProcessManagementPage(ProcessManagementViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();
        }
    }
}


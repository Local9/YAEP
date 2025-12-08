using Avalonia.Controls;
using YAEP.ViewModels.Pages;

namespace YAEP.Views.Pages
{
    public partial class ProcessManagementPage : UserControl
    {
        public ProcessManagementViewModel ViewModel { get; } = null!;

        public ProcessManagementPage()
        {
            InitializeComponent();
        }

        public ProcessManagementPage(ProcessManagementViewModel viewModel) : this()
        {
            ViewModel = viewModel;
            DataContext = viewModel;
        }
    }
}


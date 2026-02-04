using SukiUI.Controls;
using YAEP.ViewModels.Windows;

namespace YAEP.Views.Windows
{
    public partial class GroupLinksWindow : SukiWindow
    {
        public GroupLinksWindowViewModel ViewModel { get; }

        public GroupLinksWindow(GroupLinksWindowViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = viewModel;
            InitializeComponent();
            Title = $"Group: {viewModel.Group.Name}";
        }
    }
}

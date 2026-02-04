using SukiUI.Controls;
using YAEP.ViewModels.Pages;

namespace YAEP.Views.Windows
{
    public partial class EditServerGroupWindow : SukiWindow
    {
        public MumbleServerGroupsViewModel ViewModel { get; }

        public EditServerGroupWindow()
        {
            ViewModel = null!;
            DataContext = null;
            InitializeComponent();
        }

        public EditServerGroupWindow(MumbleServerGroupsViewModel viewModel, bool isNew)
        {
            ViewModel = viewModel;
            DataContext = viewModel;
            InitializeComponent();
            Title = isNew ? "Add server group" : "Edit server group";
            if (TitleText != null)
                TitleText.Text = isNew ? "Add server group" : "Edit server group";
        }
    }
}

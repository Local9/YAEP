using Avalonia.Controls;
using YAEP.ViewModels.Pages;

namespace YAEP.Views.Pages
{
    public partial class DrawerSettingsPage : UserControl
    {
        public DrawerSettingsViewModel ViewModel { get; } = null!;

        public DrawerSettingsPage()
        {
            InitializeComponent();
        }

        public DrawerSettingsPage(DrawerSettingsViewModel viewModel) : this()
        {
            ViewModel = viewModel;
            DataContext = viewModel;
        }
    }
}

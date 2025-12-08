using Avalonia.Controls;
using YAEP.ViewModels.Pages;

namespace YAEP.Views.Pages
{
    public partial class SettingsPage : UserControl
    {
        public SettingsViewModel ViewModel { get; } = null!;

        public SettingsPage()
        {
            InitializeComponent();
        }

        public SettingsPage(SettingsViewModel viewModel) : this()
        {
            ViewModel = viewModel;
            DataContext = viewModel;
        }
    }
}


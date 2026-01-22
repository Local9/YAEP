using Avalonia.Interactivity;
using SukiUI.Controls;
using System.Runtime.Versioning;
using YAEP.ViewModels.Pages;

namespace YAEP.Views.Windows
{
    [SupportedOSPlatform("windows")]
    public partial class CopyProfileWindow : SukiWindow
    {
        public EveOnlineProfilesViewModel ViewModel { get; }

        public CopyProfileWindow()
        {
            ViewModel = null!;
            DataContext = null;
            InitializeComponent();
        }

        public CopyProfileWindow(EveOnlineProfilesViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = viewModel;
            InitializeComponent();
        }

        private void CopyButton_Click(object? sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(ViewModel.NewProfileName))
            {
                ViewModel.PerformProfileCopy(ViewModel.NewProfileName);
                ViewModel.NewProfileName = string.Empty; // Reset after copy
            }
        }

        private void CancelButton_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}

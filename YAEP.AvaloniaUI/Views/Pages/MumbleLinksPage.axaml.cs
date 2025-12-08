using Avalonia.Controls;
using Avalonia.Interactivity;
using YAEP.Services;
using YAEP.ViewModels.Pages;

namespace YAEP.Views.Pages
{
    public partial class MumbleLinksPage : UserControl
    {
        public MumbleLinksViewModel ViewModel { get; } = null!;

        public MumbleLinksPage()
        {
            InitializeComponent();
        }

        public MumbleLinksPage(MumbleLinksViewModel viewModel) : this()
        {
            ViewModel = viewModel;
            DataContext = viewModel;
        }

        private void CheckBox_IsCheckedChanged(object? sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && 
                checkBox.DataContext is DatabaseService.MumbleLink link && 
                ViewModel != null)
            {
                bool? newValue = checkBox.IsChecked;
                bool newSelection = newValue == true;
                
                if (newSelection != link.IsSelected)
                {
                    ViewModel.ToggleLinkSelectionCommand.Execute(link);
                }
            }
        }
    }
}


using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using YAEP.Models;
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

        public MumbleLinksPage(MumbleLinksViewModel viewModel, DrawerSettingsViewModel? drawerSettingsViewModel = null) : this()
        {
            ViewModel = viewModel;
            if (drawerSettingsViewModel != null)
            {
                viewModel.DrawerSettingsViewModel = drawerSettingsViewModel;
            }
            DataContext = viewModel;
        }

        private void CheckBox_IsCheckedChanged(object? sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox &&
                checkBox.DataContext is MumbleLink link &&
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

        private void LinksDataGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (ViewModel == null || sender is not DataGrid grid)
                return;

            var selected = grid.SelectedItems?.OfType<MumbleLink>().ToList() ?? new List<MumbleLink>();
            ViewModel.SetSelectedLinks(selected);
        }
    }
}


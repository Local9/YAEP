using System.Collections.ObjectModel;
using Wpf.Ui.Controls;

namespace YAEP.ViewModels.Windows
{
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _applicationTitle = "YAEP - Yet Another EVE Preview";

        [ObservableProperty]
        private ObservableCollection<object> _menuItems = new()
        {

            new NavigationViewItem()
            {
                Content = "Profiles",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Person24 },
                TargetPageType = typeof(Views.Pages.ProfilesPage)
            },
            new NavigationViewItem()
            {
                Content = "Thumbnail Settings",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Image24 },
                TargetPageType = typeof(Views.Pages.ThumbnailSettingsPage)
            },
            new NavigationViewItem()
            {
                Content = "Client Grouping",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Group24 },
                TargetPageType = typeof(Views.Pages.ClientGroupingPage)
            },
            new NavigationViewItem()
            {
                Content = "Grid Layout",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Grid24 },
                TargetPageType = typeof(Views.Pages.GridLayoutPage)
            },
            new NavigationViewItem()
            {
                Content = "Process Management",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Apps24 },
                TargetPageType = typeof(Views.Pages.ProcessManagementPage)
            }
        };

        [ObservableProperty]
        private ObservableCollection<object> _footerMenuItems = new()
        {
            new NavigationViewItem()
            {
                Content = "Settings",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Settings24 },
                TargetPageType = typeof(Views.Pages.SettingsPage)
            }
        };

        [ObservableProperty]
        private ObservableCollection<MenuItem> _trayMenuItems = new()
        {
            new MenuItem { Header = "Home", Tag = "tray_home" }
        };
    }
}

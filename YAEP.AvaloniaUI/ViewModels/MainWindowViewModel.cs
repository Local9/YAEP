using System.Collections.ObjectModel;
using YAEP.Models;
using YAEP.Views.Pages;

namespace YAEP.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _applicationTitle = "YAEP - Yet Another EVE Preview";

        [ObservableProperty]
        private ObservableCollection<NavigationItem> _menuItems = new()
        {
            new NavigationItem { Title = "Profiles", PageType = typeof(ProfilesPage), Icon = "👤" },
            new NavigationItem { Title = "Thumbnail Settings", PageType = typeof(ThumbnailSettingsPage), Icon = "🖼️" },
            new NavigationItem { Title = "Client Grouping", PageType = typeof(ClientGroupingPage), Icon = "👥" },
            new NavigationItem { Title = "Grid Layout", PageType = typeof(GridLayoutPage), Icon = "📐" },
            new NavigationItem { Title = "Process Management", PageType = typeof(ProcessManagementPage), Icon = "⚙️" },
            new NavigationItem { Title = "Mumble Links", PageType = typeof(MumbleLinksPage), Icon = "🔗" }
        };

        [ObservableProperty]
        private ObservableCollection<NavigationItem> _footerMenuItems = new()
        {
            new NavigationItem { Title = "Settings", PageType = typeof(SettingsPage), Icon = "⚙️" }
        };

        [ObservableProperty]
        private NavigationItem? _selectedMenuItem;

        [ObservableProperty]
        private object? _currentPage;

        partial void OnSelectedMenuItemChanged(NavigationItem? value)
        {
            if (value != null && value.PageType != null)
            {
                // Navigation will be handled by MainWindow code-behind
                CurrentPage = value;
            }
        }

        [RelayCommand]
        private void OpenUrl(string? url)
        {
            if (!string.IsNullOrWhiteSpace(url))
            {
                MumbleLink.OpenLink(url);
            }
        }
    }

    public class NavigationItem
    {
        public string Title { get; set; } = string.Empty;
        public Type? PageType { get; set; }
        public string Icon { get; set; } = string.Empty;
    }
}

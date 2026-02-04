using System.Collections.ObjectModel;
using Avalonia.Controls;
using YAEP.Models;
using YAEP.Services;
using YAEP.ViewModels.Windows;
using YAEP.Views.Windows;

namespace YAEP.ViewModels.Pages
{
    public partial class MumbleServerGroupsViewModel : ViewModelBase
    {
        private readonly DatabaseService _databaseService;
        private readonly HotkeyService? _hotkeyService;

        [ObservableProperty]
        private ObservableCollection<MumbleServerGroup> _serverGroups = new();

        [ObservableProperty]
        private string _newGroupName = string.Empty;

        [ObservableProperty]
        private MumbleServerGroup? _editingGroup;

        [ObservableProperty]
        private string _editingGroupName = string.Empty;

        private EditServerGroupWindow? _editWindow;
        private GroupLinksWindow? _groupLinksWindow;

        public MumbleServerGroupsViewModel(DatabaseService databaseService, HotkeyService? hotkeyService = null)
        {
            _databaseService = databaseService;
            _hotkeyService = hotkeyService;
        }

        public void OnNavigatedTo()
        {
            LoadGroups();
        }

        public void OnNavigatedFrom()
        {
            _editWindow?.Close();
            _groupLinksWindow?.Close();
        }

        private void LoadGroups()
        {
            var groups = _databaseService.GetMumbleServerGroups();
            ServerGroups.Clear();
            foreach (var g in groups)
                ServerGroups.Add(g);
        }

        [RelayCommand]
        private void OnAddGroup()
        {
            EditingGroup = null;
            EditingGroupName = string.Empty;
            ShowEditWindow(isNew: true);
        }

        [RelayCommand]
        private void OnEditGroup(MumbleServerGroup? group)
        {
            if (group == null)
                return;
            EditingGroup = group;
            EditingGroupName = group.Name;
            ShowEditWindow(isNew: false);
        }

        [RelayCommand]
        private void OnDeleteGroup(MumbleServerGroup? group)
        {
            if (group == null)
                return;
            _databaseService.DeleteMumbleServerGroup(group.Id);
            LoadGroups();
        }

        [RelayCommand]
        private void OnOpenGroup(MumbleServerGroup? group)
        {
            if (group == null)
                return;

            Window? owner = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            var vm = new GroupLinksWindowViewModel(_databaseService, _hotkeyService, group, owner);
            var window = new GroupLinksWindow(vm);
            _groupLinksWindow = window;

            if (owner != null)
            {
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.ShowDialog(owner);
            }
            else
            {
                window.Show();
            }

            window.Closed += (_, _) =>
            {
                _groupLinksWindow = null;
            };
        }

        private void ShowEditWindow(bool isNew)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_editWindow != null)
                {
                    _editWindow.Activate();
                    return;
                }

                Window? owner = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                    ? desktop.MainWindow
                    : null;

                _editWindow = new EditServerGroupWindow(this, isNew);
                if (owner != null)
                {
                    _editWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    _editWindow.ShowDialog(owner);
                }
                else
                {
                    _editWindow.Show();
                }

                _editWindow.Closed += (_, _) =>
                {
                    _editWindow = null;
                    EditingGroup = null;
                    EditingGroupName = string.Empty;
                };
            });
        }

        [RelayCommand]
        private void SaveEditGroup()
        {
            if (EditingGroup == null)
            {
                if (!string.IsNullOrWhiteSpace(EditingGroupName) && _databaseService.CreateMumbleServerGroup(EditingGroupName.Trim()) != null)
                {
                    LoadGroups();
                    _editWindow?.Close();
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(EditingGroupName))
                {
                    _databaseService.UpdateMumbleServerGroup(EditingGroup.Id, EditingGroupName.Trim());
                    LoadGroups();
                    _editWindow?.Close();
                }
            }
        }

        [RelayCommand]
        private void CancelEditGroup()
        {
            EditingGroup = null;
            EditingGroupName = string.Empty;
            _editWindow?.Close();
        }
    }
}

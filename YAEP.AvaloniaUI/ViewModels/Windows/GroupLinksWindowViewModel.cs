using Avalonia.Controls;
using System.Collections.ObjectModel;
using System.Runtime.Versioning;
using YAEP.Models;
using YAEP.ViewModels.Pages;
using YAEP.Views.Windows;

namespace YAEP.ViewModels.Windows
{
    public partial class GroupLinksWindowViewModel : ViewModelBase
    {
        private readonly DatabaseService _databaseService;
        private readonly HotkeyService? _hotkeyService;
        private readonly Avalonia.Controls.Window? _ownerWindow;
        private EditMumbleLinkWindow? _editWindow;

        public MumbleServerGroup Group { get; }

        [ObservableProperty]
        private ObservableCollection<MumbleLink> _links = new();

        [ObservableProperty]
        private string _pickerSearchText = string.Empty;

        [ObservableProperty]
        private ObservableCollection<MumbleLink> _pickerSuggestions = new();

        [ObservableProperty]
        private MumbleLink? _selectedPickerLink;

        partial void OnPickerSearchTextChanged(string value)
        {
            RefreshPickerSuggestions();
        }

        public GroupLinksWindowViewModel(DatabaseService databaseService, HotkeyService? hotkeyService, MumbleServerGroup group, Avalonia.Controls.Window? ownerWindow)
        {
            _databaseService = databaseService;
            _hotkeyService = hotkeyService;
            _ownerWindow = ownerWindow;
            Group = group;
            LoadLinks();
            RefreshPickerSuggestions();
        }

        private void RefreshPickerSuggestions()
        {
            List<MumbleLink> list = _databaseService.GetMumbleLinksForPicker(Group.Id, PickerSearchText);
            PickerSuggestions.Clear();
            foreach (MumbleLink link in list)
                PickerSuggestions.Add(link);
        }

        private void LoadLinks()
        {
            List<MumbleLink> list = _databaseService.GetMumbleLinks(Group.Id);
            Links.Clear();
            foreach (MumbleLink link in list)
                Links.Add(link);
        }

        [RelayCommand]
        private void OnAddSelectedLinkToGroup()
        {
            if (SelectedPickerLink == null)
                return;

            _databaseService.AddLinkToGroup(SelectedPickerLink.Id, Group.Id);
            LoadLinks();
            PickerSearchText = string.Empty;
            SelectedPickerLink = null;
            RefreshPickerSuggestions();
        }

        [RelayCommand]
        private void OnOpenLink(MumbleLink? link)
        {
            link?.OpenLink();
        }

        [RelayCommand]
        private void OnRemoveFromGroup(MumbleLink? link)
        {
            if (link == null)
                return;
            _databaseService.RemoveLinkFromGroup(link.Id, Group.Id);
            Links.Remove(link);
        }

        [RelayCommand]
        [SupportedOSPlatform("windows")]
        private void OnEditLink(MumbleLink? link)
        {
            if (link == null)
                return;

            MumbleLinksViewModel mumbleVm = new MumbleLinksViewModel(_databaseService, _hotkeyService);
            mumbleVm.PrepareEditLink(link);

            Dispatcher.UIThread.Post(() =>
            {
                if (_editWindow != null)
                {
                    _editWindow.Activate();
                    return;
                }

                _editWindow = new EditMumbleLinkWindow(mumbleVm);
                if (_ownerWindow != null)
                {
                    _editWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    _editWindow.ShowDialog(_ownerWindow);
                }
                else
                {
                    _editWindow.Show();
                }

                _editWindow.Closed += (_, _) =>
                {
                    _editWindow = null;
                    LoadLinks();
                    _hotkeyService?.RegisterHotkeys();
                };
            });
        }
    }
}

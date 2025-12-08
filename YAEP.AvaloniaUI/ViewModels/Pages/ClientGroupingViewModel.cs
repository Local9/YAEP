using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using YAEP.Interface;
using YAEP.Services;
using YAEP.ViewModels;
using YAEP.Views.Windows;

namespace YAEP.ViewModels.Pages
{
    public partial class ClientGroupingViewModel : ViewModelBase
    {
        private readonly DatabaseService _databaseService;
        private readonly IThumbnailWindowService _thumbnailWindowService;
        private readonly HotkeyService? _hotkeyService;
        private bool _isInitialized = false;

        [ObservableProperty]
        private List<DatabaseService.ClientGroupWithMembers> _clientGroups = new();

        [ObservableProperty]
        private List<string> _availableClients = new();

        [ObservableProperty]
        private List<string> _ungroupedClients = new();

        [ObservableProperty]
        private DatabaseService.ClientGroupWithMembers? _selectedGroup;

        partial void OnSelectedGroupChanged(DatabaseService.ClientGroupWithMembers? value)
        {
            OnPropertyChanged(nameof(HasActiveGroup));
            OnPropertyChanged(nameof(CurrentGroupForwardHotkey));
            OnPropertyChanged(nameof(CurrentGroupBackwardHotkey));
        }

        partial void OnEditingGroupChanged(DatabaseService.ClientGroup? value)
        {
            OnPropertyChanged(nameof(HasActiveGroup));
            OnPropertyChanged(nameof(CurrentGroupForwardHotkey));
            OnPropertyChanged(nameof(CurrentGroupBackwardHotkey));
        }

        [ObservableProperty]
        private string _newGroupName = string.Empty;

        [ObservableProperty]
        private DatabaseService.ClientGroup? _editingGroup;

        [ObservableProperty]
        private string _editingGroupName = string.Empty;


        [ObservableProperty]
        private bool _isCapturingForwardHotkey = false;

        [ObservableProperty]
        private bool _isCapturingBackwardHotkey = false;

        [ObservableProperty]
        private string _selectedClientToAdd = string.Empty;

        public ClientGroupingViewModel(DatabaseService databaseService, IThumbnailWindowService thumbnailWindowService, HotkeyService? hotkeyService = null)
        {
            _databaseService = databaseService;
            _thumbnailWindowService = thumbnailWindowService;
            _hotkeyService = hotkeyService;

            // Subscribe to thumbnail service events
            _thumbnailWindowService.ThumbnailAdded += OnThumbnailAdded;
            _thumbnailWindowService.ThumbnailRemoved += OnThumbnailRemoved;
        }

        public void OnNavigatedTo()
        {
            if (!_isInitialized)
                InitializeViewModel();

            LoadData();
        }

        public void OnNavigatedFrom()
        {
        }

        private void OnThumbnailAdded(object? sender, YAEP.Interface.ThumbnailWindowChangedEventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                LoadData();
            });
        }

        private void OnThumbnailRemoved(object? sender, YAEP.Interface.ThumbnailWindowChangedEventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                LoadData();
            });
        }

        private void InitializeViewModel()
        {
            _isInitialized = true;
        }

        private void LoadData()
        {
            DatabaseService.Profile? activeProfile = _databaseService.GetActiveProfile() ?? _databaseService.CurrentProfile;
            if (activeProfile == null)
            {
                ClientGroups = new List<DatabaseService.ClientGroupWithMembers>();
                AvailableClients = new List<string>();
                UngroupedClients = new List<string>();
                return;
            }

            // Load groups
            ClientGroups = _databaseService.GetClientGroupsWithMembers(activeProfile.Id);

            // Load all available clients (from thumbnail settings)
            List<DatabaseService.ThumbnailSetting> allThumbnailSettings = _databaseService.GetAllThumbnailSettings(activeProfile.Id);
            List<string> allClients = allThumbnailSettings.Select(s => s.WindowTitle).ToList();

            // Get active thumbnail window titles
            List<string> activeWindowTitles = _thumbnailWindowService.GetActiveThumbnailWindowTitles();
            HashSet<string> activeTitlesSet = new HashSet<string>(activeWindowTitles, StringComparer.OrdinalIgnoreCase);

            // Filter to only active clients
            AvailableClients = allClients.Where(c => activeTitlesSet.Contains(c)).OrderBy(c => c).ToList();

            // Load ungrouped clients
            UngroupedClients = _databaseService.GetUngroupedClients(activeProfile.Id)
                .Where(c => activeTitlesSet.Contains(c))
                .OrderBy(c => c)
                .ToList();

            // Notify that editing group members may have changed
            OnPropertyChanged(nameof(EditingGroupMembers));
            OnPropertyChanged(nameof(CurrentGroupForwardHotkey));
            OnPropertyChanged(nameof(CurrentGroupBackwardHotkey));
        }

        /// <summary>
        /// Gets the forward hotkey for the currently active group (selected or editing).
        /// </summary>
        public string CurrentGroupForwardHotkey
        {
            get
            {
                if (EditingGroup != null)
                {
                    DatabaseService.ClientGroupWithMembers? group = ClientGroups.FirstOrDefault(g => g.Group.Id == EditingGroup.Id);
                    return group?.Group.CycleForwardHotkey ?? string.Empty;
                }
                else if (SelectedGroup != null)
                {
                    return SelectedGroup.Group.CycleForwardHotkey;
                }
                return string.Empty;
            }
        }

        /// <summary>
        /// Gets the backward hotkey for the currently active group (selected or editing).
        /// </summary>
        public string CurrentGroupBackwardHotkey
        {
            get
            {
                if (EditingGroup != null)
                {
                    DatabaseService.ClientGroupWithMembers? group = ClientGroups.FirstOrDefault(g => g.Group.Id == EditingGroup.Id);
                    return group?.Group.CycleBackwardHotkey ?? string.Empty;
                }
                else if (SelectedGroup != null)
                {
                    return SelectedGroup.Group.CycleBackwardHotkey;
                }
                return string.Empty;
            }
        }

        [RelayCommand]
        private void OnCreateGroup()
        {
            if (string.IsNullOrWhiteSpace(NewGroupName))
                return;

            DatabaseService.Profile? activeProfile = _databaseService.GetActiveProfile() ?? _databaseService.CurrentProfile;
            if (activeProfile == null)
                return;

            DatabaseService.ClientGroup? group = _databaseService.CreateClientGroup(activeProfile.Id, NewGroupName);
            if (group != null)
            {
                NewGroupName = string.Empty;
                LoadData();
            }
        }

        /// <summary>
        /// Gets the clients in the group being edited.
        /// </summary>
        public List<DatabaseService.ClientGroupMember> EditingGroupMembers
        {
            get
            {
                if (EditingGroup == null)
                    return new List<DatabaseService.ClientGroupMember>();

                DatabaseService.ClientGroupWithMembers? groupWithMembers = ClientGroups.FirstOrDefault(g => g.Group.Id == EditingGroup.Id);
                return groupWithMembers?.Members.OrderBy(m => m.DisplayOrder).ToList() ?? new List<DatabaseService.ClientGroupMember>();
            }
        }

        /// <summary>
        /// Gets whether there is an active group (either selected or being edited).
        /// </summary>
        public bool HasActiveGroup => SelectedGroup != null || EditingGroup != null;

        [RelayCommand]
        private void OnEditGroup(DatabaseService.ClientGroup? group)
        {
            if (group != null)
            {
                EditingGroup = group;
                EditingGroupName = group.Name;
                OnPropertyChanged(nameof(EditingGroupMembers));
                OnPropertyChanged(nameof(HasActiveGroup));
                OnPropertyChanged(nameof(CurrentGroupForwardHotkey));
                OnPropertyChanged(nameof(CurrentGroupBackwardHotkey));

                // Show edit window
                Dispatcher.UIThread.Post(() =>
                {
                    var window = new EditGroupWindow(this);
                    var mainWindow = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                        ? desktop.MainWindow
                        : null;
                    if (mainWindow != null)
                    {
                        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                        window.ShowDialog(mainWindow);
                    }
                    else
                    {
                        window.Show();
                    }
                });
            }
        }

        [RelayCommand]
        [SupportedOSPlatform("windows")]
        private void OnSaveEditGroup()
        {
            if (EditingGroup != null && !string.IsNullOrWhiteSpace(EditingGroupName))
            {
                _databaseService.UpdateClientGroupName(EditingGroup.Id, EditingGroupName);
                EditingGroup = null;
                EditingGroupName = string.Empty;
                OnPropertyChanged(nameof(EditingGroupMembers));
                OnPropertyChanged(nameof(HasActiveGroup));
                OnPropertyChanged(nameof(CurrentGroupForwardHotkey));
                OnPropertyChanged(nameof(CurrentGroupBackwardHotkey));
                LoadData();
                _hotkeyService?.RegisterHotkeys();
            }
        }

        [RelayCommand]
        private void OnCancelEditGroup()
        {
            EditingGroup = null;
            EditingGroupName = string.Empty;
            OnPropertyChanged(nameof(EditingGroupMembers));
            OnPropertyChanged(nameof(HasActiveGroup));
            OnPropertyChanged(nameof(CurrentGroupForwardHotkey));
            OnPropertyChanged(nameof(CurrentGroupBackwardHotkey));
        }

        [RelayCommand]
        [SupportedOSPlatform("windows")]
        private Task OnDeleteGroup(DatabaseService.ClientGroup? group)
        {
            if (group == null)
                return Task.CompletedTask;

            // TODO: Show confirmation dialog using Avalonia's dialog system
            // For now, proceed without confirmation
            _databaseService.DeleteClientGroup(group.Id);
            if (SelectedGroup?.Group.Id == group.Id)
            {
                SelectedGroup = null;
            }
            if (EditingGroup?.Id == group.Id)
            {
                EditingGroup = null;
                EditingGroupName = string.Empty;
            }
            LoadData();
            _hotkeyService?.RegisterHotkeys();
            return Task.CompletedTask;
        }

        [RelayCommand]
        private void OnAddClientToGroup(string? windowTitle)
        {
            if (string.IsNullOrWhiteSpace(windowTitle))
                return;

            // Check if we're editing a group
            if (EditingGroup != null)
            {
                _databaseService.AddClientToGroup(EditingGroup.Id, windowTitle);
                LoadData();
                OnPropertyChanged(nameof(EditingGroupMembers));
                OnPropertyChanged(nameof(UngroupedClients));
            }
            else if (SelectedGroup != null)
            {
                _databaseService.AddClientToGroup(SelectedGroup.Group.Id, windowTitle);
                LoadData();
            }
        }

        [RelayCommand]
        private void OnRemoveClientFromGroup(string? windowTitle)
        {
            if (string.IsNullOrWhiteSpace(windowTitle))
                return;

            // Check if we're editing a group
            if (EditingGroup != null)
            {
                _databaseService.RemoveClientFromGroup(EditingGroup.Id, windowTitle);
                LoadData();
                OnPropertyChanged(nameof(EditingGroupMembers));
                OnPropertyChanged(nameof(UngroupedClients));
            }
            else if (SelectedGroup != null)
            {
                _databaseService.RemoveClientFromGroup(SelectedGroup.Group.Id, windowTitle);
                LoadData();
            }
        }

        [RelayCommand]
        private void OnMoveGroupUp(DatabaseService.ClientGroup? group)
        {
            if (group == null)
                return;

            DatabaseService.Profile? activeProfile = _databaseService.GetActiveProfile() ?? _databaseService.CurrentProfile;
            if (activeProfile == null)
                return;

            List<DatabaseService.ClientGroupWithMembers> orderedGroups = ClientGroups.OrderBy(g => g.Group.DisplayOrder).ToList();
            int currentIndex = orderedGroups.FindIndex(g => g.Group.Id == group.Id);

            if (currentIndex > 0)
            {
                // Swap with previous group
                DatabaseService.ClientGroupWithMembers temp = orderedGroups[currentIndex];
                orderedGroups[currentIndex] = orderedGroups[currentIndex - 1];
                orderedGroups[currentIndex - 1] = temp;

                List<long> groupIdsInOrder = orderedGroups.Select(g => g.Group.Id).ToList();
                _databaseService.UpdateClientGroupOrder(activeProfile.Id, groupIdsInOrder);
                LoadData();
            }
        }

        [RelayCommand]
        private void OnMoveGroupDown(DatabaseService.ClientGroup? group)
        {
            if (group == null)
                return;

            DatabaseService.Profile? activeProfile = _databaseService.GetActiveProfile() ?? _databaseService.CurrentProfile;
            if (activeProfile == null)
                return;

            List<DatabaseService.ClientGroupWithMembers> orderedGroups = ClientGroups.OrderBy(g => g.Group.DisplayOrder).ToList();
            int currentIndex = orderedGroups.FindIndex(g => g.Group.Id == group.Id);

            if (currentIndex >= 0 && currentIndex < orderedGroups.Count - 1)
            {
                // Swap with next group
                DatabaseService.ClientGroupWithMembers temp = orderedGroups[currentIndex];
                orderedGroups[currentIndex] = orderedGroups[currentIndex + 1];
                orderedGroups[currentIndex + 1] = temp;

                List<long> groupIdsInOrder = orderedGroups.Select(g => g.Group.Id).ToList();
                _databaseService.UpdateClientGroupOrder(activeProfile.Id, groupIdsInOrder);
                LoadData();
            }
        }

        [RelayCommand]
        private void OnMoveClientUp(string? windowTitle)
        {
            if (string.IsNullOrWhiteSpace(windowTitle))
                return;

            // Check if we're editing a group
            if (EditingGroup != null)
            {
                List<DatabaseService.ClientGroupMember> orderedMembers = EditingGroupMembers.ToList();
                int currentIndex = orderedMembers.FindIndex(m => m.WindowTitle == windowTitle);

                if (currentIndex > 0)
                {
                    // Swap with previous client
                    DatabaseService.ClientGroupMember temp = orderedMembers[currentIndex];
                    orderedMembers[currentIndex] = orderedMembers[currentIndex - 1];
                    orderedMembers[currentIndex - 1] = temp;

                    List<string> windowTitlesInOrder = orderedMembers.Select(m => m.WindowTitle).ToList();
                    _databaseService.UpdateClientGroupMemberOrder(EditingGroup.Id, windowTitlesInOrder);
                    LoadData();
                    OnPropertyChanged(nameof(EditingGroupMembers));
                }
            }
            else if (SelectedGroup != null)
            {
                List<DatabaseService.ClientGroupMember> orderedMembers = SelectedGroup.Members.OrderBy(m => m.DisplayOrder).ToList();
                int currentIndex = orderedMembers.FindIndex(m => m.WindowTitle == windowTitle);

                if (currentIndex > 0)
                {
                    // Swap with previous client
                    DatabaseService.ClientGroupMember temp = orderedMembers[currentIndex];
                    orderedMembers[currentIndex] = orderedMembers[currentIndex - 1];
                    orderedMembers[currentIndex - 1] = temp;

                    List<string> windowTitlesInOrder = orderedMembers.Select(m => m.WindowTitle).ToList();
                    _databaseService.UpdateClientGroupMemberOrder(SelectedGroup.Group.Id, windowTitlesInOrder);
                    LoadData();
                }
            }
        }

        [RelayCommand]
        private void OnMoveClientDown(string? windowTitle)
        {
            if (string.IsNullOrWhiteSpace(windowTitle))
                return;

            // Check if we're editing a group
            if (EditingGroup != null)
            {
                List<DatabaseService.ClientGroupMember> orderedMembers = EditingGroupMembers.ToList();
                int currentIndex = orderedMembers.FindIndex(m => m.WindowTitle == windowTitle);

                if (currentIndex >= 0 && currentIndex < orderedMembers.Count - 1)
                {
                    // Swap with next client
                    DatabaseService.ClientGroupMember temp = orderedMembers[currentIndex];
                    orderedMembers[currentIndex] = orderedMembers[currentIndex + 1];
                    orderedMembers[currentIndex + 1] = temp;

                    List<string> windowTitlesInOrder = orderedMembers.Select(m => m.WindowTitle).ToList();
                    _databaseService.UpdateClientGroupMemberOrder(EditingGroup.Id, windowTitlesInOrder);
                    LoadData();
                    OnPropertyChanged(nameof(EditingGroupMembers));
                }
            }
            else if (SelectedGroup != null)
            {
                List<DatabaseService.ClientGroupMember> orderedMembers = SelectedGroup.Members.OrderBy(m => m.DisplayOrder).ToList();
                int currentIndex = orderedMembers.FindIndex(m => m.WindowTitle == windowTitle);

                if (currentIndex >= 0 && currentIndex < orderedMembers.Count - 1)
                {
                    // Swap with next client
                    DatabaseService.ClientGroupMember temp = orderedMembers[currentIndex];
                    orderedMembers[currentIndex] = orderedMembers[currentIndex + 1];
                    orderedMembers[currentIndex + 1] = temp;

                    List<string> windowTitlesInOrder = orderedMembers.Select(m => m.WindowTitle).ToList();
                    _databaseService.UpdateClientGroupMemberOrder(SelectedGroup.Group.Id, windowTitlesInOrder);
                    LoadData();
                }
            }
        }

        /// <summary>
        /// Sets the forward hotkey for the currently active group.
        /// </summary>
        [SupportedOSPlatform("windows")]
        public void SetCurrentGroupForwardHotkey(string hotkey)
        {
            long? groupId = EditingGroup?.Id ?? SelectedGroup?.Group.Id;
            if (groupId.HasValue)
            {
                string backwardHotkey = EditingGroup != null
                    ? (ClientGroups.FirstOrDefault(g => g.Group.Id == EditingGroup.Id)?.Group.CycleBackwardHotkey ?? string.Empty)
                    : (SelectedGroup?.Group.CycleBackwardHotkey ?? string.Empty);

                _databaseService.UpdateClientGroupHotkeys(groupId.Value, hotkey, backwardHotkey);
                LoadData();
                _hotkeyService?.RegisterHotkeys();
            }
        }

        /// <summary>
        /// Sets the backward hotkey for the currently active group.
        /// </summary>
        [SupportedOSPlatform("windows")]
        public void SetCurrentGroupBackwardHotkey(string hotkey)
        {
            long? groupId = EditingGroup?.Id ?? SelectedGroup?.Group.Id;
            if (groupId.HasValue)
            {
                string forwardHotkey = EditingGroup != null
                    ? (ClientGroups.FirstOrDefault(g => g.Group.Id == EditingGroup.Id)?.Group.CycleForwardHotkey ?? string.Empty)
                    : (SelectedGroup?.Group.CycleForwardHotkey ?? string.Empty);

                _databaseService.UpdateClientGroupHotkeys(groupId.Value, forwardHotkey, hotkey);
                LoadData();
                _hotkeyService?.RegisterHotkeys();
            }
        }

        [RelayCommand]
        [SupportedOSPlatform("windows")]
        private void OnClearForwardHotkey()
        {
            SetCurrentGroupForwardHotkey(string.Empty);
        }

        [RelayCommand]
        [SupportedOSPlatform("windows")]
        private void OnClearBackwardHotkey()
        {
            SetCurrentGroupBackwardHotkey(string.Empty);
        }

        [RelayCommand]
        [SupportedOSPlatform("windows")]
        private void OnStartCaptureForwardHotkey()
        {
            IsCapturingForwardHotkey = true;
            IsCapturingBackwardHotkey = false;

            // Unregister all hotkeys temporarily so they don't interfere with capture
            _hotkeyService?.UnregisterHotkeys();

            // TODO: Focus the page to receive key events
            System.Diagnostics.Debug.WriteLine("Hotkey capture started for forward");
        }

        [RelayCommand]
        [SupportedOSPlatform("windows")]
        private void OnStartCaptureBackwardHotkey()
        {
            IsCapturingBackwardHotkey = true;
            IsCapturingForwardHotkey = false;

            // Unregister all hotkeys temporarily so they don't interfere with capture
            _hotkeyService?.UnregisterHotkeys();

            // TODO: Focus the page to receive key events
            System.Diagnostics.Debug.WriteLine("Hotkey capture started for backward");
        }

        [RelayCommand]
        [SupportedOSPlatform("windows")]
        private void OnStopCaptureHotkey()
        {
            CancelHotkeyCapture();
        }

        /// <summary>
        /// Cancels hotkey capture without changing the value.
        /// </summary>
        [SupportedOSPlatform("windows")]
        public void CancelHotkeyCapture()
        {
            IsCapturingForwardHotkey = false;
            IsCapturingBackwardHotkey = false;

            // Re-register all hotkeys after capture is cancelled
            _hotkeyService?.RegisterHotkeys();
        }

        /// <summary>
        /// Handles a captured key combination or single key.
        /// </summary>
        [SupportedOSPlatform("windows")]
        public void HandleCapturedHotkey(Key key, KeyModifiers modifiers)
        {
            if (!IsCapturingForwardHotkey && !IsCapturingBackwardHotkey)
                return;

            // Convert key to string
            string keyString = KeyToString(key);
            if (string.IsNullOrEmpty(keyString))
            {
                // Invalid key, ignore
                return;
            }

            // Build the hotkey string
            List<string> parts = new List<string>();

            if ((modifiers & KeyModifiers.Control) == KeyModifiers.Control)
                parts.Add("Ctrl");
            if ((modifiers & KeyModifiers.Alt) == KeyModifiers.Alt)
                parts.Add("Alt");
            if ((modifiers & KeyModifiers.Shift) == KeyModifiers.Shift)
                parts.Add("Shift");
            if ((modifiers & KeyModifiers.Meta) == KeyModifiers.Meta)
                parts.Add("Win");

            // Add the key itself
            parts.Add(keyString);

            // Allow single keys or combinations
            string hotkeyString = parts.Count == 1 ? parts[0] : string.Join("+", parts);

            if (IsCapturingForwardHotkey)
            {
                SetCurrentGroupForwardHotkey(hotkeyString);
                IsCapturingForwardHotkey = false;
                // Re-register all hotkeys after capture is complete
                _hotkeyService?.RegisterHotkeys();
            }
            else if (IsCapturingBackwardHotkey)
            {
                SetCurrentGroupBackwardHotkey(hotkeyString);
                IsCapturingBackwardHotkey = false;
                // Re-register all hotkeys after capture is complete
                _hotkeyService?.RegisterHotkeys();
            }
        }

        /// <summary>
        /// Converts a Key to a string representation.
        /// </summary>
        private string KeyToString(Key key)
        {
            // Handle function keys
            if (key >= Key.F1 && key <= Key.F24)
            {
                int fNumber = (int)key - (int)Key.F1 + 1;
                return $"F{fNumber}";
            }

            // Handle regular keys
            if (key >= Key.A && key <= Key.Z)
            {
                return key.ToString();
            }

            if (key >= Key.D0 && key <= Key.D9)
            {
                return ((int)key - (int)Key.D0).ToString();
            }

            if (key >= Key.NumPad0 && key <= Key.NumPad9)
            {
                return "NumPad" + ((int)key - (int)Key.NumPad0).ToString();
            }

            // Handle other special keys
            return key switch
            {
                Key.Space => "Space",
                Key.Enter => "Enter",
                Key.Tab => "Tab",
                Key.Escape => "Escape",
                Key.Back => "Backspace",
                Key.Delete => "Delete",
                Key.Insert => "Insert",
                Key.Home => "Home",
                Key.End => "End",
                Key.PageUp => "PageUp",
                Key.PageDown => "PageDown",
                Key.Up => "Up",
                Key.Down => "Down",
                Key.Left => "Left",
                Key.Right => "Right",
                _ => string.Empty
            };
        }
    }
}


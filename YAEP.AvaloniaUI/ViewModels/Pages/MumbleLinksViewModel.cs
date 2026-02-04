using Avalonia.Controls;
using Avalonia.Input;
using System.Collections.ObjectModel;
using System.Runtime.Versioning;
using YAEP.Models;
using YAEP.Views.Windows;
using EditMumbleLinkWindow = YAEP.Views.Windows.EditMumbleLinkWindow;

namespace YAEP.ViewModels.Pages
{
    public partial class MumbleLinksViewModel : ViewModelBase
    {
        private readonly DatabaseService _databaseService;
        private readonly HotkeyService? _hotkeyService;
        private MumbleLinksWindow? _displayWindow;
        private EditMumbleLinkWindow? _editWindow;

        [ObservableProperty]
        private List<MumbleLink> _mumbleLinks = new();

        [ObservableProperty]
        private MumbleLink? _selectedLink;

        [ObservableProperty]
        private string _newLinkUrl = string.Empty;

        [ObservableProperty]
        private MumbleLink? _editingLink;

        [ObservableProperty]
        private string _editingLinkName = string.Empty;

        [ObservableProperty]
        private string _editingLinkUrl = string.Empty;

        [ObservableProperty]
        private long? _editingLinkServerGroupId;

        [ObservableProperty]
        private List<MumbleServerGroup> _serverGroupsForEdit = new();

        [ObservableProperty]
        private ObservableCollection<MumbleServerGroupChoice> _serverGroupChoicesForEdit = new();

        [ObservableProperty]
        private MumbleServerGroupChoice? _selectedEditingGroupChoice;

        partial void OnSelectedEditingGroupChoiceChanged(MumbleServerGroupChoice? value)
        {
            EditingLinkServerGroupId = value?.Id;
        }

        [ObservableProperty]
        private string _editingLinkHotkey = string.Empty;

        [ObservableProperty]
        private bool _isCapturingMumbleHotkey;

        [ObservableProperty]
        private bool _isDisplayWindowOpen = false;

        [ObservableProperty]
        private bool _isAlwaysOnTop = false;

        [ObservableProperty]
        private DrawerSettingsViewModel? _drawerSettingsViewModel;

        /// <summary>
        /// ViewModel for the Mumble Server Groups tab on this page.
        /// </summary>
        public MumbleServerGroupsViewModel ServerGroupsViewModel { get; }

        [ObservableProperty]
        private long? _bulkAssignGroupId;

        [ObservableProperty]
        private ObservableCollection<MumbleServerGroupChoice> _bulkAssignGroupChoices = new();

        [ObservableProperty]
        private MumbleServerGroupChoice? _selectedBulkAssignChoice;

        partial void OnSelectedBulkAssignChoiceChanged(MumbleServerGroupChoice? value)
        {
            BulkAssignGroupId = value?.Id;
        }

        private List<MumbleLink> _selectedLinks = new();

        /// <summary>
        /// Gets the list of selected links in the grid (for bulk assign). Set from code-behind when selection changes.
        /// </summary>
        public IList<MumbleLink> SelectedLinks => _selectedLinks;

        public bool HasSelectedLinks => _selectedLinks.Count > 0;

        partial void OnIsAlwaysOnTopChanged(bool value)
        {
            if (_displayWindow != null)
            {
                _displayWindow.Topmost = value;
                var settings = _databaseService.GetMumbleLinksOverlaySettings();
                settings.AlwaysOnTop = value;
                _databaseService.SaveMumbleLinksOverlaySettings(settings);
            }
        }

        public MumbleLinksViewModel(DatabaseService databaseService, HotkeyService? hotkeyService = null)
        {
            _databaseService = databaseService;
            _hotkeyService = hotkeyService;
            ServerGroupsViewModel = new MumbleServerGroupsViewModel(databaseService, hotkeyService);
        }

        public DatabaseService GetDatabaseService()
        {
            return _databaseService;
        }

        public void OnNavigatedTo()
        {
            LoadLinks();
            LoadAlwaysOnTopSetting();
            UpdateDisplayWindow();
            ServerGroupsViewModel.OnNavigatedTo();
        }

        private void LoadAlwaysOnTopSetting()
        {
            MumbleLinksOverlaySettings settings = _databaseService.GetMumbleLinksOverlaySettings();
            IsAlwaysOnTop = settings.AlwaysOnTop;
        }

        public void OnNavigatedFrom()
        {
            CloseDisplayWindow();
            ServerGroupsViewModel.OnNavigatedFrom();
        }

        private void LoadLinks()
        {
            List<MumbleLink> links = _databaseService.GetMumbleLinks();
            MumbleLinks = links;
            OnPropertyChanged(nameof(MumbleLinks));
            LoadServerGroups();
        }

        private void LoadServerGroups()
        {
            List<MumbleServerGroup> groups = _databaseService.GetMumbleServerGroups();
            BulkAssignGroupChoices.Clear();
            BulkAssignGroupChoices.Add(new MumbleServerGroupChoice(null, "No group"));
            foreach (MumbleServerGroup g in groups)
                BulkAssignGroupChoices.Add(new MumbleServerGroupChoice(g.Id, g.Name));
            if (SelectedBulkAssignChoice == null && BulkAssignGroupChoices.Count > 0)
                SelectedBulkAssignChoice = BulkAssignGroupChoices[0];
        }

        [RelayCommand]
        private void OnCreateLink()
        {
            if (string.IsNullOrWhiteSpace(NewLinkUrl))
                return;

            MumbleLink? link = _databaseService.CreateMumbleLink(NewLinkUrl);
            if (link != null)
            {
                NewLinkUrl = string.Empty;
                LoadLinks();
                UpdateDisplayWindow();
            }
        }

        /// <summary>
        /// Updates the selected links list from the grid. Call from code-behind when selection changes.
        /// </summary>
        public void SetSelectedLinks(IEnumerable<MumbleLink> links)
        {
            _selectedLinks = links?.ToList() ?? new List<MumbleLink>();
            OnPropertyChanged(nameof(SelectedLinks));
            OnPropertyChanged(nameof(HasSelectedLinks));
        }

        /// <summary>
        /// Prepares the edit form state for the given link (e.g. when opening edit from GroupLinksWindow).
        /// </summary>
        public void PrepareEditLink(MumbleLink link)
        {
            EditingLink = link;
            EditingLinkName = link.Name;
            EditingLinkUrl = link.Url;
            EditingLinkServerGroupId = link.ServerGroupId;
            EditingLinkHotkey = link.Hotkey ?? string.Empty;
            ServerGroupsForEdit = _databaseService.GetMumbleServerGroups();
            ServerGroupChoicesForEdit.Clear();
            ServerGroupChoicesForEdit.Add(new MumbleServerGroupChoice(null, "No group"));
            foreach (MumbleServerGroup g in ServerGroupsForEdit)
                ServerGroupChoicesForEdit.Add(new MumbleServerGroupChoice(g.Id, g.Name));
            SelectedEditingGroupChoice = ServerGroupChoicesForEdit.FirstOrDefault(c => c.Id == link.ServerGroupId);
        }

        [RelayCommand]
        private void OnEditLink(MumbleLink? link)
        {
            if (link != null)
            {
                PrepareEditLink(link);

                Dispatcher.UIThread.Post(() =>
                {
                    if (_editWindow != null)
                    {
                        _editWindow.Activate();
                        return;
                    }

                    EditMumbleLinkWindow window = new EditMumbleLinkWindow(this);
                    Window? mainWindow = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
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

                    window.Closed += (sender, e) =>
                    {
                        _editWindow = null;
                        if (EditingLink != null)
                        {
                            OnCancelEditLink();
                        }
                    };

                    _editWindow = window;
                });
            }
        }

        [RelayCommand]
        private void OnSaveEditLink()
        {
            if (EditingLink != null && !string.IsNullOrWhiteSpace(EditingLinkName) && !string.IsNullOrWhiteSpace(EditingLinkUrl))
            {
                _databaseService.UpdateMumbleLink(EditingLink.Id, EditingLinkName, EditingLinkUrl, EditingLinkServerGroupId, EditingLinkHotkey);
                EditingLink = null;
                EditingLinkName = string.Empty;
                EditingLinkUrl = string.Empty;
                EditingLinkServerGroupId = null;
                EditingLinkHotkey = string.Empty;
                LoadLinks();
                UpdateDisplayWindow();
                _hotkeyService?.RegisterHotkeys();
                _editWindow?.Close();
            }
        }

        [RelayCommand]
        private void OnCancelEditLink()
        {
            EditingLink = null;
            EditingLinkName = string.Empty;
            EditingLinkUrl = string.Empty;
            EditingLinkServerGroupId = null;
            EditingLinkHotkey = string.Empty;
            IsCapturingMumbleHotkey = false;
            _editWindow?.Close();
        }

        [RelayCommand]
        private void OnDeleteLink(MumbleLink? link)
        {
            if (link != null)
            {
                _databaseService.DeleteMumbleLink(link.Id);

                if (EditingLink?.Id == link.Id)
                {
                    EditingLink = null;
                    EditingLinkName = string.Empty;
                    EditingLinkUrl = string.Empty;
                }

                LoadLinks();
                UpdateDisplayWindow();
                _hotkeyService?.RegisterHotkeys();
            }
        }

        [RelayCommand]
        private void OnMoveLinkUp(MumbleLink? link)
        {
            if (link == null)
                return;

            List<MumbleLink> orderedLinks = MumbleLinks.OrderBy(l => l.DisplayOrder).ToList();
            int currentIndex = orderedLinks.FindIndex(l => l.Id == link.Id);

            if (currentIndex > 0)
            {
                MumbleLink temp = orderedLinks[currentIndex];
                orderedLinks[currentIndex] = orderedLinks[currentIndex - 1];
                orderedLinks[currentIndex - 1] = temp;

                List<long> linkIdsInOrder = orderedLinks.Select(l => l.Id).ToList();
                _databaseService.UpdateMumbleLinksOrder(linkIdsInOrder);
                LoadLinks();
                UpdateDisplayWindow();
            }
        }

        [RelayCommand]
        private void OnMoveLinkDown(MumbleLink? link)
        {
            if (link == null)
                return;

            List<MumbleLink> orderedLinks = MumbleLinks.OrderBy(l => l.DisplayOrder).ToList();
            int currentIndex = orderedLinks.FindIndex(l => l.Id == link.Id);

            if (currentIndex >= 0 && currentIndex < orderedLinks.Count - 1)
            {
                MumbleLink temp = orderedLinks[currentIndex];
                orderedLinks[currentIndex] = orderedLinks[currentIndex + 1];
                orderedLinks[currentIndex + 1] = temp;

                List<long> linkIdsInOrder = orderedLinks.Select(l => l.Id).ToList();
                _databaseService.UpdateMumbleLinksOrder(linkIdsInOrder);
                LoadLinks();
                UpdateDisplayWindow();
            }
        }

        [RelayCommand]
        private void OnToggleLinkSelection(MumbleLink? link)
        {
            if (link == null)
            {
                System.Diagnostics.Debug.WriteLine("OnToggleLinkSelection: link is null");
                return;
            }

            bool newSelection = !link.IsSelected;
            System.Diagnostics.Debug.WriteLine($"OnToggleLinkSelection: Link {link.Id}, current: {link.IsSelected}, setting to: {newSelection}");

            _databaseService.UpdateMumbleLinkSelection(link.Id, newSelection);

            link.IsSelected = newSelection;

            UpdateDisplayWindow();
        }


        internal void UpdateDisplayWindow()
        {
            List<MumbleLink> selectedLinks = _databaseService.GetSelectedMumbleLinks();
            bool shouldShowWindow = selectedLinks.Count > 0;

            Dispatcher.UIThread.Post(() =>
            {
                if (shouldShowWindow && !IsDisplayWindowOpen)
                {
                    MumbleLinksWindow? existingWindow = FindExistingMumbleLinksWindow();
                    if (existingWindow != null)
                    {
                        _displayWindow = existingWindow;
                        IsDisplayWindowOpen = true;
                        _displayWindow.UpdateLinks(selectedLinks);

                        MumbleLinksOverlaySettings settings = _databaseService.GetMumbleLinksOverlaySettings();
                        _displayWindow.Topmost = settings.AlwaysOnTop;
                        IsAlwaysOnTop = settings.AlwaysOnTop;

                        _displayWindow.Closed += (sender, e) =>
                        {
                            IsDisplayWindowOpen = false;
                            _displayWindow = null;
                        };
                    }
                    else
                    {
                        OpenDisplayWindow();
                    }
                }
                else if (!shouldShowWindow && IsDisplayWindowOpen)
                {
                    CloseDisplayWindow();
                }
                else if (IsDisplayWindowOpen && _displayWindow != null)
                {
                    MumbleLinksWindow displayWindow = _displayWindow;
                    displayWindow.UpdateLinks(selectedLinks);

                    MumbleLinksOverlaySettings settings = _databaseService.GetMumbleLinksOverlaySettings();
                    if (displayWindow.Topmost != settings.AlwaysOnTop)
                    {
                        displayWindow.Topmost = settings.AlwaysOnTop;
                        IsAlwaysOnTop = settings.AlwaysOnTop;
                    }
                }
            });
        }

        private MumbleLinksWindow? FindExistingMumbleLinksWindow()
        {
            if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.Windows.OfType<MumbleLinksWindow>().FirstOrDefault();
            }
            return null;
        }

        private void OpenDisplayWindow()
        {
            if (IsDisplayWindowOpen || _displayWindow != null)
                return;

            List<MumbleLink> selectedLinks = _databaseService.GetSelectedMumbleLinks();
            if (selectedLinks.Count == 0)
                return;

            MumbleLinksWindow? existingWindow = FindExistingMumbleLinksWindow();
            MumbleLinksOverlaySettings settings = _databaseService.GetMumbleLinksOverlaySettings();

            if (existingWindow != null)
            {
                _displayWindow = existingWindow;
                _displayWindow.UpdateLinks(selectedLinks);
                _displayWindow.Topmost = settings.AlwaysOnTop;
                IsAlwaysOnTop = settings.AlwaysOnTop;
                _displayWindow.Closed += (sender, e) =>
                {
                    IsDisplayWindowOpen = false;
                    _displayWindow = null;
                };
                _displayWindow.Activate();
                IsDisplayWindowOpen = true;
                return;
            }

            _displayWindow = new MumbleLinksWindow(this, selectedLinks);
            _displayWindow.Topmost = settings.AlwaysOnTop;
            IsAlwaysOnTop = settings.AlwaysOnTop;
            _displayWindow.Closed += (sender, e) =>
            {
                IsDisplayWindowOpen = false;
                _displayWindow = null;
            };
            _displayWindow.Show();
            _displayWindow.Activate();
            IsDisplayWindowOpen = true;
        }

        private void CloseDisplayWindow()
        {
            if (!IsDisplayWindowOpen || _displayWindow == null)
                return;

            _displayWindow.Close();
            _displayWindow = null;
            IsDisplayWindowOpen = false;
        }

        [RelayCommand]
        private void OnOpenLink(MumbleLink? link)
        {
            link?.OpenLink();
        }

        [RelayCommand]
        private void OnAssignSelectedToGroup()
        {
            if (_selectedLinks.Count == 0)
                return;

            _databaseService.UpdateMumbleLinksServerGroup(_selectedLinks.Select(l => l.Id), BulkAssignGroupId);
            LoadLinks();
            UpdateDisplayWindow();
        }

        [RelayCommand]
        [SupportedOSPlatform("windows")]
        private void OnStartCaptureMumbleHotkey()
        {
            IsCapturingMumbleHotkey = true;
            _hotkeyService?.UnregisterHotkeys();
        }

        [RelayCommand]
        [SupportedOSPlatform("windows")]
        private void OnStopCaptureMumbleHotkey()
        {
            IsCapturingMumbleHotkey = false;
            _hotkeyService?.RegisterHotkeys();
        }

        [RelayCommand]
        private void OnClearMumbleHotkey()
        {
            EditingLinkHotkey = string.Empty;
        }

        [SupportedOSPlatform("windows")]
        public void HandleCapturedMumbleHotkey(Key key, KeyModifiers modifiers)
        {
            if (!IsCapturingMumbleHotkey)
                return;

            string keyString = KeyToString(key);
            if (string.IsNullOrEmpty(keyString))
                return;

            List<string> parts = new List<string>();
            if ((modifiers & KeyModifiers.Control) == KeyModifiers.Control)
                parts.Add("Ctrl");
            if ((modifiers & KeyModifiers.Alt) == KeyModifiers.Alt)
                parts.Add("Alt");
            if ((modifiers & KeyModifiers.Shift) == KeyModifiers.Shift)
                parts.Add("Shift");
            if ((modifiers & KeyModifiers.Meta) == KeyModifiers.Meta)
                parts.Add("Win");
            parts.Add(keyString);
            EditingLinkHotkey = parts.Count == 1 ? parts[0] : string.Join("+", parts);
            IsCapturingMumbleHotkey = false;
            _hotkeyService?.RegisterHotkeys();
        }

        private static string KeyToString(Key key)
        {
            if (key >= Key.F1 && key <= Key.F24)
                return $"F{(int)key - (int)Key.F1 + 1}";
            if (key >= Key.A && key <= Key.Z)
                return key.ToString();
            if (key >= Key.D0 && key <= Key.D9)
                return ((int)key - (int)Key.D0).ToString();
            if (key >= Key.NumPad0 && key <= Key.NumPad9)
                return "NumPad" + ((int)key - (int)Key.NumPad0).ToString();
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


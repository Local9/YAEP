using Avalonia.Controls;
using YAEP.Models;
using YAEP.Views.Windows;
using EditMumbleLinkWindow = YAEP.Views.Windows.EditMumbleLinkWindow;

namespace YAEP.ViewModels.Pages
{
    public partial class MumbleLinksViewModel : ViewModelBase
    {
        private readonly DatabaseService _databaseService;
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
        private bool _isDisplayWindowOpen = false;

        [ObservableProperty]
        private bool _isAlwaysOnTop = false;

        [ObservableProperty]
        private DrawerSettingsViewModel? _drawerSettingsViewModel;

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

        public MumbleLinksViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
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
        }

        private void LoadAlwaysOnTopSetting()
        {
            MumbleLinksOverlaySettings settings = _databaseService.GetMumbleLinksOverlaySettings();
            IsAlwaysOnTop = settings.AlwaysOnTop;
        }

        public void OnNavigatedFrom()
        {
            CloseDisplayWindow();
        }

        private void LoadLinks()
        {
            List<MumbleLink> links = _databaseService.GetMumbleLinks();
            MumbleLinks = links;
            OnPropertyChanged(nameof(MumbleLinks));
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

        [RelayCommand]
        private void OnEditLink(MumbleLink? link)
        {
            if (link != null)
            {
                EditingLink = link;
                EditingLinkName = link.Name;
                EditingLinkUrl = link.Url;

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
                _databaseService.UpdateMumbleLink(EditingLink.Id, EditingLinkName, EditingLinkUrl);
                EditingLink = null;
                EditingLinkName = string.Empty;
                EditingLinkUrl = string.Empty;
                LoadLinks();
                UpdateDisplayWindow();
                _editWindow?.Close();
            }
        }

        [RelayCommand]
        private void OnCancelEditLink()
        {
            EditingLink = null;
            EditingLinkName = string.Empty;
            EditingLinkUrl = string.Empty;
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
    }
}


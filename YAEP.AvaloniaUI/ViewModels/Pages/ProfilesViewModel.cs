using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Runtime.Versioning;
using YAEP.Services;
using YAEP.Views.Windows;

namespace YAEP.ViewModels.Pages
{
    public partial class ProfilesViewModel : ViewModelBase
    {
        private readonly DatabaseService _databaseService;
        private readonly HotkeyService? _hotkeyService;
        private EditProfileWindow? _editProfileWindow;

        [ObservableProperty]
        private List<DatabaseService.Profile> _profiles = new();

        [ObservableProperty]
        private List<DatabaseService.Profile> _deletedProfiles = new();

        [ObservableProperty]
        private DatabaseService.Profile? _selectedProfile;

        partial void OnSelectedProfileChanged(DatabaseService.Profile? value)
        {
            OnPropertyChanged(nameof(CurrentProfileHotkey));
        }

        [ObservableProperty]
        private string _newProfileName = String.Empty;

        [ObservableProperty]
        private DatabaseService.Profile? _editingProfile;

        partial void OnEditingProfileChanged(DatabaseService.Profile? value)
        {
            OnPropertyChanged(nameof(CurrentProfileHotkey));
        }

        [ObservableProperty]
        private string _editingProfileName = String.Empty;

        [ObservableProperty]
        private bool _isCapturingHotkey = false;

        public string? CurrentProfileHotkey
        {
            get
            {
                string? hotkey = EditingProfile?.SwitchHotkey;
                return string.IsNullOrWhiteSpace(hotkey) ? null : hotkey;
            }
        }

        public ProfilesViewModel(
            DatabaseService databaseService,
            HotkeyService? hotkeyService = null
        )
        {
            _databaseService = databaseService;
            _hotkeyService = hotkeyService;
        }

        public void OnNavigatedTo()
        {
            LoadProfiles();
        }

        public void OnNavigatedFrom() { }

        private void LoadProfiles()
        {
            Profiles = _databaseService.GetProfiles();
            DeletedProfiles = _databaseService.GetDeletedProfiles();

            // Set selected profile to the active profile from database
            DatabaseService.Profile? activeProfile = _databaseService.GetActiveProfile();
            SelectedProfile = activeProfile ?? _databaseService.CurrentProfile;
            OnPropertyChanged(nameof(SelectedProfile));
        }

        [RelayCommand]
        private void OnCreateProfile()
        {
            if (string.IsNullOrWhiteSpace(NewProfileName))
                return;

            DatabaseService.Profile? profile = _databaseService.CreateProfile(NewProfileName);
            if (profile != null)
            {
                LoadProfiles(); // Refresh to update IsActive indicators
                NewProfileName = String.Empty;
            }
        }

        [RelayCommand]
        private void OnActivateProfile(DatabaseService.Profile? profile)
        {
            if (profile != null && !profile.IsActive)
            {
                _databaseService.SetCurrentProfile(profile.Id);
                LoadProfiles(); // Refresh to update IsActive indicators
            }
        }

        [RelayCommand]
        private void OnEditProfile(DatabaseService.Profile? profile)
        {
            if (profile != null)
            {
                // Reload profile from database to ensure we have all properties including SwitchHotkey
                DatabaseService.Profile? freshProfile = _databaseService.GetProfile(profile.Id);
                if (freshProfile != null)
                {
                    EditingProfile = freshProfile;
                    EditingProfileName = freshProfile.Name;
                    OnPropertyChanged(nameof(CurrentProfileHotkey));

                    // Show edit window
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (_editProfileWindow != null)
                        {
                            _editProfileWindow.Activate();
                            return;
                        }

                    EditProfileWindow window = new EditProfileWindow(this);
                    Window? mainWindow = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                        ? desktop.MainWindow
                        : null;
                    
                    // Ensure property change notification after window is created
                    OnPropertyChanged(nameof(CurrentProfileHotkey));
                    
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
                        _editProfileWindow = null;
                        if (EditingProfile != null)
                        {
                            OnCancelEditProfile();
                        }
                    };

                    _editProfileWindow = window;
                    });
                }
            }
        }

        [RelayCommand]
        [SupportedOSPlatform("windows")]
        private void OnSaveEditProfile()
        {
            if (EditingProfile != null && !string.IsNullOrWhiteSpace(EditingProfileName))
            {
                _databaseService.UpdateProfile(EditingProfile.Id, EditingProfileName);
                EditingProfile = null;
                EditingProfileName = String.Empty;
                OnPropertyChanged(nameof(CurrentProfileHotkey));
                LoadProfiles();
                _hotkeyService?.RegisterHotkeys();
                _editProfileWindow?.Close();
            }
        }

        [RelayCommand]
        private void OnCancelEditProfile()
        {
            EditingProfile = null;
            EditingProfileName = String.Empty;
            OnPropertyChanged(nameof(CurrentProfileHotkey));
            _editProfileWindow?.Close();
        }

        [RelayCommand]
        private void OnDeleteProfile(DatabaseService.Profile? profile)
        {
            if (profile != null && !profile.IsDeleted)
            {
                bool wasCurrentProfile = _databaseService.CurrentProfile?.Id == profile.Id;

                _databaseService.DeleteProfile(profile.Id);

                // If we deleted the current profile, set a new default
                if (wasCurrentProfile)
                {
                    DatabaseService.Profile? defaultProfile = _databaseService.GetDefaultProfile();
                    if (defaultProfile != null)
                    {
                        _databaseService.SetCurrentProfile(defaultProfile.Id);
                    }
                }

                // Cancel editing if we were editing the deleted profile
                if (EditingProfile?.Id == profile.Id)
                {
                    EditingProfile = null;
                    EditingProfileName = String.Empty;
                }

                LoadProfiles();
            }
        }

        [RelayCommand]
        private void OnRestoreProfile(DatabaseService.Profile? profile)
        {
            if (profile != null && profile.IsDeleted)
            {
                _databaseService.RestoreProfile(profile.Id);
                LoadProfiles();
            }
        }

        /// <summary>
        /// Sets the hotkey for the currently selected or editing profile.
        /// </summary>
        [SupportedOSPlatform("windows")]
        public void SetCurrentProfileHotkey(string hotkey)
        {
            DatabaseService.Profile? profile = EditingProfile;
            if (profile != null)
            {
                _databaseService.UpdateProfileHotkey(profile.Id, hotkey);
                // Update the EditingProfile object directly so the UI updates immediately
                profile.SwitchHotkey = hotkey;
                OnPropertyChanged(nameof(CurrentProfileHotkey));
                LoadProfiles();
                _hotkeyService?.RegisterHotkeys();
            }
        }

        [RelayCommand]
        [SupportedOSPlatform("windows")]
        private void OnClearHotkey()
        {
            SetCurrentProfileHotkey(string.Empty);
        }

        [RelayCommand]
        [SupportedOSPlatform("windows")]
        private void OnStartCaptureHotkey()
        {
            IsCapturingHotkey = true;

            // Unregister all hotkeys temporarily so they don't interfere with capture
            _hotkeyService?.UnregisterHotkeys();

            System.Diagnostics.Debug.WriteLine("Hotkey capture started for profile");
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
            IsCapturingHotkey = false;

            // Re-register all hotkeys after capture is cancelled
            _hotkeyService?.RegisterHotkeys();
        }

        /// <summary>
        /// Handles a captured key combination or single key.
        /// </summary>
        [SupportedOSPlatform("windows")]
        public void HandleCapturedHotkey(Key key, KeyModifiers modifiers)
        {
            if (!IsCapturingHotkey)
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

            SetCurrentProfileHotkey(hotkeyString);
            IsCapturingHotkey = false;
            // Re-register all hotkeys after capture is complete
            _hotkeyService?.RegisterHotkeys();
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

using System.Runtime.Versioning;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YAEP.Models;
using YAEP.Services;
using YAEP.ViewModels;
using YAEP.Views.Windows;

namespace YAEP.ViewModels.Windows
{
    [SupportedOSPlatform("windows")]
    public partial class CharacterUserManagementWindowViewModel : ViewModelBase
    {
        private readonly EveOnlineProfileService _profileService;
        private readonly EveOnlineProfile _profile;
        private ConfirmCharacterUserCopyWindow? _confirmCopyWindow;

        [ObservableProperty]
        private List<EveOnlineCharacter> _profileCharacters = new();

        [ObservableProperty]
        private List<EveOnlineUser> _profileUsers = new();

        [ObservableProperty]
        private EveOnlineCharacter? _selectedSourceCharacter;

        [ObservableProperty]
        private EveOnlineUser? _selectedSourceUser;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string? _errorMessage;

        [ObservableProperty]
        private bool _copyCharacterSettings = true;

        [ObservableProperty]
        private bool _copyUserSettings = true;

        public EveOnlineProfile Profile => _profile;

        public bool CanCopyFiles => SelectedSourceCharacter != null && SelectedSourceUser != null && (CopyCharacterSettings || CopyUserSettings);

        partial void OnSelectedSourceCharacterChanged(EveOnlineCharacter? value)
        {
            OnPropertyChanged(nameof(CanCopyFiles));
        }

        partial void OnSelectedSourceUserChanged(EveOnlineUser? value)
        {
            OnPropertyChanged(nameof(CanCopyFiles));
        }

        partial void OnCopyCharacterSettingsChanged(bool value)
        {
            OnPropertyChanged(nameof(CanCopyFiles));
        }

        partial void OnCopyUserSettingsChanged(bool value)
        {
            OnPropertyChanged(nameof(CanCopyFiles));
        }

        public CharacterUserManagementWindowViewModel(EveOnlineProfileService profileService, EveOnlineProfile profile)
        {
            _profileService = profileService;
            _profile = profile;
        }

        public async Task LoadCharactersAndUsersAsync()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = null;

                ProfileCharacters = _profileService.GetProfileCharacters(_profile.FullPath);
                ProfileUsers = _profileService.GetProfileUsers(_profile.FullPath);

                // Fetch character names from ESI API
                foreach (var character in ProfileCharacters)
                {
                    if (!string.IsNullOrEmpty(character.CharacterId))
                    {
                        string? characterName = await _profileService.GetCharacterNameFromEsiAsync(character.CharacterId);
                        if (!string.IsNullOrEmpty(characterName))
                        {
                            character.CharacterName = characterName;
                        }
                    }
                }

                // Reset selections
                SelectedSourceCharacter = null;
                SelectedSourceUser = null;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading characters and users: {ex.Message}";
                ProfileCharacters = new List<EveOnlineCharacter>();
                ProfileUsers = new List<EveOnlineUser>();
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void OnCopyCharacterAndUserFiles()
        {
            if (SelectedSourceCharacter == null || SelectedSourceUser == null)
                return;

            if (!CopyCharacterSettings && !CopyUserSettings)
                return;

            // Show confirmation dialog
            Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
            {
                if (_confirmCopyWindow != null)
                {
                    _confirmCopyWindow.Activate();
                    return;
                }

                ConfirmCharacterUserCopyWindow window = new ConfirmCharacterUserCopyWindow(
                    SelectedSourceCharacter,
                    SelectedSourceUser,
                    CopyCharacterSettings,
                    CopyUserSettings);

                Window? mainWindow = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                    ? desktop.MainWindow
                    : null;

                if (mainWindow != null)
                {
                    window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    bool? result = await window.ShowDialog<bool?>(mainWindow);
                    
                    if (result == true)
                    {
                        PerformCharacterUserCopy();
                    }
                }
                else
                {
                    window.Show();
                }

                window.Closed += (sender, e) =>
                {
                    _confirmCopyWindow = null;
                };

                _confirmCopyWindow = window;
            });
        }

        private void PerformCharacterUserCopy()
        {
            if (SelectedSourceCharacter == null || SelectedSourceUser == null)
                return;

            try
            {
                IsLoading = true;
                ErrorMessage = null;

                bool success = _profileService.CopyCharacterAndUserFiles(
                    _profile.FullPath,
                    SelectedSourceCharacter,
                    SelectedSourceUser,
                    CopyCharacterSettings,
                    CopyUserSettings);

                if (success)
                {
                    ErrorMessage = null;
                    // Reload characters and users to reflect changes
                    _ = LoadCharactersAndUsersAsync();
                }
                else
                {
                    ErrorMessage = "Failed to copy character and user files.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error copying files: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}

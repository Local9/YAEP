using Wpf.Ui.Abstractions.Controls;
using YAEP.Services;

namespace YAEP.ViewModels.Pages
{
    public partial class ProfilesViewModel : ObservableObject, INavigationAware
    {
        private readonly DatabaseService _databaseService;

        [ObservableProperty]
        private List<DatabaseService.Profile> _profiles = new();

        [ObservableProperty]
        private List<DatabaseService.Profile> _deletedProfiles = new();

        [ObservableProperty]
        private DatabaseService.Profile? _selectedProfile;

        [ObservableProperty]
        private string _newProfileName = String.Empty;

        [ObservableProperty]
        private DatabaseService.Profile? _editingProfile;

        [ObservableProperty]
        private string _editingProfileName = String.Empty;

        public ProfilesViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public Task OnNavigatedToAsync()
        {
            LoadProfiles();

            return Task.CompletedTask;
        }

        public Task OnNavigatedFromAsync()
        {
            return Task.CompletedTask;
        }

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
                EditingProfile = profile;
                EditingProfileName = profile.Name;
            }
        }

        [RelayCommand]
        private void OnSaveEditProfile()
        {
            if (EditingProfile != null && !string.IsNullOrWhiteSpace(EditingProfileName))
            {
                _databaseService.UpdateProfile(EditingProfile.Id, EditingProfileName);
                EditingProfile = null;
                EditingProfileName = String.Empty;
                LoadProfiles(); // Refresh to update IsActive indicators
            }
        }

        [RelayCommand]
        private void OnCancelEditProfile()
        {
            EditingProfile = null;
            EditingProfileName = String.Empty;
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
    }
}


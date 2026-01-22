using Avalonia.Controls;
using System.IO;
using System.Runtime.Versioning;
using YAEP.Models;
using YAEP.Views.Windows;

namespace YAEP.ViewModels.Pages
{
    [SupportedOSPlatform("windows")]
    public partial class EveOnlineProfilesViewModel : ViewModelBase
    {
        private readonly EveOnlineProfileService _profileService;
        private CopyProfileWindow? _copyProfileWindow;

        [ObservableProperty]
        private List<EveOnlineProfile> _profiles = new();

        [ObservableProperty]
        private List<string> _servers = new();

        [ObservableProperty]
        private string? _selectedServer;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string? _errorMessage;

        [ObservableProperty]
        private EveOnlineProfile? _profileToCopy;

        [ObservableProperty]
        private string _newProfileName = string.Empty;

        [ObservableProperty]
        private bool _isEveOnlineRunning;

        partial void OnSelectedServerChanged(string? value)
        {
            if (!IsLoading)
            {
                LoadProfiles();
            }
        }

        public EveOnlineProfilesViewModel(EveOnlineProfileService profileService)
        {
            _profileService = profileService;
        }

        public void OnNavigatedTo()
        {
            CheckEveOnlineStatus();
            LoadServers();
            LoadProfiles();
        }

        private void CheckEveOnlineStatus()
        {
            IsEveOnlineRunning = _profileService.IsEveOnlineRunning();
        }

        public void OnNavigatedFrom()
        {
        }

        private void LoadServers()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = null;
                List<string> allServers = _profileService.GetAllServers();

                // Filter to only show Tranquility and Singularity
                List<string> filteredServers = new List<string>();
                foreach (string server in allServers)
                {
                    string serverLower = server.ToLowerInvariant();
                    if (serverLower.Contains("tranquility") || serverLower.Contains("tq"))
                    {
                        filteredServers.Add("Tranquility");
                    }
                    else if (serverLower.Contains("singularity") || serverLower.Contains("sisi"))
                    {
                        filteredServers.Add("Singularity");
                    }
                }

                // Remove duplicates and ensure order
                Servers = filteredServers.Distinct().ToList();

                // Set Tranquility as default selection
                if (SelectedServer == null && Servers.Count > 0)
                {
                    SelectedServer = Servers.FirstOrDefault(s => s.Equals("Tranquility", StringComparison.OrdinalIgnoreCase))
                        ?? Servers[0];
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading servers: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void LoadProfiles()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = null;

                if (string.IsNullOrEmpty(SelectedServer))
                {
                    Profiles = new List<EveOnlineProfile>();
                    return;
                }

                // Map display names to actual server folder names
                string? serverFolderName = null;
                List<string> allServers = _profileService.GetAllServers();

                if (SelectedServer.Equals("Tranquility", StringComparison.OrdinalIgnoreCase))
                {
                    serverFolderName = allServers.FirstOrDefault(s => s.ToLowerInvariant().Contains("tranquility") || s.ToLowerInvariant().Contains("tq"));
                }
                else if (SelectedServer.Equals("Singularity", StringComparison.OrdinalIgnoreCase))
                {
                    serverFolderName = allServers.FirstOrDefault(s => s.ToLowerInvariant().Contains("singularity") || s.ToLowerInvariant().Contains("sisi"));
                }

                if (serverFolderName != null)
                {
                    Profiles = _profileService.GetServerProfiles(serverFolderName);
                }
                else
                {
                    Profiles = new List<EveOnlineProfile>();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading profiles: {ex.Message}";
                Profiles = new List<EveOnlineProfile>();
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void OnRefresh()
        {
            CheckEveOnlineStatus();
            LoadServers();
            LoadProfiles();
        }

        [RelayCommand]
        private void OnCopyProfile(EveOnlineProfile? profile)
        {
            if (profile == null || IsEveOnlineRunning)
                return;

            ProfileToCopy = profile;

            Dispatcher.UIThread.Post(() =>
            {
                if (_copyProfileWindow != null)
                {
                    _copyProfileWindow.Activate();
                    return;
                }

                CopyProfileWindow window = new CopyProfileWindow(this);
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
                    _copyProfileWindow = null;
                    ProfileToCopy = null;
                };

                _copyProfileWindow = window;
            });
        }

        [RelayCommand]
        private void OnLoadProfileCharactersAndUsers(EveOnlineProfile? profile)
        {
            if (profile == null || IsEveOnlineRunning)
                return;

            // Open character/user management window
            Dispatcher.UIThread.Post(() =>
            {
                CharacterUserManagementWindow window = new CharacterUserManagementWindow(_profileService, profile);
                Window? mainWindow = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                    ? desktop.MainWindow
                    : null;

                if (mainWindow != null)
                {
                    window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    window.Show(mainWindow);
                }
                else
                {
                    window.Show();
                }
            });
        }


        public void PerformProfileCopy(string newProfileName)
        {
            if (ProfileToCopy == null || string.IsNullOrWhiteSpace(newProfileName))
                return;

            try
            {
                IsLoading = true;
                ErrorMessage = null;

                // Get server folder path
                string serverFolderPath = Path.GetDirectoryName(ProfileToCopy.FullPath) ?? string.Empty;
                if (string.IsNullOrEmpty(serverFolderPath))
                {
                    ErrorMessage = "Unable to determine server folder path.";
                    return;
                }

                bool success = _profileService.CopyProfile(
                    ProfileToCopy.FullPath,
                    newProfileName,
                    serverFolderPath);

                if (success)
                {
                    ErrorMessage = null;
                    NewProfileName = string.Empty;
                    LoadProfiles(); // Refresh profile list
                    _copyProfileWindow?.Close();
                }
                else
                {
                    ErrorMessage = "Failed to copy profile. The profile name may already exist.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error copying profile: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}

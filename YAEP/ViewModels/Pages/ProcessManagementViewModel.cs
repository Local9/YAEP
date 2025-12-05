using System.Collections.Generic;
using CommunityToolkit.Mvvm.Input;
using YAEP.Services;
using YAEP.ViewModels;

namespace YAEP.ViewModels.Pages
{
    public partial class ProcessManagementViewModel : ViewModelBase
    {
        private readonly DatabaseService _databaseService;

        [ObservableProperty]
        private List<string> _processNames = new();

        [ObservableProperty]
        private string _newProcessName = String.Empty;

        public ProcessManagementViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public void OnNavigatedTo()
        {
            LoadProcessNames();
        }

        public void OnNavigatedFrom()
        {
        }

        private void LoadProcessNames()
        {
            DatabaseService.Profile? activeProfile = _databaseService.GetActiveProfile() ?? _databaseService.CurrentProfile;
            if (activeProfile != null)
            {
                ProcessNames = _databaseService.GetProcessNames(activeProfile.Id);
            }
            else
            {
                ProcessNames = new List<string>();
            }
        }

        [RelayCommand]
        private void OnAddProcess()
        {
            if (string.IsNullOrWhiteSpace(NewProcessName))
                return;

            DatabaseService.Profile? activeProfile = _databaseService.GetActiveProfile() ?? _databaseService.CurrentProfile;
            if (activeProfile != null)
            {
                _databaseService.AddProcessName(activeProfile.Id, NewProcessName);
                NewProcessName = String.Empty;
                LoadProcessNames();
            }
        }

        [RelayCommand]
        private void OnRemoveProcess(string? processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return;

            DatabaseService.Profile? activeProfile = _databaseService.GetActiveProfile() ?? _databaseService.CurrentProfile;
            if (activeProfile != null)
            {
                // Remove the process name
                _databaseService.RemoveProcessName(activeProfile.Id, processName);

                // Also remove thumbnail settings that might match this process name
                _databaseService.DeleteThumbnailSettingsByProcessName(activeProfile.Id, processName);

                LoadProcessNames();
            }
        }
    }
}


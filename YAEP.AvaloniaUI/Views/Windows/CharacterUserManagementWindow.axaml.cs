using SukiUI.Controls;
using System.Runtime.Versioning;
using YAEP.Models;
using YAEP.ViewModels.Windows;

namespace YAEP.Views.Windows
{
    [SupportedOSPlatform("windows")]
    public partial class CharacterUserManagementWindow : SukiWindow
    {
        public CharacterUserManagementWindowViewModel ViewModel { get; }

        public CharacterUserManagementWindow()
        {
            ViewModel = null!;
            DataContext = null;
            InitializeComponent();
        }

        public CharacterUserManagementWindow(EveOnlineProfileService profileService, EveOnlineProfile profile)
        {
            ViewModel = new CharacterUserManagementWindowViewModel(profileService, profile);
            DataContext = ViewModel;
            InitializeComponent();

            this.Opened += CharacterUserManagementWindow_Opened;
        }

        private async void CharacterUserManagementWindow_Opened(object? sender, System.EventArgs e)
        {
            await ViewModel.LoadCharactersAndUsersAsync();
        }
    }
}

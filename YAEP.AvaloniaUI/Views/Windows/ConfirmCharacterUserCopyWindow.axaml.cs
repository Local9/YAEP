using System.Runtime.Versioning;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SukiUI.Controls;
using YAEP.Models;

namespace YAEP.Views.Windows
{
    [SupportedOSPlatform("windows")]
    public partial class ConfirmCharacterUserCopyWindow : SukiWindow
    {
        public EveOnlineCharacter SourceCharacter { get; }
        public EveOnlineUser SourceUser { get; }
        public bool CopyCharacterSettings { get; }
        public bool CopyUserSettings { get; }
        public bool? DialogResult { get; private set; }

        public string WarningMessage
        {
            get
            {
                if (CopyCharacterSettings && CopyUserSettings)
                {
                    return "⚠ Warning: This will overwrite all character settings and user settings in the profile.";
                }
                else if (CopyCharacterSettings)
                {
                    return "⚠ Warning: This will overwrite all character settings in the profile.";
                }
                else if (CopyUserSettings)
                {
                    return "⚠ Warning: This will overwrite all user settings in the profile.";
                }
                return "⚠ Warning: No settings selected to copy.";
            }
        }

        public string DetailMessage
        {
            get
            {
                if (CopyCharacterSettings && CopyUserSettings)
                {
                    return "Selected character and user settings will replace the corresponding settings in this profile.";
                }
                else if (CopyCharacterSettings)
                {
                    return "Selected character settings will replace the character settings in this profile.";
                }
                else if (CopyUserSettings)
                {
                    return "Selected user settings will replace the user settings in this profile.";
                }

                return "No settings will be copied.";
            }
        }

        public ConfirmCharacterUserCopyWindow()
        {
            SourceCharacter = null!;
            SourceUser = null!;
            CopyCharacterSettings = false;
            CopyUserSettings = false;
            DataContext = null;
            InitializeComponent();
        }

        public ConfirmCharacterUserCopyWindow(EveOnlineCharacter sourceCharacter, EveOnlineUser sourceUser, bool copyCharacterSettings, bool copyUserSettings)
        {
            SourceCharacter = sourceCharacter;
            SourceUser = sourceUser;
            CopyCharacterSettings = copyCharacterSettings;
            CopyUserSettings = copyUserSettings;
            DataContext = this;
            InitializeComponent();
        }

        private void ConfirmButton_Click(object? sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close(true);
        }

        private void CancelButton_Click(object? sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close(false);
        }
    }
}

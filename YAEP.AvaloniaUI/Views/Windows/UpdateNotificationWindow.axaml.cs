using Avalonia.Interactivity;
using SukiUI.Controls;

namespace YAEP.Views.Windows
{
    public partial class UpdateNotificationWindow : SukiWindow
    {
        private readonly GitHubReleaseInfo? _releaseInfo;

        public string Version => _releaseInfo?.Version ?? string.Empty;

        public string ReleaseNotes =>
            string.IsNullOrWhiteSpace(_releaseInfo?.Body)
                ? "No release notes available."
                : _releaseInfo?.Body ?? string.Empty;

        public UpdateNotificationWindow()
        {
            InitializeComponent();
        }

        public UpdateNotificationWindow(GitHubReleaseInfo releaseInfo) : this()
        {
            _releaseInfo = releaseInfo;
            DataContext = this;
        }

        private void DownloadButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_releaseInfo != null)
            {
                try
                {
                    Process.Start(
                        new ProcessStartInfo { FileName = _releaseInfo.HtmlUrl, UseShellExecute = true }
                    );
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to open release URL: {ex.Message}");
                }
            }

            Close();
        }

        private void LaterButton_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}

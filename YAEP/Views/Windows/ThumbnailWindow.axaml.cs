using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Threading;
using YAEP.Services;
using YAEP.ViewModels.Windows;

namespace YAEP.Views.Windows
{
    public partial class ThumbnailWindow : Window
    {
        private readonly DatabaseService _databaseService;
        private readonly string _windowTitle;
        private long _profileId;

        public ThumbnailWindowViewModel ViewModel { get; }

        public ThumbnailWindow(string applicationTitle, IntPtr processHandle, DatabaseService databaseService, long profileId)
        {
            _databaseService = databaseService;
            _windowTitle = applicationTitle;
            _profileId = profileId;

            ViewModel = new ThumbnailWindowViewModel(applicationTitle);
            ViewModel.ProcessHandle = processHandle;

            DatabaseService.ThumbnailConfig config = _databaseService.GetThumbnailSettingsOrDefault(profileId, applicationTitle);
            ViewModel.IsAlwaysOnTop = true;
            ViewModel.Width = config.Width;
            ViewModel.Height = config.Height;
            ViewModel.FocusBorderColor = config.FocusBorderColor ?? "#0078D4";
            ViewModel.FocusBorderThickness = config.FocusBorderThickness;
            ViewModel.ShowTitleOverlay = config.ShowTitleOverlay;
            ViewModel.Opacity = config.Opacity;

            DataContext = this;
            InitializeComponent();

            this.Opacity = config.Opacity;
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            this.Position = new Avalonia.PixelPoint(config.X, config.Y);
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.Opacity))
            {
                this.Opacity = ViewModel.Opacity;
            }
        }

        public void ShowWindow() => Show();
        public void CloseWindow() => Close();

        public string WindowTitle => _windowTitle;

        public void UpdateProfile(long newProfileId)
        {
            _profileId = newProfileId;
            DatabaseService.ThumbnailConfig config = _databaseService.GetThumbnailSettingsOrDefault(newProfileId, _windowTitle);

            Dispatcher.UIThread.Post(() =>
            {
                ViewModel.Width = config.Width;
                ViewModel.Height = config.Height;
                this.Width = config.Width;
                this.Height = config.Height;
                ViewModel.Opacity = config.Opacity;
                this.Opacity = config.Opacity;
            });
        }

        public void RefreshSettings()
        {
            UpdateProfile(_profileId);
        }

        public void SetFocusPreview(bool isFocused)
        {
            Dispatcher.UIThread.Post(() =>
            {
                ViewModel.IsFocused = isFocused;
            });
        }

        public void UpdateBorderSettings(string borderColor, int borderThickness)
        {
            Dispatcher.UIThread.Post(() =>
            {
                ViewModel.FocusBorderColor = borderColor ?? "#0078D4";
                ViewModel.FocusBorderThickness = borderThickness;
            });
        }

        public void UpdateTitleOverlayVisibility(bool showTitleOverlay)
        {
            Dispatcher.UIThread.Post(() =>
            {
                ViewModel.ShowTitleOverlay = showTitleOverlay;
            });
        }

        public void UpdateSizeAndOpacity(int width, int height, double opacity)
        {
            Dispatcher.UIThread.Post(() =>
            {
                ViewModel.Width = width;
                ViewModel.Height = height;
                this.Width = width;
                this.Height = height;
                ViewModel.Opacity = opacity;
                this.Opacity = opacity;
            });
        }

        public void PauseFocusCheck() { }
        public void ResumeFocusCheck() { }
    }
}


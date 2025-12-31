using Avalonia.Input;
using SukiUI.Controls;
using System.Runtime.Versioning;
using YAEP.ViewModels.Pages;

namespace YAEP.Views.Windows
{
    public partial class EditProfileWindow : SukiWindow
    {
        public ProfilesViewModel ViewModel { get; }

        public EditProfileWindow()
        {
            ViewModel = null!;
            DataContext = null;
            InitializeComponent();
        }

        [SupportedOSPlatform("windows")]
        public EditProfileWindow(ProfilesViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = viewModel;
            InitializeComponent();

            this.KeyDown += EditProfileWindow_KeyDown;
            this.AddHandler(KeyDownEvent, EditProfileWindow_KeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.IsCapturingHotkey))
            {
                if (ViewModel.IsCapturingHotkey)
                {
                    this.Focusable = true;
                    this.Focus();
                }
            }
        }

        [SupportedOSPlatform("windows")]
        private void EditProfileWindow_KeyDown(object? sender, KeyEventArgs e)
        {
            if (!ViewModel.IsCapturingHotkey)
                return;

            if (e.Key == Key.Escape)
            {
                ViewModel.CancelHotkeyCapture();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
                e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
                e.Key == Key.LeftShift || e.Key == Key.RightShift ||
                e.Key == Key.LWin || e.Key == Key.RWin)
            {
                return;
            }

            KeyModifiers modifiers = e.KeyModifiers;
            ViewModel.HandleCapturedHotkey(e.Key, modifiers);
            e.Handled = true;
        }
    }
}


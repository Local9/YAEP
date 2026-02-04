using System.Runtime.Versioning;
using Avalonia.Input;
using SukiUI.Controls;
using YAEP.ViewModels.Pages;

namespace YAEP.Views.Windows
{
    public partial class EditMumbleLinkWindow : SukiWindow
    {
        public MumbleLinksViewModel ViewModel { get; }

        public EditMumbleLinkWindow()
        {
            ViewModel = null!;
            DataContext = null;
            InitializeComponent();
        }

        [SupportedOSPlatform("windows")]
        public EditMumbleLinkWindow(MumbleLinksViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = viewModel;
            InitializeComponent();

            KeyDown += EditMumbleLinkWindow_KeyDown;
            AddHandler(KeyDownEvent, EditMumbleLinkWindow_KeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.IsCapturingMumbleHotkey) && ViewModel.IsCapturingMumbleHotkey)
            {
                Focusable = true;
                Focus();
            }
        }

        [SupportedOSPlatform("windows")]
        private void EditMumbleLinkWindow_KeyDown(object? sender, KeyEventArgs e)
        {
            if (!ViewModel.IsCapturingMumbleHotkey)
                return;

            if (e.Key == Key.Escape)
            {
                ViewModel.StopCaptureMumbleHotkeyCommand.Execute(null);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
                e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
                e.Key == Key.LeftShift || e.Key == Key.RightShift ||
                e.Key == Key.LWin || e.Key == Key.RWin)
                return;

            KeyModifiers modifiers = e.KeyModifiers;
            ViewModel.HandleCapturedMumbleHotkey(e.Key, modifiers);
            e.Handled = true;
        }
    }
}


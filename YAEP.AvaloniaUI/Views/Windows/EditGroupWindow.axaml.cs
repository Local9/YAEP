using System.Runtime.Versioning;
using Avalonia.Input;
using SukiUI.Controls;
using YAEP.ViewModels.Pages;

namespace YAEP.Views.Windows
{
    public partial class EditGroupWindow : SukiWindow
    {
        public ClientGroupingViewModel ViewModel { get; }

        public EditGroupWindow()
        {
            ViewModel = null!;
            DataContext = null;
            InitializeComponent();
        }

        public EditGroupWindow(ClientGroupingViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = viewModel;
            InitializeComponent();

            this.KeyDown += EditGroupWindow_KeyDown;
            this.AddHandler(KeyDownEvent, EditGroupWindow_KeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.IsCapturingForwardHotkey) ||
                e.PropertyName == nameof(ViewModel.IsCapturingBackwardHotkey))
            {
                if (ViewModel.IsCapturingForwardHotkey || ViewModel.IsCapturingBackwardHotkey)
                {
                    this.Focusable = true;
                    this.Focus();
                }
            }
        }

        [SupportedOSPlatform("windows")]
        private void EditGroupWindow_KeyDown(object? sender, KeyEventArgs e)
        {
            if (!ViewModel.IsCapturingForwardHotkey && !ViewModel.IsCapturingBackwardHotkey)
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


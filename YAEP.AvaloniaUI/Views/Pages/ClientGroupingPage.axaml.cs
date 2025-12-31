using Avalonia.Controls;
using Avalonia.Input;
using System.Runtime.Versioning;
using YAEP.ViewModels.Pages;

namespace YAEP.Views.Pages
{
    public partial class ClientGroupingPage : UserControl
    {
        public ClientGroupingViewModel ViewModel { get; } = null!;

        public ClientGroupingPage()
        {
            InitializeComponent();
        }

        public ClientGroupingPage(ClientGroupingViewModel viewModel) : this()
        {
            ViewModel = viewModel;
            DataContext = viewModel;
            this.KeyDown += ClientGroupingPage_KeyDown;
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
        private void ClientGroupingPage_KeyDown(object? sender, KeyEventArgs e)
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


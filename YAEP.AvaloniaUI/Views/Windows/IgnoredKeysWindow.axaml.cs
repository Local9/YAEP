using Avalonia.Input;
using SukiUI.Controls;
using System.Runtime.Versioning;
using YAEP.ViewModels.Pages;

namespace YAEP.Views.Windows
{
    public partial class IgnoredKeysWindow : SukiWindow
    {
        public ClientGroupingViewModel ViewModel { get; }

        public IgnoredKeysWindow()
        {
            ViewModel = null!;
            DataContext = null;
            InitializeComponent();
        }

        public IgnoredKeysWindow(ClientGroupingViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = viewModel;
            InitializeComponent();

            this.KeyDown += IgnoredKeysWindow_KeyDown;
            this.AddHandler(KeyDownEvent, IgnoredKeysWindow_KeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.IsCapturingIgnoredKey))
            {
                if (ViewModel.IsCapturingIgnoredKey)
                {
                    this.Focusable = true;
                    this.Focus();
                }
            }
        }

        [SupportedOSPlatform("windows")]
        private void IgnoredKeysWindow_KeyDown(object? sender, KeyEventArgs e)
        {
            if (!ViewModel.IsCapturingIgnoredKey)
                return;

            if (e.Key == Key.Escape)
            {
                ViewModel.CancelIgnoredKeyCapture();
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

            ViewModel.HandleCapturedIgnoredKey(e.Key);
            e.Handled = true;
        }
    }
}

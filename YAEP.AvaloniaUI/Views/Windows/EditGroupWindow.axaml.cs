using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using YAEP.ViewModels.Pages;

namespace YAEP.Views.Windows
{
    public partial class EditGroupWindow : Window
    {
        public ClientGroupingViewModel ViewModel { get; }

        public EditGroupWindow(ClientGroupingViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = viewModel;
            InitializeComponent();

            // Subscribe to key events for hotkey capture
            this.KeyDown += EditGroupWindow_KeyDown;
            this.AddHandler(KeyDownEvent, EditGroupWindow_KeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);

            // Subscribe to ViewModel property changes to handle focus
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.IsCapturingForwardHotkey) ||
                e.PropertyName == nameof(ViewModel.IsCapturingBackwardHotkey))
            {
                if (ViewModel.IsCapturingForwardHotkey || ViewModel.IsCapturingBackwardHotkey)
                {
                    // Focus the window to receive key events
                    this.Focusable = true;
                    this.Focus();
                }
            }
        }

        private void EditGroupWindow_KeyDown(object? sender, KeyEventArgs e)
        {
            // Only handle if we're capturing a hotkey
            if (!ViewModel.IsCapturingForwardHotkey && !ViewModel.IsCapturingBackwardHotkey)
                return;

            // Handle ESC key to cancel capture
            if (e.Key == Key.Escape)
            {
                ViewModel.CancelHotkeyCapture();
                e.Handled = true;
                return;
            }

            // Ignore modifier keys by themselves
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
                e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
                e.Key == Key.LeftShift || e.Key == Key.RightShift ||
                e.Key == Key.LWin || e.Key == Key.RWin)
            {
                return;
            }

            // Get the actual modifiers (not the key itself)
            KeyModifiers modifiers = e.KeyModifiers;

            // Handle the captured key
            ViewModel.HandleCapturedHotkey(e.Key, modifiers);

            e.Handled = true;
        }
    }
}


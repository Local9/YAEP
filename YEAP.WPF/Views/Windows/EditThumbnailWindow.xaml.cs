using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using YAEP.ViewModels.Pages;

namespace YAEP.Views.Windows
{
    /// <summary>
    /// Interaction logic for EditThumbnailWindow.xaml
    /// </summary>
    public partial class EditThumbnailWindow : FluentWindow
    {
        public ThumbnailSettingsViewModel ViewModel { get; }

        public EditThumbnailWindow(ThumbnailSettingsViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;

            SystemThemeWatcher.Watch(this);

            InitializeComponent();
        }
    }
}


using Avalonia.Controls;
using YAEP.ViewModels.Windows;

namespace YAEP.Views.Windows
{
    public partial class EditThumbnailWindow : Window
    {
        public EditThumbnailWindowViewModel? ViewModel { get; set; }

        public EditThumbnailWindow()
        {
            InitializeComponent();
        }
    }
}


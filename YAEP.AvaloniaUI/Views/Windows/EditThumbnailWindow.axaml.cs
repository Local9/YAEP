using SukiUI.Controls;
using YAEP.ViewModels.Windows;

namespace YAEP.Views.Windows
{
    public partial class EditThumbnailWindow : SukiWindow
    {
        public EditThumbnailWindowViewModel? ViewModel { get; set; }

        public EditThumbnailWindow()
        {
            InitializeComponent();
        }
    }
}


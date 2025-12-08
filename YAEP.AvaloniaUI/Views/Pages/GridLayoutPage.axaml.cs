using Avalonia.Controls;
using YAEP.ViewModels.Pages;

namespace YAEP.Views.Pages
{
    public partial class GridLayoutPage : UserControl
    {
        public GridLayoutViewModel ViewModel { get; } = null!;

        public GridLayoutPage()
        {
            InitializeComponent();
        }

        public GridLayoutPage(GridLayoutViewModel viewModel) : this()
        {
            ViewModel = viewModel;
            DataContext = viewModel;
        }
    }
}


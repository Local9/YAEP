using Wpf.Ui.Abstractions.Controls;
using YAEP.ViewModels.Pages;

namespace YAEP.Views.Pages
{
    public partial class GridLayoutPage : INavigableView<GridLayoutViewModel>
    {
        public GridLayoutViewModel ViewModel { get; }

        public GridLayoutPage(GridLayoutViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();
        }
    }
}


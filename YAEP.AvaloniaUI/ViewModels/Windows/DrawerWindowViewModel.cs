using System.Collections.ObjectModel;
using YAEP.Models;

namespace YAEP.ViewModels.Windows
{
    public partial class DrawerWindowViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _title = "Drawer";

        [ObservableProperty]
        private double _width = 400;

        [ObservableProperty]
        private double _height = 600;

        [ObservableProperty]
        private bool _isOpen = false;

        [ObservableProperty]
        private DrawerSide _side = DrawerSide.Right;

        [ObservableProperty]
        private int _screenIndex = 0;

        [ObservableProperty]
        private string _hardwareId = string.Empty;

        [ObservableProperty]
        private ObservableCollection<MumbleLink> _mumbleLinks = new();

        [ObservableProperty]
        private double _opacity = 0;

        partial void OnOpacityChanged(double value)
        {
            OnPropertyChanged(nameof(BorderThickness));
        }

        public double BorderThickness => Opacity > 0 ? 1 : 0;
    }
}

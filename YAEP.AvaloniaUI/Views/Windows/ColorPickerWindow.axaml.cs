using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace YAEP.Views.Windows
{
    public partial class ColorPickerWindow : Window
    {
        public Color SelectedColor { get; private set; }
        public bool? DialogResult { get; private set; }

        private ColorView? GetColorPicker()
        {
            return this.FindControl<ColorView>("ColorPickerControl");
        }

        public ColorPickerWindow()
        {
            InitializeComponent();
            SelectedColor = Colors.Black;
            ColorView? colorPicker = GetColorPicker();
            if (colorPicker != null)
            {
                colorPicker.Color = SelectedColor;
            }
        }

        public ColorPickerWindow(Color initialColor) : this()
        {
            SelectedColor = initialColor;
            ColorView? colorPicker = GetColorPicker();
            if (colorPicker != null)
            {
                colorPicker.Color = initialColor;
            }
        }

        private void ColorView_ColorChanged(object? sender, Avalonia.Controls.ColorChangedEventArgs e)
        {
            SelectedColor = e.NewColor;
        }

        private void OKButton_Click(object? sender, RoutedEventArgs e)
        {
            ColorView? colorPicker = GetColorPicker();
            if (colorPicker != null)
            {
                SelectedColor = colorPicker.Color;
            }
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object? sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}


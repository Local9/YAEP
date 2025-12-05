using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace YAEP.Views.Windows
{
    public partial class ColorPickerWindow : Window
    {
        public Color SelectedColor { get; private set; }
        public bool? DialogResult { get; private set; }

        public ColorPickerWindow()
        {
            InitializeComponent();
            SelectedColor = Colors.Black;
        }

        public ColorPickerWindow(Color initialColor) : this()
        {
            SelectedColor = initialColor;

            RedSlider.Value = initialColor.R;
            GreenSlider.Value = initialColor.G;
            BlueSlider.Value = initialColor.B;

            UpdatePreview();
        }

        private void Slider_ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            byte r = (byte)RedSlider.Value;
            byte g = (byte)GreenSlider.Value;
            byte b = (byte)BlueSlider.Value;

            SelectedColor = Color.FromRgb(r, g, b);
            ColorPreview.Background = new SolidColorBrush(SelectedColor);
            HexText.Text = $"#{r:X2}{g:X2}{b:X2}";
        }

        private void OKButton_Click(object? sender, RoutedEventArgs e)
        {
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


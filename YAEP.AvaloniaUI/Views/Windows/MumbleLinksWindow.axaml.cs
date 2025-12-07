using Avalonia.Controls;
using Avalonia.Input;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using YAEP.Services;
using YAEP.ViewModels.Pages;

namespace YAEP.Views.Windows
{
    public partial class MumbleLinksWindow : Window, INotifyPropertyChanged
    {
        private const int WINDOW_POSITION_THRESHOLD_LOW = -10_000;
        private const int WINDOW_POSITION_THRESHOLD_HIGH = 31_000;

        private readonly MumbleLinksViewModel _viewModel;
        private readonly DatabaseService _databaseService;
        private ObservableCollection<DatabaseService.MumbleLink> _displayLinks = new();
        private ObservableCollection<DatabaseService.MumbleLink> _unselectedLinks = new();
        private DatabaseService.MumbleLink? _selectedUnselectedLink;
        private volatile bool _isDragging = false;
        private Avalonia.PixelPoint _dragStartMousePosition;
        private Avalonia.PixelPoint _dragStartWindowPosition;
        private bool _isRightMouseButtonDown = false;
        private bool _isUpdatingProgrammatically = false;
        private Avalonia.PixelPoint _lastKnownPosition;

        public ObservableCollection<DatabaseService.MumbleLink> DisplayLinks
        {
            get => _displayLinks;
        }

        public ObservableCollection<DatabaseService.MumbleLink> UnselectedLinks
        {
            get => _unselectedLinks;
        }

        public DatabaseService.MumbleLink? SelectedUnselectedLink
        {
            get => _selectedUnselectedLink;
            set
            {
                if (_selectedUnselectedLink != value && value != null)
                {
                    _selectedUnselectedLink = value;
                    OnPropertyChanged(nameof(SelectedUnselectedLink));

                    OnOpenLink(value);

                    SelectedUnselectedLink = null;
                }
                else if (value == null && _selectedUnselectedLink != null)
                {
                    _selectedUnselectedLink = null;
                    OnPropertyChanged(nameof(SelectedUnselectedLink));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public MumbleLinksWindow()
        {
            InitializeComponent();
        }

        public MumbleLinksWindow(MumbleLinksViewModel viewModel, List<DatabaseService.MumbleLink> links) : this()
        {
            _viewModel = viewModel;
            _databaseService = viewModel.GetDatabaseService();
            DataContext = this;

            foreach (DatabaseService.MumbleLink link in links)
            {
                _displayLinks.Add(link);
            }

            UpdateUnselectedLinks();

            DatabaseService.MumbleLinksOverlaySettings settings = _databaseService.GetMumbleLinksOverlaySettings();

            double calculatedHeight = CalculateWindowHeight(_displayLinks.Count);

            _isUpdatingProgrammatically = true;

            this.Width = settings.Width;
            this.Height = calculatedHeight;
            this.Topmost = settings.AlwaysOnTop;

            Avalonia.PixelPoint position = new Avalonia.PixelPoint(settings.X, settings.Y);
            if (IsValidWindowPosition(position.X, position.Y))
            {
                this.Position = position;
                _lastKnownPosition = position;
            }
            else
            {
                Avalonia.Platform.Screen? screen = Screens.Primary;
                if (screen != null)
                {
                    position = new Avalonia.PixelPoint(
                        (int)(screen.WorkingArea.Width - Width),
                        100
                    );
                    this.Position = position;
                    _lastKnownPosition = position;
                }
            }

            _isUpdatingProgrammatically = false;

            this.PositionChanged += MumbleLinksWindow_PositionChanged;
            this.Closed += MumbleLinksWindow_Closed;
        }

        private void MumbleLinksWindow_Closed(object? sender, EventArgs e)
        {
            SaveSettings();
        }

        public void UpdateLinks(List<DatabaseService.MumbleLink> links)
        {
            _displayLinks.Clear();
            foreach (DatabaseService.MumbleLink link in links)
            {
                _displayLinks.Add(link);
            }

            UpdateUnselectedLinks();
            UpdateWindowHeight();
        }

        private double CalculateWindowHeight(int linkCount)
        {
            const double headerHeight = 35;
            const double buttonHeight = 48;
            const double bottomSectionHeight = 50;
            const double borderAndPadding = 10;

            double calculatedHeight = headerHeight + (linkCount * buttonHeight) + bottomSectionHeight + borderAndPadding;

            double minHeight = 150;
            double maxHeight = 800;

            return Math.Max(minHeight, Math.Min(maxHeight, calculatedHeight));
        }

        private void UpdateWindowHeight()
        {
            if (_isUpdatingProgrammatically)
                return;

            double newHeight = CalculateWindowHeight(_displayLinks.Count);

            _isUpdatingProgrammatically = true;
            this.Height = newHeight;
            _isUpdatingProgrammatically = false;
        }

        private void UpdateUnselectedLinks()
        {
            List<DatabaseService.MumbleLink> allLinks = _databaseService.GetMumbleLinks();
            List<DatabaseService.MumbleLink> unselected = allLinks.Where(l => !l.IsSelected).OrderBy(l => l.DisplayOrder).ToList();

            _unselectedLinks.Clear();
            foreach (DatabaseService.MumbleLink? link in unselected)
            {
                _unselectedLinks.Add(link);
            }

            OnPropertyChanged(nameof(UnselectedLinks));

            if (_selectedUnselectedLink != null && !_unselectedLinks.Contains(_selectedUnselectedLink))
            {
                SelectedUnselectedLink = null;
            }
        }

        private bool IsValidWindowPosition(int x, int y)
        {
            return x >= WINDOW_POSITION_THRESHOLD_LOW && x <= WINDOW_POSITION_THRESHOLD_HIGH &&
                   y >= WINDOW_POSITION_THRESHOLD_LOW && y <= WINDOW_POSITION_THRESHOLD_HIGH;
        }

        private void Window_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            PointerPointProperties properties = e.GetCurrentPoint(this).Properties;
            bool isRightButton = properties.IsRightButtonPressed;

            if (isRightButton)
            {
                _isRightMouseButtonDown = true;
                _isDragging = true;
                Avalonia.Point position = e.GetPosition(this);
                _dragStartMousePosition = new Avalonia.PixelPoint(
                    this.Position.X + (int)position.X,
                    this.Position.Y + (int)position.Y);
                _dragStartWindowPosition = this.Position;
                e.Pointer.Capture(this);
                e.Handled = true;
            }
        }

        private void Window_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (_isRightMouseButtonDown)
            {
                PointerPointProperties properties = e.GetCurrentPoint(this).Properties;
                if (properties.IsRightButtonPressed)
                {
                    Avalonia.Point position = e.GetPosition(this);
                    Avalonia.PixelPoint currentScreenPosition = new Avalonia.PixelPoint(
                        this.Position.X + (int)position.X,
                        this.Position.Y + (int)position.Y);

                    int deltaX = currentScreenPosition.X - _dragStartMousePosition.X;
                    int deltaY = currentScreenPosition.Y - _dragStartMousePosition.Y;

                    double newX = _dragStartWindowPosition.X + deltaX;
                    double newY = _dragStartWindowPosition.Y + deltaY;

                    int x = (int)newX;
                    int y = (int)newY;

                    if (!IsValidWindowPosition(x, y))
                    {
                        x = 100;
                        y = 100;
                        newX = x;
                        newY = y;
                    }

                    Avalonia.PixelPoint newPosition = new Avalonia.PixelPoint(x, y);
                    this.Position = newPosition;
                }
            }
        }

        private void Window_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (e.InitialPressMouseButton == MouseButton.Right && _isRightMouseButtonDown)
            {
                _isRightMouseButtonDown = false;
                e.Pointer.Capture(null);
                _isDragging = false;
                _lastKnownPosition = this.Position;
                SaveSettings();
                e.Handled = true;
            }
        }

        private void Window_SizeChanged(object? sender, SizeChangedEventArgs e)
        {
            if (!_isUpdatingProgrammatically)
            {
                SaveSettings();
            }
        }

        private void MumbleLinksWindow_PositionChanged(object? sender, EventArgs e)
        {
            if (!_isUpdatingProgrammatically && !_isDragging)
            {
                Avalonia.PixelPoint currentPosition = this.Position;
                if (IsValidWindowPosition(currentPosition.X, currentPosition.Y))
                {
                    _lastKnownPosition = currentPosition;
                }
            }
        }

        private void SaveSettings()
        {
            if (_isUpdatingProgrammatically || _databaseService == null)
                return;

            try
            {
                Avalonia.PixelPoint positionToSave = _lastKnownPosition;
                try
                {
                    Avalonia.PixelPoint currentPosition = this.Position;
                    if (IsValidWindowPosition(currentPosition.X, currentPosition.Y))
                    {
                        positionToSave = currentPosition;
                        _lastKnownPosition = currentPosition;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.WriteLine($"SaveSettings: Error getting current position: {ex.Message}");
                }

                DatabaseService.MumbleLinksOverlaySettings settings = new DatabaseService.MumbleLinksOverlaySettings
                {
                    AlwaysOnTop = this.Topmost,
                    X = positionToSave.X,
                    Y = positionToSave.Y,
                    Width = (int)this.Width,
                    Height = (int)this.Height
                };

                _databaseService.SaveMumbleLinksOverlaySettings(settings);
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"Error saving Mumble links overlay settings: {ex.Message}");
            }
        }

        private void LinkButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is DatabaseService.MumbleLink link)
            {
                OnOpenLink(link);
            }
        }

        private void OnOpenLink(DatabaseService.MumbleLink? link)
        {
            if (link != null && !string.IsNullOrWhiteSpace(link.Url))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = link.Url,
                        UseShellExecute = true
                    });
                }
                catch (System.Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error opening Mumble link: {ex.Message}");
                }
            }
        }

    }
}


using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using WinLayout.Models;
using WinLayout.Native;

namespace WinLayout.Views;

public partial class OverlayWindow : Window
{
    private Rectangle? _dimBackground;
    private readonly List<UIElement> _zoneElements = new();

    // Configurable colors
    private static readonly Color DimColor    = Color.FromArgb(140, 0, 0, 0);   // ~55% black background
    private static readonly Color ZoneColor   = Color.FromArgb(60, 255, 255, 255); // ~25% white — visible zones
    private static readonly Color HighlightColor = Color.FromArgb(90, 0, 120, 215); // ~35% accent blue
    private static readonly Color HighlightBorderColor = Color.FromArgb(255, 0, 120, 215);

    public OverlayWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int exStyle = User32.GetWindowLong(hwnd, User32.GWL_EXSTYLE);
        exStyle |= User32.WS_EX_TRANSPARENT | User32.WS_EX_TOOLWINDOW | User32.WS_EX_NOACTIVATE;
        User32.SetWindowLong(hwnd, User32.GWL_EXSTYLE, exStyle);
    }

    private double _dpiScaleX = 1.0;
    private double _dpiScaleY = 1.0;

    public void ShowOverlay(int screenX, int screenY, int screenWidth, int screenHeight,
        List<ZoneDefinition> zones, int highlightedZoneIndex)
    {
        // Get DPI scale on first call
        if (_dpiScaleX == 1.0 && _dpiScaleY == 1.0)
        {
            var src = PresentationSource.FromVisual(this);
            if (src != null)
            {
                _dpiScaleX = src.CompositionTarget.TransformToDevice.M11;
                _dpiScaleY = src.CompositionTarget.TransformToDevice.M22;
            }
        }

        // Convert physical pixels to WPF DIPs
        Left = screenX / _dpiScaleX;
        Top = screenY / _dpiScaleY;
        Width = screenWidth / _dpiScaleX;
        Height = screenHeight / _dpiScaleY;

        double cw = Width;
        double ch = Height;

        // Clear previous elements
        OverlayCanvas.Children.Clear();
        _zoneElements.Clear();

        // Full-screen dim background
        _dimBackground = new Rectangle
        {
            Width = cw,
            Height = ch,
            Fill = new SolidColorBrush(DimColor),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(_dimBackground, 0);
        Canvas.SetTop(_dimBackground, 0);
        OverlayCanvas.Children.Add(_dimBackground);

        // Draw each zone
        for (int i = 0; i < zones.Count; i++)
        {
            var zone = zones[i];
            double x = zone.Left * cw;
            double y = zone.Top * ch;
            double w = zone.Width * cw;
            double h = zone.Height * ch;

            if (i == highlightedZoneIndex)
            {
                // Highlighted zone: accent color fill with border
                var highlightRect = new Rectangle
                {
                    Width = w,
                    Height = h,
                    Fill = new SolidColorBrush(HighlightColor),
                    Stroke = new SolidColorBrush(HighlightBorderColor),
                    StrokeThickness = 2,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(highlightRect, x);
                Canvas.SetTop(highlightRect, y);
                OverlayCanvas.Children.Add(highlightRect);
                _zoneElements.Add(highlightRect);
            }
            else
            {
                // Non-highlighted zone: light semi-transparent
                var dimRect = new Rectangle
                {
                    Width = w,
                    Height = h,
                    Fill = new SolidColorBrush(ZoneColor),
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(dimRect, x);
                Canvas.SetTop(dimRect, y);
                OverlayCanvas.Children.Add(dimRect);
                _zoneElements.Add(dimRect);
            }

            // Zone label — different color per zone
            var labelColors = new[]
            {
                Color.FromRgb(255, 100, 100),  // red
                Color.FromRgb(100, 255, 100),  // green
                Color.FromRgb(100, 180, 255),  // blue
                Color.FromRgb(255, 220, 80),   // yellow
                Color.FromRgb(200, 120, 255),  // purple
                Color.FromRgb(100, 255, 220),  // teal
            };
            var labelColor = labelColors[i % labelColors.Length];
            var label = new TextBlock
            {
                Text = $"{zone.Index}",
                FontSize = 48,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(labelColor),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(label, x + w / 2 - 20);
            Canvas.SetTop(label, y + h / 2 - 30);
            OverlayCanvas.Children.Add(label);
            _zoneElements.Add(label);
        }

        // Draw zone boundary lines — bright white
        var lineColor = Color.FromArgb(200, 255, 255, 255);
        var lineBrush = new SolidColorBrush(lineColor);

        // Collect unique X positions for vertical lines (exclude screen edges 0 and 1)
        var vertLines = new HashSet<double>();
        var horizLines = new HashSet<double>();
        foreach (var z in zones)
        {
            if (z.Left > 0.001) vertLines.Add(z.Left);
            if (z.Top > 0.001) horizLines.Add(z.Top);
            double right = z.Left + z.Width;
            double bottom = z.Top + z.Height;
            if (right < 0.999) vertLines.Add(right);
            if (bottom < 0.999) horizLines.Add(bottom);
        }

        foreach (var vx in vertLines)
        {
            var line = new Rectangle
            {
                Width = 1,
                Height = ch,
                Fill = lineBrush,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(line, vx * cw);
            Canvas.SetTop(line, 0);
            OverlayCanvas.Children.Add(line);
        }
        foreach (var hy in horizLines)
        {
            var line = new Rectangle
            {
                Width = cw,
                Height = 1,
                Fill = lineBrush,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(line, 0);
            Canvas.SetTop(line, hy * ch);
            OverlayCanvas.Children.Add(line);
        }

        // Always call Show() to ensure window is visible.
        // Do not check Visibility — WPF default is Visible even when HWND doesn't exist.
        Show();
        Topmost = true;
    }

    public void HideOverlay()
    {
        Hide();
    }
}

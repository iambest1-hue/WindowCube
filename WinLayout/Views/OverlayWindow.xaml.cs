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

        // Draw zone boundary lines — only where adjacent zones share edges
        var lineColor = Color.FromArgb(200, 255, 255, 255);
        var lineBrush = new SolidColorBrush(lineColor);

        for (int i = 0; i < zones.Count; i++)
        {
            for (int j = i + 1; j < zones.Count; j++)
            {
                var a = zones[i];
                var b = zones[j];
                const double eps = 0.001;
                double aR = a.Left + a.Width, aB = a.Top + a.Height;
                double bR = b.Left + b.Width, bB = b.Top + b.Height;

                // Vertical shared edge
                double edgeV = double.NaN;
                if (Math.Abs(aR - b.Left) < eps) edgeV = aR;
                else if (Math.Abs(bR - a.Left) < eps) edgeV = bR;

                if (!double.IsNaN(edgeV) && edgeV > eps && edgeV < 1.0 - eps)
                {
                    double top = Math.Max(a.Top, b.Top);
                    double bottom = Math.Min(aB, bB);
                    if (bottom - top > eps)
                    {
                        var line = new Rectangle
                        {
                            Width = 1,
                            Height = (bottom - top) * ch,
                            Fill = lineBrush,
                            IsHitTestVisible = false
                        };
                        Canvas.SetLeft(line, edgeV * cw);
                        Canvas.SetTop(line, top * ch);
                        OverlayCanvas.Children.Add(line);
                    }
                }

                // Horizontal shared edge
                double edgeH = double.NaN;
                if (Math.Abs(aB - b.Top) < eps) edgeH = aB;
                else if (Math.Abs(bB - a.Top) < eps) edgeH = bB;

                if (!double.IsNaN(edgeH) && edgeH > eps && edgeH < 1.0 - eps)
                {
                    double left = Math.Max(a.Left, b.Left);
                    double right = Math.Min(aR, bR);
                    if (right - left > eps)
                    {
                        var line = new Rectangle
                        {
                            Width = (right - left) * cw,
                            Height = 1,
                            Fill = lineBrush,
                            IsHitTestVisible = false
                        };
                        Canvas.SetLeft(line, left * cw);
                        Canvas.SetTop(line, edgeH * ch);
                        OverlayCanvas.Children.Add(line);
                    }
                }
            }
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

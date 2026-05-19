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

    // Configurable colors — pulled from UserConfig later
    private static readonly Color DimColor = Color.FromArgb(128, 0, 0, 0);       // 50% black
    private static readonly Color HighlightColor = Color.FromArgb(90, 0, 120, 215); // ~35% system blue
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

    public void ShowOverlay(int screenX, int screenY, int screenWidth, int screenHeight,
        List<ZoneDefinition> zones, int highlightedZoneIndex)
    {
        // Position the window to cover this screen
        Left = screenX;
        Top = screenY;
        Width = screenWidth;
        Height = screenHeight;

        // Clear previous elements
        OverlayCanvas.Children.Clear();
        _zoneElements.Clear();

        // Full-screen dim background
        _dimBackground = new Rectangle
        {
            Width = screenWidth,
            Height = screenHeight,
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
            double x = zone.Left * screenWidth;
            double y = zone.Top * screenHeight;
            double w = zone.Width * screenWidth;
            double h = zone.Height * screenHeight;

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

                // Zone label
                var label = new TextBlock
                {
                    Text = $"{zone.Index}",
                    FontSize = 48,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(label, x + w / 2 - 20);
                Canvas.SetTop(label, y + h / 2 - 30);
                OverlayCanvas.Children.Add(label);
                _zoneElements.Add(label);
            }
            else
            {
                // Non-highlighted: black dim
                var dimRect = new Rectangle
                {
                    Width = w,
                    Height = h,
                    Fill = new SolidColorBrush(DimColor),
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(dimRect, x);
                Canvas.SetTop(dimRect, y);
                OverlayCanvas.Children.Add(dimRect);
                _zoneElements.Add(dimRect);
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

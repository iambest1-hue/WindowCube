using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using WinLayout.Models;
using WinLayout.Services;

namespace WinLayout.Views;

public partial class LayoutEditorWindow : Window
{
    private readonly LayoutService _layoutService;
    private readonly ConfigService _configService;
    private List<ZoneDefinition> _zones = new();
    private LayoutDefinition? _currentLayout;
    private readonly List<FrameworkElement> _zoneVisuals = new();

    private UIElement? _draggingSplitter;
    private bool _isRendering;
    private bool _isHorizontalSplitter;
    private int _zoneA, _zoneB; // The two zones separated by this splitter

    public LayoutEditorWindow(LayoutService layoutService, ConfigService configService)
    {
        InitializeComponent();
        _layoutService = layoutService;
        _configService = configService;

        AddHandler(PreviewMouseMoveEvent, (MouseEventHandler)OnDragMouseMove, handledEventsToo: true);
        AddHandler(PreviewMouseLeftButtonUpEvent, (MouseButtonEventHandler)OnDragMouseUp, handledEventsToo: true);

        LoadTemplates();
        LoadCurrentLayout();
    }

    private bool _suppressTemplateChanged;

    private void LoadTemplates()
    {
        _suppressTemplateChanged = true;
        TemplateCombo.ItemsSource = null;
        TemplateCombo.Items.Clear();
        TemplateCombo.Items.Add("-- 选择模板 --");
        foreach (var t in PresetTemplates.All)
            TemplateCombo.Items.Add(t);
        TemplateCombo.SelectedIndex = 0;
        _suppressTemplateChanged = false;
    }

    private void LoadCurrentLayout()
    {
        var layout = _layoutService.GetActiveLayout();
        if (layout != null)
            LoadLayout(layout);
        else
            LoadPreset(PresetTemplates.All[0]);
    }

    private void LoadLayout(LayoutDefinition layout)
    {
        _currentLayout = layout;
        _zones = layout.Zones.Select(z => new ZoneDefinition
        {
            Index = z.Index,
            Left = z.Left,
            Top = z.Top,
            Width = z.Width,
            Height = z.Height,
            Padding = z.Padding
        }).ToList();

        LayoutNameBox.Text = layout.Name;
        RenderPreview();
    }

    private void LoadPreset(PresetTemplate template)
    {
        _zones = template.Zones.Select(z => new ZoneDefinition
        {
            Index = z.Index,
            Left = z.Left,
            Top = z.Top,
            Width = z.Width,
            Height = z.Height,
            Padding = z.Padding
        }).ToList();

        LayoutNameBox.Text = template.Name;
        _currentLayout = null;
        RenderPreview();
    }

    private void RenderPreview()
    {
        if (_isRendering) return;
        _isRendering = true;
        try
        {
            PreviewCanvas.Children.Clear();
            _zoneVisuals.Clear();

        double cw = PreviewCanvas.ActualWidth;
        double ch = PreviewCanvas.ActualHeight;

        if (cw <= 0 || ch <= 0) return;

        var colors = new[]
        {
            Color.FromArgb(80, 66, 133, 244),
            Color.FromArgb(80, 52, 168, 83),
            Color.FromArgb(80, 251, 188, 4),
            Color.FromArgb(80, 234, 67, 53),
            Color.FromArgb(80, 142, 68, 173),
        };

        // Draw zones
        for (int i = 0; i < _zones.Count; i++)
        {
            var z = _zones[i];
            int x = (int)(z.Left * cw);
            int y = (int)(z.Top * ch);
            int w = (int)(z.Width * cw);
            int h = (int)(z.Height * ch);

            var rect = new Rectangle
            {
                Width = w,
                Height = h,
                Fill = new SolidColorBrush(colors[i % colors.Length]),
                Stroke = new SolidColorBrush(Colors.Gray),
                StrokeThickness = 1
            };
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y);
            PreviewCanvas.Children.Add(rect);
            _zoneVisuals.Add(rect);

            // Zone label
            var label = new TextBlock
            {
                Text = $"{z.Index}",
                FontSize = Math.Min(w, h) * 0.3,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255))
            };
            Canvas.SetLeft(label, x + w / 2 - 15);
            Canvas.SetTop(label, y + h / 2 - 15);
            PreviewCanvas.Children.Add(label);
            _zoneVisuals.Add(label);
        }

        // Draw splitters between zones
        DrawSplitters(cw, ch);
        }
        finally { _isRendering = false; }
    }

    private void DrawSplitters(double cw, double ch)
    {
        for (int i = 0; i < _zones.Count; i++)
        {
            for (int j = i + 1; j < _zones.Count; j++)
            {
                var edge = FindSplitterEdge(_zones[i], _zones[j]);
                if (edge == null) continue;

                var (x, y, w, h, isHorizontal) = edge.Value;

                var splitter = new Rectangle
                {
                    Width = Math.Max(w, 6),
                    Height = Math.Max(h, 6),
                    Fill = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                    Cursor = isHorizontal ? Cursors.SizeNS : Cursors.SizeWE,
                    Tag = (i, j, isHorizontal)
                };
                Canvas.SetLeft(splitter, x - (isHorizontal ? 0 : 3));
                Canvas.SetTop(splitter, y - (isHorizontal ? 3 : 0));

                splitter.MouseDown += OnSplitterMouseDown;
                PreviewCanvas.Children.Add(splitter);
                _zoneVisuals.Add(splitter);
            }
        }
    }

    private (int X, int Y, int W, int H, bool IsHorizontal)? FindSplitterEdge(
        ZoneDefinition a, ZoneDefinition b)
    {
        const double eps = 0.001;

        double aR = a.Left + a.Width;
        double aB = a.Top + a.Height;
        double bR = b.Left + b.Width;
        double bB = b.Top + b.Height;

        // Vertical splitter: a's right edge touches b's left edge
        if (Math.Abs(aR - b.Left) < eps)
        {
            double overlapTop = Math.Max(a.Top, b.Top);
            double overlapBottom = Math.Min(aB, bB);
            if (overlapBottom > overlapTop)
            {
                int x = (int)(aR * PreviewCanvas.ActualWidth);
                int y = (int)(overlapTop * PreviewCanvas.ActualHeight);
                int h = (int)((overlapBottom - overlapTop) * PreviewCanvas.ActualHeight);
                return (x, y, 6, h, false);
            }
        }

        // Horizontal splitter: a's bottom edge touches b's top edge
        if (Math.Abs(aB - b.Top) < eps)
        {
            double overlapLeft = Math.Max(a.Left, b.Left);
            double overlapRight = Math.Min(aR, bR);
            if (overlapRight > overlapLeft)
            {
                int y = (int)(aB * PreviewCanvas.ActualHeight);
                int x = (int)(overlapLeft * PreviewCanvas.ActualWidth);
                int w = (int)((overlapRight - overlapLeft) * PreviewCanvas.ActualWidth);
                return (x, y, w, 6, true);
            }
        }

        // Reverse: b's right touches a's left
        if (Math.Abs(bR - a.Left) < eps)
        {
            return FindSplitterEdge(b, a);
        }
        // Reverse: b's bottom touches a's top
        if (Math.Abs(bB - a.Top) < eps)
        {
            return FindSplitterEdge(b, a);
        }

        return null;
    }

    private int _diagDownCount;

    private void OnSplitterMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Rectangle splitter && splitter.Tag is (int i, int j, bool isH))
        {
            _draggingSplitter = splitter;
            _zoneA = i;
            _zoneB = j;
            _isHorizontalSplitter = isH;
            Mouse.Capture(PreviewCanvas, CaptureMode.SubTree);
            e.Handled = true;
        }
    }

    private void OnDragMouseMove(object sender, MouseEventArgs e)
    {
        if (_draggingSplitter == null) return;
        if (e.LeftButton != MouseButtonState.Pressed) return;

        var pos = e.GetPosition(PreviewCanvas);
        double relX = Math.Clamp(pos.X / PreviewCanvas.ActualWidth, 0.05, 0.95);
        double relY = Math.Clamp(pos.Y / PreviewCanvas.ActualHeight, 0.05, 0.95);

        var a = _zones[_zoneA];
        var b = _zones[_zoneB];

        if (_isHorizontalSplitter)
        {
            double newAHeight = relY - a.Top;
            double newBHeight = (b.Top + b.Height) - relY;
            a.Height = Math.Max(0.05, newAHeight);
            b.Top = relY;
            b.Height = Math.Max(0.05, newBHeight);
        }
        else
        {
            double newAWidth = relX - a.Left;
            double newBWidth = (b.Left + b.Width) - relX;
            a.Width = Math.Max(0.05, newAWidth);
            b.Left = relX;
            b.Width = Math.Max(0.05, newBWidth);
        }

        RenderPreview();
        UpdateStatus(relX, relY);
    }

    private void OnDragMouseUp(object sender, MouseButtonEventArgs e)
    {
        Mouse.Capture(null);
        _draggingSplitter = null;
    }

    private void UpdateStatus(double relX, double relY)
    {
        StatusText.Text = _isHorizontalSplitter
            ? $"区域{_zoneA + 1}: {(int)(_zones[_zoneA].Height * 100)}% / 区域{_zoneB + 1}: {(int)(_zones[_zoneB].Height * 100)}%"
            : $"区域{_zoneA + 1}: {(int)(_zones[_zoneA].Width * 100)}% / 区域{_zoneB + 1}: {(int)(_zones[_zoneB].Width * 100)}%";
    }

    private void OnTemplateSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressTemplateChanged) return;
        if (TemplateCombo.SelectedItem is PresetTemplate template)
        {
            LoadPreset(template);
        }
    }

    private void OnNew(object sender, RoutedEventArgs e)
    {
        _currentLayout = null;
        _zones = PresetTemplates.All[0].Zones.Select(z => new ZoneDefinition
        {
            Index = z.Index, Left = z.Left, Top = z.Top,
            Width = z.Width, Height = z.Height, Padding = z.Padding
        }).ToList();
        LayoutNameBox.Text = "新建布局";
        RenderPreview();
        StatusText.Text = "已创建新布局";
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var name = LayoutNameBox.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            System.Windows.MessageBox.Show("请输入布局名称。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var layout = _currentLayout ?? new LayoutDefinition();
        layout.Name = name;
        layout.Zones = _zones.Select((z, i) => new ZoneDefinition
        {
            Index = i + 1,
            Left = z.Left,
            Top = z.Top,
            Width = z.Width,
            Height = z.Height,
            Padding = z.Padding
        }).ToList();

        _layoutService.Save(layout);
        _currentLayout = layout;
        StatusText.Text = $"已保存: {name}";
    }

    private void OnDelete(object sender, RoutedEventArgs e)
    {
        if (_currentLayout == null)
        {
            System.Windows.MessageBox.Show("请先保存布局再删除。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = System.Windows.MessageBox.Show(
            $"确定删除布局 \"{_currentLayout.Name}\" 吗？",
            "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _layoutService.Delete(_currentLayout.LayoutId);
            _currentLayout = null;
            _zones.Clear();
            LayoutNameBox.Text = "";
            PreviewCanvas.Children.Clear();
            _zoneVisuals.Clear();
            StatusText.Text = "已删除布局";
        }
    }

    private void OnCanvasSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isRendering) return;
        if (PreviewCanvas.ActualWidth > 0 && PreviewCanvas.ActualHeight > 0)
            RenderPreview();
    }
}

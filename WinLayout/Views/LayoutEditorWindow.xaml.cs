using System.Linq;
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
    private bool _suppressLayoutChanged;
    private bool _suppressDirty;
    private bool _isDirty;
    private int _zoneA, _zoneB;

    public LayoutEditorWindow(LayoutService layoutService, ConfigService configService)
    {
        InitializeComponent();
        _layoutService = layoutService;
        _configService = configService;

        AddHandler(PreviewMouseMoveEvent, (MouseEventHandler)OnDragMouseMove, handledEventsToo: true);
        AddHandler(PreviewMouseLeftButtonUpEvent, (MouseButtonEventHandler)OnDragMouseUp, handledEventsToo: true);
        LayoutNameBox.TextChanged += (_, _) => { if (!_suppressDirty) _isDirty = true; };

        RefreshLayoutList();
        LoadCurrentLayout();
    }

    private void RefreshLayoutList()
    {
        _suppressLayoutChanged = true;
        var layouts = _layoutService.GetAllLayouts().OrderBy(l => l.Zones.Count).ToList();
        LayoutCombo.ItemsSource = layouts;
        _suppressLayoutChanged = false;
    }

    private void LoadCurrentLayout()
    {
        var layout = _layoutService.GetActiveLayout();
        if (layout != null)
        {
            _currentLayout = layout;
            _zones = layout.Zones.Select(z => new ZoneDefinition
            {
                Index = z.Index, Left = z.Left, Top = z.Top,
                Width = z.Width, Height = z.Height, Padding = z.Padding
            }).ToList();
            _suppressDirty = true;
            LayoutNameBox.Text = layout.Name;
            _suppressDirty = false;
            _isDirty = false;
            RenderPreview();

            // Select matching item in combo
            _suppressLayoutChanged = true;
            for (int i = 0; i < LayoutCombo.Items.Count; i++)
            {
                if (LayoutCombo.Items[i] is LayoutDefinition l && l.LayoutId == layout.LayoutId)
                {
                    LayoutCombo.SelectedIndex = i;
                    break;
                }
            }
            _suppressLayoutChanged = false;
            StatusText.Text = $"已加载: {layout.Name}";
        }
        else
        {
            LoadPreset(PresetTemplates.All[0]);
        }
    }

    private void LoadPreset(PresetTemplate template)
    {
        _zones = template.Zones.Select(z => new ZoneDefinition
        {
            Index = z.Index, Left = z.Left, Top = z.Top,
            Width = z.Width, Height = z.Height, Padding = z.Padding
        }).ToList();
        _suppressDirty = true;
        LayoutNameBox.Text = template.Name;
        _suppressDirty = false;
        _currentLayout = null;
        _isDirty = true;
        RenderPreview();
        StatusText.Text = $"模板: {template.Name}（请修改后保存）";
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

            for (int i = 0; i < _zones.Count; i++)
            {
                var z = _zones[i];
                int x = (int)(z.Left * cw);
                int y = (int)(z.Top * ch);
                int w = (int)(z.Width * cw);
                int h = (int)(z.Height * ch);

                var rect = new Rectangle
                {
                    Width = w, Height = h,
                    Fill = new SolidColorBrush(colors[i % colors.Length]),
                    Stroke = new SolidColorBrush(Colors.Gray),
                    StrokeThickness = 1
                };
                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, y);
                PreviewCanvas.Children.Add(rect);
                _zoneVisuals.Add(rect);

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
        double aR = a.Left + a.Width, aB = a.Top + a.Height;
        double bR = b.Left + b.Width, bB = b.Top + b.Height;

        // Vertical edge: a's right at b's left, or b's right at a's left
        double edgeV = double.NaN;
        if (Math.Abs(aR - b.Left) < eps) edgeV = aR;
        else if (Math.Abs(bR - a.Left) < eps) edgeV = bR;

        if (!double.IsNaN(edgeV))
        {
            double t = Math.Max(a.Top, b.Top);
            double bt = Math.Min(aB, bB);
            if (bt - t > eps)
            {
                int x = (int)(edgeV * PreviewCanvas.ActualWidth);
                int y = (int)(t * PreviewCanvas.ActualHeight);
                int h = (int)((bt - t) * PreviewCanvas.ActualHeight);
                return (x, y, 6, h, false);
            }
        }

        // Horizontal edge: a's bottom at b's top, or b's bottom at a's top
        double edgeH = double.NaN;
        if (Math.Abs(aB - b.Top) < eps) edgeH = aB;
        else if (Math.Abs(bB - a.Top) < eps) edgeH = bB;

        if (!double.IsNaN(edgeH))
        {
            double l = Math.Max(a.Left, b.Left);
            double r = Math.Min(aR, bR);
            if (r - l > eps)
            {
                int y = (int)(edgeH * PreviewCanvas.ActualHeight);
                int x = (int)(l * PreviewCanvas.ActualWidth);
                int w = (int)((r - l) * PreviewCanvas.ActualWidth);
                return (x, y, w, 6, true);
            }
        }

        return null;
    }

    private void OnSplitterMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Rectangle splitter && splitter.Tag is (int i, int j, bool isH))
        {
            _draggingSplitter = splitter;
            _zoneA = i; _zoneB = j;
            _isHorizontalSplitter = isH;
            Mouse.Capture(PreviewCanvas, CaptureMode.SubTree);
            e.Handled = true;
        }
    }

    private bool _defaultStripped;

    private void OnDragMouseMove(object sender, MouseEventArgs e)
    {
        if (_draggingSplitter == null) return;
        if (e.LeftButton != MouseButtonState.Pressed) return;

        _isDirty = true;

        // Strip "(默认)" suffix on first move so user sees it in real-time
        if (_currentLayout != null && _currentLayout.IsDefault)
        {
            _currentLayout.IsDefault = false;
            var defaultSuffix = " (默认)";
            if (LayoutNameBox.Text.EndsWith(defaultSuffix))
                LayoutNameBox.Text = LayoutNameBox.Text[..^defaultSuffix.Length];
            _defaultStripped = true;
        }

        var pos = e.GetPosition(PreviewCanvas);
        double relX = Math.Clamp(pos.X / PreviewCanvas.ActualWidth, 0.05, 0.95);
        double relY = Math.Clamp(pos.Y / PreviewCanvas.ActualHeight, 0.05, 0.95);

        var a = _zones[_zoneA];
        var b = _zones[_zoneB];

        if (_isHorizontalSplitter)
        {
            // Store original values before any modification
            double origBTop = b.Top;
            double origBHeight = b.Height;
            double origBTopPlusHeight = origBTop + origBHeight;

            double newAHeight = relY - a.Top;
            double newBTop = relY;
            double newBHeight = origBTopPlusHeight - newBTop;

            a.Height = Math.Max(0.05, newAHeight);
            b.Top = newBTop;
            b.Height = Math.Max(0.05, newBHeight);
        }
        else
        {
            double origBLeft = b.Left;
            double origBWidth = b.Width;
            double origBLeftPlusWidth = origBLeft + origBWidth;

            double newAWidth = relX - a.Left;
            double newBLeft = relX;
            double newBWidth = origBLeftPlusWidth - newBLeft;

            a.Width = Math.Max(0.05, newAWidth);
            b.Left = newBLeft;
            b.Width = Math.Max(0.05, newBWidth);
        }

        RenderPreview();
        UpdateStatus(relX, relY);
    }

    private void OnDragMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggingSplitter != null)
        {
            Mouse.Capture(null);
            _draggingSplitter = null;

            if (_defaultStripped && _currentLayout != null)
            {
                _layoutService.Save(_currentLayout);
                _defaultStripped = false;
            }
        }
    }

    private void UpdateStatus(double relX, double relY)
    {
        StatusText.Text = _isHorizontalSplitter
            ? $"区域{_zoneA + 1}: {(int)(_zones[_zoneA].Height * 100)}% / 区域{_zoneB + 1}: {(int)(_zones[_zoneB].Height * 100)}%"
            : $"区域{_zoneA + 1}: {(int)(_zones[_zoneA].Width * 100)}% / 区域{_zoneB + 1}: {(int)(_zones[_zoneB].Width * 100)}%";
    }

    private void OnLayoutSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressLayoutChanged) return;
        if (LayoutCombo.SelectedItem is LayoutDefinition layout)
        {
            _currentLayout = layout;
            _zones = layout.Zones.Select(z => new ZoneDefinition
            {
                Index = z.Index, Left = z.Left, Top = z.Top,
                Width = z.Width, Height = z.Height, Padding = z.Padding
            }).ToList();
            _suppressDirty = true;
            LayoutNameBox.Text = layout.Name;
            _suppressDirty = false;
            _isDirty = false;
            RenderPreview();
            StatusText.Text = $"已加载: {layout.Name}";
        }
    }

    private void OnNewFromTemplate(object sender, RoutedEventArgs e)
    {
        // Show a simple choice dialog for preset templates
        var dialog = new Window
        {
            Title = "选择预设模板",
            Width = 300, Height = 350,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize
        };
        var listBox = new ListBox
        {
            DisplayMemberPath = "Name",
            Margin = new Thickness(10),
            ItemsSource = PresetTemplates.All
        };
        var btn = new Button
        {
            Content = "确定", Width = 60, Height = 28,
            Margin = new Thickness(10), IsDefault = true
        };
        var sp = new StackPanel();
        sp.Children.Add(listBox);
        sp.Children.Add(btn);
        dialog.Content = sp;
        btn.Click += (_, _) =>
        {
            if (listBox.SelectedItem is PresetTemplate template)
            {
                ApplyTemplateAndSave(template);
            }
            dialog.Close();
        };
        listBox.MouseDoubleClick += (_, _) =>
        {
            if (listBox.SelectedItem is PresetTemplate template)
            {
                ApplyTemplateAndSave(template);
                dialog.Close();
            }
        };
        dialog.ShowDialog();
    }

    private void ApplyTemplateAndSave(PresetTemplate template)
    {
        // Find existing default layout for this template or create/save new one
        var presetName = template.Name + " (默认)";
        var allLayouts = _layoutService.GetAllLayouts();
        var existing = allLayouts.FirstOrDefault(l => l.Name == presetName);
        if (existing != null)
        {
            _currentLayout = existing;
            _zones = template.Zones.Select(z => new ZoneDefinition
            {
                Index = z.Index, Left = z.Left, Top = z.Top,
                Width = z.Width, Height = z.Height, Padding = z.Padding
            }).ToList();
            _suppressDirty = true;
            LayoutNameBox.Text = template.Name;
            _suppressDirty = false;
            _isDirty = false;
            RenderPreview();
            StatusText.Text = $"模板: {template.Name}（默认布局）";
            // Select in combo
            _suppressLayoutChanged = true;
            for (int i = 0; i < LayoutCombo.Items.Count; i++)
            {
                if (LayoutCombo.Items[i] is LayoutDefinition l && l.LayoutId == existing.LayoutId)
                {
                    LayoutCombo.SelectedIndex = i;
                    break;
                }
            }
            _suppressLayoutChanged = false;
        }
        else
        {
            LoadPreset(template);
        }
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var name = LayoutNameBox.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            System.Windows.MessageBox.Show("请输入布局名称。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (_zones.Count == 0)
        {
            System.Windows.MessageBox.Show("布局区域为空，无法保存。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // Reuse existing layout with same name to avoid duplicates
        var allLayouts = _layoutService.GetAllLayouts();
        var existing = allLayouts.FirstOrDefault(l =>
            string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase) &&
            (_currentLayout == null || l.LayoutId != _currentLayout.LayoutId));
        if (existing != null)
        {
            var result = System.Windows.MessageBox.Show(
                $"已存在同名布局 \"{name}\"，是否覆盖？", "同名冲突",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;
            _currentLayout = existing;
        }

        var layout = _currentLayout ?? new LayoutDefinition();
        layout.Name = name;
        layout.Zones = _zones.Select((z, i) => new ZoneDefinition
        {
            Index = i + 1,
            Left = z.Left, Top = z.Top,
            Width = z.Width, Height = z.Height,
            Padding = z.Padding
        }).ToList();

        _layoutService.Save(layout);
        _currentLayout = layout;
        _isDirty = false;

        // Verify by reading back
        var verify = _layoutService.GetActiveLayout();
        StatusText.Text = verify?.LayoutId == layout.LayoutId
            ? $"已保存: {name}"
            : $"已保存: {name}（注意：未设为活跃）";

        RefreshLayoutList();
    }

    private void OnDelete(object sender, RoutedEventArgs e)
    {
        if (_currentLayout == null)
        {
            System.Windows.MessageBox.Show("请先保存布局再删除。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (_currentLayout.IsDefault)
        {
            System.Windows.MessageBox.Show("默认布局模板不可删除。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var result = System.Windows.MessageBox.Show(
            $"确定删除布局 \"{_currentLayout.Name}\" 吗？", "确认删除",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            _layoutService.Delete(_currentLayout.LayoutId);
            _currentLayout = null;
            _zones.Clear();
            LayoutNameBox.Text = "";
            PreviewCanvas.Children.Clear();
            _zoneVisuals.Clear();
            RefreshLayoutList();
            LoadPreset(PresetTemplates.All[0]);
            StatusText.Text = "已删除布局";
        }
    }

    private void OnCanvasSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isRendering) return;
        if (PreviewCanvas.ActualWidth > 0 && PreviewCanvas.ActualHeight > 0)
            RenderPreview();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_isDirty)
        {
            var result = MessageBox.Show(
                "当前布局有未保存的更改，是否保存？\n\n是 — 保存后关闭\n否 — 放弃更改并关闭\n取消 — 返回编辑器",
                "未保存的更改", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

            switch (result)
            {
                case MessageBoxResult.Yes:
                    OnSave(this, new RoutedEventArgs());
                    if (_isDirty)
                        e.Cancel = true;
                    break;
                case MessageBoxResult.No:
                    break;
                case MessageBoxResult.Cancel:
                    e.Cancel = true;
                    break;
            }
        }
        base.OnClosing(e);
    }
}

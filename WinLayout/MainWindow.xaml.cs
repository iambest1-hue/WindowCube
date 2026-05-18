using System.Diagnostics;
using System.Windows;
using WinLayout.Services;
using WinLayout.Views;

namespace WinLayout;

public partial class MainWindow : Window
{
    private readonly ConfigService _configService = new();
    private readonly LayoutService _layoutService;
    private readonly WindowManager _windowManager = new();
    private HookService? _hookService;
    private OverlayService? _overlayService;

    public MainWindow()
    {
        InitializeComponent();

        _layoutService = new LayoutService(_configService);
        _overlayService = new OverlayService(_configService, _layoutService);

        _hookService = new HookService(Dispatcher, _configService);
        _hookService.DragStarted += OnDragStarted;
        _hookService.DragMoved += OnDragMoved;
        _hookService.DragEnded += OnDragEnded;
        _hookService.Start();

        var layout = _layoutService.GetActiveLayout();
        StatusLabel.Text = layout != null
            ? $"当前布局: {layout.Name} — Shift+拖拽吸附就绪"
            : "Shift+拖拽吸附就绪";
    }

    private void OnOpenEditor(object sender, RoutedEventArgs e)
    {
        var editor = new LayoutEditorWindow(_layoutService, _configService);
        editor.Owner = this;
        editor.Closed += (_, _) =>
        {
            var layout = _layoutService.GetActiveLayout();
            StatusLabel.Text = layout != null
                ? $"当前布局: {layout.Name} — Shift+拖拽吸附就绪"
                : "Shift+拖拽吸附就绪";
        };
        editor.ShowDialog();
    }

    private void OnDragStarted(object? sender, WindowDragEventArgs e)
    {
        Debug.WriteLine($"[MainWindow] DragStarted hwnd=0x{e.WindowHandle:X}");
        _overlayService?.Show(e.CursorX, e.CursorY);
    }

    private void OnDragMoved(object? sender, WindowDragEventArgs e)
    {
        _overlayService?.UpdateCursor(e.CursorX, e.CursorY);
    }

    private void OnDragEnded(object? sender, WindowDragEventArgs e)
    {
        Debug.WriteLine($"[MainWindow] DragEnded hwnd=0x{e.WindowHandle:X}");

        if (_overlayService != null)
        {
            var target = _overlayService.GetSnapTarget(e.CursorX, e.CursorY);
            if (target != null)
            {
                _windowManager.SnapWindow(e.WindowHandle, target);
            }
            _overlayService.Hide();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _hookService?.Dispose();
        base.OnClosed(e);
    }
}

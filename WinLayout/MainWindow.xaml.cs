using System.Diagnostics;
using System.Windows;
using WinLayout.Services;

namespace WinLayout;

public partial class MainWindow : Window
{
    private readonly ConfigService _configService = new();
    private HookService? _hookService;
    private OverlayService? _overlayService;

    public MainWindow()
    {
        InitializeComponent();

        _overlayService = new OverlayService(_configService);

        _hookService = new HookService(Dispatcher, _configService);
        _hookService.DragStarted += OnDragStarted;
        _hookService.DragMoved += OnDragMoved;
        _hookService.DragEnded += OnDragEnded;
        _hookService.Start();

        Title = $"WinLayout — Shift+拖拽吸附就绪";
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
        _overlayService?.Hide();
    }

    protected override void OnClosed(EventArgs e)
    {
        _hookService?.Dispose();
        base.OnClosed(e);
    }
}

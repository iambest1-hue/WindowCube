using System.Diagnostics;
using System.Windows;
using WinLayout.Services;

namespace WinLayout;

public partial class MainWindow : Window
{
    private readonly ConfigService _configService = new();
    private HookService? _hookService;

    public MainWindow()
    {
        InitializeComponent();

        _hookService = new HookService(Dispatcher, _configService);
        _hookService.DragStarted += OnDragStarted;
        _hookService.DragMoved += OnDragMoved;
        _hookService.DragEnded += OnDragEnded;
        _hookService.Start();

        Title = $"WinLayout — 已启动 (修饰键: {_configService.LoadConfig().ModifierKey})";
    }

    private void OnDragStarted(object? sender, WindowDragEventArgs e)
    {
        Debug.WriteLine($"[MainWindow] DragStarted hwnd=0x{e.WindowHandle:X} pos=({e.CursorX},{e.CursorY})");
    }

    private void OnDragMoved(object? sender, WindowDragEventArgs e)
    {
        Debug.WriteLine($"[MainWindow] DragMoved hwnd=0x{e.WindowHandle:X} cursor=({e.CursorX},{e.CursorY}) window=({e.WindowX},{e.WindowY})");
    }

    private void OnDragEnded(object? sender, WindowDragEventArgs e)
    {
        Debug.WriteLine($"[MainWindow] DragEnded hwnd=0x{e.WindowHandle:X} cursor=({e.CursorX},{e.CursorY})");
    }

    protected override void OnClosed(EventArgs e)
    {
        _hookService?.Dispose();
        base.OnClosed(e);
    }
}

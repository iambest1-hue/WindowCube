using System.Diagnostics;
using System.Windows;
using WinLayout.Services;
using WinLayout.Views;

namespace WinLayout;

public partial class MainWindow : Window
{
    private readonly ConfigService _configService = new();
    private readonly LayoutService _layoutService;
    private readonly MonitorService _monitorService;
    private readonly WindowManager _windowManager = new();
    private HookService? _hookService;
    private OverlayService? _overlayService;
    private TrayService? _trayService;

    public MainWindow()
    {
        InitializeComponent();

        _layoutService = new LayoutService(_configService);
        _monitorService = new MonitorService(_configService, _layoutService);
        _overlayService = new OverlayService(_configService, _layoutService, _monitorService);

        var helper = new System.Windows.Interop.WindowInteropHelper(this);
        _monitorService.Initialize(helper.Handle);

        _hookService = new HookService(Dispatcher, _configService);
        _hookService.DragStarted += OnDragStarted;
        _hookService.DragMoved += OnDragMoved;
        _hookService.DragEnded += OnDragEnded;
        _hookService.Start();

        _trayService = new TrayService(this, _layoutService, _monitorService, _configService);
        _trayService.OpenEditorRequested += (_, _) => OnOpenEditor(this, new RoutedEventArgs());
        _trayService.PauseStateChanged += (_, paused) => OnPauseChanged(paused);
        _trayService.LayoutSwitchRequested += (_, layoutId) => OnLayoutSwitched(layoutId);

        UpdateStatus();
    }

    private void UpdateStatus()
    {
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
            _trayService?.RefreshLayoutMenuItems();
            UpdateStatus();
        };
        editor.ShowDialog();
    }

    private void OnPauseChanged(bool paused)
    {
        if (paused)
        {
            _hookService?.Dispose();
            _overlayService?.Hide();
        }
        else
        {
            _hookService = new HookService(Dispatcher, _configService);
            _hookService.DragStarted += OnDragStarted;
            _hookService.DragMoved += OnDragMoved;
            _hookService.DragEnded += OnDragEnded;
            _hookService.Start();
        }
        StatusLabel.Text = paused ? "已暂停 — 拖拽吸附禁用" : "Shift+拖拽吸附就绪";
    }

    private void OnLayoutSwitched(string layoutId)
    {
        var layout = _layoutService.GetActiveLayout();
        if (layout == null || _windowManager.LastSnapTarget == null) return;

        var lastTarget = _windowManager.LastSnapTarget;
        _windowManager.RearrangeAll(layout.Zones,
            lastTarget.ScreenX, lastTarget.ScreenY,
            lastTarget.ScreenWidth, lastTarget.ScreenHeight);

        UpdateStatus();
    }

    private void OnDragStarted(object? sender, WindowDragEventArgs e)
    {
        if (_trayService?.IsPaused == true) return;
        _overlayService?.Show(e.CursorX, e.CursorY);
    }

    private void OnDragMoved(object? sender, WindowDragEventArgs e)
    {
        if (_trayService?.IsPaused == true) return;
        _overlayService?.UpdateCursor(e.CursorX, e.CursorY,
            e.WindowX, e.WindowY, e.WindowWidth, e.WindowHeight);
    }

    private void OnDragEnded(object? sender, WindowDragEventArgs e)
    {
        if (_trayService?.IsPaused == true) return;

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
        _trayService?.Dispose();
        _hookService?.Dispose();
        base.OnClosed(e);
    }
}

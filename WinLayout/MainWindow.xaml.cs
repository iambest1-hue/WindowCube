using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using WinLayout.Services;
using WinLayout.Views;

namespace WinLayout;

public partial class MainWindow : Window
{
    private readonly ConfigService _configService = new();
    private readonly LayoutService _layoutService;
    private readonly MonitorService _monitorService;
    private readonly OverlayService _overlayService;
    private readonly WindowManager _windowManager = new();
    private readonly WindowFilterService _filterService;
    private HookService? _hookService;
    private TrayService? _trayService;
    private VirtualDesktopService? _virtualDesktopService;

    public MainWindow()
    {
        InitializeComponent();

        _layoutService = new LayoutService(_configService);
        _filterService = new WindowFilterService(_configService);
        _monitorService = new MonitorService(_configService, _layoutService);
        _overlayService = new OverlayService(_configService, _layoutService, _monitorService);

        _hookService = new HookService(_configService, _filterService);
        _hookService.DragStarted += OnDragStarted;
        _hookService.DragMoved += OnDragMoved;
        _hookService.DragEnded += OnDragEnded;

        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        _monitorService.Initialize(handle);

        _trayService = new TrayService(this, _layoutService, _monitorService, _configService);
        _trayService.OpenEditorRequested += (_, _) => OnOpenEditor(this, new RoutedEventArgs());
        _trayService.OpenSettingsRequested += (_, _) => OpenSettings();
        _trayService.PauseStateChanged += (_, paused) => OnPauseChanged(paused);
        _trayService.LayoutSwitchRequested += (_, layoutId) => OnLayoutSwitched(layoutId);
        _trayService.QuickFillRequested += (_, _) => OnQuickFill();
        _trayService.ExportRequested += (_, _) => OnExportLayouts();
        _trayService.ImportRequested += (_, _) => OnImportLayouts();
        _trayService.ExitRequested += (_, _) => ShutdownApp();

        _virtualDesktopService = new VirtualDesktopService(_configService, _layoutService);
        _virtualDesktopService.DesktopChanged += (_, _) =>
        {
            if (_configService.LoadConfig().AutoApplyOnDesktopSwitch)
                OnQuickFill();
        };
        _virtualDesktopService.Start();

        UpdateStatus();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _hookService?.Start();
    }

    private void UpdateStatus()
    {
        var layout = _layoutService.GetActiveLayout();
        StatusLabel.Text = layout != null
            ? $"当前布局: {layout.Name} — 拖拽吸附就绪"
            : "拖拽吸附就绪";
    }

    private bool _isActuallyClosing;

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_isActuallyClosing)
        {
            e.Cancel = true;
            Hide();
        }
    }

    public void ShutdownApp()
    {
        _isActuallyClosing = true;
        _trayService?.Dispose();
        _virtualDesktopService?.Dispose();
        _hookService?.Dispose();
        Application.Current.Shutdown();
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

    private void OpenSettings()
    {
        var settings = new SettingsWindow(_configService);
        settings.Owner = this;
        settings.Topmost = true;
        settings.SettingsSaved += () =>
        {
            ReloadHookService();
            UpdateStatus();
        };
        settings.ShowDialog();
    }

    private void ReloadHookService()
    {
        if (_trayService?.IsPaused == true) return;
        _hookService?.Dispose();
        _hookService = new HookService(_configService, _filterService);
        _hookService.DragStarted += OnDragStarted;
        _hookService.DragMoved += OnDragMoved;
        _hookService.DragEnded += OnDragEnded;
        _hookService.Start();
    }

    private void OnPauseChanged(bool paused)
    {
        if (paused)
        {
            _hookService?.Dispose();
            _overlayService.Hide();
        }
        else
        {
            _hookService = new HookService(_configService, _filterService);
            _hookService.DragStarted += OnDragStarted;
            _hookService.DragMoved += OnDragMoved;
            _hookService.DragEnded += OnDragEnded;
            _hookService.Start();
        }
        StatusLabel.Text = paused ? "已暂停 — 拖拽吸附禁用" : "拖拽吸附就绪";
    }

    private void OnQuickFill()
    {
        var layout = _layoutService.GetActiveLayout();
        if (layout == null || _windowManager.LastSnapTarget == null) return;

        var lastTarget = _windowManager.LastSnapTarget;
        _windowManager.QuickFill(layout.Zones,
            lastTarget.ScreenX, lastTarget.ScreenY,
            lastTarget.ScreenWidth, lastTarget.ScreenHeight);
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

    private void OnExportLayouts()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "JSON 文件|*.json",
            DefaultExt = ".json",
            FileName = "WinLayout_Export.json"
        };
        if (dlg.ShowDialog() == true)
        {
            var layouts = _layoutService.GetAllLayouts();
            var json = JsonSerializer.Serialize(layouts, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(dlg.FileName, json);
        }
    }

    private void OnImportLayouts()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "JSON 文件|*.json",
            DefaultExt = ".json"
        };
        if (dlg.ShowDialog() == true)
        {
            try
            {
                var json = File.ReadAllText(dlg.FileName);
                var layouts = JsonSerializer.Deserialize<List<Models.LayoutDefinition>>(json);
                if (layouts != null)
                {
                    foreach (var layout in layouts)
                        _layoutService.Save(layout);
                }
                _trayService?.RefreshLayoutMenuItems();
                UpdateStatus();
                MessageBox.Show($"已导入 {layouts?.Count ?? 0} 个布局。", "导入成功");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导入失败: {ex.Message}", "错误");
            }
        }
    }

    private void OnDragStarted(object? sender, WindowDragEventArgs e)
    {
        if (_trayService?.IsPaused == true) return;
        _overlayService.Show(e.CursorX, e.CursorY);
    }

    private void OnDragMoved(object? sender, WindowDragEventArgs e)
    {
        if (_trayService?.IsPaused == true) return;
        _overlayService.UpdateCursor(e.CursorX, e.CursorY,
            e.WindowX, e.WindowY, e.WindowWidth, e.WindowHeight);
    }

    private void OnDragEnded(object? sender, WindowDragEventArgs e)
    {
        if (_trayService?.IsPaused == true) return;

        var target = _overlayService.LastSnapTarget;
        _overlayService.Hide();

        if (target != null)
        {
            bool stacking = _hookService?.IsStackingKeyPressed() == true;
            var hwnd = e.WindowHandle;
            // Dispatch at Background priority so SetWindowPos runs after
            // the window has finished its own drag-drop positioning.
            Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Background,
                () => _windowManager.SnapWindow(hwnd, target, stacking));
        }
    }
}

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
    private readonly WindowManager _windowManager = new();
    private readonly WindowFilterService _filterService;
    private HookService? _hookService;
    private OverlayService? _overlayService;
    private MonitorService? _monitorService;
    private TrayService? _trayService;
    private VirtualDesktopService? _virtualDesktopService;

    public MainWindow()
    {
        InitializeComponent();

        _layoutService = new LayoutService(_configService);
        _filterService = new WindowFilterService(_configService);

        _hookService = new HookService(Dispatcher, _configService, _filterService);
        _hookService.DragStarted += OnDragStarted;
        _hookService.DragMoved += OnDragMoved;
        _hookService.DragEnded += OnDragEnded;
        _hookService.Start();

        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;

        _monitorService = new MonitorService(_configService, _layoutService);
        _monitorService.Initialize(handle);

        _overlayService = new OverlayService(_configService, _layoutService, _monitorService);

        _trayService = new TrayService(this, _layoutService, _monitorService, _configService);
        _trayService.OpenEditorRequested += (_, _) => OnOpenEditor(this, new RoutedEventArgs());
        _trayService.OpenSettingsRequested += (_, _) => OpenSettings();
        _trayService.PauseStateChanged += (_, paused) => OnPauseChanged(paused);
        _trayService.LayoutSwitchRequested += (_, layoutId) => OnLayoutSwitched(layoutId);
        _trayService.QuickFillRequested += (_, _) => OnQuickFill();
        _trayService.ExportRequested += (_, _) => OnExportLayouts();
        _trayService.ImportRequested += (_, _) => OnImportLayouts();

        _virtualDesktopService = new VirtualDesktopService(_configService, _layoutService);
        _virtualDesktopService.DesktopChanged += (_, _) =>
        {
            if (_configService.LoadConfig().AutoApplyOnDesktopSwitch)
                OnQuickFill();
        };
        _virtualDesktopService.Start();

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

    private void OpenSettings()
    {
        var settings = new SettingsWindow(_configService);
        settings.Owner = this;
        settings.ShowDialog();
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
            _hookService = new HookService(Dispatcher, _configService, _filterService);
            _hookService.DragStarted += OnDragStarted;
            _hookService.DragMoved += OnDragMoved;
            _hookService.DragEnded += OnDragEnded;
            _hookService.Start();
        }
        StatusLabel.Text = paused ? "已暂停 — 拖拽吸附禁用" : "Shift+拖拽吸附就绪";
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
                bool stacking = _hookService?.IsStackingKeyPressed() == true;
                _windowManager.SnapWindow(e.WindowHandle, target, stacking);
            }
            _overlayService.Hide();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _trayService?.Dispose();
        _virtualDesktopService?.Dispose();
        _hookService?.Dispose();
        base.OnClosed(e);
    }
}

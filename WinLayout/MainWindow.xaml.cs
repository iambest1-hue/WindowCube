using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using WinLayout.Models;
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
        RefreshLayoutLists();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _hookService?.Start();
    }

    private void UpdateStatus()
    {
        var config = _configService.LoadConfig();
        var modifierText = config.ModifierKey == "None" ? "直接拖拽" : config.ModifierKey + "+拖拽";

        var primary = _monitorService.GetPrimaryMonitor();
        if (primary != null)
        {
            var primaryLayout = _monitorService.GetActiveLayoutForScreen(primary.ScreenId);
            StatusLabel.Text = primaryLayout != null
                ? $"主屏: {primaryLayout.Name} — {modifierText}吸附就绪"
                : $"{modifierText}吸附就绪";
        }
        else
        {
            var layout = _layoutService.GetActiveLayout();
            StatusLabel.Text = layout != null
                ? $"当前布局: {layout.Name} — {modifierText}吸附就绪"
                : $"{modifierText}吸附就绪";
        }
    }

    private record LayoutListItem(string DisplayName, LayoutDefinition Layout);

    private void RefreshLayoutLists()
    {
        var layouts = _layoutService.GetAllLayouts().OrderBy(l => l.Zones.Count).ToList();
        var activeLayoutId = _layoutService.GetActiveLayout()?.LayoutId;

        string Display(LayoutDefinition l) =>
            l.LayoutId == activeLayoutId ? $"✓ {l.Name}" : l.Name;

        AllLayoutsList.ItemsSource = layouts
            .Where(l => !l.IsFavorite)
            .Select(l => new LayoutListItem(Display(l), l))
            .ToList();
        FavoriteLayoutsList.ItemsSource = layouts
            .Where(l => l.IsFavorite)
            .Select(l => new LayoutListItem(Display(l), l))
            .ToList();
    }

    private void OnAddFavorite(object sender, RoutedEventArgs e)
    {
        if ((AllLayoutsList.SelectedItem as LayoutListItem)?.Layout is LayoutDefinition layout)
        {
            layout.IsFavorite = true;
            _layoutService.Save(layout);
            _trayService!.RefreshLayoutMenuItems();
            RefreshLayoutLists();
        }
    }

    private void OnRemoveFavorite(object sender, RoutedEventArgs e)
    {
        if ((FavoriteLayoutsList.SelectedItem as LayoutListItem)?.Layout is LayoutDefinition layout)
        {
            if (IsActiveLayout(layout.LayoutId))
            {
                System.Windows.MessageBox.Show(
                    $"布局 \"{layout.Name}\" 当前正在使用中，不能取消收藏。\n请先切换到其他布局。",
                    "无法移除", System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            var favorites = _layoutService.GetAllLayouts().Count(l => l.IsFavorite);
            if (favorites <= 1) return;

            layout.IsFavorite = false;
            _layoutService.Save(layout);
            _trayService!.RefreshLayoutMenuItems();
            RefreshLayoutLists();
        }
    }

    private bool IsActiveLayout(string layoutId)
    {
        foreach (var monitor in _monitorService.GetMonitors())
        {
            var active = _monitorService.GetActiveLayoutForScreen(monitor.ScreenId);
            if (active != null && active.LayoutId == layoutId)
                return true;
        }
        return false;
    }

    private void OnLayoutDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if ((sender as ListBox)?.SelectedItem is LayoutListItem item)
        {
            var screenId = _monitorService.GetScreenIdAtCursor();
            _monitorService.SetActiveLayoutForScreen(screenId, item.Layout.LayoutId);
            OnLayoutSwitched(item.Layout.LayoutId);
        }
    }

    private void OnClearFavorites(object sender, RoutedEventArgs e)
    {
        var layouts = _layoutService.GetAllLayouts().OrderBy(l => l.Zones.Count).ToList();
        var favorites = layouts.Where(l => l.IsFavorite).ToList();
        if (favorites.Count <= 1) return;

        for (int i = 1; i < favorites.Count; i++)
        {
            if (IsActiveLayout(favorites[i].LayoutId)) continue;
            favorites[i].IsFavorite = false;
            _layoutService.Save(favorites[i]);
        }
        _trayService!.RefreshLayoutMenuItems();
        RefreshLayoutLists();
    }

    private void OnShowMenu(object sender, RoutedEventArgs e)
    {
        var menu = _trayService!.GetContextMenu();
        menu.PlacementTarget = sender as UIElement;
        menu.IsOpen = true;
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
        editor.Closed += (_, _) => RefreshAll();
        editor.ShowDialog();
    }

    public void RefreshAll()
    {
        _trayService?.RefreshLayoutMenuItems();
        UpdateStatus();
        RefreshLayoutLists();
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
        if (paused)
            StatusLabel.Text = "已暂停 — 拖拽吸附禁用";
        else
            UpdateStatus();
    }

    private void OnQuickFill()
    {
        var monitors = _monitorService.GetMonitors();
        foreach (var monitor in monitors)
        {
            var layout = _monitorService.GetActiveLayoutForScreen(monitor.ScreenId);
            if (layout == null) continue;

            _windowManager.QuickFill(layout.Zones,
                monitor.X, monitor.Y, monitor.Width, monitor.Height);
        }
    }

    private void OnLayoutSwitched(string layoutId)
    {
        UpdateStatus();
        RefreshLayoutLists();

        if (_windowManager.LastSnapTarget == null) return;

        var lastTarget = _windowManager.LastSnapTarget;
        var screen = _monitorService.GetMonitorAtCursor(lastTarget.ScreenX, lastTarget.ScreenY);
        var layout = screen != null
            ? _monitorService.GetActiveLayoutForScreen(screen.ScreenId)
            : _layoutService.GetActiveLayout();
        if (layout == null) return;

        _windowManager.RearrangeAll(layout.Zones,
            lastTarget.ScreenX, lastTarget.ScreenY,
            lastTarget.ScreenWidth, lastTarget.ScreenHeight);
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
                RefreshLayoutLists();
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

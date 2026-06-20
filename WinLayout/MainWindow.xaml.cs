using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinLayout.Models;
using WinLayout.Native;
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
    private readonly Dictionary<string, ImageSource?> _iconCache = new(StringComparer.OrdinalIgnoreCase);
    private HookService? _hookService;
    private TrayService? _trayService;
    private VirtualDesktopService? _virtualDesktopService;
    private WindowButtonsService? _windowButtonsService;

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

        // Set window icon from the application executable
        try
        {
            var exePath = Environment.ProcessPath;
            if (exePath != null)
            {
                using var icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                if (icon != null)
                    this.Icon = Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
        }
        catch { }

        _trayService = new TrayService(this, _layoutService, _monitorService, _configService);
        _trayService.OpenEditorRequested += (_, _) => OnOpenEditor(this, new RoutedEventArgs());
        _trayService.OpenSettingsRequested += (_, _) => OpenSettings();
        _trayService.PauseStateChanged += (_, paused) => OnPauseChanged(paused);
        _trayService.LayoutSwitchRequested += (_, layoutId) => OnLayoutSwitched(layoutId);
        _trayService.QuickFillRequested += (_, _) => OnQuickFill();
        _trayService.ExportRequested += (_, _) => OnExportLayouts();
        _trayService.ImportRequested += (_, _) => OnImportLayouts();
        _trayService.ExitRequested += (_, _) => ShutdownApp();
        _trayService.DonateRequested += (_, _) => new DonateWindow(this).ShowDialog();

        _virtualDesktopService = new VirtualDesktopService(_configService, _layoutService);
        _virtualDesktopService.DesktopChanged += (_, _) =>
        {
            if (_configService.LoadConfig().AutoApplyOnDesktopSwitch)
                OnQuickFill();
        };
        _virtualDesktopService.Start();

        _monitorService.SeedScreenFavoritesIfEmpty();
        UpdateStatus();
        RefreshLayoutLists();
        RefreshRunningAppsList();
        RefreshButtonBlacklistList();

        _windowButtonsService = new WindowButtonsService(
            _windowManager, _monitorService, _filterService, _configService, _layoutService);
        _windowButtonsService.Start();
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
        var secondary = _monitorService.GetMonitors().FirstOrDefault(m => !m.IsPrimary);

        if (primary != null)
        {
            var primaryLayout = _monitorService.GetActiveLayoutForScreen(primary.ScreenId);
            var parts = new List<string>();
            if (primaryLayout != null)
                parts.Add($"主屏: {primaryLayout.Name}");

            if (secondary != null)
            {
                var secondaryLayout = _monitorService.GetActiveLayoutForScreen(secondary.ScreenId);
                if (secondaryLayout != null)
                    parts.Add($"副屏: {secondaryLayout.Name}");
            }

            parts.Add($"{modifierText}吸附就绪");
            StatusLabel.Text = string.Join(" — ", parts);
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
    private record RunningAppItem(string DisplayName, string ProcessName, ImageSource? Icon = null);

    private void RefreshLayoutLists()
    {
        var layouts = _layoutService.GetAllLayouts().OrderBy(l => l.Zones.Count).ToList();
        var monitors = _monitorService.GetMonitors();
        var primary = monitors.FirstOrDefault(m => m.IsPrimary);
        var secondary = monitors.FirstOrDefault(m => !m.IsPrimary);

        var primaryId = primary?.ScreenId ?? "default";
        var secondaryId = secondary?.ScreenId;

        var primaryFavIds = _monitorService.GetFavoriteIdsForScreen(primaryId);
        var secondaryFavIds = secondaryId != null
            ? _monitorService.GetFavoriteIdsForScreen(secondaryId) : new List<string>();

        var visibleFavIds = new List<List<string>> { primaryFavIds };
        if (secondaryId != null) visibleFavIds.Add(secondaryFavIds);
        // Only hide from all-layouts if favorited on every visible screen
        var hiddenIds = new HashSet<string>(visibleFavIds[0]);
        for (int i = 1; i < visibleFavIds.Count; i++)
            hiddenIds.IntersectWith(visibleFavIds[i]);

        string Display(LayoutDefinition l, string screenId) =>
            _monitorService.GetActiveLayoutForScreen(screenId)?.LayoutId == l.LayoutId
                ? $"✓ {l.Name}" : l.Name;

        AllLayoutsList.ItemsSource = layouts
            .Where(l => !hiddenIds.Contains(l.LayoutId))
            .Select(l => new LayoutListItem(Display(l, primaryId), l))
            .ToList();

        PrimaryFavoriteList.ItemsSource = layouts
            .Where(l => primaryFavIds.Contains(l.LayoutId))
            .Select(l => new LayoutListItem(Display(l, primaryId), l))
            .ToList();

        if (secondaryId != null)
        {
            SecondaryHeader.Visibility = Visibility.Visible;
            SecondaryFavoriteList.Visibility = Visibility.Visible;
            SecondaryBtns.Visibility = Visibility.Visible;
            SecondaryFavoriteList.ItemsSource = layouts
                .Where(l => secondaryFavIds.Contains(l.LayoutId))
                .Select(l => new LayoutListItem(Display(l, secondaryId), l))
                .ToList();
        }
        else
        {
            SecondaryHeader.Visibility = Visibility.Collapsed;
            SecondaryFavoriteList.Visibility = Visibility.Collapsed;
            SecondaryBtns.Visibility = Visibility.Collapsed;
        }
    }

    private void RefreshRunningAppsList()
    {
        var config = _configService.LoadConfig();
        var apps = new List<(string procName, string title)>();
        var seenProcs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var systemClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Progman", "Shell_TrayWnd", "WorkerW"
        };

        User32.EnumWindows((hwnd, _) =>
        {
            try
            {
                if (!User32.IsWindowVisible(hwnd)) return true;

                var cls = GetWindowClassName(hwnd);
                if (systemClasses.Contains(cls)) return true;

                var sb = new System.Text.StringBuilder(256);
                User32.InternalGetWindowText(hwnd, sb, sb.Capacity);
                if (sb.Length == 0) return true;

                User32.GetWindowThreadProcessId(hwnd, out uint pid);
                if (pid == Environment.ProcessId) return true;

                string procName;
                try { procName = Process.GetProcessById((int)pid).ProcessName; }
                catch { return true; }

                if (seenProcs.Contains(procName)) return true;
                seenProcs.Add(procName);

                apps.Add((procName, sb.ToString()));
            }
            catch { }
            return true;
        }, IntPtr.Zero);

        // Fallback: if EnumWindows found very few apps, try Process enumeration
        if (apps.Count < 5)
        {
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (string.IsNullOrEmpty(p.MainWindowTitle)) continue;
                    if (p.Id == Environment.ProcessId) continue;
                    if (seenProcs.Contains(p.ProcessName)) continue;
                    seenProcs.Add(p.ProcessName);
                    apps.Add((p.ProcessName, p.MainWindowTitle));
                }
                catch { }
            }
        }

        RunningAppsList.ItemsSource = apps
            .Where(a => !config.WindowButtonsBlacklist.Any(
                p => string.Equals(p, a.procName, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(a => a.procName)
            .Select(a => new RunningAppItem(
                $"{a.procName} — {a.title}",
                a.procName,
                GetProcessIcon(a.procName)))
            .ToList();
    }

    private static string GetWindowClassName(IntPtr hwnd)
    {
        var sb = new System.Text.StringBuilder(256);
        User32.GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private void RefreshButtonBlacklistList()
    {
        var config = _configService.LoadConfig();
        ButtonBlacklistList.ItemsSource = config.WindowButtonsBlacklist
            .Select(p => new RunningAppItem(p, p, GetProcessIcon(p)))
            .ToList();
    }

    private ImageSource? GetProcessIcon(string procName)
    {
        if (_iconCache.TryGetValue(procName, out var cached)) return cached;

        try
        {
            var procs = Process.GetProcessesByName(procName);
            if (procs.Length == 0) { _iconCache[procName] = null; return null; }
            string? path = null;
            try { path = procs[0].MainModule?.FileName; } catch { }
            if (path == null) { _iconCache[procName] = null; return null; }

            var shfi = new User32.SHFILEINFO();
            IntPtr result = User32.SHGetFileInfo(path, 0, ref shfi,
                (uint)Marshal.SizeOf<User32.SHFILEINFO>(),
                User32.SHGFI_ICON | User32.SHGFI_SMALLICON);
            if (result == IntPtr.Zero || shfi.hIcon == IntPtr.Zero)
            {
                _iconCache[procName] = null;
                return null;
            }

            var icon = Imaging.CreateBitmapSourceFromHIcon(
                shfi.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            User32.DestroyIcon(shfi.hIcon);
            _iconCache[procName] = icon;
            return icon;
        }
        catch { _iconCache[procName] = null; return null; }
    }

    private string PrimaryId => _monitorService.GetPrimaryMonitor()?.ScreenId ?? "default";
    private string? SecondaryId => _monitorService.GetMonitors().FirstOrDefault(m => !m.IsPrimary)?.ScreenId;

    private void OnAddPrimary(object sender, RoutedEventArgs e) => AddToScreen(PrimaryId);
    private void OnAddSecondary(object sender, RoutedEventArgs e) => AddToScreen(SecondaryId ?? PrimaryId);

    private void AddToScreen(string screenId)
    {
        if ((AllLayoutsList.SelectedItem as LayoutListItem)?.Layout is not LayoutDefinition layout) return;
        _monitorService.SetFavoriteForScreen(screenId, layout.LayoutId, true);
        _trayService!.RefreshLayoutMenuItems();
        RefreshLayoutLists();
    }

    private void OnRemovePrimary(object sender, RoutedEventArgs e) => RemoveFromScreen(PrimaryId, PrimaryFavoriteList);
    private void OnRemoveSecondary(object sender, RoutedEventArgs e) => RemoveFromScreen(SecondaryId ?? PrimaryId, SecondaryFavoriteList);

    private void RemoveFromScreen(string screenId, ListBox list)
    {
        if ((list.SelectedItem as LayoutListItem)?.Layout is not LayoutDefinition layout) return;

        if (_monitorService.GetActiveLayoutForScreen(screenId)?.LayoutId == layout.LayoutId)
        {
            System.Windows.MessageBox.Show(
                $"布局 \"{layout.Name}\" 当前正在使用中，不能取消收藏。\n请先切换到其他布局。",
                "无法移除", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        if (_monitorService.GetFavoriteIdsForScreen(screenId).Count <= 1) return;

        _monitorService.SetFavoriteForScreen(screenId, layout.LayoutId, false);
        _trayService!.RefreshLayoutMenuItems();
        RefreshLayoutLists();
    }

    private void OnClearPrimary(object sender, RoutedEventArgs e) => ClearScreen(PrimaryId);
    private void OnClearSecondary(object sender, RoutedEventArgs e) => ClearScreen(SecondaryId ?? PrimaryId);

    private void ClearScreen(string screenId)
    {
        var favIds = _monitorService.GetFavoriteIdsForScreen(screenId);
        if (favIds.Count <= 1) return;

        var activeId = _monitorService.GetActiveLayoutForScreen(screenId)?.LayoutId;
        for (int i = favIds.Count - 1; i >= 0; i--)
        {
            if (favIds[i] == activeId) continue;
            _monitorService.SetFavoriteForScreen(screenId, favIds[i], false);
        }
        if (_monitorService.GetFavoriteIdsForScreen(screenId).Count == 0)
            _monitorService.SetFavoriteForScreen(screenId, favIds[0], true);

        _trayService!.RefreshLayoutMenuItems();
        RefreshLayoutLists();
    }

    private void OnLayoutDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if ((sender as ListBox)?.SelectedItem is not LayoutListItem item) return;
        if (sender != PrimaryFavoriteList && sender != SecondaryFavoriteList) return;

        string screenId = sender == SecondaryFavoriteList ? (SecondaryId ?? PrimaryId) : PrimaryId;
        _monitorService.SetActiveLayoutForScreen(screenId, item.Layout.LayoutId);
        OnLayoutSwitched(item.Layout.LayoutId);
    }

    private void OnAddToButtonBlacklist(object sender, RoutedEventArgs e)
    {
        if (RunningAppsList.SelectedItem is not RunningAppItem item) return;
        if (string.IsNullOrEmpty(item.ProcessName)) return;

        var config = _configService.LoadConfig();
        if (config.WindowButtonsBlacklist.Any(
            p => string.Equals(p, item.ProcessName, StringComparison.OrdinalIgnoreCase)))
            return;

        config.WindowButtonsBlacklist.Add(item.ProcessName);
        _configService.SaveConfig(config);
        RefreshRunningAppsList();
        RefreshButtonBlacklistList();
    }

    private void OnRemoveFromButtonBlacklist(object sender, RoutedEventArgs e)
    {
        if (ButtonBlacklistList.SelectedItem is not RunningAppItem item) return;

        var config = _configService.LoadConfig();
        config.WindowButtonsBlacklist.RemoveAll(
            p => string.Equals(p, item.ProcessName, StringComparison.OrdinalIgnoreCase));
        _configService.SaveConfig(config);
        RefreshRunningAppsList();
        RefreshButtonBlacklistList();
    }

    private void OnRefreshRunningApps(object sender, RoutedEventArgs e)
    {
        _iconCache.Clear();
        RefreshRunningAppsList();
    }

    private void OnClearButtonBlacklist(object sender, RoutedEventArgs e)
    {
        var config = _configService.LoadConfig();
        config.WindowButtonsBlacklist.Clear();
        _configService.SaveConfig(config);
        RefreshRunningAppsList();
        RefreshButtonBlacklistList();
    }

    private void OnMainTabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MainTabControl.SelectedIndex == 1)
        {
            RefreshRunningAppsList();
            RefreshButtonBlacklistList();
        }
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
        _windowButtonsService?.Dispose();
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
            _windowButtonsService?.Dispose();
            _windowButtonsService = new WindowButtonsService(
                _windowManager, _monitorService, _filterService, _configService, _layoutService);
            if (_trayService?.IsPaused == true)
                _windowButtonsService.IsPaused = true;
            _windowButtonsService.Start();
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
            _windowButtonsService!.IsPaused = true;
        }
        else
        {
            _hookService = new HookService(_configService, _filterService);
            _hookService.DragStarted += OnDragStarted;
            _hookService.DragMoved += OnDragMoved;
            _hookService.DragEnded += OnDragEnded;
            _hookService.Start();
            _windowButtonsService!.IsPaused = false;
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

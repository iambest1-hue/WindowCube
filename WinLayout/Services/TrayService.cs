using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using WinLayout.Models;
using WinLayout.Native;

namespace WinLayout.Services;

public class TrayService : IDisposable
{
    private readonly Window _mainWindow;
    private readonly LayoutService _layoutService;
    private readonly MonitorService _monitorService;
    private readonly ConfigService _configService;
    private readonly HwndSource _hwndSource;
    private readonly List<Tuple<int, uint>> _registeredHotkeys = new();

    private IntPtr _trayIconHandle;
    private bool _isPaused;
    private ContextMenu? _trayMenu;

    private string _currentMenuScreenId = "default";

    public event EventHandler? OpenEditorRequested;
    public event EventHandler? OpenSettingsRequested;
    public event EventHandler<string>? LayoutSwitchRequested;
    public event EventHandler<bool>? PauseStateChanged;
    public event EventHandler? QuickFillRequested;
    public event EventHandler? ExportRequested;
    public event EventHandler? ImportRequested;
    public event EventHandler? ExitRequested;

    public bool IsPaused => _isPaused;

    public TrayService(Window mainWindow, LayoutService layoutService,
        MonitorService monitorService, ConfigService configService)
    {
        _mainWindow = mainWindow;
        _layoutService = layoutService;
        _monitorService = monitorService;
        _configService = configService;

        var helper = new WindowInteropHelper(mainWindow);
        _hwndSource = HwndSource.FromHwnd(helper.Handle)!;
        _hwndSource.AddHook(WndProc);

        CreateTrayIcon();
        BuildTrayMenu();
        RegisterDefaultHotkeys();
    }

    private void CreateTrayIcon()
    {
        var hwnd = new WindowInteropHelper(_mainWindow).Handle;

        // Create a simple colored icon programmatically
        using var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.Clear(System.Drawing.Color.FromArgb(66, 133, 244));
        g.DrawRectangle(new System.Drawing.Pen(System.Drawing.Color.White, 2), 4, 4, 24, 24);
        _trayIconHandle = bmp.GetHicon();

        var nid = new Shell32.NOTIFYICONDATA
        {
            cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Shell32.NOTIFYICONDATA>(),
            hWnd = hwnd,
            uID = 1,
            uFlags = Shell32.NIF_ICON | Shell32.NIF_MESSAGE | Shell32.NIF_TIP,
            uCallbackMessage = (int)Shell32.WM_TRAYICON,
            hIcon = _trayIconHandle,
            szTip = "WinLayout"
        };

        Shell32.Shell_NotifyIcon(Shell32.NIM_ADD, ref nid);
    }

    private void BuildTrayMenu()
    {
        _trayMenu = new ContextMenu();
        RefreshLayoutMenuItems();
    }

    public ContextMenu GetContextMenu()
    {
        RefreshLayoutMenuItems();
        return _trayMenu!;
    }

    public void RefreshLayoutMenuItems()
    {
        if (_trayMenu == null) return;

        _trayMenu.Items.Clear();

        var layouts = _layoutService.GetAllLayouts().OrderBy(l => l.Zones.Count).ToList();
        var active = _monitorService.GetActiveLayoutForScreen(_currentMenuScreenId);

        foreach (var layout in layouts.Where(l => l.IsFavorite))
        {
            var header = layout.Name;
            if (active != null && layout.LayoutId == active.LayoutId)
                header = "✓ " + header;

            var item = new MenuItem { Header = header, Tag = layout.LayoutId };
            var capturedScreenId = _currentMenuScreenId;
            item.Click += (_, _) =>
            {
                _monitorService.SetActiveLayoutForScreen(capturedScreenId, layout.LayoutId);
                LayoutSwitchRequested?.Invoke(this, layout.LayoutId);
                RefreshLayoutMenuItems();
            };
            _trayMenu.Items.Add(item);
        }

        _trayMenu.Items.Add(new Separator());
        _trayMenu.Items.Add(CreateMenuItem("快速填充当前布局", () => QuickFillRequested?.Invoke(this, EventArgs.Empty)));
        _trayMenu.Items.Add(new Separator());
        _trayMenu.Items.Add(CreateMenuItem("导出布局...", () => ExportRequested?.Invoke(this, EventArgs.Empty)));
        _trayMenu.Items.Add(CreateMenuItem("导入布局...", () => ImportRequested?.Invoke(this, EventArgs.Empty)));
        _trayMenu.Items.Add(CreateMenuItem("布局编辑器", () => OpenEditorRequested?.Invoke(this, EventArgs.Empty)));
        _trayMenu.Items.Add(CreateMenuItem("设置", () => OpenSettingsRequested?.Invoke(this, EventArgs.Empty)));
        _trayMenu.Items.Add(new Separator());

        var pauseItem = new MenuItem { Header = _isPaused ? "▶ 恢复吸附" : "⏸ 暂停吸附" };
        pauseItem.Click += (_, _) =>
        {
            _isPaused = !_isPaused;
            ToggleTrayIconPauseState();
            RefreshLayoutMenuItems();
            PauseStateChanged?.Invoke(this, _isPaused);
        };
        _trayMenu.Items.Add(pauseItem);

        _trayMenu.Items.Add(new Separator());
        _trayMenu.Items.Add(CreateMenuItem("语言 / Language", () =>
        {
            var next = LocalizationService.CurrentCulture == "zh-CN" ? "en-US" : "zh-CN";
            LocalizationService.SetCulture(next);
            RefreshLayoutMenuItems();
        }));
        _trayMenu.Items.Add(new Separator());
        _trayMenu.Items.Add(CreateMenuItem("退出", () =>
        {
            ExitRequested?.Invoke(this, EventArgs.Empty);
        }));
    }

    private void ToggleTrayIconPauseState()
    {
        var hwnd = new WindowInteropHelper(_mainWindow).Handle;
        var color = _isPaused ? System.Drawing.Color.Gray : System.Drawing.Color.FromArgb(66, 133, 244);
        using var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.Clear(color);
        var pen = new System.Drawing.Pen(System.Drawing.Color.White, 2);
        g.DrawRectangle(pen, 4, 4, 24, 24);
        var iconHandle = bmp.GetHicon();

        var nid = new Shell32.NOTIFYICONDATA
        {
            cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Shell32.NOTIFYICONDATA>(),
            hWnd = hwnd,
            uID = 1,
            uFlags = Shell32.NIF_ICON | Shell32.NIF_TIP,
            hIcon = iconHandle,
            szTip = _isPaused ? "WinLayout (已暂停)" : "WinLayout"
        };
        Shell32.Shell_NotifyIcon(Shell32.NIM_MODIFY, ref nid);
        DestroyIcon(iconHandle);

        // Update tooltip
        var nidTip = new Shell32.NOTIFYICONDATA
        {
            cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Shell32.NOTIFYICONDATA>(),
            hWnd = hwnd,
            uID = 1,
            uFlags = Shell32.NIF_TIP,
            szTip = _isPaused ? "WinLayout (已暂停)" : "WinLayout"
        };
        Shell32.Shell_NotifyIcon(Shell32.NIM_MODIFY, ref nidTip);
    }

    private static MenuItem CreateMenuItem(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }

    private void RegisterDefaultHotkeys()
    {
        var hwnd = new WindowInteropHelper(_mainWindow).Handle;
        for (int i = 0; i < 9; i++)
        {
            uint vk = (uint)(0x31 + i); // '1' through '9'
            int id = 9000 + i;
            if (User32.RegisterHotKey(hwnd, id, User32.MOD_WIN, vk))
                _registeredHotkeys.Add(Tuple.Create(id, vk));
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == (int)Shell32.WM_TRAYICON)
        {
            if (lParam == (IntPtr)Shell32.WM_RBUTTONUP)
            {
                ShowTrayMenu();
                handled = true;
            }
            else if (lParam == (IntPtr)Shell32.WM_LBUTTONDBLCLK)
            {
                _mainWindow.Show();
                _mainWindow.WindowState = WindowState.Normal;
                _mainWindow.Activate();
                handled = true;
            }
        }
        else if (msg == User32.WM_HOTKEY)
        {
            int hotkeyId = wParam.ToInt32();
            if (hotkeyId >= 9000 && hotkeyId < 9009)
            {
                int layoutIndex = hotkeyId - 9000;
                var layouts = _layoutService.GetAllLayouts();
                if (layoutIndex < layouts.Count)
                {
                    var layout = layouts[layoutIndex];
                    var screenId = _monitorService.GetScreenIdAtCursor();
                    _monitorService.SetActiveLayoutForScreen(screenId, layout.LayoutId);
                    LayoutSwitchRequested?.Invoke(this, layout.LayoutId);
                    RefreshLayoutMenuItems();
                }
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    private void ShowTrayMenu()
    {
        if (_trayMenu == null) return;

        _currentMenuScreenId = _monitorService.GetScreenIdAtCursor();
        RefreshLayoutMenuItems();
        _trayMenu.IsOpen = true;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public void Dispose()
    {
        var hwnd = new WindowInteropHelper(_mainWindow).Handle;

        foreach (var (id, _) in _registeredHotkeys)
            User32.UnregisterHotKey(hwnd, id);
        _registeredHotkeys.Clear();

        var nid = new Shell32.NOTIFYICONDATA
        {
            cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Shell32.NOTIFYICONDATA>(),
            hWnd = hwnd,
            uID = 1
        };
        Shell32.Shell_NotifyIcon(Shell32.NIM_DELETE, ref nid);

        if (_trayIconHandle != IntPtr.Zero)
        {
            DestroyIcon(_trayIconHandle);
            _trayIconHandle = IntPtr.Zero;
        }

        _hwndSource.RemoveHook(WndProc);
    }
}

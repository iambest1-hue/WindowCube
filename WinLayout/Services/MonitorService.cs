using System.Diagnostics;
using System.Windows.Interop;
using WinLayout.Models;
using WinLayout.Native;

namespace WinLayout.Services;

public class MonitorInfo
{
    public string ScreenId { get; init; } = "";
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public string DeviceName { get; init; } = "";
    public bool IsPrimary { get; init; }
    public string DisplayLabel => IsPrimary ? "主屏" : "副屏";
}

public class MonitorService
{
    private readonly ConfigService _configService;
    private readonly LayoutService _layoutService;
    private readonly List<MonitorInfo> _monitors = new();
    private HwndSource? _hwndSource;

    public event EventHandler? MonitorsChanged;

    public MonitorService(ConfigService configService, LayoutService layoutService)
    {
        _configService = configService;
        _layoutService = layoutService;
        EnumerateMonitors();
    }

    public void Initialize(IntPtr mainWindowHandle)
    {
        _hwndSource = HwndSource.FromHwnd(mainWindowHandle);
        _hwndSource?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == User32.WM_DISPLAYCHANGE)
        {
            Debug.WriteLine("[Monitor] Display change detected");
            EnumerateMonitors();
            MonitorsChanged?.Invoke(this, EventArgs.Empty);
        }
        return IntPtr.Zero;
    }

    public List<MonitorInfo> GetMonitors() => _monitors.ToList();

    public MonitorInfo? GetMonitorAtCursor(int cursorX, int cursorY)
    {
        var pt = new User32.POINT { X = cursorX, Y = cursorY };
        var hMonitor = User32.MonitorFromPoint(pt, User32.MONITOR_DEFAULTTONULL);

        foreach (var m in _monitors)
        {
            if (cursorX >= m.X && cursorX < m.X + m.Width &&
                cursorY >= m.Y && cursorY < m.Y + m.Height)
            {
                return m;
            }
        }
        return _monitors.FirstOrDefault();
    }

    public string GetScreenIdAtCursor()
    {
        User32.GetCursorPos(out var cursor);
        return GetMonitorAtCursor(cursor.X, cursor.Y)?.ScreenId ?? "default";
    }

    public MonitorInfo? GetPrimaryMonitor() => _monitors.FirstOrDefault(m => m.IsPrimary);

    public LayoutDefinition? GetActiveLayoutForScreen(string screenId)
    {
        var config = _configService.LoadConfig();
        if (config.ScreenLayouts.TryGetValue(screenId, out var screenCfg))
        {
            var layouts = _layoutService.GetAllLayouts();
            var matched = layouts.FirstOrDefault(l => l.LayoutId == screenCfg.ActiveLayoutId);
            if (matched != null)
                return matched;
            // ActiveLayoutId is stale or empty, fall through to default
        }
        // Fallback to default active
        return _layoutService.GetActiveLayout();
    }

    public void SetActiveLayoutForScreen(string screenId, string layoutId)
    {
        var config = _configService.LoadConfig();
        if (!config.ScreenLayouts.ContainsKey(screenId))
            config.ScreenLayouts[screenId] = new ScreenLayoutConfig();
        config.ScreenLayouts[screenId].ActiveLayoutId = layoutId;
        _configService.SaveConfig(config);
    }

    public bool IsFavoriteForScreen(string screenId, string layoutId)
    {
        var config = _configService.LoadConfig();
        if (config.ScreenLayouts.TryGetValue(screenId, out var sc))
            return sc.FavoriteLayoutIds.Contains(layoutId);
        return false;
    }

    public void SetFavoriteForScreen(string screenId, string layoutId, bool isFavorite)
    {
        var config = _configService.LoadConfig();
        if (!config.ScreenLayouts.ContainsKey(screenId))
            config.ScreenLayouts[screenId] = new ScreenLayoutConfig();
        var list = config.ScreenLayouts[screenId].FavoriteLayoutIds;
        if (isFavorite && !list.Contains(layoutId))
            list.Add(layoutId);
        else if (!isFavorite)
            list.Remove(layoutId);
        _configService.SaveConfig(config);
    }

    public void SeedScreenFavoritesIfEmpty()
    {
        var config = _configService.LoadConfig();
        foreach (var monitor in _monitors)
        {
            if (!config.ScreenLayouts.ContainsKey(monitor.ScreenId))
                config.ScreenLayouts[monitor.ScreenId] = new ScreenLayoutConfig();
            if (config.ScreenLayouts[monitor.ScreenId].FavoriteLayoutIds.Count == 0)
            {
                var layouts = _layoutService.GetAllLayouts();
                config.ScreenLayouts[monitor.ScreenId].FavoriteLayoutIds =
                    layouts.Where(l => l.IsFavorite).Select(l => l.LayoutId).ToList();
            }
        }
        _configService.SaveConfig(config);
    }

    public List<string> GetFavoriteIdsForScreen(string screenId)
    {
        var config = _configService.LoadConfig();
        if (config.ScreenLayouts.TryGetValue(screenId, out var sc) && sc.FavoriteLayoutIds.Count > 0)
            return sc.FavoriteLayoutIds;
        return new List<string>();
    }

    private void EnumerateMonitors()
    {
        _monitors.Clear();

        User32.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
            (IntPtr hMonitor, IntPtr hdc, ref User32.RECT rect, IntPtr data) =>
            {
                var mi = new User32.MONITORINFO();
                mi.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(mi);
                if (User32.GetMonitorInfo(hMonitor, ref mi))
                {
                    var baseId = GetScreenId(hMonitor, rect);
                    var isPrimary = (mi.dwFlags & User32.MONITORINFOF_PRIMARY) != 0;
                    var screenId = baseId + (isPrimary ? "_p" : "_s");
                    _monitors.Add(new MonitorInfo
                    {
                        ScreenId = screenId,
                        X = mi.rcMonitor.Left,
                        Y = mi.rcMonitor.Top,
                        Width = mi.rcMonitor.Right - mi.rcMonitor.Left,
                        Height = mi.rcMonitor.Bottom - mi.rcMonitor.Top,
                        DeviceName = baseId,
                        IsPrimary = isPrimary
                    });
                }
                return true;
            }, IntPtr.Zero);

        Debug.WriteLine($"[Monitor] Found {_monitors.Count} monitors");
        foreach (var m in _monitors)
            Debug.WriteLine($"  {m.ScreenId}: {m.Width}x{m.Height} @ ({m.X},{m.Y})");
    }

    private static string GetScreenId(IntPtr hMonitor, User32.RECT rect)
    {
        // Try to get a unique device ID via EnumDisplayDevices
        uint i = 0;
        var dd = new User32.DISPLAY_DEVICE();
        dd.cb = System.Runtime.InteropServices.Marshal.SizeOf(dd);

        while (User32.EnumDisplayDevices(null, i, ref dd, 0))
        {
            // Check if this device's monitor matches our handle
            var pt = new User32.POINT
            {
                X = (rect.Left + rect.Right) / 2,
                Y = (rect.Top + rect.Bottom) / 2
            };
            var devMonitor = User32.MonitorFromPoint(pt, User32.MONITOR_DEFAULTTONULL);
            if (devMonitor == hMonitor)
            {
                // Use DeviceID as unique identifier (falls back to DeviceName)
                if (!string.IsNullOrEmpty(dd.DeviceID) && dd.DeviceID.Length > 0)
                    return dd.DeviceID;
                return dd.DeviceName;
            }
            i++;
        }

        // Fallback: position-based ID
        return $"MONITOR_{rect.Left}_{rect.Top}";
    }
}

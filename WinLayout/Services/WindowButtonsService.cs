using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using WinLayout.Models;
using WinLayout.Native;
using WinLayout.Views;

namespace WinLayout.Services;

public class WindowButtonsService : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly WindowManager _windowManager;
    private readonly MonitorService _monitorService;
    private readonly WindowFilterService _filterService;
    private WindowButtonsOverlay? _overlay;

    private IntPtr _lastFgHwnd = IntPtr.Zero;
    private User32.RECT _lastWindowRect;
    private int _lastMaxZones = -1;
    private bool _lastHasMultiMonitor;

    private bool _isPaused;
    private IntPtr _fgWinEventHook;
    private readonly User32.WinEventDelegate _winEventDelegate;

    public WindowButtonsService(
        WindowManager windowManager,
        MonitorService monitorService,
        WindowFilterService filterService)
    {
        _windowManager = windowManager;
        _monitorService = monitorService;
        _filterService = filterService;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(80)
        };
        _timer.Tick += OnTimerTick;

        // Keep delegate alive — SetWinEventHook callback runs on system thread
        _winEventDelegate = OnWinEvent;
        _fgWinEventHook = User32.SetWinEventHook(
            User32.EVENT_SYSTEM_FOREGROUND,
            User32.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero,
            _winEventDelegate,
            0, 0,
            User32.WINEVENT_OUTOFCONTEXT);
    }

    public bool IsPaused
    {
        get => _isPaused;
        set
        {
            _isPaused = value;
            if (_isPaused)
                HideOverlay();
        }
    }

    public void Start() => _timer.Start();

    private void OnWinEvent(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (idObject != 0) return;
        Application.Current.Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            () => DoPoll());
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (_isPaused) return;
        DoPoll();
    }

    private void DoPoll()
    {
        if (_isPaused) return;

        var fgHwnd = User32.GetForegroundWindow();
        if (fgHwnd == IntPtr.Zero) { HideOverlay(); return; }

        // Exclude our own process windows
        User32.GetWindowThreadProcessId(fgHwnd, out uint pid);
        if (pid == Environment.ProcessId) { HideOverlay(); return; }

        // Check if it's a manageable window
        if (!_filterService.ShouldManage(fgHwnd)) { HideOverlay(); return; }

        // Get window rect
        User32.GetWindowRect(fgHwnd, out var rect);
        int windowWidth = rect.Right - rect.Left;
        int windowHeight = rect.Bottom - rect.Top;

        if (windowWidth < 200 || windowHeight < 100) { HideOverlay(); return; }

        // Determine which monitor the window center is on
        int centerX = rect.Left + windowWidth / 2;
        int centerY = rect.Top + windowHeight / 2;
        var monitor = _monitorService.GetMonitorAtCursor(centerX, centerY);
        if (monitor == null) { HideOverlay(); return; }

        // Get active layout zone count
        var layout = _monitorService.GetActiveLayoutForScreen(monitor.ScreenId);
        int maxZones = layout?.Zones.Count ?? 2;
        maxZones = Math.Min(maxZones, 4);

        bool hasMultiMonitor = _monitorService.GetMonitors().Count > 1;

        // Skip update if nothing changed
        if (fgHwnd == _lastFgHwnd &&
            rect.Left == _lastWindowRect.Left &&
            rect.Top == _lastWindowRect.Top &&
            rect.Right == _lastWindowRect.Right &&
            rect.Bottom == _lastWindowRect.Bottom &&
            maxZones == _lastMaxZones &&
            hasMultiMonitor == _lastHasMultiMonitor)
            return;

        ShowOrUpdateOverlay(rect, maxZones, hasMultiMonitor);

        _lastFgHwnd = fgHwnd;
        _lastWindowRect = rect;
        _lastMaxZones = maxZones;
        _lastHasMultiMonitor = hasMultiMonitor;
    }

    private void ShowOrUpdateOverlay(
        User32.RECT windowRect,
        int maxZones,
        bool hasMultiMonitor)
    {
        if (_overlay == null)
        {
            _overlay = new WindowButtonsOverlay();
            _overlay.SnapToZoneClicked += OnSnapToZone;
            _overlay.MoveToOtherMonitorClicked += OnMoveToOther;
        }

        _overlay.PositionNearWindow(windowRect, maxZones, hasMultiMonitor);
    }

    private void OnSnapToZone(object? sender, int zoneNumber)
    {
        var fgHwnd = User32.GetForegroundWindow();
        if (fgHwnd == IntPtr.Zero) return;

        User32.GetWindowThreadProcessId(fgHwnd, out uint pid);
        if (pid == Environment.ProcessId) return;
        if (!_filterService.ShouldManage(fgHwnd)) return;

        User32.GetWindowRect(fgHwnd, out var rect);
        int centerX = rect.Left + (rect.Right - rect.Left) / 2;
        int centerY = rect.Top + (rect.Bottom - rect.Top) / 2;

        var monitor = _monitorService.GetMonitorAtCursor(centerX, centerY);
        var layout = monitor != null
            ? _monitorService.GetActiveLayoutForScreen(monitor.ScreenId)
            : null;

        List<ZoneDefinition> zones;
        if (layout != null && layout.Zones.Count >= zoneNumber)
        {
            zones = layout.Zones;
        }
        else
        {
            zones = new List<ZoneDefinition>
            {
                new() { Index = 1, Left = 0.0, Top = 0.0, Width = 0.25, Height = 1.0, Padding = 0 },
                new() { Index = 2, Left = 0.25, Top = 0.0, Width = 0.25, Height = 1.0, Padding = 0 },
                new() { Index = 3, Left = 0.50, Top = 0.0, Width = 0.25, Height = 1.0, Padding = 0 },
                new() { Index = 4, Left = 0.75, Top = 0.0, Width = 0.25, Height = 1.0, Padding = 0 },
            };
        }

        int zoneIndex = zoneNumber - 1;
        if (zoneIndex >= zones.Count) return;

        var workArea = GetScreenWorkArea(centerX, centerY);
        var zone = zones[zoneIndex];

        var target = new SnapTarget
        {
            ZoneIndex = zoneIndex,
            ScreenX = workArea.Left,
            ScreenY = workArea.Top,
            ScreenWidth = workArea.Right - workArea.Left,
            ScreenHeight = workArea.Bottom - workArea.Top,
            ZoneLeft = zone.Left,
            ZoneTop = zone.Top,
            ZoneWidth = zone.Width,
            ZoneHeight = zone.Height,
            Padding = zone.Padding
        };

        _windowManager.SnapWindow(fgHwnd, target, false);
        Debug.WriteLine($"[WindowButtons] Snapped 0x{fgHwnd:X} to zone {zoneNumber}");
    }

    private void OnMoveToOther(object? sender, EventArgs e)
    {
        var fgHwnd = User32.GetForegroundWindow();
        if (fgHwnd == IntPtr.Zero) return;

        var monitors = _monitorService.GetMonitors();
        if (monitors.Count < 2) return;

        User32.GetWindowRect(fgHwnd, out var rect);
        int centerX = rect.Left + (rect.Right - rect.Left) / 2;
        int centerY = rect.Top + (rect.Bottom - rect.Top) / 2;

        var currentMonitor = monitors.FirstOrDefault(m =>
            centerX >= m.X && centerX < m.X + m.Width &&
            centerY >= m.Y && centerY < m.Y + m.Height);

        var otherMonitor = monitors.FirstOrDefault(m => m != currentMonitor);
        if (otherMonitor == null) return;

        // Unmaximize first
        User32.ShowWindow(fgHwnd, User32.SW_RESTORE);

        int windowW = rect.Right - rect.Left;
        int windowH = rect.Bottom - rect.Top;

        // Clamp window size to fit target monitor
        if (windowW > otherMonitor.Width) windowW = otherMonitor.Width;
        if (windowH > otherMonitor.Height) windowH = otherMonitor.Height;

        // Center on the other monitor
        int newX = otherMonitor.X + (otherMonitor.Width - windowW) / 2;
        int newY = otherMonitor.Y + (otherMonitor.Height - windowH) / 2;

        User32.SetWindowPos(fgHwnd, User32.HWND_TOP,
            newX, newY, windowW, windowH,
            User32.SWP_NOZORDER | User32.SWP_NOACTIVATE | User32.SWP_SHOWWINDOW);

        Debug.WriteLine($"[WindowButtons] Moved 0x{fgHwnd:X} to {otherMonitor.ScreenId}");
    }

    private static User32.RECT GetScreenWorkArea(int x, int y)
    {
        var pt = new User32.POINT { X = x, Y = y };
        var hMonitor = User32.MonitorFromPoint(pt, User32.MONITOR_DEFAULTTONULL);

        if (hMonitor == IntPtr.Zero)
            return new User32.RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1080 };

        var mi = new User32.MONITORINFO();
        mi.cbSize = Marshal.SizeOf(mi);
        if (User32.GetMonitorInfo(hMonitor, ref mi))
            return mi.rcWork;

        return new User32.RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1080 };
    }

    private void HideOverlay()
    {
        _overlay?.Hide();
        _lastFgHwnd = IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_fgWinEventHook != IntPtr.Zero)
        {
            User32.UnhookWinEvent(_fgWinEventHook);
            _fgWinEventHook = IntPtr.Zero;
        }
        _timer.Stop();
        if (_overlay != null)
        {
            _overlay.SnapToZoneClicked -= OnSnapToZone;
            _overlay.MoveToOtherMonitorClicked -= OnMoveToOther;
            _overlay.Close();
            _overlay = null;
        }
    }
}

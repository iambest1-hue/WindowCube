using System.Runtime.InteropServices;
using System.Windows;
using WinLayout.Models;
using WinLayout.Native;
using WinLayout.Views;

namespace WinLayout.Services;

public class OverlayService
{
    private OverlayWindow? _overlayWindow;
    private readonly LayoutService _layoutService;
    private readonly MonitorService _monitorService;
    private string _currentScreenId = "";

    public OverlayService(ConfigService configService, LayoutService layoutService,
        MonitorService monitorService)
    {
        _layoutService = layoutService;
        _monitorService = monitorService;
    }

    public void Show(int cursorX, int cursorY)
    {
        var monitor = _monitorService.GetMonitorAtCursor(cursorX, cursorY);
        _currentScreenId = monitor?.ScreenId ?? "";
        var bounds = GetScreenBounds(cursorX, cursorY);
        var zones = GetActiveZones();

        EnsureOverlayWindow();
        int highlightedIndex = GetZoneIndexAtCursor(cursorX, cursorY, bounds, zones);

        _overlayWindow!.ShowOverlay(bounds.X, bounds.Y, bounds.Width, bounds.Height, zones, highlightedIndex);
    }

    public void UpdateCursor(int cursorX, int cursorY)
    {
        if (_overlayWindow == null || _overlayWindow.Visibility != Visibility.Visible)
            return;

        var bounds = GetScreenBounds(cursorX, cursorY);
        var zones = GetActiveZones();
        int highlightedIndex = GetZoneIndexAtCursor(cursorX, cursorY, bounds, zones);

        _overlayWindow.ShowOverlay(bounds.X, bounds.Y, bounds.Width, bounds.Height, zones, highlightedIndex);
    }

    public SnapTarget? GetSnapTarget(int cursorX, int cursorY)
    {
        var screen = GetScreenBounds(cursorX, cursorY);
        var zones = GetActiveZones();
        int zoneIndex = GetZoneIndexAtCursor(cursorX, cursorY, screen, zones);

        if (zoneIndex < 0 || zoneIndex >= zones.Count)
            return null;

        var zone = zones[zoneIndex];
        return new SnapTarget
        {
            ZoneIndex = zoneIndex,
            ScreenX = screen.X,
            ScreenY = screen.Y,
            ScreenWidth = screen.Width,
            ScreenHeight = screen.Height,
            ZoneLeft = zone.Left,
            ZoneTop = zone.Top,
            ZoneWidth = zone.Width,
            ZoneHeight = zone.Height,
            Padding = zone.Padding
        };
    }

    public void Hide()
    {
        _overlayWindow?.HideOverlay();
    }

    private List<ZoneDefinition> GetActiveZones()
    {
        LayoutDefinition? layout = null;

        if (!string.IsNullOrEmpty(_currentScreenId))
            layout = _monitorService.GetActiveLayoutForScreen(_currentScreenId);

        layout ??= _layoutService.GetActiveLayout();

        if (layout != null && layout.Zones.Count > 0)
            return layout.Zones;

        // Fallback: default left-right split
        return new List<ZoneDefinition>
        {
            new() { Index = 1, Left = 0.0, Top = 0.0, Width = 0.5, Height = 1.0, Padding = 0 },
            new() { Index = 2, Left = 0.5, Top = 0.0, Width = 0.5, Height = 1.0, Padding = 0 }
        };
    }

    private void EnsureOverlayWindow()
    {
        if (_overlayWindow == null)
        {
            _overlayWindow = new OverlayWindow();
        }
    }

    private static ScreenBounds GetScreenBounds(int cursorX, int cursorY)
    {
        var pt = new User32.POINT { X = cursorX, Y = cursorY };
        var hMonitor = User32.MonitorFromPoint(pt, User32.MONITOR_DEFAULTTONULL);

        if (hMonitor == IntPtr.Zero)
            return new ScreenBounds(0, 0, 1920, 1080); // fallback

        var mi = new User32.MONITORINFO();
        mi.cbSize = Marshal.SizeOf(mi);
        if (User32.GetMonitorInfo(hMonitor, ref mi))
        {
            return new ScreenBounds(
                mi.rcMonitor.Left,
                mi.rcMonitor.Top,
                mi.rcMonitor.Right - mi.rcMonitor.Left,
                mi.rcMonitor.Bottom - mi.rcMonitor.Top);
        }

        return new ScreenBounds(0, 0, 1920, 1080);
    }

    private static int GetZoneIndexAtCursor(int cursorX, int cursorY,
        ScreenBounds screenBounds, List<ZoneDefinition> zones)
    {
        double relX = (double)(cursorX - screenBounds.X) / screenBounds.Width;
        double relY = (double)(cursorY - screenBounds.Y) / screenBounds.Height;

        for (int i = 0; i < zones.Count; i++)
        {
            var z = zones[i];
            if (relX >= z.Left && relX <= z.Left + z.Width &&
                relY >= z.Top && relY <= z.Top + z.Height)
            {
                return i;
            }
        }
        return -1;
    }

    private record struct ScreenBounds(int X, int Y, int Width, int Height);
}

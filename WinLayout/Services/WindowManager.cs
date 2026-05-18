using System.Diagnostics;
using System.Runtime.InteropServices;
using WinLayout.Models;
using WinLayout.Native;

namespace WinLayout.Services;

public class SnapTarget
{
    public int ZoneIndex { get; init; }
    public int ScreenX { get; init; }
    public int ScreenY { get; init; }
    public int ScreenWidth { get; init; }
    public int ScreenHeight { get; init; }
    public double ZoneLeft { get; init; }
    public double ZoneTop { get; init; }
    public double ZoneWidth { get; init; }
    public double ZoneHeight { get; init; }
    public int Padding { get; init; }
}

public class WindowManager
{
    // ScreenId -> ZoneIndex -> WindowHandle
    private readonly Dictionary<(string ScreenId, int ZoneIndex), IntPtr> _zoneOccupancy = new();

    public SnapTarget? LastSnapTarget { get; private set; }

    public void SnapWindow(IntPtr hwnd, SnapTarget target)
    {
        // Un-maximize first
        UnmaximizeWindow(hwnd);

        var screenId = GetScreenId(target);

        // Check if zone is occupied
        var key = (screenId, target.ZoneIndex);
        if (_zoneOccupancy.TryGetValue(key, out var existingHwnd) && existingHwnd != IntPtr.Zero)
        {
            if (existingHwnd != hwnd)
            {
                // Displace the existing window
                DisplaceWindow(existingHwnd, target);
            }
        }

        // Calculate pixel position
        int x = target.ScreenX + (int)(target.ZoneLeft * target.ScreenWidth) + target.Padding;
        int y = target.ScreenY + (int)(target.ZoneTop * target.ScreenHeight) + target.Padding;
        int w = (int)(target.ZoneWidth * target.ScreenWidth) - target.Padding * 2;
        int h = (int)(target.ZoneHeight * target.ScreenHeight) - target.Padding * 2;

        User32.SetWindowPos(
            hwnd,
            User32.HWND_TOP,
            x, y, w, h,
            User32.SWP_NOZORDER | User32.SWP_NOACTIVATE | User32.SWP_SHOWWINDOW);

        _zoneOccupancy[key] = hwnd;
        LastSnapTarget = target;

        Debug.WriteLine($"[WindowManager] Snapped 0x{hwnd:X} to zone {target.ZoneIndex} @ ({x},{y}) {w}x{h}");
    }

    private void DisplaceWindow(IntPtr hwnd, SnapTarget target)
    {
        // Restore original size and center on screen
        User32.GetWindowRect(hwnd, out var rect);
        int windowWidth = rect.Right - rect.Left;
        int windowHeight = rect.Bottom - rect.Top;

        // Clamp to reasonable size if the window is weirdly sized
        if (windowWidth <= 0 || windowHeight <= 0 ||
            windowWidth > target.ScreenWidth || windowHeight > target.ScreenHeight)
        {
            windowWidth = 800;
            windowHeight = 600;
        }

        int cx = target.ScreenX + (target.ScreenWidth - windowWidth) / 2;
        int cy = target.ScreenY + (target.ScreenHeight - windowHeight) / 2;

        User32.SetWindowPos(
            hwnd,
            User32.HWND_TOP,
            cx, cy, windowWidth, windowHeight,
            User32.SWP_NOZORDER | User32.SWP_NOACTIVATE | User32.SWP_SHOWWINDOW);

        Debug.WriteLine($"[WindowManager] Displaced 0x{hwnd:X} to center ({cx},{cy})");
    }

    public void RearrangeAll(List<ZoneDefinition> newZones, int screenX, int screenY,
        int screenWidth, int screenHeight)
    {
        if (LastSnapTarget == null) return;

        var screenId = GetScreenId(LastSnapTarget);

        for (int i = 0; i < newZones.Count; i++)
        {
            var key = (screenId, i);
            if (_zoneOccupancy.TryGetValue(key, out var hwnd) && hwnd != IntPtr.Zero)
            {
                // Remove old entry and recall SnapWindow with updated zone
                _zoneOccupancy.Remove(key);

                // Check if window still exists
                if (User32.IsWindow(hwnd))
                {
                    var zone = newZones[i];
                    SnapWindow(hwnd, new SnapTarget
                    {
                        ZoneIndex = i,
                        ScreenX = screenX,
                        ScreenY = screenY,
                        ScreenWidth = screenWidth,
                        ScreenHeight = screenHeight,
                        ZoneLeft = zone.Left,
                        ZoneTop = zone.Top,
                        ZoneWidth = zone.Width,
                        ZoneHeight = zone.Height,
                        Padding = zone.Padding
                    });
                }
            }
        }
    }

    public void QuickFill(List<ZoneDefinition> zones, int screenX, int screenY,
        int screenWidth, int screenHeight)
    {
        var windows = new List<IntPtr>();
        User32.EnumWindows((hwnd, _) =>
        {
            if (IsFillableWindow(hwnd))
                windows.Add(hwnd);
            return true;
        }, IntPtr.Zero);

        for (int i = 0; i < Math.Min(windows.Count, zones.Count); i++)
        {
            var zone = zones[i];
            SnapWindow(windows[i], new SnapTarget
            {
                ZoneIndex = i,
                ScreenX = screenX,
                ScreenY = screenY,
                ScreenWidth = screenWidth,
                ScreenHeight = screenHeight,
                ZoneLeft = zone.Left,
                ZoneTop = zone.Top,
                ZoneWidth = zone.Width,
                ZoneHeight = zone.Height,
                Padding = zone.Padding
            });
        }
    }

    private static bool IsFillableWindow(IntPtr hwnd)
    {
        if (!User32.IsWindowVisible(hwnd)) return false;
        if (User32.IsIconic(hwnd)) return false;

        User32.GetWindowRect(hwnd, out var rect);
        int w = rect.Right - rect.Left;
        int h = rect.Bottom - rect.Top;
        if (w < 200 || h < 150) return false;

        // Skip windows without titles (tooltips, menus, etc.)
        var sb = new System.Text.StringBuilder(256);
        User32.GetWindowText(hwnd, sb, sb.Capacity);
        if (sb.Length == 0) return false;

        return true;
    }

    public void RemoveWindow(IntPtr hwnd)
    {
        var keys = _zoneOccupancy.Where(kvp => kvp.Value == hwnd).Select(kvp => kvp.Key).ToList();
        foreach (var key in keys)
            _zoneOccupancy.Remove(key);
    }

    public IntPtr? GetWindowInZone(string screenId, int zoneIndex)
    {
        var key = (screenId, zoneIndex);
        return _zoneOccupancy.TryGetValue(key, out var hwnd) ? hwnd : null;
    }

    private static void UnmaximizeWindow(IntPtr hwnd)
    {
        var wp = new User32.WINDOWPLACEMENT();
        wp.length = Marshal.SizeOf(wp);
        if (User32.GetWindowPlacement(hwnd, ref wp))
        {
            if (wp.showCmd == User32.SW_SHOWMAXIMIZED)
            {
                User32.ShowWindow(hwnd, User32.SW_RESTORE);
            }
        }
    }

    private static string GetScreenId(SnapTarget target)
    {
        return $"{target.ScreenX}x{target.ScreenY}_{target.ScreenWidth}x{target.ScreenHeight}";
    }
}

using System.Diagnostics;
using System.Runtime.InteropServices;
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

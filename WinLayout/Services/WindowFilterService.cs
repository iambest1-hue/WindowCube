using System.Diagnostics;
using System.Text;
using WinLayout.Models;
using WinLayout.Native;

namespace WinLayout.Services;

public class WindowFilterService
{
    private readonly ConfigService _configService;

    // System window class names to always exclude
    private static readonly HashSet<string> SystemClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Progman", "Shell_TrayWnd", "Shell_SecondaryTrayWnd",
        "WorkerW", "TaskSwitcherWnd", "Windows.UI.Core.CoreWindow",
        "ApplicationFrameWindow", "TaskManagerWindow"
    };

    public WindowFilterService(ConfigService configService)
    {
        _configService = configService;
    }

    public bool ShouldManage(IntPtr hwnd)
    {
        var config = _configService.LoadConfig();

        // Check minimum size
        User32.GetWindowRect(hwnd, out var rect);
        int w = rect.Right - rect.Left;
        int h = rect.Bottom - rect.Top;
        if (w < config.MinWindowSize.Width || h < config.MinWindowSize.Height)
            return false;

        // Check system class exclusion
        var className = GetClassName(hwnd);
        if (SystemClasses.Contains(className))
            return false;

        // Check process name against blacklist
        var processName = GetProcessName(hwnd);
        if (config.BlacklistedProcesses.Any(
            p => string.Equals(p, processName, StringComparison.OrdinalIgnoreCase)))
            return false;

        return true;
    }

    private static string GetClassName(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        User32.GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public static string GetProcessName(IntPtr hwnd)
    {
        try
        {
            User32.GetWindowThreadProcessId(hwnd, out uint pid);
            var process = Process.GetProcessById((int)pid);
            return process.ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }
}

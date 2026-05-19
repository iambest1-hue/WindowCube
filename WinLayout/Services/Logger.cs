using System.Diagnostics;
using System.IO;

namespace WinLayout.Services;

internal static class Logger
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WinLayout", "debug.log");

    private static readonly object _lock = new();

    static Logger()
    {
        var dir = Path.GetDirectoryName(LogPath);
        if (dir != null) Directory.CreateDirectory(dir);
    }

    [Conditional("DEBUG")]
    public static void Log(string message)
    {
        lock (_lock)
        {
            File.AppendAllText(LogPath,
                $"{DateTime.Now:HH:mm:ss.fff} [{Environment.CurrentManagedThreadId}] {message}\n");
        }
    }
}

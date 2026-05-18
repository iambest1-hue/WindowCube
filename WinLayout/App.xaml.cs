using System.Windows;
using Microsoft.Win32;
using WinLayout.Native;

namespace WinLayout;

public partial class App : Application
{
    private const string StartupRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "WinLayout";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Register for automatic restart on crash
        User32.RegisterApplicationRestart(string.Empty, 0);

        // Handle startup toggle
        var config = new Services.ConfigService().LoadConfig();
        SetStartup(config.RunAtStartup);
    }

    public static void SetStartup(bool enable)
    {
        using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, true);
        if (key == null) return;

        if (enable)
        {
            var exePath = Environment.ProcessPath;
            if (exePath != null)
                key.SetValue(AppName, $"\"{exePath}\"");
        }
        else
        {
            key.DeleteValue(AppName, false);
        }
    }
}

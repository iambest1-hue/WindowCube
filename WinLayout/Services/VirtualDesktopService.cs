using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using WinLayout.Models;
using WinLayout.Native;

namespace WinLayout.Services;

public class VirtualDesktopService : IDisposable
{
    private readonly ConfigService _configService;
    private readonly LayoutService _layoutService;
    private readonly DispatcherTimer _pollTimer;
    private readonly IVirtualDesktopManager _desktopManager;
    private Guid _currentDesktopId;

    public event EventHandler<Guid>? DesktopChanged;

    public VirtualDesktopService(ConfigService configService, LayoutService layoutService)
    {
        _configService = configService;
        _layoutService = layoutService;

        var type = Type.GetTypeFromCLSID(
            new Guid(VirtualDesktopCLSID.VirtualDesktopManager));
        _desktopManager = (IVirtualDesktopManager)Activator.CreateInstance(type!)!;

        _currentDesktopId = GetCurrentDesktopId();

        _pollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _pollTimer.Tick += OnPollTimer;
    }

    public void Start()
    {
        _pollTimer.Start();
    }

    private void OnPollTimer(object? sender, EventArgs e)
    {
        var desktopId = GetCurrentDesktopId();
        if (desktopId != _currentDesktopId)
        {
            var oldId = _currentDesktopId;
            _currentDesktopId = desktopId;
            Debug.WriteLine($"[VirtualDesktop] Switched from {oldId} to {desktopId}");

            // Load layout for this desktop
            var config = _configService.LoadConfig();
            if (config.PerDesktopLayout)
            {
                // Try to load layout associated with this desktop
                // For now, each desktop uses the default active layout
                // Full per-desktop layout storage can be added later
            }

            DesktopChanged?.Invoke(this, desktopId);
        }
    }

    public Guid GetCurrentDesktopId()
    {
        // Use a dummy window to query the virtual desktop
        var dummyHwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
        if (dummyHwnd != IntPtr.Zero)
        {
            int hr = _desktopManager.GetWindowDesktopId(dummyHwnd, out var desktopId);
            if (hr == 0)
                return desktopId;
        }

        // Fallback: use GetForegroundWindow
        var fgHwnd = GetForegroundWindow();
        if (fgHwnd != IntPtr.Zero)
        {
            int hr = _desktopManager.GetWindowDesktopId(fgHwnd, out var desktopId);
            if (hr == 0)
                return desktopId;
        }

        return Guid.Empty;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    public void Dispose()
    {
        _pollTimer.Stop();
    }
}

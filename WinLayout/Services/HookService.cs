using WinLayout.Models;
using WinLayout.Native;

namespace WinLayout.Services;

public class WindowDragEventArgs : EventArgs
{
    public IntPtr WindowHandle { get; init; }
    public int CursorX { get; init; }
    public int CursorY { get; init; }
    public int WindowX { get; init; }
    public int WindowY { get; init; }
    public int WindowWidth { get; init; }
    public int WindowHeight { get; init; }
}

public class HookService : IDisposable
{
    private readonly WindowFilterService _filterService;
    private readonly UserConfig _config;
    private IntPtr _hook;
    private User32.WinEventDelegate? _hookDelegate;

    private IntPtr _dragWindow;
    private int _dragStartX, _dragStartY;
    private bool _isDragging;
    private bool _dragStartedFired;

    public event EventHandler<WindowDragEventArgs>? DragStarted;
    public event EventHandler<WindowDragEventArgs>? DragMoved;
    public event EventHandler<WindowDragEventArgs>? DragEnded;

    public bool IsStackingKeyPressed()
    {
        return IsKeyDown(GetVkForModifier(_config.StackingKey));
    }

    private static int GetVkForModifier(string key) => key switch
    {
        "Shift" => User32.VK_SHIFT,
        "Ctrl" => User32.VK_CONTROL,
        "Alt" => User32.VK_MENU,
        _ => User32.VK_CONTROL
    };

    public HookService(ConfigService configService, WindowFilterService filterService)
    {
        _filterService = filterService;
        _config = configService.LoadConfig();
    }

    public void Start()
    {
        _hookDelegate = WinEventCallback;
        _hook = User32.SetWinEventHook(
            User32.EVENT_SYSTEM_MOVESIZESTART,
            User32.EVENT_OBJECT_LOCATIONCHANGE,
            IntPtr.Zero,
            _hookDelegate,
            0, 0,
            User32.WINEVENT_INCONTEXT);

        var logPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinLayout", "hook.log");
        var dir = System.IO.Path.GetDirectoryName(logPath);
        if (dir != null) System.IO.Directory.CreateDirectory(dir);
        System.IO.File.WriteAllText(logPath, $"hook=0x{_hook:X} time={DateTime.Now:HH:mm:ss}");
    }

    private void WinEventCallback(IntPtr hWinEventHook, uint eventType,
        IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        try
        {
            if (hwnd == IntPtr.Zero || idObject != 0) return;

            if (eventType == User32.EVENT_SYSTEM_MOVESIZESTART)
            {
                OnMoveSizeStart(hwnd);
            }
            else if (_isDragging && eventType == User32.EVENT_OBJECT_LOCATIONCHANGE)
            {
                OnLocationChange(hwnd);
            }
            else if (eventType == User32.EVENT_SYSTEM_MOVESIZEEND)
            {
                OnMoveSizeEnd(hwnd);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Hook] WinEventCallback error: {ex.Message}");
        }
    }

    private void OnMoveSizeStart(IntPtr hwnd)
    {
        if (IsOwnWindow(hwnd)) return;
        if (!_filterService.ShouldManage(hwnd)) return;
        if (!IsModifierPressed()) return;

        _dragWindow = hwnd;
        _isDragging = true;

        User32.GetCursorPos(out var cursor);
        User32.GetWindowRect(hwnd, out var rect);
        _dragStartX = cursor.X;
        _dragStartY = cursor.Y;
    }

    private void OnLocationChange(IntPtr hwnd)
    {
        if (hwnd != _dragWindow) return;

        User32.GetCursorPos(out var cursor);
        User32.GetWindowRect(hwnd, out var rect);

        int movedX = Math.Abs(cursor.X - _dragStartX);
        int movedY = Math.Abs(cursor.Y - _dragStartY);
        int threshold = _config.DragThreshold;

        if (movedX >= threshold || movedY >= threshold)
        {
            int ww = rect.Right - rect.Left;
            int wh = rect.Bottom - rect.Top;
            var args = new WindowDragEventArgs
            {
                WindowHandle = hwnd,
                CursorX = cursor.X,
                CursorY = cursor.Y,
                WindowX = rect.Left,
                WindowY = rect.Top,
                WindowWidth = ww,
                WindowHeight = wh
            };

            if (!_dragStartedFired)
            {
                _dragStartedFired = true;
                DragStarted?.Invoke(this, args);
            }

            DragMoved?.Invoke(this, args);
        }
    }

    private void OnMoveSizeEnd(IntPtr hwnd)
    {
        if (hwnd != _dragWindow) return;

        User32.GetCursorPos(out var cursor);
        User32.GetWindowRect(hwnd, out var rect);

        int movedX = Math.Abs(cursor.X - _dragStartX);
        int movedY = Math.Abs(cursor.Y - _dragStartY);
        int threshold = _config.DragThreshold;

        if (movedX >= threshold || movedY >= threshold)
        {
            int ww = rect.Right - rect.Left;
            int wh = rect.Bottom - rect.Top;
            var args = new WindowDragEventArgs
            {
                WindowHandle = hwnd,
                CursorX = cursor.X,
                CursorY = cursor.Y,
                WindowX = rect.Left,
                WindowY = rect.Top,
                WindowWidth = ww,
                WindowHeight = wh
            };

            DragEnded?.Invoke(this, args);
        }

        _isDragging = false;
        _dragStartedFired = false;
        _dragWindow = IntPtr.Zero;
    }

    private bool IsModifierPressed()
    {
        string key = _config.ModifierKey;
        return key switch
        {
            "None" => true,
            "Shift" => IsKeyDown(User32.VK_SHIFT),
            "Ctrl" => IsKeyDown(User32.VK_CONTROL),
            "Alt" => IsKeyDown(User32.VK_MENU),
            "Shift+Ctrl" => IsKeyDown(User32.VK_SHIFT) && IsKeyDown(User32.VK_CONTROL),
            "Shift+Alt" => IsKeyDown(User32.VK_SHIFT) && IsKeyDown(User32.VK_MENU),
            "Ctrl+Alt" => IsKeyDown(User32.VK_CONTROL) && IsKeyDown(User32.VK_MENU),
            _ => true
        };
    }

    private static bool IsKeyDown(int vk)
    {
        return (User32.GetAsyncKeyState(vk) & 0x8000) != 0;
    }

    private bool IsOwnWindow(IntPtr hwnd)
    {
        // Avoid hooking our own overlay/main window
        // This is a simple guard; will be refined later
        return false;
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            User32.UnhookWinEvent(_hook);
            _hook = IntPtr.Zero;
        }
        _hookDelegate = null;
        GC.SuppressFinalize(this);
    }
}

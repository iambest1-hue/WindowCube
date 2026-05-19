using System.Diagnostics;
using System.Windows.Threading;
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
    private readonly Dispatcher _dispatcher;
    private readonly WindowFilterService _filterService;
    private readonly UserConfig _config;
    private IntPtr _hook;
    private User32.WinEventDelegate? _hookDelegate;

    private IntPtr _dragWindow;
    private int _dragStartX, _dragStartY;
    private int _dragWindowStartX, _dragWindowStartY;
    private bool _isDragging;
    private bool _dragStartedFired;
    private int _eventCount;

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

    public HookService(Dispatcher dispatcher, ConfigService configService,
        WindowFilterService filterService)
    {
        _dispatcher = dispatcher;
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
        Logger.Log($"HookService started, hook=0x{_hook:X}");
    }

    private void WinEventCallback(IntPtr hWinEventHook, uint eventType,
        IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        try
        {
            _eventCount++;

            if (hwnd == IntPtr.Zero || idObject != 0)
            {
                if (_eventCount % 500 == 0)
                    Logger.Log($"WinEvent filtered: {_eventCount} total, hwnd=0x{hwnd:X} idObj={idObject}");
                return;
            }

            if (eventType == User32.EVENT_SYSTEM_MOVESIZESTART)
            {
                Logger.Log($"EVENT_MOVESIZESTART hwnd=0x{hwnd:X}");
                OnMoveSizeStart(hwnd);
            }
            else if (_isDragging && eventType == User32.EVENT_OBJECT_LOCATIONCHANGE)
            {
                OnLocationChange(hwnd);
            }
            else if (eventType == User32.EVENT_SYSTEM_MOVESIZEEND)
            {
                Logger.Log($"EVENT_MOVESIZEEND hwnd=0x{hwnd:X}");
                OnMoveSizeEnd(hwnd);
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"WinEventCallback ERROR: {ex}");
        }
    }

    private void OnMoveSizeStart(IntPtr hwnd)
    {
        Logger.Log($"OnMoveSizeStart hwnd=0x{hwnd:X}");

        if (IsOwnWindow(hwnd)) { Logger.Log("  -> rejected: own window"); return; }
        if (!_filterService.ShouldManage(hwnd)) { Logger.Log("  -> rejected: filter"); return; }
        if (!IsModifierPressed()) { Logger.Log("  -> rejected: modifier not pressed"); return; }

        _dragWindow = hwnd;
        _isDragging = true;
        Logger.Log($"  -> ACCEPTED, modifier={_config.ModifierKey}");

        User32.GetCursorPos(out var cursor);
        User32.GetWindowRect(hwnd, out var rect);

        _dragStartX = cursor.X;
        _dragStartY = cursor.Y;
        _dragWindowStartX = rect.Left;
        _dragWindowStartY = rect.Top;

        Debug.WriteLine($"[Hook] MoveSizeStart hwnd=0x{hwnd:X} pos=({cursor.X},{cursor.Y})");
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
                Logger.Log($"  -> FIRING DragStarted cursor=({cursor.X},{cursor.Y}) moved=({movedX},{movedY})");
                _dispatcher.BeginInvoke(() => DragStarted?.Invoke(this, args));
            }

            _dispatcher.BeginInvoke(() => DragMoved?.Invoke(this, args));
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

            _dispatcher.BeginInvoke(() => DragEnded?.Invoke(this, args));
        }

        _isDragging = false;
        _dragStartedFired = false;
        _dragWindow = IntPtr.Zero;

        Debug.WriteLine($"[Hook] MoveSizeEnd hwnd=0x{hwnd:X}");
    }

    private bool IsModifierPressed()
    {
        string key = _config.ModifierKey;
        return key switch
        {
            "Shift" => IsKeyDown(User32.VK_SHIFT),
            "Ctrl" => IsKeyDown(User32.VK_CONTROL),
            "Alt" => IsKeyDown(User32.VK_MENU),
            "Shift+Ctrl" => IsKeyDown(User32.VK_SHIFT) && IsKeyDown(User32.VK_CONTROL),
            "Shift+Alt" => IsKeyDown(User32.VK_SHIFT) && IsKeyDown(User32.VK_MENU),
            "Ctrl+Alt" => IsKeyDown(User32.VK_CONTROL) && IsKeyDown(User32.VK_MENU),
            _ => IsKeyDown(User32.VK_SHIFT)
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

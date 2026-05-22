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
    private readonly DispatcherTimer _timer;
    private readonly WindowFilterService _filterService;
    private readonly UserConfig _config;
    private IntPtr _dragWindow;
    private int _dragStartX, _dragStartY;
    private bool _isDragging;
    private bool _dragStartedFired;
    private int _lastCursorX, _lastCursorY;

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

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(30)
        };
        _timer.Tick += OnTimerTick;
    }

    public void Start()
    {
        _timer.Start();
        Debug.WriteLine($"[Hook] Polling started, interval=30ms");
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        bool mouseDown = IsKeyDown(User32.VK_LBUTTON);
        User32.GetCursorPos(out var cursor);

        if (!mouseDown)
        {
            if (_isDragging)
            {
                EndDrag(cursor);
            }
            _isDragging = false;
            _dragStartedFired = false;
            _dragWindow = IntPtr.Zero;
            return;
        }

        if (!_isDragging)
        {
            // Check if user is starting a drag (mouse down + movement)
            if (!IsModifierPressed()) return;

            // Get top-level window under cursor
            var hwnd = User32.WindowFromPoint(cursor);
            if (hwnd != IntPtr.Zero)
                hwnd = User32.GetAncestor(hwnd, User32.GA_ROOT);
            if (hwnd == IntPtr.Zero) return;
            if (!_filterService.ShouldManage(hwnd)) return;

            // Only trigger on actual window dragging (title bar), not any mouse drag
            var lParam = (IntPtr)(cursor.Y << 16 | (cursor.X & 0xFFFF));
            var hit = User32.SendMessage(hwnd, User32.WM_NCHITTEST, IntPtr.Zero, lParam);
            if ((int)hit != User32.HTCAPTION) return;

            // Start tracking
            _isDragging = true;
            _dragWindow = hwnd;
            _dragStartX = cursor.X;
            _dragStartY = cursor.Y;
            _lastCursorX = cursor.X;
            _lastCursorY = cursor.Y;
            return;
        }

        // Already dragging — check for movement
        int movedX = Math.Abs(cursor.X - _dragStartX);
        int movedY = Math.Abs(cursor.Y - _dragStartY);
        int threshold = _config.DragThreshold;

        if (movedX >= threshold || movedY >= threshold)
        {
            // Window might have changed during drag (cross-window drags are rare)
            // Keep using the same _dragWindow

            User32.GetWindowRect(_dragWindow, out var rect);
            int ww = rect.Right - rect.Left;
            int wh = rect.Bottom - rect.Top;

            var args = new WindowDragEventArgs
            {
                WindowHandle = _dragWindow,
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

        _lastCursorX = cursor.X;
        _lastCursorY = cursor.Y;
    }

    private void EndDrag(User32.POINT cursor)
    {
        int movedX = Math.Abs(cursor.X - _dragStartX);
        int movedY = Math.Abs(cursor.Y - _dragStartY);
        int threshold = _config.DragThreshold;

        if ((movedX >= threshold || movedY >= threshold) && _dragWindow != IntPtr.Zero)
        {
            User32.GetWindowRect(_dragWindow, out var rect);
            int ww = rect.Right - rect.Left;
            int wh = rect.Bottom - rect.Top;

            var args = new WindowDragEventArgs
            {
                WindowHandle = _dragWindow,
                CursorX = cursor.X,
                CursorY = cursor.Y,
                WindowX = rect.Left,
                WindowY = rect.Top,
                WindowWidth = ww,
                WindowHeight = wh
            };

            DragEnded?.Invoke(this, args);
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(User32.POINT point);

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

    public void Dispose()
    {
        _timer.Stop();
    }
}

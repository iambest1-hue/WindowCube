using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using WinLayout.Native;

namespace WinLayout.Views;

public partial class WindowButtonsOverlay : Window
{
    public event EventHandler<int>? SnapToZoneClicked;
    public event EventHandler? MoveToOtherMonitorClicked;

    private double _dpiScaleX = 1.0;
    private double _dpiScaleY = 1.0;
    private bool _stylesApplied;

    public WindowButtonsOverlay()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;

        Btn1.Click += (_, _) => SnapToZoneClicked?.Invoke(this, 1);
        Btn2.Click += (_, _) => SnapToZoneClicked?.Invoke(this, 2);
        Btn3.Click += (_, _) => SnapToZoneClicked?.Invoke(this, 3);
        Btn4.Click += (_, _) => SnapToZoneClicked?.Invoke(this, 4);
        BtnAlt.Click += (_, _) => MoveToOtherMonitorClicked?.Invoke(this, EventArgs.Empty);
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (_stylesApplied) return;
        _stylesApplied = true;

        var hwnd = new WindowInteropHelper(this).Handle;
        int exStyle = User32.GetWindowLong(hwnd, User32.GWL_EXSTYLE);
        exStyle |= User32.WS_EX_TOOLWINDOW | User32.WS_EX_NOACTIVATE;
        User32.SetWindowLong(hwnd, User32.GWL_EXSTYLE, exStyle);
    }

    internal void PositionNearWindow(
        User32.RECT windowRect,
        int maxVisibleZones,
        bool hasMultipleMonitors)
    {
        var hwnd = new WindowInteropHelper(this).Handle;

        if (_dpiScaleX == 1.0 && _dpiScaleY == 1.0)
        {
            var src = PresentationSource.FromVisual(this);
            if (src != null)
            {
                _dpiScaleX = src.CompositionTarget.TransformToDevice.M11;
                _dpiScaleY = src.CompositionTarget.TransformToDevice.M22;
            }
        }

        // Show zone buttons only up to the layout's zone count, max 4
        Btn1.Visibility = maxVisibleZones >= 1 ? Visibility.Visible : Visibility.Collapsed;
        Btn2.Visibility = maxVisibleZones >= 2 ? Visibility.Visible : Visibility.Collapsed;
        Btn3.Visibility = maxVisibleZones >= 3 ? Visibility.Visible : Visibility.Collapsed;
        Btn4.Visibility = maxVisibleZones >= 4 ? Visibility.Visible : Visibility.Collapsed;
        BtnAlt.Visibility = hasMultipleMonitors ? Visibility.Visible : Visibility.Collapsed;

        // Reposition visible buttons compactly: Alt first, then zone 1-4
        double xOffset = 0;
        RepositionButton(BtnAlt, ref xOffset);
        RepositionButton(Btn1, ref xOffset);
        RepositionButton(Btn2, ref xOffset);
        RepositionButton(Btn3, ref xOffset);
        RepositionButton(Btn4, ref xOffset);

        int totalDipWidth = (int)xOffset;
        if (totalDipWidth == 0) { Hide(); return; }

        // Convert DIP width to physical pixels for positioning math
        int totalPhysicalWidth = (int)(totalDipWidth * _dpiScaleX);

        const int btnWindowHeight = 32;
        const int systemButtonsArea = 180;
        const int gapFromSystemBtns = 4;
        const int titleBarInset = 0;
        const int windowLeftPad = 8;

        // Ideal: right of title bar, just left of system min/max/close buttons
        int idealLeft = windowRect.Right - systemButtonsArea - gapFromSystemBtns - totalPhysicalWidth;

        // Clamp: never go left of the window's left edge
        int minLeft = windowRect.Left + windowLeftPad;
        int leftPhysical = Math.Max(idealLeft, minLeft);

        Left = leftPhysical / _dpiScaleX;
        Top = (windowRect.Top + titleBarInset) / _dpiScaleY;
        Width = totalDipWidth;
        Height = btnWindowHeight / _dpiScaleY;

        Show();
        Topmost = true;
    }

    private static void RepositionButton(Button btn, ref double xOffset)
    {
        if (btn.Visibility == Visibility.Visible)
        {
            Canvas.SetLeft(btn, xOffset);
            xOffset += 26;
        }
    }

    public new void Hide()
    {
        base.Hide();
    }
}

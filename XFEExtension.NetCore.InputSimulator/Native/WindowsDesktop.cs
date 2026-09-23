using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;

namespace XFEExtension.NetCore.InputSimulator.Native;

internal static class WindowsDesktop
{
    internal static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("系统输入和屏幕查询仅支持 Windows。");
    }

    internal static Size ScreenSize()
    {
        EnsureWindows();
        return new Size(NativeMethods.GetSystemMetrics(0), NativeMethods.GetSystemMetrics(1));
    }

    internal static Rectangle Bounds(bool virtualDesktop)
    {
        using var dpi = new PhysicalCoordinates();
        return virtualDesktop
            ? new Rectangle(NativeMethods.GetSystemMetrics(76), NativeMethods.GetSystemMetrics(77),
                NativeMethods.GetSystemMetrics(78), NativeMethods.GetSystemMetrics(79))
            : new Rectangle(Point.Empty, ScreenSize());
    }

    internal static Point CursorPosition()
    {
        using var dpi = new PhysicalCoordinates();
        if (!NativeMethods.GetCursorPos(out var point))
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        return point;
    }

    internal static Point Normalize(Point point, Rectangle bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
            throw new InvalidOperationException("没有可用的屏幕区域。");
        // 选取目标像素在 0..65535 区间内的中心，支持虚拟桌面的负坐标。
        static int Axis(int value, int origin, int length) =>
            (int)Math.Min(65535, (Math.Clamp((long)value - origin, 0, length - 1) * 65536 + 32768) / length);
        return new Point(Axis(point.X, bounds.X, bounds.Width), Axis(point.Y, bounds.Y, bounds.Height));
    }

    internal static Point Position(ScreenPosition position, Rectangle bounds) => position switch
    {
        ScreenPosition.TopLeft => bounds.Location,
        ScreenPosition.Top => new(bounds.Left + bounds.Width / 2, bounds.Top),
        ScreenPosition.TopRight => new(bounds.Right - 1, bounds.Top),
        ScreenPosition.Left => new(bounds.Left, bounds.Top + bounds.Height / 2),
        ScreenPosition.Center => new(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2),
        ScreenPosition.Right => new(bounds.Right - 1, bounds.Top + bounds.Height / 2),
        ScreenPosition.BottomLeft => new(bounds.Left, bounds.Bottom - 1),
        ScreenPosition.Bottom => new(bounds.Left + bounds.Width / 2, bounds.Bottom - 1),
        ScreenPosition.BottomRight => new(bounds.Right - 1, bounds.Bottom - 1),
        _ => throw new ArgumentOutOfRangeException(nameof(position))
    };

    // 只在同步查询期间修改当前线程的 DPI 上下文，退出时恢复，不改变宿主进程设置。
    private readonly struct PhysicalCoordinates : IDisposable
    {
        private readonly nint previous;

        public PhysicalCoordinates()
        {
            EnsureWindows();
            previous = NativeMethods.SetThreadDpiAwarenessContext(-4);
            if (previous == 0)
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "无法获取物理屏幕坐标；需要 Windows 10 1703 或更高版本。");
        }

        public void Dispose() => NativeMethods.SetThreadDpiAwarenessContext(previous);
    }
}

using XFEExtension.NetCore.InputSimulator.Native;

namespace XFEExtension.NetCore.InputSimulator;

/// <summary>连接已安装的 UMDF 虚拟 HID 驱动；驱动缺失或不可用时报告错误，不自动安装。</summary>
/// <remarks>输入会话独占，同一时间只能有一个控制器。支持六个普通键及八个修饰键；不回退到软件注入。</remarks>
public sealed class DriverBackend : IInputBackend
{
    private readonly IDriverTransport transport;
    private readonly object sync = new();
    private bool disposed;

    /// <summary>连接已安装的驱动。使用 DriverDeployment 显式检测和安装驱动。</summary>
    public DriverBackend() : this(new DriverTransport()) { }

    internal DriverBackend(IDriverTransport transport)
    {
        this.transport = transport;
        try
        {
            var info = transport.Query();
            if (!DriverProtocol.IsCompatible(info))
                throw new NotSupportedException("已安装的 XFE 驱动与本 DLL 协议不匹配，请更新驱动后重新启动应用。");
        }
        catch { transport.Dispose(); throw; }
    }

    /// <inheritdoc/>
    public string Name => "XFE UMDF HID driver";
    /// <inheritdoc/>
    public bool SupportsUnicodeText => false;

    /// <inheritdoc/>
    public void SendKey(ScanCode key, bool isKeyUp)
    {
        var command = DriverCommand.Create(1);
        command.Code = HidKeyMapping.GetUsage(key);
        command.Flags = isKeyUp ? 1u : 0u;
        Send(command);
    }

    /// <inheritdoc/>
    public void SendUnicode(char codeUnit, bool isKeyUp) =>
        throw new NotSupportedException("HID 驱动不支持 Unicode 文本注入。请用 InputKeys 按当前键盘布局输入可映射的字符。");

    /// <inheritdoc/>
    public void MoveMouse(int x, int y, bool absolute, bool virtualDesktop)
    {
        InputValidation.Coordinates(x, y, absolute, virtualDesktop);
        if (absolute) throw new NotSupportedException("当前 HID 驱动提供相对鼠标移动，不支持绝对定位。");
        lock (sync)
        {
            CheckDisposed();
            do
            {
                var command = DriverCommand.Create(3);
                command.X = Math.Clamp(x, -32767, 32767);
                command.Y = Math.Clamp(y, -32767, 32767);
                transport.Send(command);
                x -= command.X;
                y -= command.Y;
            } while (x != 0 || y != 0);
        }
    }

    /// <inheritdoc/>
    public void SendMouseButton(MouseButton button, bool isKeyUp)
    {
        InputValidation.Button(button);
        var command = DriverCommand.Create(2);
        command.Code = (uint)button;
        command.Flags = isKeyUp ? 1u : 0u;
        Send(command);
    }

    /// <inheritdoc/>
    public void ScrollMouse(int delta, bool horizontal)
    {
        if (delta % 120 != 0) throw new ArgumentOutOfRangeException(nameof(delta), "HID 滚轮以一格为单位，delta 必须是 120 的整数倍。");
        lock (sync)
        {
            CheckDisposed();
            for (var ticks = delta / 120; ticks != 0;)
            {
                var command = DriverCommand.Create(4);
                command.Flags = horizontal ? 1u : 0u;
                command.Delta = Math.Clamp(ticks, -127, 127);
                transport.Send(command);
                ticks -= command.Delta;
            }
        }
    }

    private void Send(DriverCommand command)
    {
        lock (sync) { CheckDisposed(); transport.Send(command); }
    }

    private void CheckDisposed() => ObjectDisposedException.ThrowIf(disposed, this);

    /// <summary>请求驱动释放全部输入并关闭会话；进程异常退出后，驱动会在约三秒无心跳时尝试释放。</summary>
    public void Dispose()
    {
        lock (sync)
        {
            if (disposed) return;
            disposed = true;
            try { transport.Reset(); }
            finally { transport.Dispose(); }
        }
    }
}

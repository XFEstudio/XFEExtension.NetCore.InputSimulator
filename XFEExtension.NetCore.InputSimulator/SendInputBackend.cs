using System.ComponentModel;
using System.Runtime.InteropServices;
using XFEExtension.NetCore.InputSimulator.Native;

namespace XFEExtension.NetCore.InputSimulator;

/// <summary>使用 Win32 SendInput 的扫描码后端。无需驱动，仍属于用户态输入。</summary>
public sealed class SendInputBackend : IInputBackend
{
    private bool disposed;

    /// <inheritdoc/>
    public string Name => "SendInput (scan code)";
    /// <inheritdoc/>
    public bool SupportsUnicodeText => true;

    /// <inheritdoc/>
    public void SendKey(ScanCode key, bool isKeyUp) => Send(CreateKeyInput(key, isKeyUp));

    /// <inheritdoc/>
    public void SendUnicode(char codeUnit, bool isKeyUp) => Send(CreateUnicodeInput(codeUnit, isKeyUp));

    internal static NativeInput CreateUnicodeInput(char codeUnit, bool isKeyUp) => new()
    {
        Type = 1,
        Data = new InputUnion { Keyboard = new KeyboardInput
        {
            ScanCode = codeUnit,
            Flags = 0x0004u | (isKeyUp ? 0x0002u : 0)
        }}
    };

    /// <inheritdoc/>
    public void MoveMouse(int x, int y, bool absolute, bool virtualDesktop)
    {
        Send(CreateMoveInput(x, y, absolute, virtualDesktop));
    }

    /// <inheritdoc/>
    public void SendMouseButton(MouseButton button, bool isKeyUp) => Send(CreateButtonInput(button, isKeyUp));

    /// <inheritdoc/>
    public void ScrollMouse(int delta, bool horizontal) => Send(CreateWheelInput(delta, horizontal));

    internal static NativeInput CreateWheelInput(int delta, bool horizontal) =>
        Mouse(0, 0, unchecked((uint)delta), horizontal ? 0x1000u : 0x0800u);

    internal static NativeInput CreateMoveInput(int x, int y, bool absolute, bool virtualDesktop)
    {
        InputValidation.Coordinates(x, y, absolute, virtualDesktop);
        return Mouse(x, y, 0, 0x0001u | 0x2000u | (absolute ? 0x8000u : 0) | (virtualDesktop ? 0x4000u : 0));
    }

    internal static NativeInput CreateKeyInput(ScanCode key, bool isKeyUp)
    {
        InputValidation.Key(key);
        return new NativeInput
        {
            Type = 1,
            Data = new InputUnion { Keyboard = new KeyboardInput
            {
                ScanCode = (ushort)((ushort)key & 0xFF),
                Flags = 0x0008u | (isKeyUp ? 0x0002u : 0) | (((ushort)key & 0xFF00) == 0xE000 ? 0x0001u : 0)
            }}
        };
    }

    internal static NativeInput CreateButtonInput(MouseButton button, bool isKeyUp)
    {
        InputValidation.Button(button);
        uint flags = button switch
        {
            MouseButton.Left => isKeyUp ? 0x0004u : 0x0002u,
            MouseButton.Right => isKeyUp ? 0x0010u : 0x0008u,
            MouseButton.Middle => isKeyUp ? 0x0040u : 0x0020u,
            _ => isKeyUp ? 0x0100u : 0x0080u
        };
        return Mouse(0, 0, button == MouseButton.XButton1 ? 1u : button == MouseButton.XButton2 ? 2u : 0u, flags);
    }

    private static NativeInput Mouse(int x, int y, uint data, uint flags) => new()
    {
        Data = new InputUnion { Mouse = new MouseInput { X = x, Y = y, MouseData = data, Flags = flags } }
    };

    private unsafe void Send(NativeInput input)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("SendInput 仅支持 Windows。");
        var sent = NativeMethods.SendInput(1, &input, sizeof(NativeInput));
        if (sent != 1)
            throw new Win32Exception(Marshal.GetLastPInvokeError(),
                "SendInput 未能发送输入。请检查目标窗口、交互桌面和双方权限级别；UIPI 拦截不一定提供错误码。");
    }

    /// <inheritdoc/>
    public void Dispose() => disposed = true;
}

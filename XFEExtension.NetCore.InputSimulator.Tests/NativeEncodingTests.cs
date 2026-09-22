using System.Drawing;
using System.Runtime.InteropServices;
using XFEExtension.NetCore.InputSimulator.Native;

namespace XFEExtension.NetCore.InputSimulator.Tests;

public class NativeEncodingTests
{
    [Fact]
    public void InputLayoutMatchesWindowsAbi()
    {
        Assert.Equal(IntPtr.Size == 8 ? 40 : 28, Marshal.SizeOf<NativeInput>());
        Assert.Equal(IntPtr.Size == 8 ? 8 : 4, Marshal.OffsetOf<NativeInput>("Data").ToInt32());
        Assert.Equal(IntPtr.Size == 8 ? 24 : 16, Marshal.SizeOf<KeyboardInput>());
        Assert.Equal(IntPtr.Size == 8 ? 32 : 24, Marshal.SizeOf<MouseInput>());
        Assert.Equal(IntPtr.Size == 8 ? 16 : 12, Marshal.OffsetOf<KeyboardInput>("ExtraInfo").ToInt32());
        Assert.Equal(IntPtr.Size == 8 ? 24 : 20, Marshal.OffsetOf<MouseInput>("ExtraInfo").ToInt32());
    }

    [Theory]
    [InlineData(ScanCode.W, 0x11, 0x08)]
    [InlineData(ScanCode.RightControl, 0x1D, 0x09)]
    [InlineData(ScanCode.RightAlt, 0x38, 0x09)]
    [InlineData(ScanCode.RightShift, 0x36, 0x08)]
    [InlineData(ScanCode.Up, 0x48, 0x09)]
    [InlineData(ScanCode.NumPad8, 0x48, 0x08)]
    [InlineData(ScanCode.Enter, 0x1C, 0x08)]
    [InlineData(ScanCode.NumPadEnter, 0x1C, 0x09)]
    public void KeysUseScanCodesAndCorrectExtensionFlags(ScanCode key, int code, uint flags)
    {
        var down = SendInputBackend.CreateKeyInput(key, false);
        var up = SendInputBackend.CreateKeyInput(key, true);
        Assert.Equal(1u, down.Type);
        Assert.Equal(0, down.Data.Keyboard.VirtualKey);
        Assert.Equal(code, down.Data.Keyboard.ScanCode);
        Assert.Equal(flags, down.Data.Keyboard.Flags);
        Assert.Equal(flags | 2u, up.Data.Keyboard.Flags);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0xE11D)]
    [InlineData(0xE000)]
    [InlineData(0x1011)]
    [InlineData(0x91)]
    public void InvalidOrE1CodesAreRejected(int key) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => SendInputBackend.CreateKeyInput((ScanCode)key, false));

    [Fact]
    public void UnicodeFlagsDoNotIncludeScanCodeOrExtendedFlags()
    {
        foreach (var character in "中🙂")
        {
            var down = SendInputBackend.CreateUnicodeInput(character, false).Data.Keyboard;
            var up = SendInputBackend.CreateUnicodeInput(character, true).Data.Keyboard;
            Assert.Equal(character, down.ScanCode);
            Assert.Equal(0, down.VirtualKey);
            Assert.Equal(4u, down.Flags);
            Assert.Equal(6u, up.Flags);
        }
    }

    [Theory]
    [InlineData(MouseButton.Left, 0x02u, 0x04u, 0u)]
    [InlineData(MouseButton.Right, 0x08u, 0x10u, 0u)]
    [InlineData(MouseButton.Middle, 0x20u, 0x40u, 0u)]
    [InlineData(MouseButton.XButton1, 0x80u, 0x100u, 1u)]
    [InlineData(MouseButton.XButton2, 0x80u, 0x100u, 2u)]
    public void MouseButtonsHaveCorrectFlagsAndPayload(MouseButton button, uint down, uint up, uint data)
    {
        var input = SendInputBackend.CreateButtonInput(button, false);
        Assert.Equal(0u, input.Type);
        Assert.Equal(down, input.Data.Mouse.Flags);
        Assert.Equal(data, input.Data.Mouse.MouseData);
        Assert.Equal(up, SendInputBackend.CreateButtonInput(button, true).Data.Mouse.Flags);
    }

    [Fact]
    public void WheelPreservesNegativeSignedDelta()
    {
        var vertical = SendInputBackend.CreateWheelInput(-120, false).Data.Mouse;
        var horizontal = SendInputBackend.CreateWheelInput(240, true).Data.Mouse;
        Assert.Equal(-120, unchecked((int)vertical.MouseData));
        Assert.Equal(0x800u, vertical.Flags);
        Assert.Equal(240u, horizontal.MouseData);
        Assert.Equal(0x1000u, horizontal.Flags);
    }

    [Fact]
    public void MovementFlagsDistinguishRelativePrimaryAndVirtualDesktop()
    {
        var relative = SendInputBackend.CreateMoveInput(-15, 25, false, false).Data.Mouse;
        Assert.Equal((-15, 25, 0x2001u), (relative.X, relative.Y, relative.Flags));
        Assert.Equal(0xA001u, SendInputBackend.CreateMoveInput(0, 65535, true, false).Data.Mouse.Flags);
        Assert.Equal(0xE001u, SendInputBackend.CreateMoveInput(65535, 0, true, true).Data.Mouse.Flags);
        Assert.Throws<ArgumentOutOfRangeException>(() => SendInputBackend.CreateMoveInput(-1, 0, true, true));
        Assert.Throws<ArgumentException>(() => SendInputBackend.CreateMoveInput(0, 0, false, true));
    }

    [Theory]
    [InlineData(-1920, -1080)]
    [InlineData(-1919, -1079)]
    [InlineData(0, 0)]
    [InlineData(3839, 2159)]
    [InlineData(int.MinValue, int.MaxValue)]
    public void PixelNormalizationTargetsTheCorrectPixelAndClamps(int x, int y)
    {
        var bounds = new Rectangle(-1920, -1080, 5760, 3240);
        var point = WindowsDesktop.Normalize(new Point(x, y), bounds);
        Assert.InRange(point.X, 0, 65535);
        Assert.InRange(point.Y, 0, 65535);
        Assert.Equal(Math.Clamp(x, bounds.Left, bounds.Right - 1), (int)((long)point.X * bounds.Width / 65536) + bounds.Left);
        Assert.Equal(Math.Clamp(y, bounds.Top, bounds.Bottom - 1), (int)((long)point.Y * bounds.Height / 65536) + bounds.Top);
    }

    [Fact]
    public void DegenerateScreenBoundsAreHandled()
    {
        Assert.Equal(new Point(32768, 32768), WindowsDesktop.Normalize(Point.Empty, new Rectangle(0, 0, 1, 1)));
        Assert.Throws<InvalidOperationException>(() => WindowsDesktop.Normalize(Point.Empty, Rectangle.Empty));
    }
}

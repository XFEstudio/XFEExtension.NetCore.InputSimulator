using System.Drawing;
using System.Runtime.InteropServices;
using XFEExtension.NetCore.InputSimulator.Native;

namespace XFEExtension.NetCore.InputSimulator.Tests;

[TestFixture]
public class NativeEncodingTests
{
    [Test]
    public void DriverAbiIsPointerFreeAndStableOnBothArchitectures()
    {
        Assert.Equal(32, Marshal.SizeOf<DriverCommand>());
        Assert.Equal(16, Marshal.SizeOf<DriverInfo>());
        Assert.Equal(16, Marshal.OffsetOf<DriverCommand>("X").ToInt32());
        Assert.Equal(28, Marshal.OffsetOf<DriverCommand>("Reserved").ToInt32());
        Assert.Equal(64, HidControlProtocol.ReportSize);
    }

    [TestCase(ScanCode.W, 0x1A)]
    [TestCase(ScanCode.RightControl, 0xE4)]
    [TestCase(ScanCode.RightAlt, 0xE6)]
    [TestCase(ScanCode.RightShift, 0xE5)]
    [TestCase(ScanCode.Up, 0x52)]
    [TestCase(ScanCode.NumPad8, 0x60)]
    [TestCase(ScanCode.Enter, 0x28)]
    [TestCase(ScanCode.NumPadEnter, 0x58)]
    [TestCase(ScanCode.Oem102, 0x64)]
    [TestCase(ScanCode.D0, 0x27)]
    [TestCase(ScanCode.F12, 0x45)]
    public void KeysMapToHidUsages(ScanCode key, int usage) => Assert.Equal(usage, HidKeyMapping.GetUsage(key));

    [Test]
    public void EveryNamedScanCodeMapsToADistinctHidUsage()
    {
        var codes = Enum.GetValues<ScanCode>();
        var usages = codes.Select(HidKeyMapping.GetUsage).ToArray();
        Assert.Equal(codes.Length, usages.Distinct().Count());
        Assert.True(usages.All(value => value is >= 4 and <= 0x73 or >= 0xE0 and <= 0xE7));
    }

    [TestCase(0)]
    [TestCase(0xE11D)]
    [TestCase(0xE000)]
    [TestCase(0x1011)]
    [TestCase(0xE020)]
    public void InvalidAndUnmappedCodesAreRejected(int key) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => HidKeyMapping.GetUsage((ScanCode)key));

    [TestCase(-1920, -1080)]
    [TestCase(-1919, -1079)]
    [TestCase(0, 0)]
    [TestCase(3839, 2159)]
    [TestCase(int.MinValue, int.MaxValue)]
    public void PixelNormalizationTargetsTheCorrectPixelAndClamps(int x, int y)
    {
        var bounds = new Rectangle(-1920, -1080, 5760, 3240);
        var point = WindowsDesktop.Normalize(new Point(x, y), bounds);
        Assert.InRange(point.X, 0, 65535);
        Assert.InRange(point.Y, 0, 65535);
        Assert.Equal(Math.Clamp(x, bounds.Left, bounds.Right - 1), (int)((long)point.X * bounds.Width / 65536) + bounds.Left);
        Assert.Equal(Math.Clamp(y, bounds.Top, bounds.Bottom - 1), (int)((long)point.Y * bounds.Height / 65536) + bounds.Top);
    }

    [Test]
    public void DegenerateScreenBoundsAreHandled()
    {
        Assert.Equal(new Point(32768, 32768), WindowsDesktop.Normalize(Point.Empty, new Rectangle(0, 0, 1, 1)));
        Assert.Throws<InvalidOperationException>(() => WindowsDesktop.Normalize(Point.Empty, Rectangle.Empty));
    }
}

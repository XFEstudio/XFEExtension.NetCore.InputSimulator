using System.Buffers.Binary;
using XFEExtension.NetCore.InputSimulator.Native;

namespace XFEExtension.NetCore.InputSimulator.Tests;

[TestFixture]
public class HidControlTests
{
    [Test]
    public void FeaturePacketMatchesNativeWireOffsetsAndSignedCoordinates()
    {
        var command = DriverCommand.Create(3);
        command.X = -32767;
        command.Y = 125;
        var bytes = HidControlProtocol.Encode(HidControlProtocol.Send, 0x0102030405060708, command);
        Assert.Equal(64, bytes.Length);
        Assert.Equal((byte)3, bytes[0]);
        Assert.Equal((byte)2, bytes[1]);
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4)));
        Assert.Equal((byte)8, bytes[8]);
        Assert.Equal((byte)1, bytes[15]);
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(16)));
        Assert.Equal(3u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(20)));
        Assert.Equal(-32767, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(32)));
        Assert.Equal(125, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(36)));
        Assert.True(bytes.AsSpan(48).IndexOfAnyExcept((byte)0) < 0);
    }

    [Test]
    public void InvalidSessionOrOperationCannotBeSent()
    {
        Assert.Throws<ArgumentException>(() => HidControlProtocol.Encode(HidControlProtocol.Acquire, 0));
        Assert.Throws<ArgumentException>(() => HidControlProtocol.Encode(0, 1));
        Assert.Throws<ArgumentException>(() => HidControlProtocol.Encode(6, 1));
    }

    [Test]
    public void StatusDecodesProtocolCapabilities()
    {
        var info = HidControlProtocol.DecodeInfo(Status());
        Assert.Equal((1u, 31u, 6u, 0u), (info.Version, info.Capabilities, info.MaxKeys, info.Reserved));
    }

    [Test]
    public void StatusRejectsTruncationAndUnexpectedDeviceFormat()
    {
        var bytes = Status();
        Assert.Throws<InvalidDataException>(() => HidControlProtocol.DecodeInfo(bytes.AsSpan(0, 63)));
        foreach (var offset in new[] { 0, 1, 4, 24, 28, 40, 63 })
        {
            var corrupt = Status();
            corrupt[offset] ^= 0xFF;
            Assert.Throws<InvalidDataException>(() => HidControlProtocol.DecodeInfo(corrupt));
        }
    }

    [Test]
    public void StatusRejectsImpossibleQueueAndLeaseState()
    {
        var bytes = Status();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(32), 65);
        Assert.Throws<InvalidDataException>(() => HidControlProtocol.DecodeInfo(bytes));
        bytes = Status();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(36), 2);
        Assert.Throws<InvalidDataException>(() => HidControlProtocol.DecodeInfo(bytes));
    }

    private static byte[] Status()
    {
        var bytes = new byte[64];
        bytes[0] = 3;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 0x55454658);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), 31);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), 6);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(24), 3000);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(28), 64);
        return bytes;
    }
}

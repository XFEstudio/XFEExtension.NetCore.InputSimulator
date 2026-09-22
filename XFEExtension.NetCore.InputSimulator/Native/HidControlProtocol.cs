using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace XFEExtension.NetCore.InputSimulator.Native;

internal static class HidControlProtocol
{
    internal const int ReportSize = 64;
    internal const byte ReportId = 3, Acquire = 1, Send = 2, Reset = 3, Heartbeat = 4, Release = 5;
    internal const uint Magic = 0x55454658, LeaseTimeoutMilliseconds = 3000, QueueCapacity = 64;

    internal static byte[] Encode(byte operation, ulong session, DriverCommand command = default)
    {
        if (operation is < Acquire or > Release || session == 0) throw new ArgumentException("无效的 HID 会话指令。");
        var bytes = new byte[ReportSize];
        bytes[0] = ReportId;
        bytes[1] = operation;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), DriverProtocol.Version);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(8), session);
        MemoryMarshal.Write(bytes.AsSpan(16), in command);
        return bytes;
    }

    internal static DriverInfo DecodeInfo(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != ReportSize || bytes[0] != ReportId || bytes[1..4].ContainsAnyExcept((byte)0) ||
            BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]) != Magic ||
            BinaryPrimitives.ReadUInt32LittleEndian(bytes[24..]) != LeaseTimeoutMilliseconds ||
            BinaryPrimitives.ReadUInt32LittleEndian(bytes[28..]) != QueueCapacity ||
            BinaryPrimitives.ReadUInt32LittleEndian(bytes[32..]) > QueueCapacity ||
            BinaryPrimitives.ReadUInt32LittleEndian(bytes[36..]) > 1 || bytes[40..].ContainsAnyExcept((byte)0))
            throw new InvalidDataException("HID 驱动状态报告格式不匹配。");
        return MemoryMarshal.Read<DriverInfo>(bytes[8..]);
    }
}

using System.Runtime.InteropServices;

namespace XFEExtension.NetCore.InputSimulator.Native;

internal static class DriverProtocol
{
    internal const uint Version = 1;
    internal const uint Capabilities = 31;
}

// Matches driver/Protocol.h on both x86 and x64. No pointers cross this boundary.
[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct DriverCommand
{
    internal uint Version, Kind, Code, Flags;
    internal int X, Y, Delta;
    internal uint Reserved;

    internal static DriverCommand Create(uint kind) => new() { Version = DriverProtocol.Version, Kind = kind };
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct DriverInfo
{
    internal uint Version, Capabilities, MaxKeys, Reserved;
}

internal interface IDriverTransport : IDisposable
{
    DriverInfo Query();
    void Send(DriverCommand command);
    void Reset();
}

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

namespace XFEExtension.NetCore.InputSimulator.Native;

internal sealed unsafe partial class DriverTransport : IDriverTransport
{
    private readonly SafeFileHandle handle;
    private readonly object sync = new();
    private readonly ulong session;
    private readonly Timer heartbeat;
    private Exception? failure;
    private bool disposed;

    internal DriverTransport()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) || RuntimeInformation.OSArchitecture != Architecture.X64)
            throw new PlatformNotSupportedException("内置 UMDF HID 驱动支持 Windows 11 x64。");
        var opened = FindDevice();
        if (opened is null)
        {
            DriverDeployment.InstallEmbeddedDriver();
            for (var attempt = 0; attempt < 40 && opened is null; attempt++)
            {
                Thread.Sleep(250);
                opened = FindDevice();
            }
        }
        handle = opened ?? throw new IOException("驱动已安装，但 HID 控制设备尚未启动。请查看设备管理器中的 XFE UMDF 设备状态。");
        try
        {
            var info = ReadInfo();
            if (info.Version != DriverProtocol.Version || info.Capabilities != DriverProtocol.Capabilities || info.MaxKeys != 6 || info.Reserved != 0)
                throw new NotSupportedException("已安装的 XFE UMDF 驱动与本 DLL 协议不匹配。");
            do { session = BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(sizeof(ulong))); } while (session == 0);
            WriteControl(HidControlProtocol.Acquire);
            heartbeat = new Timer(KeepAlive, null, 500, 500);
        }
        catch { handle.Dispose(); throw; }
    }

    public DriverInfo Query() { lock (sync) { CheckState(); return ReadInfo(); } }
    public void Send(DriverCommand command) { lock (sync) { CheckState(); WriteControl(HidControlProtocol.Send, command); } }
    public void Reset() { lock (sync) { CheckState(); WriteControl(HidControlProtocol.Reset); } }

    private DriverInfo ReadInfo()
    {
        var bytes = new byte[HidControlProtocol.ReportSize];
        bytes[0] = HidControlProtocol.ReportId;
        fixed (byte* buffer = bytes)
            if (!HidD_GetFeature(handle, buffer, (uint)bytes.Length)) throw Error("读取驱动状态");
        return HidControlProtocol.DecodeInfo(bytes);
    }

    private void WriteControl(byte operation, DriverCommand command = default)
    {
        var bytes = HidControlProtocol.Encode(operation, session, command);
        fixed (byte* buffer = bytes)
            if (!HidD_SetFeature(handle, buffer, (uint)bytes.Length))
                throw Error(operation == HidControlProtocol.Acquire ? "申请输入会话（可能已有另一个控制器）" : "发送 HID 指令");
    }

    private void KeepAlive(object? state)
    {
        lock (sync)
        {
            if (disposed || failure is not null) return;
            try { WriteControl(HidControlProtocol.Heartbeat); }
            catch (Exception error) { failure = error; }
        }
    }

    private void CheckState()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (failure is not null) throw new IOException("HID 会话已失效；请释放控制器并重新连接。驱动在失联后会尝试松开输入。", failure);
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed) return;
            disposed = true;
            heartbeat.Dispose();
            try { if (failure is null) WriteControl(HidControlProtocol.Release); }
            catch (Win32Exception) { }
            finally { handle.Dispose(); }
        }
    }

    private static Win32Exception Error(string action)
    {
        var code = Marshal.GetLastPInvokeError();
        return new Win32Exception(code, $"{action}失败（Win32 {code}）。请检查设备状态、会话占用及六键并发限制。");
    }

    private static SafeFileHandle? FindDevice()
    {
        HidD_GetHidGuid(out var guid);
        var devices = SetupDiGetClassDevsW(in guid, null, 0, 0x12);
        if (devices == -1) throw Error("枚举 HID 设备");
        try
        {
            var item = new DeviceInterface { Size = (uint)sizeof(DeviceInterface) };
            for (uint index = 0; ; index++)
            {
                if (!SetupDiEnumDeviceInterfaces(devices, 0, in guid, index, ref item))
                {
                    if (Marshal.GetLastPInvokeError() == 259) return null;
                    throw Error("枚举 HID 接口");
                }
                SetupDiGetDeviceInterfaceDetailW(devices, ref item, null, 0, out var length, 0);
                if (length < 6 || length > 65536) continue;
                var detail = (byte*)NativeMemory.AllocZeroed(length);
                try
                {
                    *(uint*)detail = Environment.Is64BitProcess ? 8u : 6u;
                    if (!SetupDiGetDeviceInterfaceDetailW(devices, ref item, detail, length, out _, 0)) throw Error("读取 HID 接口路径");
                    var path = Marshal.PtrToStringUni((nint)(detail + 4))!;
                    using var probe = CreateFileW(path, 0, 3, 0, 3, 0, 0);
                    if (probe.IsInvalid) continue;
                    var attributes = new HidAttributes { Size = (uint)sizeof(HidAttributes) };
                    if (!HidD_GetAttributes(probe, ref attributes) || attributes.Vendor != 0xFEFE || attributes.Product != 0x0003) continue;
                    if (!HidD_GetPreparsedData(probe, out var preparsed)) continue;
                    HidCapabilities caps;
                    int status;
                    try { status = HidP_GetCaps(preparsed, out caps); }
                    finally { HidD_FreePreparsedData(preparsed); }
                    if (status != 0x00110000 || caps.UsagePage != 0xFF00 || caps.Usage != 1 || caps.FeatureBytes != HidControlProtocol.ReportSize) continue;
                    var opened = CreateFileW(path, 0xC0000000, 3, 0, 3, 0, 0);
                    if (!opened.IsInvalid) return opened;
                    var error = Error("打开 HID 控制通道");
                    opened.Dispose();
                    throw error;
                }
                finally { NativeMemory.Free(detail); }
            }
        }
        finally { SetupDiDestroyDeviceInfoList(devices); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DeviceInterface { internal uint Size; internal Guid Interface; internal uint Flags; internal nint Reserved; }
    [StructLayout(LayoutKind.Sequential)]
    private struct HidAttributes { internal uint Size; internal ushort Vendor, Product, Version; }
    [StructLayout(LayoutKind.Sequential)]
    private struct HidCapabilities
    {
        internal ushort Usage, UsagePage, InputBytes, OutputBytes, FeatureBytes;
        internal fixed ushort Reserved[17];
        internal fixed ushort Counts[10];
    }

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial SafeFileHandle CreateFileW(string name, uint access, uint share, nint security, uint disposition, uint flags, nint template);
    [LibraryImport("hid.dll")]
    private static partial void HidD_GetHidGuid(out Guid guid);
    [LibraryImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)] private static partial bool HidD_GetAttributes(SafeFileHandle handle, ref HidAttributes attributes);
    [LibraryImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)] private static partial bool HidD_GetPreparsedData(SafeFileHandle handle, out nint data);
    [LibraryImport("hid.dll")]
    [return: MarshalAs(UnmanagedType.U1)] private static partial bool HidD_FreePreparsedData(nint data);
    [LibraryImport("hid.dll")]
    private static partial int HidP_GetCaps(nint data, out HidCapabilities caps);
    [LibraryImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)] private static partial bool HidD_GetFeature(SafeFileHandle handle, byte* bytes, uint length);
    [LibraryImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)] private static partial bool HidD_SetFeature(SafeFileHandle handle, byte* bytes, uint length);
    [LibraryImport("setupapi.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint SetupDiGetClassDevsW(in Guid guid, string? enumerator, nint parent, uint flags);
    [LibraryImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static partial bool SetupDiEnumDeviceInterfaces(nint devices, nint device, in Guid guid, uint index, ref DeviceInterface data);
    [LibraryImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static partial bool SetupDiGetDeviceInterfaceDetailW(nint devices, ref DeviceInterface data, byte* detail, uint length, out uint required, nint device);
    [LibraryImport("setupapi.dll")]
    [return: MarshalAs(UnmanagedType.Bool)] private static partial bool SetupDiDestroyDeviceInfoList(nint devices);
}

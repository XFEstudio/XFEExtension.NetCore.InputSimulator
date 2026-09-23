namespace XFEExtension.NetCore.InputSimulator;

/// <summary>驱动控制设备的检测状态；检测不会安装驱动或占用输入会话。</summary>
public enum DriverStatus
{
    /// <summary>未发现已启动的控制设备；驱动可能未安装、被禁用或尚未启动。</summary>
    NotDetected,
    /// <summary>控制设备可访问且协议兼容；实际连接时仍可能存在会话占用。</summary>
    Ready,
    /// <summary>设备协议或状态报告与本 DLL 不兼容。</summary>
    Incompatible,
    /// <summary>枚举或读取设备失败，具体原因见检测结果的 Message。</summary>
    Unavailable,
    /// <summary>系统不支持此驱动。</summary>
    UnsupportedPlatform
}

/// <summary>一次驱动检测的结果，不保证后续连接成功，也不代表已安装最新版本。</summary>
public sealed class DriverCheckResult
{
    internal DriverCheckResult(DriverStatus status, string message)
    {
        Status = status;
        Message = message;
    }

    /// <summary>设备状态。</summary>
    public DriverStatus Status { get; }
    /// <summary>状态说明或设备错误信息。</summary>
    public string Message { get; }
    /// <summary>控制设备可访问且协议兼容；不申请或检查独占输入会话。</summary>
    public bool IsAvailable => Status == DriverStatus.Ready;
}

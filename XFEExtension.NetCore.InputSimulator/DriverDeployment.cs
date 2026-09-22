using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace XFEExtension.NetCore.InputSimulator;

/// <summary>部署 DLL 内嵌的驱动包。使用方不需要 WDK、单独下载驱动或第三方输入环境。</summary>
public static class DriverDeployment
{
    private const string Prefix = "XFE.InputDriver.";
    private static readonly string[] Files = ["XfeInputDriver.dll", "XfeInput.inf", "XfeInput.cat", "XfeInputSetup.exe", "publisher.cer"];
    private static readonly object InstallLock = new();

    /// <summary>此 DLL 是否包含完整驱动负载。</summary>
    public static bool HasEmbeddedDriver
    {
        get
        {
            var assembly = Assembly.GetExecutingAssembly();
            if (!Files.Take(4).All(file => assembly.GetManifestResourceInfo(Prefix + file) is not null) ||
                assembly.GetManifestResourceInfo(Prefix + "payload.json") is null) return false;
            using var manifest = ReadManifest();
            return !manifest.RootElement.GetProperty("signed").GetBoolean() ||
                assembly.GetManifestResourceInfo(Prefix + "publisher.cer") is not null;
        }
    }

    /// <summary>打包时驱动目录文件和安装器是否通过签名校验；最终能否加载仍由 Windows 决定。</summary>
    public static bool IsEmbeddedDriverSigned
    {
        get
        {
            if (!HasEmbeddedDriver) return false;
            using var manifest = ReadManifest();
            return manifest.RootElement.GetProperty("signed").GetBoolean();
        }
    }

    /// <summary>内嵌签名类型：unsigned、self-signed 或 trusted-publisher。自签名包需用户信任签名者。</summary>
    public static string EmbeddedDriverSigningKind
    {
        get
        {
            if (!HasEmbeddedDriver) return "unsigned";
            using var manifest = ReadManifest();
            return manifest.RootElement.GetProperty("signingKind").GetString()!;
        }
    }

    /// <summary>发布者证书到期时间；未签名时为空。安装器会再次检查证书有效期。</summary>
    public static DateTimeOffset? EmbeddedDriverCertificateExpires
    {
        get
        {
            if (!IsEmbeddedDriverSigned) return null;
            using var manifest = ReadManifest();
            return manifest.RootElement.GetProperty("certificateExpires").GetDateTimeOffset();
        }
    }

    /// <summary>打包时所有签名负载是否带有通过校验的时间戳；不表示包可永久安装。</summary>
    public static bool IsEmbeddedDriverTimestamped
    {
        get
        {
            if (!IsEmbeddedDriverSigned) return false;
            using var manifest = ReadManifest();
            return manifest.RootElement.TryGetProperty("timestamped", out var value) && value.GetBoolean();
        }
    }

    /// <summary>安装内嵌驱动。首次需要 UAC 管理员授权；不会修改启动安全策略或自动重启。</summary>
    /// <remarks>默认控制器在设备不存在时自动调用。可在应用安装阶段提前调用，避免首次输入时弹出授权。</remarks>
    public static void InstallEmbeddedDriver()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) || RuntimeInformation.OSArchitecture != Architecture.X64)
            throw new PlatformNotSupportedException("驱动安装支持 Windows 11 x64 系统。");
        lock (InstallLock)
        {
            if (!HasEmbeddedDriver) throw new InvalidOperationException("此 DLL 未内嵌驱动包；请使用完成驱动打包的发行版。");
            using var manifest = ReadManifest();
            var root = manifest.RootElement;
            if (root.GetProperty("protocol").GetUInt32() != Native.DriverProtocol.Version)
                throw new InvalidDataException("内嵌驱动包协议版本不匹配。");
            if (!root.GetProperty("signed").GetBoolean())
                throw new InvalidOperationException("此 DLL 属于未签名开发版，不能自动安装。请由发布者签名 UMDF 驱动包并重新打包；调用方无需申请证书或安装 WDK。");
            if (root.GetProperty("certificateExpires").GetDateTimeOffset() <= DateTimeOffset.UtcNow)
                throw new InvalidOperationException("内嵌驱动的签名证书已过期，请向发布者获取使用有效证书签名的新版本。");

            var directory = Path.Combine(Path.GetTempPath(), "XfeInput-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var cleanup = true;
            try
            {
                foreach (var name in Files)
                {
                    using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(Prefix + name)
                        ?? throw new InvalidDataException($"驱动资源缺失：{name}");
                    using var contents = new MemoryStream();
                    resource.CopyTo(contents);
                    var data = contents.ToArray();
                    var expected = root.GetProperty("files").GetProperty(name).GetString();
                    if (!Convert.ToHexString(SHA256.HashData(data)).Equals(expected, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException($"内嵌驱动资源校验失败：{name}");
                    File.WriteAllBytes(Path.Combine(directory, name), data);
                }
                var start = new ProcessStartInfo(Path.Combine(directory, "XfeInputSetup.exe"))
                {
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = directory
                };
                start.ArgumentList.Add("install");
                start.ArgumentList.Add(Path.Combine(directory, "XfeInput.inf"));
                start.ArgumentList.Add(Path.Combine(directory, "publisher.cer"));
                start.ArgumentList.Add(root.GetProperty("files").GetProperty("publisher.cer").GetString()!);
                using var installer = Process.Start(start) ?? throw new IOException("无法启动驱动安装器。");
                if (!installer.WaitForExit(120000))
                {
                    cleanup = false; // An active Windows installer may still need these files.
                    throw new TimeoutException($"驱动安装尚未结束；请等待安装器完成。临时文件保留于 {directory}");
                }
                if (installer.ExitCode == 3010) throw new InvalidOperationException("驱动已安装，Windows 要求重启后才能使用；未自动重启。");
                if (installer.ExitCode != 0) throw new Win32Exception(installer.ExitCode, $"Windows 驱动安装失败（{installer.ExitCode}），请检查签名和系统安装日志。");
            }
            finally
            {
                if (cleanup)
                {
                    // Delete only files extracted by this invocation; never recursively clean user paths.
                    foreach (var name in Files)
                        try { File.Delete(Path.Combine(directory, name)); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                    try { Directory.Delete(directory); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                }
            }
        }
    }

    private static JsonDocument ReadManifest()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(Prefix + "payload.json")
            ?? throw new InvalidDataException("内嵌驱动清单缺失。");
        return JsonDocument.Parse(stream);
    }
}

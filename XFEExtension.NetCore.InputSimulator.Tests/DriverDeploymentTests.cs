using System.ComponentModel;
using XFEExtension.NetCore.InputSimulator.Native;

namespace XFEExtension.NetCore.InputSimulator.Tests;

[TestFixture]
public class DriverDeploymentTests
{
    [Test]
    public void MissingDeviceReturnsDiagnosticWithoutRetryingOrInstalling()
    {
        var queries = 0;
        var result = DriverDeployment.CheckDriver(() => { queries++; return null; });
        Assert.Equal(DriverStatus.NotDetected, result.Status);
        Assert.False(result.IsAvailable);
        Assert.True(result.Message.Contains(nameof(DriverDeployment.InstallEmbeddedDriver)));
        Assert.Equal(1, queries);
    }

    [Test]
    public void CompatibleDeviceIsAvailable()
    {
        var result = DriverDeployment.CheckDriver(() => new DriverInfo { Version = 1, Capabilities = 31, MaxKeys = 6 });
        Assert.Equal(DriverStatus.Ready, result.Status);
        Assert.True(result.IsAvailable);
    }

    [TestCase(99, 31, 6, 0)]
    [TestCase(1, 0, 6, 0)]
    [TestCase(1, 31, 5, 0)]
    [TestCase(1, 31, 6, 1)]
    public void IncompatibleDeviceIsNotReportedAsMissing(int version, int capabilities, int maxKeys, int reserved)
    {
        var result = DriverDeployment.CheckDriver(() => new DriverInfo
        {
            Version = (uint)version, Capabilities = (uint)capabilities, MaxKeys = (uint)maxKeys, Reserved = (uint)reserved
        });
        Assert.Equal(DriverStatus.Incompatible, result.Status);
        Assert.False(result.IsAvailable);
    }

    [Test]
    public void MalformedReportIsIncompatible()
    {
        var result = DriverDeployment.CheckDriver(() => throw new InvalidDataException("invalid HID report"));
        Assert.Equal(DriverStatus.Incompatible, result.Status);
        Assert.False(result.IsAvailable);
        Assert.Equal("invalid HID report", result.Message);
    }

    [Test]
    public void DeviceAccessFailureIsNotReportedAsMissing()
    {
        foreach (var error in new Exception[]
        {
            new Win32Exception(5, "access denied"), new IOException("device disconnected"),
            new UnauthorizedAccessException("device policy")
        })
        {
            var result = DriverDeployment.CheckDriver(() => throw error);
            Assert.Equal(DriverStatus.Unavailable, result.Status);
            Assert.False(result.IsAvailable);
            Assert.Equal(error.Message, result.Message);
        }
    }

    [Test]
    public void UnsupportedPlatformReturnsDiagnostic()
    {
        var result = DriverDeployment.CheckDriver(() => throw new PlatformNotSupportedException("unsupported OS"));
        Assert.Equal(DriverStatus.UnsupportedPlatform, result.Status);
        Assert.False(result.IsAvailable);
    }

    [Test]
    public void MissingDeviceConnectionFailsBeforeAnyNativeSessionOrInstallation()
    {
        var attempts = 0;
        var error = Assert.Throws<InvalidOperationException>(() => new DriverTransport(() => { attempts++; return null; }));
        Assert.True(error.Message.Contains(nameof(DriverDeployment.InstallEmbeddedDriver)));
        Assert.Equal(1, attempts);
    }

    [Test]
    public void ConnectionAccessFailureIsPreservedWithoutRetryingOrInstalling()
    {
        var attempts = 0;
        var error = Assert.Throws<Win32Exception>(() => new DriverTransport(() =>
        {
            attempts++;
            throw new Win32Exception(5, "access denied");
        }));
        Assert.Equal(5, error.NativeErrorCode);
        Assert.Equal(1, attempts);
    }
}

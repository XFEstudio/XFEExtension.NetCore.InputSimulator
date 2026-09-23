using System.Security.Cryptography;
using System.Text.Json;
using XFEExtension.NetCore.InputSimulator.Native;

namespace XFEExtension.NetCore.InputSimulator.Tests;

[TestFixture]
public class DriverBackendTests
{
    [Test]
    public void KeyButtonAndMovementUseTheDriverProtocol()
    {
        var channel = new RecordingDriver();
        using var driver = new DriverBackend(channel);
        driver.SendKey(ScanCode.RightControl, false);
        driver.SendKey(ScanCode.RightControl, true);
        driver.SendMouseButton(MouseButton.XButton2, false);
        driver.SendMouseButton(MouseButton.XButton2, true);
        driver.MoveMouse(-70000, 32768, false, false);
        Assert.Equal(7, channel.Commands.Count);
        Assert.Equal((1u, 0xE4u, 0u), (channel.Commands[0].Kind, channel.Commands[0].Code, channel.Commands[0].Flags));
        Assert.Equal(1u, channel.Commands[1].Flags);
        Assert.Equal((2u, 4u, 0u), (channel.Commands[2].Kind, channel.Commands[2].Code, channel.Commands[2].Flags));
        Assert.Equal(1u, channel.Commands[3].Flags);
        Assert.Equal(-70000, channel.Commands.Skip(4).Sum(command => command.X));
        Assert.Equal(32768, channel.Commands.Skip(4).Sum(command => command.Y));
        Assert.True(channel.Commands.Skip(4).All(command => command.Kind == 3 && Math.Abs(command.X) <= 32767 && Math.Abs(command.Y) <= 32767));
        Assert.True(channel.Commands.All(command => command.Version == 1 && command.Reserved == 0));
    }

    [Test]
    public void WheelSplitsLargeNotchCountsWithoutLosingSignOrOrientation()
    {
        var channel = new RecordingDriver();
        using var driver = new DriverBackend(channel);
        driver.ScrollMouse(-120 * 300, false);
        driver.ScrollMouse(120 * 128, true);
        Assert.Equal(5, channel.Commands.Count);
        Assert.Equal(-300, channel.Commands.Where(command => command.Flags == 0).Sum(command => command.Delta));
        Assert.Equal(128, channel.Commands.Where(command => command.Flags == 1).Sum(command => command.Delta));
        Assert.True(channel.Commands.All(command => command.Kind == 4 && Math.Abs(command.Delta) <= 127));
        driver.ScrollMouse(0, false);
        Assert.Equal(5, channel.Commands.Count);
    }

    [Test]
    public void UnsupportedInputFailsWithoutSendingAnything()
    {
        var channel = new RecordingDriver();
        using var driver = new DriverBackend(channel);
        Assert.False(driver.SupportsUnicodeText);
        Assert.Throws<NotSupportedException>(() => driver.SendUnicode('中', false));
        Assert.Throws<NotSupportedException>(() => driver.MoveMouse(100, 100, true, false));
        Assert.Throws<ArgumentException>(() => driver.MoveMouse(1, 1, false, true));
        Assert.Throws<ArgumentOutOfRangeException>(() => driver.ScrollMouse(1, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => driver.SendKey((ScanCode)0xE020, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => driver.SendMouseButton((MouseButton)5, false));
        Assert.Empty(channel.Commands);
    }

    [Test]
    public async Task CancellingDriverCombinationReleasesKeysInReverseOrder()
    {
        var channel = new RecordingDriver();
        using var input = new InputController(new DriverBackend(channel));
        using var cancellation = new CancellationTokenSource();
        var task = input.PressCombinationAsync([ScanCode.LeftShift, ScanCode.W], 10000, cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => task);
        Assert.Equal<IEnumerable<uint>>(new uint[] { 0xE1, 0x1A, 0x1A, 0xE1 }, channel.Commands.Select(command => command.Code));
        Assert.Equal<IEnumerable<uint>>(new uint[] { 0, 0, 1, 1 }, channel.Commands.Select(command => command.Flags));
    }

    [Test]
    public void ProtocolMismatchDisposesTheConnection()
    {
        var channel = new RecordingDriver { Info = new() { Version = 99 } };
        Assert.Throws<NotSupportedException>(() => new DriverBackend(channel));
        Assert.True(channel.Disposed);
        Assert.Empty(channel.Commands);
    }

    [Test]
    public void FailedSendStopsSplitMovementAndStillAllowsCleanup()
    {
        var channel = new RecordingDriver { FailOnCommand = 2 };
        using (var driver = new DriverBackend(channel))
            Assert.Throws<IOException>(() => driver.MoveMouse(100000, 0, false, false));
        Assert.Equal(2, channel.Commands.Count);
        Assert.Equal(1, channel.Resets);
        Assert.True(channel.Disposed);
    }

    [Test]
    public void DisposalClosesHandleEvenWhenResetFails()
    {
        var channel = new RecordingDriver { FailReset = true };
        var driver = new DriverBackend(channel);
        Assert.Throws<IOException>(driver.Dispose);
        Assert.True(channel.Disposed);
        driver.Dispose();
        Assert.Equal(1, channel.Resets);
        Assert.Throws<ObjectDisposedException>(() => driver.SendKey(ScanCode.W, false));
    }

    [Test]
    public void PayloadIsEmbeddedInDllAndEveryResourceMatchesItsManifest()
    {
        Assert.True(DriverDeployment.HasEmbeddedDriver);
        var assembly = typeof(DriverDeployment).Assembly;
        using var stream = assembly.GetManifestResourceStream("XFE.InputDriver.payload.json")!;
        using var manifest = JsonDocument.Parse(stream);
        Assert.Equal(1, manifest.RootElement.GetProperty("protocol").GetInt32());
        Assert.Equal(DriverDeployment.IsEmbeddedDriverSigned ? 5 : 4, manifest.RootElement.GetProperty("files").EnumerateObject().Count());
        Assert.True(manifest.RootElement.GetProperty("files").TryGetProperty("XfeInputDriver.dll", out _));
        Assert.Equal(manifest.RootElement.GetProperty("timestamped").GetBoolean(), DriverDeployment.IsEmbeddedDriverTimestamped);
        if (DriverDeployment.IsEmbeddedDriverSigned)
        {
            using var certificateStream = assembly.GetManifestResourceStream("XFE.InputDriver.publisher.cer")!;
            using var bytes = new MemoryStream();
            certificateStream.CopyTo(bytes);
            using var certificate = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadCertificate(bytes.ToArray());
            Assert.False(certificate.HasPrivateKey);
            Assert.Equal(new DateTimeOffset(certificate.NotAfter.ToUniversalTime()), DriverDeployment.EmbeddedDriverCertificateExpires!.Value);
        }
        foreach (var file in manifest.RootElement.GetProperty("files").EnumerateObject())
        {
            using var content = assembly.GetManifestResourceStream("XFE.InputDriver." + file.Name)!;
            Assert.Equal(file.Value.GetString(), Convert.ToHexString(SHA256.HashData(content)));
        }
    }

    private sealed class RecordingDriver : IDriverTransport
    {
        internal DriverInfo Info = new() { Version = 1, Capabilities = 31, MaxKeys = 6 };
        internal List<DriverCommand> Commands { get; } = [];
        internal int Resets;
        internal bool Disposed, FailReset;
        internal int FailOnCommand;
        public DriverInfo Query() => Info;
        public void Send(DriverCommand command)
        {
            Commands.Add(command);
            if (Commands.Count == FailOnCommand) throw new IOException("test send failure");
        }
        public void Reset() { Resets++; if (FailReset) throw new IOException("test reset failure"); }
        public void Dispose() => Disposed = true;
    }
}

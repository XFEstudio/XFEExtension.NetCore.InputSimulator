namespace XFEExtension.NetCore.InputSimulator.Tests;

public class SequenceTests
{
    [Fact]
    public async Task SequenceReplaysInOrderWithoutMutatingChordArguments()
    {
        var backend = new RecordingBackend();
        using var input = new InputController(backend);
        ScanCode[] keys = [ScanCode.LeftControl, ScanCode.C];
        var sequence = new InputSequence().PressCombination(keys, 0).Wait(0).MouseMove(3, 5).MouseWheel(-120);
        keys[1] = ScanCode.V;
        await sequence.PlayAsync(input, repeat: 2);
        var expected = new[] { new SentInput("Key", (int)ScanCode.LeftControl), new SentInput("Key", (int)ScanCode.C),
            new SentInput("Key", (int)ScanCode.C, true), new SentInput("Key", (int)ScanCode.LeftControl, true),
            new SentInput("Move", X: 3, Y: 5), new SentInput("Wheel", -120) };
        Assert.Equal(expected.Concat(expected), backend.Events);
    }

    [Fact]
    public async Task CancelledSequenceReleasesItsOwnKeyAndPreservesManualHold()
    {
        var backend = new RecordingBackend();
        using var input = new InputController(backend);
        using var cancellation = new CancellationTokenSource();
        input.KeyDown(ScanCode.LeftShift);
        var sequence = new InputSequence().PressKey(ScanCode.W, 10000).MouseClick(MouseButton.Left);
        var task = sequence.PlayAsync(input, cancellationToken: cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.Equal(new[] { new SentInput("Key", (int)ScanCode.LeftShift), new SentInput("Key", (int)ScanCode.W),
            new SentInput("Key", (int)ScanCode.W, true) }, backend.Events);
        input.KeyUp(ScanCode.LeftShift);
    }

    [Fact]
    public async Task InvalidSequenceParametersFailBeforePlayback()
    {
        var backend = new RecordingBackend();
        using var input = new InputController(backend);
        var sequence = new InputSequence();
        Assert.Throws<ArgumentOutOfRangeException>(() => sequence.Wait(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => sequence.PressKey(ScanCode.W, -1));
        Assert.Throws<ArgumentException>(() => sequence.PressCombination([]));
        Assert.Throws<ArgumentOutOfRangeException>(() => sequence.MouseClick((MouseButton)99));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sequence.PlayAsync(input, 0));
        Assert.Equal(0, sequence.Count);
        Assert.Empty(backend.Events);
    }
}

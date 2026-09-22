namespace XFEExtension.NetCore.InputSimulator.Tests;

[TestFixture]
public class ControllerTests
{
    private static SentInput Key(ScanCode key, bool up = false) => new("Key", (int)key, up);

    [Test]
    public void CombinationPressesInOrderAndReleasesInReverse()
    {
        var backend = new RecordingBackend();
        using var input = new InputController(backend);
        input.PressCombination([ScanCode.LeftControl, ScanCode.LeftShift, ScanCode.S], 0);
        Assert.Equal<IEnumerable<SentInput>>(new[] { Key(ScanCode.LeftControl), Key(ScanCode.LeftShift), Key(ScanCode.S),
            Key(ScanCode.S, true), Key(ScanCode.LeftShift, true), Key(ScanCode.LeftControl, true) }, backend.Events);
    }

    [Test]
    public void InvalidCombinationIsRejectedBeforeAnyKeyIsPressed()
    {
        var backend = new RecordingBackend();
        using var input = new InputController(backend);
        Assert.IsType<ArgumentOutOfRangeException>(Assert.Throws<ArgumentOutOfRangeException>(() => input.PressCombination([ScanCode.LeftControl, (ScanCode)0], 0)));
        Assert.IsType<ArgumentException>(Assert.Throws<ArgumentException>(() => input.PressCombination([], 0)));
        Assert.IsType<ArgumentOutOfRangeException>(Assert.Throws<ArgumentOutOfRangeException>(() => input.PressKey(ScanCode.W, -1)));
        Assert.Empty(backend.Events);
    }

    [Test]
    public void LowerCaseCharDoesNotBecomeANumpadVirtualKey()
    {
        var backend = new RecordingBackend();
        using var input = new InputController(backend);
        input.PressKey('w', 0);
        input.PressKey('W', 0);
        Assert.Equal<IEnumerable<SentInput>>(new[] { Key(ScanCode.W), Key(ScanCode.W, true), Key(ScanCode.W), Key(ScanCode.W, true) }, backend.Events);
        Assert.IsType<ArgumentException>(Assert.Throws<ArgumentException>(() => input.PressKey('中', 0)));
    }

    [Test]
    public void RepeatedManualDownAndNestedHoldsPreserveOwnership()
    {
        var backend = new RecordingBackend();
        using var input = new InputController(backend);
        input.KeyDown(ScanCode.LeftControl);
        input.KeyDown(ScanCode.LeftControl);
        using (input.HoldKey(ScanCode.LeftControl))
        {
            input.PressCombination([ScanCode.LeftControl, ScanCode.C, ScanCode.C], 0);
        }
        Assert.Equal<IEnumerable<SentInput>>(new[] { Key(ScanCode.LeftControl), Key(ScanCode.C), Key(ScanCode.C, true) }, backend.Events);
        input.KeyUp(ScanCode.LeftControl);
        Assert.Equal(Key(ScanCode.LeftControl, true), backend.Events[^1]);
    }

    [Test]
    public void OverlappingHoldsReleaseOnlyWhenLastOwnerFinishes()
    {
        var backend = new RecordingBackend();
        using var input = new InputController(backend);
        var first = input.HoldKey(ScanCode.W);
        var second = input.HoldKey(ScanCode.W);
        first.Dispose();
        first.Dispose();
        Assert.Single(backend.Events);
        second.Dispose();
        Assert.Equal<IEnumerable<SentInput>>(new[] { Key(ScanCode.W), Key(ScanCode.W, true) }, backend.Events);
    }

    [Test]
    public void OldScopeCannotReleaseAKeyPressedAfterReleaseAll()
    {
        var backend = new RecordingBackend();
        using var input = new InputController(backend);
        var old = input.HoldKey(ScanCode.W);
        input.ReleaseAll();
        var current = input.HoldKey(ScanCode.W);
        old.Dispose();
        Assert.Equal(3, backend.Events.Count);
        current.Dispose();
        Assert.Equal(Key(ScanCode.W, true), backend.Events[^1]);
    }

    [Test]
    public async Task CancellingACombinationReleasesAllKeys()
    {
        var backend = new RecordingBackend();
        using var input = new InputController(backend);
        using var cancellation = new CancellationTokenSource();
        var task = input.PressCombinationAsync([ScanCode.LeftShift, ScanCode.W], 10000, cancellation.Token);
        Assert.Equal(2, backend.Events.Count);
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => task);
        Assert.Equal<IEnumerable<SentInput>>(new[] { Key(ScanCode.LeftShift), Key(ScanCode.W), Key(ScanCode.W, true), Key(ScanCode.LeftShift, true) }, backend.Events);
    }

    [Test]
    public async Task AlreadyCancelledAndInvalidDelaysNeverInject()
    {
        var backend = new RecordingBackend();
        using var input = new InputController(backend);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => input.PressKeyAsync(ScanCode.W, 1, cancellation.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(() => input.TypeTextAsync("abc", 0, cancellation.Token));
        Assert.IsType<ArgumentOutOfRangeException>(await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => input.PressKeyAsync(ScanCode.W, -1)));
        Assert.IsType<ArgumentOutOfRangeException>(await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => input.MouseClickAsync(MouseButton.Left, -1)));
        Assert.IsType<ArgumentOutOfRangeException>(await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => input.InputKeysAsync("abc", 0, -1)));
        Assert.Empty(backend.Events);
    }

    [Test]
    public void FailedDownInCombinationReleasesEarlierModifiers()
    {
        var backend = new RecordingBackend { BeforeSend = input =>
        {
            if (input == Key(ScanCode.S)) throw new IOException("Sending S failed");
        }};
        using var input = new InputController(backend);
        Assert.IsType<IOException>(Assert.Throws<IOException>(() => input.PressCombination([ScanCode.LeftControl, ScanCode.S], 0)));
        Assert.Equal<IEnumerable<SentInput>>(new[] { Key(ScanCode.LeftControl), Key(ScanCode.LeftControl, true) }, backend.Events);
    }

    [Test]
    public void FailedReleaseDoesNotSkipOtherModifiersAndCanBeRetried()
    {
        var backend = new RecordingBackend { BeforeSend = input =>
        {
            if (input == Key(ScanCode.S, true)) throw new IOException("Releasing S failed");
        }};
        using var input = new InputController(backend);
        Assert.IsType<AggregateException>(Assert.Throws<AggregateException>(() => input.PressCombination([ScanCode.LeftControl, ScanCode.S], 0)));
        Assert.Equal(Key(ScanCode.LeftControl, true), backend.Events[^1]);
        backend.BeforeSend = null;
        input.ReleaseAll();
        Assert.Equal(Key(ScanCode.S, true), backend.Events[^1]);
    }

    [Test]
    public void ReleaseAllContinuesAfterFailureAndRetainsOnlyFailedKeys()
    {
        var backend = new RecordingBackend();
        using var input = new InputController(backend);
        input.KeyDown(ScanCode.LeftShift);
        input.KeyDown(ScanCode.W);
        input.MouseDown(MouseButton.Left);
        backend.BeforeSend = sent => { if (sent == Key(ScanCode.W, true)) throw new IOException(); };
        Assert.IsType<AggregateException>(Assert.Throws<AggregateException>(() => input.ReleaseAll()));
        Assert.Equal<IEnumerable<SentInput>>(new[] { new SentInput("Button", (int)MouseButton.Left, true), Key(ScanCode.LeftShift, true) }, backend.Events.Skip(3));
        backend.BeforeSend = null;
        input.ReleaseAll();
        Assert.Equal(Key(ScanCode.W, true), backend.Events[^1]);
        var count = backend.Events.Count;
        input.ReleaseAll();
        Assert.Equal(count, backend.Events.Count);
    }

    [Test]
    public async Task CancellingMouseClickReleasesSideButton()
    {
        var backend = new RecordingBackend();
        using var input = new InputController(backend);
        using var cancellation = new CancellationTokenSource();
        var task = input.MouseClickAsync(MouseButton.XButton2, 10000, cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => task);
        Assert.Equal<IEnumerable<SentInput>>(new[] { new SentInput("Button", 4), new SentInput("Button", 4, true) }, backend.Events);
    }

    [Test]
    public void UnicodePreservesCaseChineseAndSurrogatePairs()
    {
        var backend = new RecordingBackend();
        using var input = new InputController(backend);
        const string text = "aA中🙂";
        input.TypeText(text);
        Assert.Equal<IEnumerable<SentInput>>(text.SelectMany(c => new[] { new SentInput("Text", c), new SentInput("Text", c, true) }), backend.Events);
    }

    [Test]
    public async Task CancellationDoesNotSplitASurrogatePair()
    {
        using var cancellation = new CancellationTokenSource();
        var backend = new RecordingBackend { BeforeSend = sent =>
        {
            if (sent.Kind == "Text" && char.IsHighSurrogate((char)sent.Code) && sent.Up) cancellation.Cancel();
        }};
        using var input = new InputController(backend);
        await Assert.ThrowsAsync<OperationCanceledException>(() => input.TypeTextAsync("🙂x", 1, cancellation.Token));
        Assert.Equal<IEnumerable<SentInput>>("🙂".SelectMany(c => new[] { new SentInput("Text", c), new SentInput("Text", c, true) }), backend.Events);
    }

    [Test]
    public void UnsupportedTextDoesNotFallBackToAnotherBackend()
    {
        var backend = new RecordingBackend { SupportsUnicodeText = false };
        using var input = new InputController(backend);
        Assert.IsType<NotSupportedException>(Assert.Throws<NotSupportedException>(() => input.TypeText("test")));
        Assert.Empty(backend.Events);
    }

    [Test]
    public void DisposeReleasesInReverseAndInvalidatesOutstandingScopes()
    {
        var backend = new RecordingBackend();
        var input = new InputController(backend);
        var scope = input.HoldKey(ScanCode.W);
        input.MouseDown(MouseButton.Right);
        input.Dispose();
        scope.Dispose();
        input.Dispose();
        Assert.Equal<IEnumerable<SentInput>>(new[] { Key(ScanCode.W), new SentInput("Button", 1), new SentInput("Button", 1, true), Key(ScanCode.W, true) }, backend.Events);
        Assert.True(backend.Disposed);
        Assert.IsType<ObjectDisposedException>(Assert.Throws<ObjectDisposedException>(() => input.KeyDown(ScanCode.W)));
        Assert.IsType<ObjectDisposedException>(Assert.Throws<ObjectDisposedException>(() => input.TypeText("")));
    }

    [Test]
    public void DisposeStillClosesBackendWhenReleaseFails()
    {
        var backend = new RecordingBackend();
        var input = new InputController(backend);
        var scope = input.HoldKey(ScanCode.W);
        backend.BeforeSend = sent => { if (sent.Up) throw new IOException(); };
        Assert.IsType<AggregateException>(Assert.Throws<AggregateException>(() => input.Dispose()));
        Assert.True(backend.Disposed);
        scope.Dispose();
    }

    [Test]
    public void CustomBackendReceivesMouseCoordinatesAndSignedWheelDeltas()
    {
        var backend = new RecordingBackend();
        using var input = new InputController(backend);
        input.MouseMove(-20, 40);
        input.LocateToNormalized(65535, 0);
        input.MouseWheelRoll(-120);
        input.MouseHorizontalWheelRoll(240);
        Assert.Equal<IEnumerable<SentInput>>(new[] { new SentInput("Move", X: -20, Y: 40),
            new SentInput("Move", X: 65535, Y: 0, Absolute: true, VirtualDesktop: true),
            new SentInput("Wheel", -120), new SentInput("HWheel", 240) }, backend.Events);
    }

    [Test]
    public async Task CancellationAndReleaseFailureAreBothReported()
    {
        var backend = new RecordingBackend();
        using var input = new InputController(backend);
        using var cancellation = new CancellationTokenSource();
        var task = input.PressKeyAsync(ScanCode.W, 10000, cancellation.Token);
        backend.BeforeSend = sent => { if (sent.Up) throw new IOException("release failed"); };
        cancellation.Cancel();
        var error = Assert.IsType<AggregateException>(await Assert.ThrowsAsync<AggregateException>(() => task));
        var failures = error.Flatten().InnerExceptions;
        Assert.True(failures.Any(e => e is OperationCanceledException), "Expected an exception of type OperationCanceledException.");
        Assert.True(failures.Any(e => e is IOException), "Expected an exception of type IOException.");
        backend.BeforeSend = null;
        input.ReleaseAll();
        Assert.Equal(new SentInput("Key", (int)ScanCode.W, true), backend.Events[^1]);
    }

    [Test]
    public void DisposeReportsBothReleaseAndBackendErrors()
    {
        var backend = new RecordingBackend { OnDispose = () => throw new InvalidOperationException("backend cleanup failed") };
        var input = new InputController(backend);
        input.KeyDown(ScanCode.W);
        backend.BeforeSend = sent => { if (sent.Up) throw new IOException("release failed"); };
        var error = Assert.IsType<AggregateException>(Assert.Throws<AggregateException>(() => input.Dispose()));
        var failures = error.Flatten().InnerExceptions;
        Assert.True(failures.Any(e => e is IOException), "Expected an exception of type IOException.");
        Assert.True(failures.Any(e => e is InvalidOperationException), "Expected an exception of type InvalidOperationException.");
        Assert.True(backend.Disposed);
        input.Dispose();
    }
}

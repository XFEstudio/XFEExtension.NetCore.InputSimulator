namespace XFEExtension.NetCore.InputSimulator.Tests;

internal sealed record SentInput(string Kind, int Code = 0, bool Up = false, int X = 0, int Y = 0,
    bool Absolute = false, bool VirtualDesktop = false);

internal sealed class RecordingBackend : IInputBackend
{
    public string Name => "Recording";
    public bool SupportsUnicodeText { get; set; } = true;
    internal List<SentInput> Events { get; } = [];
    internal List<SentInput> Attempts { get; } = [];
    internal Action<SentInput>? BeforeSend { get; set; }
    internal bool Disposed { get; private set; }
    internal Action? OnDispose { get; set; }

    public void SendKey(ScanCode key, bool isKeyUp) => Record(new("Key", (int)key, isKeyUp));
    public void SendUnicode(char codeUnit, bool isKeyUp) => Record(new("Text", codeUnit, isKeyUp));
    public void MoveMouse(int x, int y, bool absolute, bool virtualDesktop) =>
        Record(new("Move", X: x, Y: y, Absolute: absolute, VirtualDesktop: virtualDesktop));
    public void SendMouseButton(MouseButton button, bool isKeyUp) => Record(new("Button", (int)button, isKeyUp));
    public void ScrollMouse(int delta, bool horizontal) => Record(new(horizontal ? "HWheel" : "Wheel", delta));
    public void Dispose()
    {
        Disposed = true;
        OnDispose?.Invoke();
    }

    private void Record(SentInput input)
    {
        ObjectDisposedException.ThrowIf(Disposed, this);
        Attempts.Add(input);
        BeforeSend?.Invoke(input);
        Events.Add(input);
    }
}

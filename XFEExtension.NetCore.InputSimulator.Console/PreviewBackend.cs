namespace XFEExtension.NetCore.InputSimulator.ConsoleApp;

// 预演只记录事件，不调用 Win32 输入 API。
internal sealed class PreviewBackend : IInputBackend
{
    public string Name => "预演（不发送输入）";
    public bool SupportsUnicodeText => true;

    public void SendKey(ScanCode key, bool isKeyUp) =>
        Console.WriteLine($"  [预演] 键盘 {key} {(isKeyUp ? "松开" : "按下")}");

    public void SendUnicode(char codeUnit, bool isKeyUp) =>
        Console.WriteLine($"  [预演] Unicode U+{(int)codeUnit:X4} {(isKeyUp ? "松开" : "按下")}");

    public void MoveMouse(int x, int y, bool absolute, bool virtualDesktop) =>
        Console.WriteLine($"  [预演] 鼠标 {(absolute ? "绝对" : "相对")}移动 ({x}, {y})");

    public void SendMouseButton(MouseButton button, bool isKeyUp) =>
        Console.WriteLine($"  [预演] 鼠标 {button} {(isKeyUp ? "松开" : "按下")}");

    public void ScrollMouse(int delta, bool horizontal) =>
        Console.WriteLine($"  [预演] {(horizontal ? "水平" : "垂直")}滚轮 {delta}");

    public void Dispose() { }
}

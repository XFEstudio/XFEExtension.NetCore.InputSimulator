namespace XFEExtension.NetCore.InputSimulator;

/// <summary>可替换的输入发送后端。实现必须同步发送，失败时抛出异常。</summary>
/// <remarks>由 InputController 串行调用；不要在多个控制器之间共享同一后端实例。</remarks>
public interface IInputBackend : IDisposable
{
    /// <summary>后端名称。</summary>
    string Name { get; }
    /// <summary>是否支持 Unicode 文本事件。</summary>
    bool SupportsUnicodeText { get; }
    /// <summary>发送物理按键。isKeyUp 为 true 时释放。</summary>
    void SendKey(ScanCode key, bool isKeyUp);
    /// <summary>发送一个 UTF-16 码元的按下或释放事件；不支持时抛出 NotSupportedException。</summary>
    void SendUnicode(char codeUnit, bool isKeyUp);
    /// <summary>移动鼠标。绝对模式下 x/y 为 0..65535，virtualDesktop 决定映射至整个桌面或主屏。</summary>
    void MoveMouse(int x, int y, bool absolute, bool virtualDesktop);
    /// <summary>发送鼠标按键事件。</summary>
    void SendMouseButton(MouseButton button, bool isKeyUp);
    /// <summary>滚动鼠标；120 为一格，水平正值向右，垂直正值向上。</summary>
    void ScrollMouse(int delta, bool horizontal);
}

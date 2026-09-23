using System.Drawing;
using XFEExtension.NetCore.InputSimulator.Native;

namespace XFEExtension.NetCore.InputSimulator;

/// <summary>使用共享的 UMDF HID 驱动控制器模拟输入；驱动需显式安装。需要独立生命周期时请创建 InputController。</summary>
public static class InputSimulator
{
    private static readonly object SharedSync = new();
    private static InputController? sharedController;
    private static InputController Controller
    {
        // A failed connection can be retried after the caller explicitly installs the driver.
        get { lock (SharedSync) return sharedController ??= new InputController(); }
    }

    /// <summary>当前后端的名称。</summary>
    public static string BackendName => "XFE UMDF HID driver";
    /// <summary>按住字符对应的物理键，大小写字母对应同一按键。</summary>
    public static void KeyDown(char key) => Controller.KeyDown(key);
    /// <summary>释放字符对应的物理键。</summary>
    public static void KeyUp(char key) => Controller.KeyUp(key);
    /// <summary>按下字符对应的物理键，保持 30ms 后释放。</summary>
    public static void PressKey(char key) => Controller.PressKey(key);
    /// <summary>异步按键，holdTime 单位为毫秒。</summary>
    public static Task PressKeyAsync(char key, int holdTime) => Controller.PressKeyAsync(key, holdTime);
    /// <summary>可取消的异步按键。</summary>
    public static Task PressKeyAsync(char key, int holdTime, CancellationToken cancellationToken) =>
        Controller.PressKeyAsync(key, holdTime, cancellationToken);
    /// <summary>按住扫描码对应的物理键。</summary>
    public static void KeyDown(ScanCode key) => Controller.KeyDown(key);
    /// <summary>释放扫描码对应的物理键。</summary>
    public static void KeyUp(ScanCode key) => Controller.KeyUp(key);
    /// <summary>按下物理键并保持指定毫秒数，再释放。</summary>
    public static void PressKey(ScanCode key, int holdTime = 30) => Controller.PressKey(key, holdTime);
    /// <summary>可取消的物理按键操作。</summary>
    public static Task PressKeyAsync(ScanCode key, int holdTime = 30, CancellationToken cancellationToken = default) =>
        Controller.PressKeyAsync(key, holdTime, cancellationToken);
    /// <summary>在作用域内保持物理键按下。</summary>
    public static IDisposable HoldKey(ScanCode key) => Controller.HoldKey(key);
    /// <summary>依次按下组合键并逆序释放。</summary>
    public static void PressCombination(params ScanCode[] keys) => Controller.PressCombination(keys);
    /// <summary>可取消的组合键操作。</summary>
    public static Task PressCombinationAsync(IEnumerable<ScanCode> keys, int holdTime = 30, CancellationToken cancellationToken = default) =>
        Controller.PressCombinationAsync(keys, holdTime, cancellationToken);
    /// <summary>按前台窗口键盘布局逐字符模拟按键，遵循系统大小写锁定及输入法状态。</summary>
    public static void InputKeys(string keys) => Controller.InputKeys(keys);
    /// <summary>按键序列；holdTime 为按住时间，delay 为字符间隔，单位均为毫秒。</summary>
    public static Task InputKeysAsync(string keys, int holdTime = 0, int delay = 0) => Controller.InputKeysAsync(keys, holdTime, delay);
    /// <summary>可取消的按键序列。</summary>
    public static Task InputKeysAsync(string keys, int holdTime, int delay, CancellationToken cancellationToken) =>
        Controller.InputKeysAsync(keys, holdTime, delay, cancellationToken);
    /// <summary>输入 Unicode 文本，支持中文和表情；不用于物理游戏按键。</summary>
    public static void TypeText(string text) => Controller.TypeText(text);
    /// <summary>可取消的 Unicode 文本输入，delay 为字符间隔，单位 ms。</summary>
    public static Task TypeTextAsync(string text, int delay = 0, CancellationToken cancellationToken = default) =>
        Controller.TypeTextAsync(text, delay, cancellationToken);

    /// <summary>相对移动鼠标；受系统鼠标加速影响。</summary>
    public static void MouseMove(int x, int y) => Controller.MouseMove(x, y);
    /// <summary>MouseMove 的别名，兼容旧 README 示例。</summary>
    public static void Move(int x, int y) => MouseMove(x, y);
    /// <summary>移动至虚拟桌面的物理像素坐标，支持负坐标。</summary>
    public static void LocateTo(int x, int y) => Controller.LocateTo(x, y);
    /// <summary>移动至虚拟桌面的物理像素坐标。</summary>
    public static void LocateTo(Point point) => Controller.LocateTo(point);
    /// <summary>移动至主屏的指定位置。</summary>
    public static void LocateTo(ScreenPosition screenPosition) => Controller.LocateTo(screenPosition);
    /// <summary>发送归一化绝对坐标（0..65535）。</summary>
    public static void LocateToNormalized(int x, int y, bool virtualDesktop = true) => Controller.LocateToNormalized(x, y, virtualDesktop);
    /// <summary>平滑移动到物理像素坐标，duration 单位为 ms。</summary>
    public static Task LocateToAsync(Point destination, int duration = 250, CancellationToken cancellationToken = default) =>
        Controller.LocateToAsync(destination, duration, cancellationToken);
    /// <summary>按住鼠标键。</summary>
    public static void MouseDown(MouseButton button) => Controller.MouseDown(button);
    /// <summary>释放鼠标键。</summary>
    public static void MouseUp(MouseButton button) => Controller.MouseUp(button);
    /// <summary>在作用域内按住鼠标键。</summary>
    public static IDisposable HoldMouseButton(MouseButton button) => Controller.HoldMouseButton(button);
    /// <summary>点击鼠标。</summary>
    public static void MouseClick(MouseButton mouseButton) => Controller.MouseClick(mouseButton);
    /// <summary>点击鼠标并保持指定毫秒数。</summary>
    public static Task MouseClickAsync(MouseButton mouseButton, int holdTime) => Controller.MouseClickAsync(mouseButton, holdTime);
    /// <summary>可取消的鼠标点击。</summary>
    public static Task MouseClickAsync(MouseButton mouseButton, int holdTime, CancellationToken cancellationToken) =>
        Controller.MouseClickAsync(mouseButton, holdTime, cancellationToken);
    /// <summary>鼠标双击。</summary>
    public static Task MouseDoubleClickAsync(MouseButton button, int interval = 100, CancellationToken cancellationToken = default) =>
        Controller.MouseDoubleClickAsync(button, interval, cancellationToken);
    /// <summary>平滑拖动；退出时尝试释放鼠标键。</summary>
    public static Task MouseDragAsync(Point start, Point end, int duration = 250, MouseButton button = MouseButton.Left,
        CancellationToken cancellationToken = default) => Controller.MouseDragAsync(start, end, duration, button, cancellationToken);
    /// <summary>垂直滚动；120 为一格，负值向下。保留原有拼写以兼容旧代码。</summary>
    public static void MouseWhellRoll(int length) => MouseWheelRoll(length);
    /// <summary>垂直滚动；120 为一格，负值向下。</summary>
    public static void MouseWheelRoll(int delta) => Controller.MouseWheelRoll(delta);
    /// <summary>水平滚动；120 为一格，正值向右。</summary>
    public static void MouseHorizontalWheelRoll(int delta) => Controller.MouseHorizontalWheelRoll(delta);
    /// <summary>释放共享控制器持有的键；应先取消并等待活动序列结束。</summary>
    public static void ReleaseAll() { lock (SharedSync) sharedController?.ReleaseAll(); }

    /// <summary>检查鼠标键的系统状态。</summary>
    public static bool GetMouseDown(MouseButton button)
    {
        InputValidation.Button(button);
        WindowsDesktop.EnsureWindows();
        int virtualKey = button switch
        {
            MouseButton.Left => 0x01,
            MouseButton.Right => 0x02,
            MouseButton.Middle => 0x04,
            MouseButton.XButton1 => 0x05,
            _ => 0x06
        };
        return (NativeMethods.GetAsyncKeyState(virtualKey) & 0x8000) != 0;
    }

    /// <summary>检查字符对应按键的系统状态。</summary>
    public static bool GetKeyDown(char key) => GetKeyDown(KeyboardMapping.FromCharacter(key));
    /// <summary>检查扫描码对应虚拟键的系统状态；某些按键（如两个 Enter）共享系统状态。</summary>
    public static bool GetKeyDown(ScanCode key)
    {
        InputValidation.Key(key);
        var virtualKey = NativeMethods.MapVirtualKeyExW((ushort)key, 3, KeyboardMapping.ForegroundLayout());
        if (virtualKey == 0) throw new ArgumentException("扫描码没有对应的虚拟键。", nameof(key));
        return (NativeMethods.GetAsyncKeyState((int)virtualKey) & 0x8000) != 0;
    }

    /// <summary>获取主屏尺寸，受调用线程 DPI 感知设置影响。</summary>
    public static Size GetScreenSize() => WindowsDesktop.ScreenSize();
    /// <summary>获取主屏的物理像素尺寸。</summary>
    public static Size GetAbsoluteScreenSize() => WindowsDesktop.Bounds(false).Size;
    /// <summary>获取所有显示器组成的虚拟桌面边界，使用物理像素，可包含负坐标。</summary>
    public static Rectangle GetVirtualScreenBounds() => WindowsDesktop.Bounds(true);
    /// <summary>获取鼠标的物理像素坐标。</summary>
    public static Point GetMousePosition() => WindowsDesktop.CursorPosition();
    /// <summary>相对主屏的位置比例；鼠标位于副屏时可能超出 0..1。</summary>
    public static (double X, double Y) GetMousePointRelatively()
    {
        var point = GetMousePosition();
        var bounds = WindowsDesktop.Bounds(false);
        return ((double)point.X / Math.Max(1, bounds.Width - 1), (double)point.Y / Math.Max(1, bounds.Height - 1));
    }
    /// <summary>主屏物理宽度与当前线程感知宽度的比值；DPI 感知线程上通常为 1。</summary>
    public static double GetScalingFactorForWindow() => (double)GetAbsoluteScreenSize().Width / GetScreenSize().Width;
    /// <summary>获取指定窗口报告的 DPI 缩放倍率。</summary>
    public static double GetScalingFactorForWindow(nint hWnd)
    {
        WindowsDesktop.EnsureWindows();
        var dpi = NativeMethods.GetDpiForWindow(hWnd);
        if (dpi == 0) throw new ArgumentException("窗口句柄无效或无法获取窗口 DPI。", nameof(hWnd));
        return dpi / 96.0;
    }
}

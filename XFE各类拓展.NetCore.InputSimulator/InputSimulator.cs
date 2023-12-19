using System.Drawing;
using System.Runtime.InteropServices;

namespace XFE各类拓展.NetCore.InputSimulator;

/// <summary>
/// 模拟用户输入
/// </summary>
public static partial class InputSimulator
{
    #region DLL引用
    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial void keybd_event(byte bVk, byte bScan, uint dwFlags, IntPtr dwExtraInfo);
    [LibraryImport("user32.dll")]
    private static partial void mouse_event(uint dwFlags, int dx, int dy, uint dwData, IntPtr dwExtraInfo);
    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetrics(int nIndex);
    [LibraryImport("user32.dll")]
    private static partial void SetCursorPos(int x, int y);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out Point lpPoint);
    #endregion
    #region 常量
    private const int KEYEVENTF_KEYDOWN = 0x0000; // 按键按下
    private const int KEYEVENTF_KEYUP = 0x0002; // 按键松开
    private const uint MOUSEEVENTF_MOVE = 0x0001; // 鼠标移动
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002; // 鼠标左键按下
    private const uint MOUSEEVENTF_LEFTUP = 0x0004; // 鼠标左键松开
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008; // 鼠标右键按下
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010; // 鼠标右键松开
    private const uint MOUSEEVENTF_MIDDOWN = 0x0020; // 鼠标中键按下
    private const uint MOUSEEVENTF_MIDUP = 0x0040; // 鼠标中键松开
    private const uint MOUSEEVENTF_WHEEL = 0x0800; // 鼠标滚轮
    private const int SM_CXSCREEN = 0; // 屏幕宽度
    private const int SM_CYSCREEN = 1; // 屏幕高度
    #endregion
    #region 键盘方法
    /// <summary>
    /// 按下某个按键
    /// </summary>
    /// <remarks>
    /// 如果需要输入一组按键，请使用<see cref="InputKeys(string)"/>
    /// </remarks>
    /// <param name="key">按键</param>
    public static void PressKey(char key)
    {
        byte keyCode = (byte)key;
        keybd_event(keyCode, 0, KEYEVENTF_KEYDOWN, IntPtr.Zero);
        keybd_event(keyCode, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
    }
    /// <summary>
    /// 按下某个按键
    /// </summary>
    /// <remarks>
    /// 如果需要输入一组按键，请使用<see cref="InputKeys(string)"/>
    /// </remarks>
    /// <param name="key">按键</param>
    /// <param name="holdTime">按下时间</param>
    public static async Task PressKeyAsync(char key, int holdTime)
    {
        byte keyCode = (byte)key;
        keybd_event(keyCode, 0, KEYEVENTF_KEYDOWN, IntPtr.Zero);
        await Task.Delay(holdTime);
        keybd_event(keyCode, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
    }
    /// <summary>
    /// 输入一组按键
    /// </summary>
    /// <remarks>
    /// 如果需要输入延迟，请使用<see cref="InputKeysAsync(string, int, int)"/>
    /// </remarks>
    /// <param name="keys">待输入按键</param>
    public static void InputKeys(string keys)
    {
        foreach (var key in keys)
        {
            byte keyCode = (byte)key;
            keybd_event(keyCode, 0, KEYEVENTF_KEYDOWN, IntPtr.Zero);
            keybd_event(keyCode, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
        }
    }
    /// <summary>
    /// 输入一组按键
    /// </summary>
    /// <param name="keys">待输入按键</param>
    /// <param name="holdTime">每个按键按住的时间</param>
    /// <param name="delay">按键间延迟</param>
    /// <returns></returns>
    public static async Task InputKeysAsync(string keys, int holdTime = 0, int delay = 0)
    {
        foreach (var key in keys)
        {
            byte keyCode = (byte)key;
            keybd_event(keyCode, 0, KEYEVENTF_KEYDOWN, IntPtr.Zero);
            await Task.Delay(holdTime);
            keybd_event(keyCode, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
            await Task.Delay(delay);
        }
    }
    #endregion
    #region 鼠标方法
    /// <summary>
    /// 相对移动
    /// </summary>
    /// <remarks>
    /// 如果需要绝对移动请使用<see cref="LocateTo(int, int)"/>方法
    /// </remarks>
    /// <param name="x">X偏移</param>
    /// <param name="y">Y偏移</param>
    public static void Move(int x, int y)
    {
        mouse_event(MOUSEEVENTF_MOVE, x, y, 0, IntPtr.Zero);
    }
    /// <summary>
    /// 将鼠标定位至
    /// </summary>
    /// <remarks>
    /// 如果需要相对移动请使用<see cref="Move(int, int)"/>方法
    /// </remarks>
    /// <param name="x">X坐标</param>
    /// <param name="y">Y坐标</param>
    public static void LocateTo(int x, int y)
    {
        SetCursorPos(x, y);
    }
    /// <summary>
    /// 将鼠标定位至
    /// </summary>
    /// <remarks>
    /// 如果需要相对移动请使用<see cref="Move(int, int)"/>方法
    /// </remarks>
    /// <param name="point">定位到的位置</param>
    public static void LocateTo(Point point)
    {
        LocateTo(point.X, point.Y);
    }
    /// <summary>
    /// 将鼠标定位至
    /// </summary>
    /// <remarks>
    /// 如果需要相对移动请使用<see cref="Move(int, int)"/>方法
    /// </remarks>
    /// <param name="screenPosition">屏幕空间位置</param>
    public static void LocateTo(ScreenPosition screenPosition)
    {
        switch (screenPosition)
        {
            case ScreenPosition.TopLeft:
                LocateTo(0, 0);
                break;
            case ScreenPosition.TopRight:
                LocateTo(GetScreenSize().X - 1, 0);
                break;
            case ScreenPosition.Top:
                LocateTo(GetScreenSize().X / 2, 0);
                break;
            case ScreenPosition.Left:
                LocateTo(0, GetScreenSize().Y / 2);
                break;
            case ScreenPosition.Right:
                LocateTo(GetScreenSize().X - 1, GetScreenSize().Y / 2);
                break;
            case ScreenPosition.Center:
                LocateTo(GetScreenSize().X / 2, GetScreenSize().Y / 2);
                break;
            case ScreenPosition.BottomLeft:
                LocateTo(0, GetScreenSize().Y - 1);
                break;
            case ScreenPosition.Bottom:
                LocateTo(GetScreenSize().X / 2, GetScreenSize().Y - 1);
                break;
            case ScreenPosition.BottomRight:
                LocateTo(GetScreenSize().X - 1, GetScreenSize().Y - 1);
                break;
            default:
                break;
        }
    }
    /// <summary>
    /// 模拟鼠标点击
    /// </summary>
    /// <param name="mouseButton">鼠标按键</param>
    public static void MouseClick(MouseButton mouseButton)
    {
        switch (mouseButton)
        {
            case MouseButton.Left:
                mouse_event(MOUSEEVENTF_LEFTDOWN, GetMousePosition().X, GetMousePosition().Y, 0, IntPtr.Zero);
                mouse_event(MOUSEEVENTF_LEFTUP, GetMousePosition().X, GetMousePosition().Y, 0, IntPtr.Zero);
                break;
            case MouseButton.Right:
                mouse_event(MOUSEEVENTF_RIGHTDOWN, GetMousePosition().X, GetMousePosition().Y, 0, IntPtr.Zero);
                mouse_event(MOUSEEVENTF_RIGHTUP, GetMousePosition().X, GetMousePosition().Y, 0, IntPtr.Zero);
                break;
            case MouseButton.Middle:
                mouse_event(MOUSEEVENTF_MIDDOWN, GetMousePosition().X, GetMousePosition().Y, 0, IntPtr.Zero);
                mouse_event(MOUSEEVENTF_MIDUP, GetMousePosition().X, GetMousePosition().Y, 0, IntPtr.Zero);
                break;
            default:
                break;
        }
    }
    /// <summary>
    /// 模拟鼠标点击
    /// </summary>
    /// <param name="mouseButton">鼠标按键</param>
    /// <param name="holdTime">鼠标按键按住时间</param>
    public static async Task MouseClickAsync(MouseButton mouseButton, int holdTime)
    {
        switch (mouseButton)
        {
            case MouseButton.Left:
                mouse_event(MOUSEEVENTF_LEFTDOWN, GetMousePosition().X, GetMousePosition().Y, 0, IntPtr.Zero);
                await Task.Delay(holdTime);
                mouse_event(MOUSEEVENTF_LEFTUP, GetMousePosition().X, GetMousePosition().Y, 0, IntPtr.Zero);
                break;
            case MouseButton.Right:
                mouse_event(MOUSEEVENTF_RIGHTDOWN, GetMousePosition().X, GetMousePosition().Y, 0, IntPtr.Zero);
                await Task.Delay(holdTime);
                mouse_event(MOUSEEVENTF_RIGHTUP, GetMousePosition().X, GetMousePosition().Y, 0, IntPtr.Zero);
                break;
            case MouseButton.Middle:
                mouse_event(MOUSEEVENTF_MIDDOWN, GetMousePosition().X, GetMousePosition().Y, 0, IntPtr.Zero);
                await Task.Delay(holdTime);
                mouse_event(MOUSEEVENTF_MIDUP, GetMousePosition().X, GetMousePosition().Y, 0, IntPtr.Zero);
                break;
            default:
                break;
        }
    }
    #endregion
    #region 系统信息获取方法
    /// <summary>
    /// 获取屏幕的宽高
    /// </summary>
    /// <returns>屏幕宽高</returns>
    public static Point GetScreenSize()
    {
        return new Point(GetSystemMetrics(SM_CXSCREEN), GetSystemMetrics(SM_CYSCREEN));
    }
    /// <summary>
    /// 获取鼠标的当前位置
    /// </summary>
    /// <returns>鼠标位置</returns>
    public static Point GetMousePosition()
    {
        GetCursorPos(out Point point);
        return point;
    }
    /// <summary>
    /// 获取鼠标位置相对于屏幕大小的百分比
    /// </summary>
    /// <returns>百分比，0-1之间(X, Y)</returns>
    public static (double X, double Y) GetMousePointRelatively()
    {
        GetCursorPos(out Point point);
        return ((double)point.X / GetSystemMetrics(SM_CXSCREEN), (double)point.Y / GetSystemMetrics(SM_CYSCREEN));
    }
    #endregion
}
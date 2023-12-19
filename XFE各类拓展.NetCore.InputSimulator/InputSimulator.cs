using System.Drawing;
using System.Runtime.InteropServices;

namespace XFE各类拓展.NetCore.InputSimulator;

public enum MouseButtons
{
    Left,
    Right,
    Middle
}

public static class InputSimulator
{
    #region DLL引用
    [DllImport("user32.dll", SetLastError = true)]
    public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, IntPtr dwExtraInfo);
    [DllImport("user32.dll")]
    static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, IntPtr dwExtraInfo);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool SetCursorPos(int x, int y);
    #endregion
    #region 常量
    private const int KEYEVENTF_KEYDOWN = 0x0000; // Key down flag
    private const int KEYEVENTF_KEYUP = 0x0002;   // Key up flag
    private const uint MOUSEEVENTF_MOVE = 0x0001;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    #endregion
    public static void PressKey(char key)
    {
        byte keyCode = (byte)key;
        keybd_event(keyCode, 0, KEYEVENTF_KEYDOWN, IntPtr.Zero);
        keybd_event(keyCode, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
    }
    public static void InputKeys(string keys)
    {
        foreach (var key in keys)
        {
            byte keyCode = (byte)key;
            keybd_event(keyCode, 0, KEYEVENTF_KEYDOWN, IntPtr.Zero);
            keybd_event(keyCode, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
        }
    }
    static void MouseClick(MouseButtons button, int x, int y)
    {

    }
}
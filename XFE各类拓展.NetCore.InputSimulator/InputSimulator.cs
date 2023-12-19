using System.Runtime.InteropServices;

namespace XFE各类拓展.NetCore.InputSimulator;

public static class InputSimulator
{
    #region DLL引用
    [DllImport("user32.dll", SetLastError = true)]
    public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, IntPtr dwExtraInfo);
    #endregion
    #region 常量
    private const int KEYEVENTF_KEYDOWN = 0x0000; // Key down flag
    private const int KEYEVENTF_KEYUP = 0x0002;   // Key up flag
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
}
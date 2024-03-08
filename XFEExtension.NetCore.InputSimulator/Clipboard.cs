using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace XFEExtension.NetCore.InputSimulator;

/// <summary>
/// 剪切板操作类
/// </summary>
[SupportedOSPlatform("windows")]
public static partial class Clipboard
{
    /// <summary>
    /// 打开剪贴板以进行检查，并防止其他应用程序修改剪贴板内容
    /// </summary>
    /// <param name="hWndNewOwner"></param>
    /// <returns></returns>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool OpenClipboard(IntPtr hWndNewOwner);

    /// <summary>
    /// 清空剪贴板内容
    /// </summary>
    /// <returns></returns>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool EmptyClipboard();

    /// <summary>
    /// 将数据放入剪贴板
    /// </summary>
    /// <param name="uFormat"></param>
    /// <param name="data"></param>
    /// <returns></returns>
    [LibraryImport("user32.dll")]
    private static partial IntPtr SetClipboardData(uint uFormat, IntPtr data);

    /// <summary>
    /// 获取剪贴板数据
    /// </summary>
    /// <param name="uFormat"></param>
    /// <returns></returns>
    [LibraryImport("user32.dll")]
    private static partial IntPtr GetClipboardData(uint uFormat);

    /// <summary>
    /// 关闭剪贴板
    /// </summary>
    /// <returns></returns>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CloseClipboard();

    /// <summary>
    /// 分配内存
    /// </summary>
    /// <param name="uFlags"></param>
    /// <param name="dwBytes"></param>
    /// <returns></returns>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    /// <summary>
    /// 锁定内存
    /// </summary>
    /// <param name="hMem"></param>
    /// <returns></returns>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial IntPtr GlobalLock(IntPtr hMem);

    /// <summary>
    /// 解锁内存
    /// </summary>
    /// <param name="hMem"></param>
    /// <returns></returns>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GlobalUnlock(IntPtr hMem);

    /// <summary>
    /// 设置剪贴板内容
    /// </summary>
    /// <param name="text"></param>
    /// <param name="format"></param>
    /// <returns></returns>
    public static bool SetClipboardContent(string text, uint format = ClipboardFormat.CF_UNICODETEXT)
    {
        if (!OpenClipboard(IntPtr.Zero))
            return false;
        EmptyClipboard();
        IntPtr hGlobal = GlobalAlloc(0x2000, (UIntPtr)((text.Length + 1) * 2));
        IntPtr pGlobal = GlobalLock(hGlobal);
        byte[] bytes = Encoding.Unicode.GetBytes(text);
        Marshal.Copy(bytes, 0, pGlobal, bytes.Length);
        GlobalUnlock(hGlobal);
        SetClipboardData(format, hGlobal);
        CloseClipboard();
        return true;
    }

    /// <summary>
    /// 获取剪贴板内容
    /// </summary>
    /// <param name="format"></param>
    /// <returns></returns>
    public static object? GetClipboardContent(uint format = ClipboardFormat.CF_UNICODETEXT)
    {
        if (!OpenClipboard(IntPtr.Zero))
            return null;
        IntPtr hClipboardData = GetClipboardData(format);

        if (hClipboardData == IntPtr.Zero)
        {
            CloseClipboard();
            return null;
        }
        IntPtr pClipboardData = GlobalLock(hClipboardData);
        if (pClipboardData == IntPtr.Zero)
        {
            CloseClipboard();
            return null;
        }
        var content = Marshal.PtrToStringAnsi(pClipboardData);
        GlobalUnlock(hClipboardData);
        CloseClipboard();
        return content;
    }
}
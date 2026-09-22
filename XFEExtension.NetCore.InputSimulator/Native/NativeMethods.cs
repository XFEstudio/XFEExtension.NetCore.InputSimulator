using System.Drawing;
using System.Runtime.InteropServices;

namespace XFEExtension.NetCore.InputSimulator.Native;

internal static partial class NativeMethods
{
    [LibraryImport("user32.dll")]
    internal static partial uint MapVirtualKeyExW(uint code, uint mapType, nint layout);

    [LibraryImport("user32.dll")]
    internal static partial short VkKeyScanExW(ushort character, nint layout);

    [LibraryImport("user32.dll")]
    internal static partial nint GetKeyboardLayout(uint threadId);

    [LibraryImport("user32.dll")]
    internal static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    internal static partial uint GetWindowThreadProcessId(nint window, out uint processId);

    [LibraryImport("user32.dll")]
    internal static partial int GetSystemMetrics(int index);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetCursorPos(out Point point);

    [LibraryImport("user32.dll")]
    internal static partial short GetAsyncKeyState(int key);

    [LibraryImport("user32.dll")]
    internal static partial uint GetDpiForWindow(nint window);

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial nint SetThreadDpiAwarenessContext(nint context);
}

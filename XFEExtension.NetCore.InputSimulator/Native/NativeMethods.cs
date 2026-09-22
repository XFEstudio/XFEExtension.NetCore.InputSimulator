using System.Drawing;
using System.Runtime.InteropServices;

namespace XFEExtension.NetCore.InputSimulator.Native;

internal static partial class NativeMethods
{
    [LibraryImport("user32.dll", SetLastError = true)]
    internal static unsafe partial uint SendInput(uint count, NativeInput* inputs, int size);

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

// INPUT 的 union 对齐由 ULONG_PTR 决定，不能把 union 固定放在偏移 4。
[StructLayout(LayoutKind.Sequential)]
internal struct NativeInput
{
    internal uint Type;
    internal InputUnion Data;
}

[StructLayout(LayoutKind.Explicit)]
internal struct InputUnion
{
    [FieldOffset(0)] internal KeyboardInput Keyboard;
    [FieldOffset(0)] internal MouseInput Mouse;
}

[StructLayout(LayoutKind.Sequential)]
internal struct KeyboardInput
{
    internal ushort VirtualKey;
    internal ushort ScanCode;
    internal uint Flags;
    internal uint Time;
    internal nuint ExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MouseInput
{
    internal int X;
    internal int Y;
    internal uint MouseData;
    internal uint Flags;
    internal uint Time;
    internal nuint ExtraInfo;
}

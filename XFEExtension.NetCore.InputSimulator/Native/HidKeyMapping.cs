namespace XFEExtension.NetCore.InputSimulator.Native;

internal static class HidKeyMapping
{
    private static readonly Dictionary<ScanCode, byte> Usages = CreateUsages();

    internal static byte GetUsage(ScanCode code) => Usages.TryGetValue(code, out var usage) ? usage
        : throw new ArgumentOutOfRangeException(nameof(code), "驱动只支持 ScanCode 中已命名的键；不接受未知扫描码或 E1 序列。");

    private static Dictionary<ScanCode, byte> CreateUsages()
    {
        Dictionary<ScanCode, byte> keys = new()
        {
            [ScanCode.Enter] = 0x28, [ScanCode.Escape] = 0x29, [ScanCode.Backspace] = 0x2A,
            [ScanCode.Tab] = 0x2B, [ScanCode.Space] = 0x2C, [ScanCode.Minus] = 0x2D,
            [ScanCode.Equals] = 0x2E, [ScanCode.LeftBracket] = 0x2F, [ScanCode.RightBracket] = 0x30,
            [ScanCode.Backslash] = 0x31, [ScanCode.Semicolon] = 0x33, [ScanCode.Quote] = 0x34,
            [ScanCode.Grave] = 0x35, [ScanCode.Comma] = 0x36, [ScanCode.Period] = 0x37,
            [ScanCode.Slash] = 0x38, [ScanCode.CapsLock] = 0x39,
            [ScanCode.PrintScreen] = 0x46, [ScanCode.ScrollLock] = 0x47,
            [ScanCode.Insert] = 0x49, [ScanCode.Home] = 0x4A, [ScanCode.PageUp] = 0x4B,
            [ScanCode.Delete] = 0x4C, [ScanCode.End] = 0x4D, [ScanCode.PageDown] = 0x4E,
            [ScanCode.Right] = 0x4F, [ScanCode.Left] = 0x50, [ScanCode.Down] = 0x51, [ScanCode.Up] = 0x52,
            [ScanCode.NumLock] = 0x53, [ScanCode.NumPadDivide] = 0x54, [ScanCode.NumPadMultiply] = 0x55,
            [ScanCode.NumPadSubtract] = 0x56, [ScanCode.NumPadAdd] = 0x57, [ScanCode.NumPadEnter] = 0x58,
            [ScanCode.NumPad0] = 0x62, [ScanCode.NumPadDecimal] = 0x63,
            [ScanCode.Oem102] = 0x64, [ScanCode.Applications] = 0x65,
            [ScanCode.LeftControl] = 0xE0, [ScanCode.LeftShift] = 0xE1,
            [ScanCode.LeftAlt] = 0xE2, [ScanCode.LeftWindows] = 0xE3,
            [ScanCode.RightControl] = 0xE4, [ScanCode.RightShift] = 0xE5,
            [ScanCode.RightAlt] = 0xE6, [ScanCode.RightWindows] = 0xE7
        };
        for (var letter = 'A'; letter <= 'Z'; letter++) keys[Enum.Parse<ScanCode>(letter.ToString())] = (byte)(4 + letter - 'A');
        for (var digit = 0; digit <= 9; digit++) keys[Enum.Parse<ScanCode>($"D{digit}")] = (byte)(digit == 0 ? 0x27 : 0x1D + digit);
        for (var key = 1; key <= 12; key++) keys[Enum.Parse<ScanCode>($"F{key}")] = (byte)(0x39 + key);
        for (var key = 1; key <= 9; key++) keys[Enum.Parse<ScanCode>($"NumPad{key}")] = (byte)(0x58 + key);
        return keys;
    }
}

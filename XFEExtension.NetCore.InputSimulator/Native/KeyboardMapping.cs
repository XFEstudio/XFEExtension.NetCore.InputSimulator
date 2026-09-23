namespace XFEExtension.NetCore.InputSimulator.Native;

internal static class KeyboardMapping
{
    // char 单键重载保持物理键语义：大小写字母使用同一个按键，不隐式按 Shift。
    internal static ScanCode FromCharacter(char key) => key switch
    {
        >= 'a' and <= 'z' => Enum.Parse<ScanCode>(char.ToUpperInvariant(key).ToString()),
        >= 'A' and <= 'Z' => Enum.Parse<ScanCode>(key.ToString()),
        >= '0' and <= '9' => Enum.Parse<ScanCode>($"D{key}"),
        ' ' => ScanCode.Space,
        '\r' or '\n' => ScanCode.Enter,
        '\t' => ScanCode.Tab,
        '\b' => ScanCode.Backspace,
        '\u001B' => ScanCode.Escape,
        _ => throw new ArgumentException("单键字符支持英文字母、数字、空格和常用控制字符；其他按键请使用 ScanCode，文本请使用 TypeText。", nameof(key))
    };

    internal static nint ForegroundLayout()
    {
        WindowsDesktop.EnsureWindows();
        var thread = NativeMethods.GetWindowThreadProcessId(NativeMethods.GetForegroundWindow(), out _);
        return NativeMethods.GetKeyboardLayout(thread);
    }

    internal static ScanCode[][] CharacterChords(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
            return [];
        var layout = ForegroundLayout();
        // 在输入前完成整段校验，避免遇到不能映射的字符时只输入半段内容。
        return text.Select(character => CharacterChord(character, layout)).ToArray();
    }

    private static ScanCode[] CharacterChord(char character, nint layout)
    {
        if (character is '\r' or '\n' or '\t' or '\b' or '\u001B')
            return [FromCharacter(character)];
        var mapping = NativeMethods.VkKeyScanExW(character, layout);
        if (mapping == -1 || ((mapping >> 8) & ~7) != 0)
                throw new ArgumentException($"当前键盘布局不能直接输入 U+{(int)character:X4}；HID 驱动需要通过目标程序的输入法输入此字符。", nameof(character));
        var scan = (ScanCode)NativeMethods.MapVirtualKeyExW((uint)(mapping & 0xFF), 4, layout);
        InputValidation.Key(scan);
        List<ScanCode> keys = [];
        if ((mapping & 0x200) != 0) keys.Add(ScanCode.LeftControl);
        if ((mapping & 0x400) != 0) keys.Add(ScanCode.LeftAlt);
        if ((mapping & 0x100) != 0) keys.Add(ScanCode.LeftShift);
        keys.Add(scan);
        return keys.ToArray();
    }
}

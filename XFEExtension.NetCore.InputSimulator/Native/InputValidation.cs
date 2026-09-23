namespace XFEExtension.NetCore.InputSimulator.Native;

internal static class InputValidation
{
    internal static void Key(ScanCode key)
    {
        var code = (ushort)key;
        // 允许未在枚举中命名的普通或 E0 Set 1 扫描码。E1/Pause 需要特殊序列，不隐式误发。
        if ((code & 0xFF) is 0 or > 0x7F || (code & 0xFF00) is not (0 or 0xE000))
            throw new ArgumentOutOfRangeException(nameof(key), "扫描码必须是 0x01..0x7F 或 0xE001..0xE07F；不支持 E1 序列。");
    }

    internal static void Button(MouseButton button)
    {
        if (!Enum.IsDefined(button))
            throw new ArgumentOutOfRangeException(nameof(button));
    }

    internal static void Delay(int milliseconds, string name)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(milliseconds, name);
    }

    internal static void Coordinates(int x, int y, bool absolute, bool virtualDesktop)
    {
        if (absolute && (x is < 0 or > 65535 || y is < 0 or > 65535))
            throw new ArgumentOutOfRangeException(nameof(x), "绝对坐标必须在 0..65535 之间。");
        if (virtualDesktop && !absolute)
            throw new ArgumentException("虚拟桌面标记仅用于绝对移动。", nameof(virtualDesktop));
    }
}

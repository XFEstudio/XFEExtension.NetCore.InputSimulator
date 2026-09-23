namespace XFEExtension.NetCore.InputSimulator;

/// <summary>键盘物理扫描码（Set 1）。E0 扩展键用 0xE0xx 表示，与键盘布局无关。</summary>
public enum ScanCode : ushort
{
    /// <summary>Esc</summary>
    Escape = 0x01,
    /// <summary>主键盘 1</summary>
    D1 = 0x02,
    /// <summary>主键盘 2</summary>
    D2 = 0x03,
    /// <summary>主键盘 3</summary>
    D3 = 0x04,
    /// <summary>主键盘 4</summary>
    D4 = 0x05,
    /// <summary>主键盘 5</summary>
    D5 = 0x06,
    /// <summary>主键盘 6</summary>
    D6 = 0x07,
    /// <summary>主键盘 7</summary>
    D7 = 0x08,
    /// <summary>主键盘 8</summary>
    D8 = 0x09,
    /// <summary>主键盘 9</summary>
    D9 = 0x0A,
    /// <summary>主键盘 0</summary>
    D0 = 0x0B,
    /// <summary>减号</summary>
    Minus = 0x0C,
    /// <summary>等号</summary>
    Equals = 0x0D,
    /// <summary>退格</summary>
    Backspace = 0x0E,
    /// <summary>Tab</summary>
    Tab = 0x0F,
    /// <summary>Q</summary>
    Q = 0x10,
    /// <summary>W</summary>
    W = 0x11,
    /// <summary>E</summary>
    E = 0x12,
    /// <summary>R</summary>
    R = 0x13,
    /// <summary>T</summary>
    T = 0x14,
    /// <summary>Y</summary>
    Y = 0x15,
    /// <summary>U</summary>
    U = 0x16,
    /// <summary>I</summary>
    I = 0x17,
    /// <summary>O</summary>
    O = 0x18,
    /// <summary>P</summary>
    P = 0x19,
    /// <summary>左方括号</summary>
    LeftBracket = 0x1A,
    /// <summary>右方括号</summary>
    RightBracket = 0x1B,
    /// <summary>主键盘回车</summary>
    Enter = 0x1C,
    /// <summary>左 Ctrl</summary>
    LeftControl = 0x1D,
    /// <summary>A</summary>
    A = 0x1E,
    /// <summary>S</summary>
    S = 0x1F,
    /// <summary>D</summary>
    D = 0x20,
    /// <summary>F</summary>
    F = 0x21,
    /// <summary>G</summary>
    G = 0x22,
    /// <summary>H</summary>
    H = 0x23,
    /// <summary>J</summary>
    J = 0x24,
    /// <summary>K</summary>
    K = 0x25,
    /// <summary>L</summary>
    L = 0x26,
    /// <summary>分号</summary>
    Semicolon = 0x27,
    /// <summary>引号</summary>
    Quote = 0x28,
    /// <summary>反引号</summary>
    Grave = 0x29,
    /// <summary>左 Shift</summary>
    LeftShift = 0x2A,
    /// <summary>反斜杠</summary>
    Backslash = 0x2B,
    /// <summary>Z</summary>
    Z = 0x2C,
    /// <summary>X</summary>
    X = 0x2D,
    /// <summary>C</summary>
    C = 0x2E,
    /// <summary>V</summary>
    V = 0x2F,
    /// <summary>B</summary>
    B = 0x30,
    /// <summary>N</summary>
    N = 0x31,
    /// <summary>M</summary>
    M = 0x32,
    /// <summary>逗号</summary>
    Comma = 0x33,
    /// <summary>句号</summary>
    Period = 0x34,
    /// <summary>斜杠</summary>
    Slash = 0x35,
    /// <summary>右 Shift（不是 E0 扩展键）</summary>
    RightShift = 0x36,
    /// <summary>小键盘乘号</summary>
    NumPadMultiply = 0x37,
    /// <summary>左 Alt</summary>
    LeftAlt = 0x38,
    /// <summary>空格</summary>
    Space = 0x39,
    /// <summary>大小写锁定</summary>
    CapsLock = 0x3A,
    /// <summary>F1</summary>
    F1 = 0x3B,
    /// <summary>F2</summary>
    F2 = 0x3C,
    /// <summary>F3</summary>
    F3 = 0x3D,
    /// <summary>F4</summary>
    F4 = 0x3E,
    /// <summary>F5</summary>
    F5 = 0x3F,
    /// <summary>F6</summary>
    F6 = 0x40,
    /// <summary>F7</summary>
    F7 = 0x41,
    /// <summary>F8</summary>
    F8 = 0x42,
    /// <summary>F9</summary>
    F9 = 0x43,
    /// <summary>F10</summary>
    F10 = 0x44,
    /// <summary>数字锁定</summary>
    NumLock = 0x45,
    /// <summary>滚动锁定</summary>
    ScrollLock = 0x46,
    /// <summary>小键盘 7</summary>
    NumPad7 = 0x47,
    /// <summary>小键盘 8</summary>
    NumPad8 = 0x48,
    /// <summary>小键盘 9</summary>
    NumPad9 = 0x49,
    /// <summary>小键盘减号</summary>
    NumPadSubtract = 0x4A,
    /// <summary>小键盘 4</summary>
    NumPad4 = 0x4B,
    /// <summary>小键盘 5</summary>
    NumPad5 = 0x4C,
    /// <summary>小键盘 6</summary>
    NumPad6 = 0x4D,
    /// <summary>小键盘加号</summary>
    NumPadAdd = 0x4E,
    /// <summary>小键盘 1</summary>
    NumPad1 = 0x4F,
    /// <summary>小键盘 2</summary>
    NumPad2 = 0x50,
    /// <summary>小键盘 3</summary>
    NumPad3 = 0x51,
    /// <summary>小键盘 0</summary>
    NumPad0 = 0x52,
    /// <summary>小键盘小数点</summary>
    NumPadDecimal = 0x53,
    /// <summary>ISO 键盘额外按键</summary>
    Oem102 = 0x56,
    /// <summary>F11</summary>
    F11 = 0x57,
    /// <summary>F12</summary>
    F12 = 0x58,
    /// <summary>小键盘回车</summary>
    NumPadEnter = 0xE01C,
    /// <summary>右 Ctrl</summary>
    RightControl = 0xE01D,
    /// <summary>小键盘除号</summary>
    NumPadDivide = 0xE035,
    /// <summary>Print Screen</summary>
    PrintScreen = 0xE037,
    /// <summary>右 Alt</summary>
    RightAlt = 0xE038,
    /// <summary>Home</summary>
    Home = 0xE047,
    /// <summary>上方向键</summary>
    Up = 0xE048,
    /// <summary>Page Up</summary>
    PageUp = 0xE049,
    /// <summary>左方向键</summary>
    Left = 0xE04B,
    /// <summary>右方向键</summary>
    Right = 0xE04D,
    /// <summary>End</summary>
    End = 0xE04F,
    /// <summary>下方向键</summary>
    Down = 0xE050,
    /// <summary>Page Down</summary>
    PageDown = 0xE051,
    /// <summary>Insert</summary>
    Insert = 0xE052,
    /// <summary>Delete</summary>
    Delete = 0xE053,
    /// <summary>左 Windows</summary>
    LeftWindows = 0xE05B,
    /// <summary>右 Windows</summary>
    RightWindows = 0xE05C,
    /// <summary>上下文菜单</summary>
    Applications = 0xE05D
}

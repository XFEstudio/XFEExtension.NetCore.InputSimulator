# XFEExtension.NetCore.InputSimulator

用于 .NET 8 的 Windows 键鼠输入库。3.0 使用 `SendInput` 扫描码替代旧的 `keybd_event` / `mouse_event`，提供组合键、可取消的长按、Unicode 文本、侧键、滚轮、多屏定位、平滑拖动和输入序列。无需安装驱动，没有运行时 NuGet 依赖。

## 兼容性边界

**本库仍发送软件输入，不提供“免驱动且无法被游戏识别”的硬件输入。** 扫描码可以准确表达物理按键，但不会消除 Windows 的软件注入标记。对于三角洲行动等主动拒绝软件输入的目标，不能据此承诺兼容；本项目尚未进行该游戏的实测。

- 扫描码、Unicode 和鼠标模式都属于用户态注入，不是内核驱动或虚拟 HID。直接改用未经文档化的系统调用不能据此宣称输入变成真实硬件。
- 键盘事件通常由前台窗口接收，鼠标输入作用于当前桌面；没有后台窗口投递保证。
- Windows 的 UIPI 会限制向更高完整性级别的程序注入。返回成功仅表示系统接受事件，不表示目标程序已经消费。
- 基础输入支持 Windows；物理坐标查询使用 Windows 10 1703 及以上的线程 DPI API。建议运行环境为 Windows 10/11。

依据：[扫描码与 Unicode 的定义](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-keybdinput)、[SendInput 与 UIPI](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput)、[Windows 的注入标记](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-kbdllhookstruct)。

## 快速开始

```csharp
using XFEExtension.NetCore.InputSimulator;

// 原有静态调用仍可用，大小写字母对应同一个物理键。
InputSimulator.PressKey('X');

// 游戏操作建议明确使用扫描码和保持时间；这不保证目标接受注入。
await InputSimulator.PressKeyAsync(ScanCode.W, holdTime: 100);
InputSimulator.PressKey(ScanCode.Space);
InputSimulator.PressCombination(ScanCode.LeftControl, ScanCode.S);

// 向支持文本输入的控件发送 Unicode。
InputSimulator.TypeText("Hello，世界 🙂");
```

同步 `PressKey` / `MouseClick` 默认保持 30ms 再松开，便于轮询输入的应用检测。需要零延迟时使用 `PressKey(ScanCode.W, holdTime: 0)`，或独立控制器的 `MouseClick(MouseButton.Left, holdTime: 0)`。

## 独立控制器与按键清理

```csharp
using XFEExtension.NetCore.InputSimulator;

using var input = new InputController();
using var cancellation = new CancellationTokenSource();
cancellation.CancelAfter(500);

try
{
    // 500ms 后取消；退出时仍尝试逆序释放 W 和左 Shift。
    await input.PressCombinationAsync(
        [ScanCode.LeftShift, ScanCode.W],
        holdTime: 2000,
        cancellationToken: cancellation.Token);
}
catch (OperationCanceledException)
{
    // 本次操作已取消。
}

using (input.HoldKey(ScanCode.LeftControl))
{
    await input.PressKeyAsync(ScanCode.C);
}
```

- `KeyDown` / `KeyUp`、`MouseDown` / `MouseUp` 显式管理长按。同一实例重复 `KeyDown` 不会增加需要 `KeyUp` 的次数。
- `HoldKey` / `HoldMouseButton` 支持嵌套与重叠；最后一个作用域退出后才松开。如果已通过 `KeyDown` 手动持有，还需对应的 `KeyUp`。
- 组合键先校验全部参数，再按顺序按下、逆序释放。中途失败也会尝试释放已经按下的键。
- `ReleaseAll()` 尝试逆序释放本实例记录的所有键。失败项保留供重试，某次释放失败不会阻止其他键的释放。
- `Dispose()` 尝试释放持有的键并关闭后端。后端报错时无法保证系统已完成释放，错误会向调用者传播。
- 各实例独立管理状态，不协调其他实例或真实键盘。同一实例可以并发持有不同键；需要确定的步骤顺序时逐个 `await`，或使用 `InputSequence`。
- 调用 `ReleaseAll()` 前，先取消并等待正在运行的序列，防止后续步骤重新按键。静态 API 共享一个控制器；需要明确生命周期时使用独立实例。

## 扫描码与文本

```csharp
using var input = new InputController();

// 左右修饰键、方向键与小键盘是不同的扫描码。
input.PressKey(ScanCode.RightControl);
input.PressKey(ScanCode.Up);
input.PressKey(ScanCode.NumPad8);
input.PressKey(ScanCode.NumPadEnter);

// 按前台窗口布局模拟字符按键，自动处理 Shift / Ctrl / Alt。
await input.InputKeysAsync("Abc123!", holdTime: 30, delay: 50);

// Unicode 文本不依赖字符能否映射为物理键。
await input.TypeTextAsync("中文与 Emoji 🙂", delay: 40);
```

`ScanCode` 表示 Set 1 物理键位，字母名称按常见 QWERTY 键盘标记。也可显式传入未命名的合法码，例如 `(ScanCode)0xE020`；支持 `0x01..0x7F` 和 `0xE001..0xE07F`。`Pause` 的 E1 特殊序列目前不支持，会拒绝而不是误发为其他键。

`InputKeys` 保留物理按键序列语义，遵循 CapsLock、输入法和当前修饰键状态。它先校验整段字符的布局映射；无法映射的中文等字符会报错，改用 `TypeText`。`TypeText` 通过 `KEYEVENTF_UNICODE` 发送 UTF-16 文本，完整代理对之间不插入等待，不等同于游戏控制按键。回车、Tab 等操作键推荐使用对应的 `ScanCode`。

## 鼠标与多显示器

```csharp
using System.Drawing;
using XFEExtension.NetCore.InputSimulator;

using var input = new InputController();
input.MouseMove(100, -50);                         // 相对移动
input.LocateTo(-500, 200);                         // 虚拟桌面物理像素，支持副屏负坐标
input.LocateTo(ScreenPosition.Center);            // 主屏中央
input.LocateToNormalized(32768, 32768);            // 虚拟桌面中心附近
await input.LocateToAsync(new Point(800, 500), duration: 300);

input.MouseClick(MouseButton.Left);
input.MouseClick(MouseButton.XButton1);           // 侧键 1
await input.MouseDoubleClickAsync(MouseButton.Left);
input.MouseWheelRoll(-120);                       // 垂直向下一格
input.MouseHorizontalWheelRoll(120);              // 水平向右一格
await input.MouseDragAsync(new Point(100, 100), new Point(600, 400), duration: 500);

var desktop = InputSimulator.GetVirtualScreenBounds();
var cursor = InputSimulator.GetMousePosition();
var primaryPixels = InputSimulator.GetAbsoluteScreenSize();
var isWDown = InputSimulator.GetKeyDown(ScanCode.W);
var isSideButtonDown = InputSimulator.GetMouseDown(MouseButton.XButton1);
```

绝对定位使用物理像素，临时修改并恢复当前线程的 DPI 上下文，不改变宿主进程设置。越界坐标夹取到虚拟桌面的外接矩形边界；显示器间空隙由 Windows 决定最终指针位置。相对移动受系统指针速度和加速影响。滚轮参数是增量，**120 为一格，不是像素**。

`GetScreenSize()` 保留受调用线程 DPI 感知影响的主屏尺寸；`GetAbsoluteScreenSize()` 返回物理尺寸。无参数 `GetScalingFactorForWindow()` 返回两者宽度的比值，DPI 感知线程上通常为 1；查询指定窗口请传入有效句柄。

## 可重复的输入序列

```csharp
using var input = new InputController();
using var cancellation = new CancellationTokenSource();

var sequence = new InputSequence()
    .PressKey(ScanCode.W, holdTime: 100)
    .Wait(200)
    .MouseMove(20, 0)
    .MouseClick(MouseButton.Left)
    .MouseWheel(-120)
    .Wait(500);

await sequence.PlayAsync(input, repeat: 3, cancellationToken: cancellation.Token);
```

播放使用步骤快照，顺序执行，取消或失败后停止后续步骤。每个按键步骤只清理自己持有的按键，不会打断调用者已有的长按。延迟使用 .NET 调度器，不承诺实时精度。构建期间不要并发修改同一个序列。

## 后端扩展

可通过 `new InputController(new SendInputBackend())` 显式选择后端，也可实现 `IInputBackend` 接入其他输入设备或测试记录器。控制器拥有传入后端，释放控制器时关闭它；不要在多个控制器间共享同一后端实例。发送方法应同步完成，失败时抛出异常。

当前包只提供无需驱动的 `SendInputBackend`，不包含驱动安装、输入拦截或自动切换后端。

## 从 2.x 升级

原有公开方法签名保留，包括 `MouseWhellRoll`，建议改用 `MouseWheelRoll`。提供 `Move` 作为 `MouseMove` 的别名，兼容旧 README 示例。3.0 的行为调整：

- 单键 `char` 重载将字母大小写映射到同一物理键，支持数字、空格、回车、Tab、退格和 Esc。以前强转 `char` 传虚拟键码的写法应改用 `ScanCode`。
- 修复 `InputKeys` 恒按 Shift、ASCII 编码丢失字符的问题；无法映射的字符不再静默发送错误键码。
- 同步点击默认保持 30ms，重复 `KeyDown` 合并为一次持有。
- 非法参数、发送失败等会明确抛出异常；负的保持时间（包括 `-1`）不再被接受。
- 绝对定位及位置查询统一为物理像素；DPI 不感知的旧调用方需要检查坐标来源。
- 缩放比使用浮点除法，修复小数部分被截断的问题。
- 默认构建不再使用 .NET SDK 不支持的 PFX 强名称签名。要保持既有程序集身份，发布者需用原身份对应的 SNK 显式签名。

## 构建与验证

```powershell
dotnet build .\XFEExtension.NetCore.InputSimulator.sln -c Release
dotnet test .\XFEExtension.NetCore.InputSimulator.sln -c Release
```

测试通过记录后端验证编码、按键释放、取消、异常恢复、Unicode 和序列顺序，不向当前桌面发送真实键鼠输入，也不能替代游戏实测。`INPUT` 的 32/64 位布局、E0 扩展键、侧键及负坐标归一化均有回归测试。

库项目的 Release 构建会在 `bin/Release` 生成 NuGet 包。强名称签名构建示例（签名文件不包含在仓库中，测试使用默认无签名构建）：

```powershell
dotnet build .\XFEExtension.NetCore.InputSimulator\XFEExtension.NetCore.InputSimulator.csproj -c Release -p:SignAssembly=true -p:AssemblyOriginatorKeyFile=C:\keys\XFEstudio.snk
```

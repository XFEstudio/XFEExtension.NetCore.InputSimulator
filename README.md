# XFEExtension.NetCore.InputSimulator

.NET 10 Windows 键鼠输入库。4.0 使用项目自有的 **UMDF HID 驱动**，通过 Windows 自带 HID 栈提供虚拟键盘和鼠标。驱动 C 源码、安装器源码、INF、构建和验证脚本均在本仓库；没有 Interception 依赖或 SendInput 回退路径。

## 调用方如何使用

签名包的 DLL 内嵌 `XfeInputDriver.dll`、`XfeInput.inf`、`XfeInput.cat`、`XfeInputSetup.exe`、发布者公开证书和校验清单。**使用者不需要自己签名或申请证书。** 引用 DLL/NuGet、创建控制器和发送输入都不会自动安装驱动。由调用方使用 `DriverDeployment.CheckDriver()` 检测，在自己的安装按钮或命令中显式调用 `DriverDeployment.InstallEmbeddedDriver()` 才请求管理员授权。安装后可以普通权限运行，无需 Visual Studio、WDK、第三方输入驱动或额外下载。

```csharp
using XFEExtension.NetCore.InputSimulator;

// 只检测设备和协议；不安装、不请求 UAC、不占用输入会话。
var driver = DriverDeployment.CheckDriver();
if (!driver.IsAvailable)
{
    Console.WriteLine($"{driver.Status}: {driver.Message}");
    return; // 由调用方在需要时提供安装入口。
}

using var input = new InputController();
await input.PressKeyAsync(ScanCode.W, holdTime: 2000);
await input.PressCombinationAsync([ScanCode.LeftShift, ScanCode.W], holdTime: 1000);
input.MouseMove(100, -30);
await input.MouseClickAsync(MouseButton.Left);
input.MouseWheelRoll(-120);
```

在调用方的“安装或更新驱动”操作中单独执行：

```csharp
DriverDeployment.InstallEmbeddedDriver(); // 此处才请求 UAC 和所需的签名者信任。
Console.WriteLine(DriverDeployment.CheckDriver().Message);
```

`CheckDriver()` 返回 `Ready`、`NotDetected`、`Incompatible`、`Unavailable` 或 `UnsupportedPlatform`，并提供 `IsAvailable` 和 `Message`。`NotDetected` 也可能表示设备被禁用或尚未启动；`Ready` 表示设备可访问且协议兼容，不保证会话空闲或已安装最新驱动。检测不会抢占、释放或续约其他控制器的会话。直接创建控制器遇到驱动缺失时会抛出异常，提示显式检测和安装。

自签名包需要用户在首次安装时同意信任发布者的专用代码签名证书；它会加入本机 `Root` 和 `TrustedPublisher`，使系统信任该证书签名的代码。发布者私钥不会分发。设备安装策略仍然适用，系统也可能要求重启。安装器不更改 Secure Boot、内存完整性、测试模式或签名强制策略，不自动重启。

包有三种状态：`Build.ps1` 默认重新编译的驱动为 `unsigned` / `-driver-dev`，安装接口会拒绝该包；仓库内置的自签名包是 `self-signed` / `-driver-local`，需要信任专用证书；公共代码签名发行包标记为 `trusted-publisher`。本地信任方案已经在 Windows 11、Secure Boot 开启的机器上安装成功，但不是微软 WHQL 认证。4.0.1 驱动使用有效期至 **2036-09-23（UTC+8）**的专用 RSA 4096 位代码证书，并附加 RFC 3161 时间戳。证书持续复用于后续构建，期限和私钥备份详见 [签名与分发](driver/SIGNING.md)。

当前驱动支持 **Windows 11 x64**；C# 调用方可以是 x64 或 x86。调用方自己的 .NET 10 应用仍需运行时或采用自包含发布。

以下属性仅检查 DLL 内嵌包，不表示系统已经安装驱动；系统设备状态使用上面的 `CheckDriver()`：

```csharp
Console.WriteLine(DriverDeployment.HasEmbeddedDriver);
Console.WriteLine(DriverDeployment.IsEmbeddedDriverSigned);
Console.WriteLine(DriverDeployment.EmbeddedDriverSigningKind);
Console.WriteLine(DriverDeployment.EmbeddedDriverCertificateExpires);
Console.WriteLine(DriverDeployment.IsEmbeddedDriverTimestamped);
```

驱动文件安装进 Windows 驱动存储，临时提取文件在安装完成后清理。取消 UAC、签名不受信任、需要重启、设备被其他控制器占用等情况均会抛出明确异常。

## 功能与限制

| 功能 | 驱动行为 |
| --- | --- |
| 物理键、左右修饰键、组合键 | 将命名 `ScanCode` 转换为 HID Usage；支持同时 6 个普通键及 8 个修饰键 |
| 长按、取消、序列重复 | 支持；控制器释放及失联约三秒后尝试松开输入 |
| 鼠标左/右/中/侧键 | 支持 5 个按钮 |
| 鼠标移动 | 支持相对移动；大位移按有符号 16 位范围拆分 |
| 垂直、水平滚轮 | 支持；参数必须是 120 的整数倍，大增量分段提交 |
| `InputKeys` | 按前台键盘布局发送可映射字符，受输入法和修饰键状态影响 |
| `TypeText` / Unicode | 当前 HID 驱动不支持，明确抛出异常 |
| 绝对定位、平滑绝对移动/拖动 | 当前 HID 驱动不支持，明确抛出异常 |

虚拟 HID 设备仍可被游戏或其他应用识别和拒绝。已收到用户测试成功的反馈；不同目标环境和版本仍需各自验证，本项目不承诺所有游戏都接受输入。驱动采用 Windows 官方 [UMDF HID 架构](https://learn.microsoft.com/en-us/windows-hardware/drivers/wdf/creating-umdf-hid-minidrivers)，项目代码运行在用户态驱动宿主，内核桥接由系统自带组件负责。

输入会话是独占的：系统同一时间只允许一个实际输入控制器。请在应用内共享一个 `InputController`，或统一使用 `InputSimulator` 静态接口。静态接口在首次发送输入时建立长连接，直至进程退出；仅查询键状态、屏幕或鼠标位置不会打开驱动。不要同时混用静态发送接口和独立控制器。

## 取消与按键清理

```csharp
using var input = new InputController();
using var cancellation = new CancellationTokenSource(500);
try
{
    await input.PressCombinationAsync([ScanCode.LeftShift, ScanCode.W],
        holdTime: 2000, cancellationToken: cancellation.Token);
}
catch (OperationCanceledException) { }

var sequence = new InputSequence()
    .PressKey(ScanCode.W, 100)
    .Wait(200)
    .MouseMove(20, 0)
    .MouseClick(MouseButton.Left)
    .MouseWheel(-120);
await sequence.PlayAsync(input, repeat: 3);
```

`HoldKey` / `HoldMouseButton` 作用域支持嵌套；最后一个作用域退出时释放。`KeyDown` / `KeyUp` 则显式持有和释放。调用 `ReleaseAll` 前应先取消并等待正在执行的任务。释放失败会报告异常，不能保证故障设备已经完成释放。

驱动通过 64 字节 HID Feature 报告接收指令，检查协议版本、长度、保留位、随机会话标识和数值范围。报告队列容量为 64，满时明确报错；入队成功才更新按键状态。客户端每 500ms 续约，约三秒无有效心跳后驱动排队提交键鼠空状态；正常释放、休眠和恢复也重置状态。这是尽力清理机制，设备故障或宿主异常不能保证立即释放。无需拦截或修改现有硬件键鼠驱动。

## 交互式控制台

完成本地构建后，双击 `Start-InputConsole.cmd`，或打开发布的 `InputSimulator.Console.exe`。启动菜单和选择输入测试均不会自动安装。先选 `9` 检测驱动，需要安装时选 `I` 显式安装或更新，然后选择输入测试。

1. 打开目标程序，控制台输入 `2` 测试 W 长按 2 秒。
2. 在默认 5 秒倒计时内 Alt+Tab 切回目标，松开切换窗口使用的按键。
3. 查看是否产生输入，再切回控制台选择下一项。

`1` 短按 W，`3` 空格，`4` Shift + W，`5` 鼠标移动，`6` 鼠标按键，`7` 滚轮，`8` 自定义组合及重复次数，`9` 检测驱动状态，`I` 安装或更新驱动。`T` 修改倒计时，`K` 查看键名，`0` 退出。预演模式下 `9` 和 `I` 均跳过系统设备操作。

F8 在倒计时或执行中取消；控制台内也可以 Ctrl+C 取消。F8 通过系统键状态轮询检测，不屏蔽目标对 F8 的处理。预演不会连接驱动、安装驱动或向桌面发送输入：

```powershell
dotnet run --project .\XFEExtension.NetCore.InputSimulator.Console -c Release -- --dry-run
# 只检测设备及协议，不安装、不申请输入会话、不发送键鼠输入；就绪返回 0，否则返回 1：
dotnet run --project .\XFEExtension.NetCore.InputSimulator.Console -c Release -- --driver-check
# 显式安装或更新内嵌驱动，需要管理员授权：
dotnet run --project .\XFEExtension.NetCore.InputSimulator.Console -c Release -- --driver-install
```

## 从源码构建

仓库已包含 `driver/payload/win-x64` 下的完整已签名驱动包。克隆后可直接使用 .NET 10 SDK 构建库和控制台，沿用内置驱动的签名，无需 WDK、C++ 工具或签名私钥：

```powershell
dotnet build .\XFEExtension.NetCore.InputSimulator.sln -c Release -p:GeneratePackageOnBuild=false -warnaserror
```

需要修改并重新编译原生驱动或安装器时，**项目维护者**还需 Windows x64、PowerShell 7、Visual Studio C++ 桌面开发工具。脚本从 NuGet 自动还原固定版本的微软 WDK/SDK，不安装系统级 WDK。

```powershell
# 编译 UMDF 驱动和静态链接运行库的安装器，验证报告、会话和 INF，内嵌未签名开发产物。
pwsh -File .\Build.ps1

# XFEExtension.NetCore.XUnit 4.0.2 独立运行器；测试不会加载驱动或发送真实输入。
dotnet run --project .\XFEExtension.NetCore.InputSimulator.Tests -c Release --no-build -- --artifacts artifacts/tests
dotnet run --project .\XFEExtension.NetCore.InputSimulator.Tests -c Release --arch x86 -- --artifacts artifacts/tests-x86

# 带完整内嵌驱动负载的 DLL / NuGet 包。
dotnet pack .\XFEExtension.NetCore.InputSimulator -c Release --no-build -o .\artifacts\packages

# 自包含控制台单文件 EXE，运行机器不需要另外安装 .NET。
dotnet publish .\XFEExtension.NetCore.InputSimulator.Console -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:GeneratePackageOnBuild=false -o .\artifacts\input-console
```

`driver/Driver.c` 是 UMDF 驱动，`driver/Reports.c` 和 `driver/HidControl.c` 分别实现报告编码、队列和会话状态，`driver/Setup.c` 实现签名者信任及首次安装，`driver/Protocol.h` 与 C# 协议同步。原生构建输出在忽略版本控制的 `artifacts/driver-umdf`；嵌入负载 `driver/payload/win-x64` 中的 7 个发行文件已纳入 Git，包含公开证书，不包含私钥。Git 属性保留签名文件（包括 INF）的原始字节，避免换行转换破坏 CAT 校验。`Build.ps1` 会覆盖嵌入负载；更新仓库驱动包时应完成签名后一起提交全部 7 个文件，不要提交未签名开发产物。

## 签名和分发

签名由发布者完成，调用方不需要签名环境。本地自签名模式不需要微软硬件审核，要求每台机器首次明确同意信任专用代码证书。首次创建长期证书，后续构建复用已保存的身份：

```powershell
pwsh -File .\driver\New-SigningCertificate.ps1
pwsh -File .\Build.ps1 -LocalSigning
dotnet pack .\XFEExtension.NetCore.InputSimulator -c Release --no-build -o .\artifacts\packages
# 或一次生成签名 NuGet 和带 .NET 的签名控制台：
pwsh -File .\Publish-Local.ps1
```

不要分发私钥；请自行用密码加密备份到离线介质。时间戳不是永久安装保证，当前安装器仍会拒绝过期证书，需要在 2036 年期限前发布新证书签名的更新。已安装 30 天验证版时用 `--driver-install` 升级，普通连接不会自动替换兼容的旧驱动。完整步骤、信任范围和卸载方式见 [驱动签名与分发](driver/SIGNING.md)。

`Validate` 工作流构建 UMDF 驱动、验证原生测试和 C# 测试，并保存未签名驱动及开发 NuGet 包。`Publish Packages` 只接受 `driver/signed/win-x64` 中通过公共签名校验的发行负载；本地信任版通过本地命令分发。配置详见 [发布说明](.github/PUBLISHING.md)。

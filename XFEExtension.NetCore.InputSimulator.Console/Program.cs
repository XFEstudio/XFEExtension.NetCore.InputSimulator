using System.Text;
using Simulator = XFEExtension.NetCore.InputSimulator.InputSimulator;

namespace XFEExtension.NetCore.InputSimulator.ConsoleApp;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;
        if (args is ["--help"] or ["-h"])
        {
            Console.WriteLine("InputSimulator.Console.exe [--dry-run | --driver-check | --driver-install]");
            Console.WriteLine("无参数：交互式真实输入测试；--dry-run：只打印事件，跳过倒计时，不发送输入。");
            Console.WriteLine("真实输入使用内置 UMDF HID 驱动；首次需要管理员安装及签名者信任。--driver-check 仅验证连接和心跳，不按键、不移动鼠标。");
            Console.WriteLine("--driver-install：安装或更新为内嵌驱动包，更换签名证书时使用；需要管理员授权。");
            Console.WriteLine("选择测试后切回目标窗口；默认倒计时 5 秒，F8 取消；Ctrl+C 取消当前操作。");
            return 0;
        }

        if (args is ["--driver-check"] or ["--driver-install"])
        {
            try
            {
                if (args[0] == "--driver-install") DriverDeployment.InstallEmbeddedDriver();
                string name;
                using (var driver = new DriverBackend())
                {
                    name = driver.Name;
                    await Task.Delay(4000);
                }
                Console.WriteLine($"连接、心跳及释放成功：{name}；签名：{DriverDeployment.EmbeddedDriverSigningKind}；时间戳：{DriverDeployment.IsEmbeddedDriverTimestamped}；证书到期：{DriverDeployment.EmbeddedDriverCertificateExpires:O}");
                return 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error.Message); return 1; }
        }

        var preview = args is ["--dry-run"];
        if (args.Length != 0 && !preview)
        {
            Console.Error.WriteLine("不支持的参数。用 --help 查看帮助。");
            return 2;
        }
        if (!preview && !OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("真实输入测试只支持 Windows。");
            return 2;
        }
        if (!preview && Console.IsInputRedirected)
        {
            Console.Error.WriteLine("请在交互式控制台中运行；通过管道检查流程时请使用 --dry-run。");
            return 2;
        }

        Console.WriteLine("=== InputSimulator 手动测试 / .NET 10 ===");
        Console.WriteLine(preview ? "预演模式：只打印事件，不加载驱动或发送真实输入。" : "后端：项目自有 UMDF 虚拟 HID 驱动。启动菜单不会发送输入。");
        Console.WriteLine($"内嵌驱动：{(DriverDeployment.HasEmbeddedDriver ? "有" : "缺失")}；签名：{(DriverDeployment.IsEmbeddedDriverSigned ? "已校验" : "开发版，未签名，不能自动安装")}");
        Console.WriteLine($"签名类型：{DriverDeployment.EmbeddedDriverSigningKind}；证书到期：{DriverDeployment.EmbeddedDriverCertificateExpires:yyyy-MM-dd}");
        Console.WriteLine("驱动设备仍可被目标识别；发送成功不代表游戏已接受。");
        Console.WriteLine("真实测试时请在倒计时内 Alt+Tab 切回目标，并松开切换窗口使用的按键。");
        Console.WriteLine(preview
            ? "Ctrl+C 取消当前预演。0 退出。"
            : "F8 取消倒计时或执行中的测试；控制台内也可以 Ctrl+C 取消。0 退出。");

        var countdown = 5;
        var exitCode = 0;
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("1  W 短按 100ms          2  W 长按 2 秒");
            Console.WriteLine("3  空格 100ms            4  Shift + W 长按 2 秒");
            Console.WriteLine("5  鼠标向右相对移动      6  鼠标按键测试（可选左/右/中/侧键）");
            Console.WriteLine("7  滚轮向下一格          8  自定义键/组合键、保持时间和次数");
            Console.WriteLine("9  查看驱动状态（此驱动不支持直接 Unicode 输入和绝对鼠标定位）");
            Console.WriteLine($"T  设置倒计时（当前 {countdown} 秒）   K  查看可用键名   0  退出");
            Console.Write("请选择 > ");
            var choice = Console.ReadLine()?.Trim().ToUpperInvariant();
            if (choice is null or "0" or "Q") return exitCode;

            try
            {
                if (choice == "T")
                {
                    countdown = ReadNumber("倒计时秒数", countdown, 1, 30);
                    continue;
                }
                if (choice == "K")
                {
                    Console.WriteLine(string.Join(", ", Enum.GetNames<ScanCode>()));
                    Console.WriteLine("F8 保留为取消键。Ctrl / Shift / Alt / Esc 可作为左侧修饰键或 Escape 的简写。");
                    continue;
                }

                if (choice == "9")
                {
                    Console.WriteLine($"内嵌驱动：{DriverDeployment.HasEmbeddedDriver}；已校验签名：{DriverDeployment.IsEmbeddedDriverSigned}");
                    Console.WriteLine("签名版首次测试时自动安装；系统同时只允许一个控制器连接。最多同时按住 6 个普通键及 8 个修饰键。");
                    continue;
                }
                var test = CreateTest(choice);
                if (test is null)
                {
                    Console.WriteLine("请输入菜单中的编号或字母。");
                    continue;
                }
                await RunTestAsync(test, countdown, preview);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("已取消；本次持有的键已清理。切回控制台可再次选择。");
            }
            catch (Exception error)
            {
                exitCode = 1;
                Console.Error.WriteLine($"测试未完成：{error.Message}");
                if (error is AggregateException aggregate)
                    foreach (var inner in aggregate.Flatten().InnerExceptions)
                        Console.Error.WriteLine($"  {inner.Message}");
                Console.WriteLine("请按错误提示检查驱动签名、安装和设备占用情况。清理报错时请手动按下并松开对应键。");
            }
        }
    }

    private static ManualTest? CreateTest(string choice) => choice switch
    {
        "1" => new("W 短按 100ms", new InputSequence().PressKey(ScanCode.W, 100)),
        "2" => new("W 长按 2 秒", new InputSequence().PressKey(ScanCode.W, 2000)),
        "3" => new("空格 100ms", new InputSequence().PressKey(ScanCode.Space, 100)),
        "4" => new("Shift + W 长按 2 秒", new InputSequence().PressCombination([ScanCode.LeftShift, ScanCode.W], 2000)),
        "5" => new("鼠标向右相对移动 120 单位（12 次，每次 10；实际距离取决于目标）",
            new InputSequence().MouseMove(10, 0).Wait(20), 12),
        "6" => ReadMouseTest(),
        "7" => new("滚轮向下一格", new InputSequence().MouseWheel(-120)),
        "8" => ReadKeyboardTest(),
        _ => null
    };

    private static ManualTest ReadMouseTest()
    {
        Console.WriteLine("1 左键，2 右键，3 中键，4 侧键 XButton1，5 侧键 XButton2");
        var button = (MouseButton)(ReadNumber("鼠标键", 1, 1, 5) - 1);
        var hold = ReadNumber("保持毫秒", 100, 1, 10000);
        return new($"鼠标 {button}，保持 {hold}ms", new InputSequence().MouseClick(button, hold));
    }

    private static ManualTest ReadKeyboardTest()
    {
        ScanCode[] keys;
        while (true)
        {
            Console.Write("键名或组合（例如 W、Space、Ctrl+A；/cancel 返回菜单）[W] > ");
            var value = ReadValue();
            if (value.Length == 0) value = "W";
            var parts = value.Split('+', StringSplitOptions.TrimEntries);
            var parsed = parts.Select(ParseKey).ToArray();
            if (parsed.Length is < 1 or > 8 || parsed.Any(key => key is null or ScanCode.F8))
            {
                Console.WriteLine("请输入 1 到 8 个有效键名，用 + 分隔；F8 保留为取消键。主菜单 K 可查看键名。");
                continue;
            }
            keys = parsed.Select(key => key!.Value).Distinct().ToArray();
            break;
        }
        var hold = ReadNumber("保持毫秒", 100, 1, 10000);
        var repeat = ReadNumber("重复次数", 1, 1, 100);
        var interval = repeat > 1 ? ReadNumber("每次结束后等待毫秒", 500, 0, 10000) : 0;
        return new($"{string.Join(" + ", keys)}，保持 {hold}ms，重复 {repeat} 次，结束后等待 {interval}ms",
            new InputSequence().PressCombination(keys, hold).Wait(interval), repeat);
    }

    private static ScanCode? ParseKey(string value)
    {
        value = value.ToUpperInvariant() switch
        {
            "CTRL" => nameof(ScanCode.LeftControl),
            "SHIFT" => nameof(ScanCode.LeftShift),
            "ALT" => nameof(ScanCode.LeftAlt),
            "ESC" => nameof(ScanCode.Escape),
            _ when value.Length == 1 && char.IsAsciiDigit(value[0]) => $"D{value}",
            _ => value
        };
        // 只接受命名键；Enum.TryParse 的数字输入会被解释为底层枚举值。
        var name = Enum.GetNames<ScanCode>().FirstOrDefault(name => name.Equals(value, StringComparison.OrdinalIgnoreCase));
        return name is null ? null : Enum.Parse<ScanCode>(name);
    }

    private static int ReadNumber(string prompt, int defaultValue, int minimum, int maximum)
    {
        while (true)
        {
            Console.Write($"{prompt}（{minimum}..{maximum}，/cancel 返回菜单）[{defaultValue}] > ");
            var value = ReadValue();
            if (value.Length == 0) return defaultValue;
            if (int.TryParse(value, out var number) && number >= minimum && number <= maximum) return number;
            Console.WriteLine($"请输入 {minimum} 到 {maximum} 之间的整数。");
        }
    }

    private static string ReadValue(bool trim = true)
    {
        var value = Console.ReadLine();
        if (value is null || value.Trim().Equals("/cancel", StringComparison.OrdinalIgnoreCase))
            throw new OperationCanceledException();
        return trim ? value.Trim() : value;
    }

    private static async Task RunTestAsync(ManualTest test, int countdown, bool preview)
    {
        using var cancellation = new CancellationTokenSource();
        using var stopWatching = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (_, e) =>
        {
            e.Cancel = true;
            cancellation.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;
        var watcher = preview ? Task.CompletedTask : WatchStopKeyAsync(cancellation, stopWatching.Token);
        try
        {
            // Complete driver connection / first-time UAC installation before starting the countdown.
            using var input = new InputController(preview ? new PreviewBackend() : new DriverBackend());
            Console.WriteLine($"\n本次测试：{test.Name}");
            if (!preview)
            {
                Console.WriteLine("现在切回目标窗口，F8 可取消。");
                for (var seconds = countdown; seconds > 0; seconds--)
                {
                    Console.WriteLine($"  {seconds} 秒后执行…");
                    await Task.Delay(1000, cancellation.Token);
                }
            }
            cancellation.Token.ThrowIfCancellationRequested();
            // 每次测试独立拥有控制器；完成、取消和异常退出都会尝试清理。
            await test.Sequence.PlayAsync(input, test.Repeat, cancellation.Token);
        }
        finally
        {
            stopWatching.Cancel();
            try { await watcher; }
            finally { Console.CancelKeyPress -= cancelHandler; }
        }
        Console.WriteLine(preview
            ? "预演完成，没有发送真实输入。"
            : "输入已提交，按键已释放。是否识别请查看目标程序；切回控制台可再次选择。");
    }

    private static async Task WatchStopKeyAsync(CancellationTokenSource operation, CancellationToken stop)
    {
        try
        {
            while (true)
            {
                stop.ThrowIfCancellationRequested();
                if (Simulator.GetKeyDown(ScanCode.F8))
                {
                    operation.Cancel();
                    return;
                }
                await Task.Delay(20, stop);
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        catch
        {
            // 无法监测取消键时，停止操作，不让测试在失去取消能力后继续。
            operation.Cancel();
            throw;
        }
    }

    private sealed record ManualTest(string Name, InputSequence Sequence, int Repeat = 1);
}

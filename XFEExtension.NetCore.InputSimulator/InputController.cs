using System.Diagnostics;
using System.Drawing;
using XFEExtension.NetCore.InputSimulator.Native;

namespace XFEExtension.NetCore.InputSimulator;

/// <summary>独立的输入控制器，管理后端、按键生命周期和可取消的输入操作。</summary>
/// <remarks>拥有传入后端的所有权，Dispose 时释放本实例持有的按键并关闭后端。多个实例的按键状态相互独立。</remarks>
public sealed class InputController : IDisposable
{
    private readonly IInputBackend backend;
    private readonly object sync = new();
    private readonly Dictionary<Button, HeldButton> held = [];
    private readonly List<Button> pressOrder = [];
    private bool disposed;

    /// <summary>创建控制器；默认连接已安装的 UMDF HID 驱动，缺失时报告错误，不自动安装。</summary>
    public InputController(IInputBackend? backend = null) => this.backend = backend ?? new DriverBackend();

    /// <summary>正在使用的后端名称。</summary>
    public string BackendName => backend.Name;

    /// <summary>按住物理键，重复调用不会增加释放次数。</summary>
    public void KeyDown(ScanCode key) => SetManual(Button.Key(key), true);
    /// <summary>释放通过 KeyDown 持有的物理键。</summary>
    public void KeyUp(ScanCode key) => SetManual(Button.Key(key), false);
    /// <summary>按住字母、数字或常用控制键；大小写字母对应同一物理键。</summary>
    public void KeyDown(char key) => KeyDown(KeyboardMapping.FromCharacter(key));
    /// <summary>释放字母、数字或常用控制键。</summary>
    public void KeyUp(char key) => KeyUp(KeyboardMapping.FromCharacter(key));

    /// <summary>在返回的作用域内按住物理键；嵌套作用域全部结束后才释放。</summary>
    public IDisposable HoldKey(ScanCode key) => Acquire(Button.Key(key));
    /// <summary>在返回的作用域内按住鼠标键。</summary>
    public IDisposable HoldMouseButton(MouseButton button) => Acquire(Button.Mouse(button));

    /// <summary>按下并释放物理键；默认保持 30ms，便于按帧轮询的程序检测。</summary>
    public void PressKey(ScanCode key, int holdTime = 30) => PressCombination([key], holdTime);
    /// <summary>按下并释放字符对应的物理键。</summary>
    public void PressKey(char key, int holdTime = 30) => PressKey(KeyboardMapping.FromCharacter(key), holdTime);
    /// <summary>异步按键，取消或异常时也会尝试释放。</summary>
    public Task PressKeyAsync(ScanCode key, int holdTime = 30, CancellationToken cancellationToken = default) =>
        PressCombinationAsync([key], holdTime, cancellationToken);
    /// <summary>异步按下并释放字符对应的物理键。</summary>
    public Task PressKeyAsync(char key, int holdTime = 30, CancellationToken cancellationToken = default) =>
        PressKeyAsync(KeyboardMapping.FromCharacter(key), holdTime, cancellationToken);

    /// <summary>按参数顺序按下组合键，保持 30ms，再逆序释放。</summary>
    public void PressCombination(params ScanCode[] keys) => PressCombination(keys, 30);
    /// <summary>按顺序按下组合键，自定义保持时间，再逆序释放。</summary>
    public void PressCombination(IEnumerable<ScanCode> keys, int holdTime)
    {
        InputValidation.Delay(holdTime, nameof(holdTime));
        using var scope = HoldKeys(keys);
        if (holdTime > 0) Thread.Sleep(holdTime);
    }

    /// <summary>异步组合键；取消时逆序释放本操作持有的按键。</summary>
    public async Task PressCombinationAsync(IEnumerable<ScanCode> keys, int holdTime = 30, CancellationToken cancellationToken = default)
    {
        InputValidation.Delay(holdTime, nameof(holdTime));
        cancellationToken.ThrowIfCancellationRequested();
        await WithHeldAsync(HoldKeys(keys), () => Task.Delay(holdTime, cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>按当前前台窗口的键盘布局逐字符模拟按键。驱动不能直接注入 Unicode 文本。</summary>
    /// <remarks>遵循系统 CapsLock、修饰键及输入法状态；不保证最终文本与参数完全相同。</remarks>
    public void InputKeys(string keys)
    {
        EnsureAlive();
        foreach (var chord in KeyboardMapping.CharacterChords(keys))
            PressCombination(chord, 0);
    }

    /// <summary>按当前键盘布局输入字符，支持保持时间、间隔和取消。</summary>
    public async Task InputKeysAsync(string keys, int holdTime = 0, int delay = 0, CancellationToken cancellationToken = default)
    {
        InputValidation.Delay(holdTime, nameof(holdTime));
        InputValidation.Delay(delay, nameof(delay));
        EnsureAlive();
        cancellationToken.ThrowIfCancellationRequested();
        var chords = KeyboardMapping.CharacterChords(keys);
        for (var i = 0; i < chords.Length; i++)
        {
            await PressCombinationAsync(chords[i], holdTime, cancellationToken).ConfigureAwait(false);
            if (i + 1 < chords.Length)
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>发送 Unicode 文本，支持中文及代理对；目标必须支持 Unicode 文本输入。</summary>
    public void TypeText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        lock (sync)
        {
            CheckTextSupport();
            foreach (var character in text)
                SendCodeUnit(character);
        }
    }

    /// <summary>逐字符发送 Unicode 文本；完整代理对之间不会插入等待或取消。</summary>
    public async Task TypeTextAsync(string text, int delay = 0, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        InputValidation.Delay(delay, nameof(delay));
        lock (sync) CheckTextSupport();
        cancellationToken.ThrowIfCancellationRequested();
        for (var i = 0; i < text.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (sync)
            {
                CheckTextSupport();
                SendCodeUnit(text[i]);
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                    SendCodeUnit(text[++i]);
            }
            if (i + 1 < text.Length)
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>发送相对鼠标移动；系统鼠标加速可能影响最终指针位移。</summary>
    public void MouseMove(int x, int y) => Send(() => backend.MoveMouse(x, y, false, false));
    /// <summary>移动至虚拟桌面的物理像素坐标，支持副屏负坐标，越界时夹取到桌面边界。</summary>
    public void LocateTo(int x, int y) => LocateTo(new Point(x, y));
    /// <summary>移动至虚拟桌面的物理像素坐标。</summary>
    public void LocateTo(Point point)
    {
        var normalized = WindowsDesktop.Normalize(point, WindowsDesktop.Bounds(true));
        Send(() => backend.MoveMouse(normalized.X, normalized.Y, true, true));
    }
    /// <summary>移动至主屏的指定位置。</summary>
    public void LocateTo(ScreenPosition position) => LocateTo(WindowsDesktop.Position(position, WindowsDesktop.Bounds(false)));
    /// <summary>直接发送 0..65535 范围的归一化绝对坐标。</summary>
    public void LocateToNormalized(int x, int y, bool virtualDesktop = true)
    {
        InputValidation.Coordinates(x, y, true, virtualDesktop);
        Send(() => backend.MoveMouse(x, y, true, virtualDesktop));
    }

    /// <summary>按住鼠标键。</summary>
    public void MouseDown(MouseButton button) => SetManual(Button.Mouse(button), true);
    /// <summary>释放通过 MouseDown 持有的鼠标键。</summary>
    public void MouseUp(MouseButton button) => SetManual(Button.Mouse(button), false);
    /// <summary>点击鼠标，默认保持 30ms。</summary>
    public void MouseClick(MouseButton button, int holdTime = 30)
    {
        InputValidation.Delay(holdTime, nameof(holdTime));
        using var scope = HoldMouseButton(button);
        if (holdTime > 0) Thread.Sleep(holdTime);
    }
    /// <summary>可取消的鼠标点击，退出时释放。</summary>
    public async Task MouseClickAsync(MouseButton button, int holdTime = 30, CancellationToken cancellationToken = default)
    {
        InputValidation.Delay(holdTime, nameof(holdTime));
        cancellationToken.ThrowIfCancellationRequested();
        await WithHeldAsync(HoldMouseButton(button), () => Task.Delay(holdTime, cancellationToken)).ConfigureAwait(false);
    }
    /// <summary>鼠标双击；interval 是两次点击之间的间隔，单位 ms。</summary>
    public async Task MouseDoubleClickAsync(MouseButton button, int interval = 100, CancellationToken cancellationToken = default)
    {
        InputValidation.Delay(interval, nameof(interval));
        await MouseClickAsync(button, 30, cancellationToken).ConfigureAwait(false);
        await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
        await MouseClickAsync(button, 30, cancellationToken).ConfigureAwait(false);
    }
    /// <summary>垂直滚动；120 为一格，正值向上。</summary>
    public void MouseWheelRoll(int delta) => Send(() => backend.ScrollMouse(delta, false));
    /// <summary>水平滚动；120 为一格，正值向右。</summary>
    public void MouseHorizontalWheelRoll(int delta) => Send(() => backend.ScrollMouse(delta, true));

    /// <summary>从当前位置平滑移动到目标物理像素坐标；duration 为毫秒。</summary>
    public Task LocateToAsync(Point destination, int duration = 250, CancellationToken cancellationToken = default)
    {
        InputValidation.Delay(duration, nameof(duration));
        EnsureAlive();
        cancellationToken.ThrowIfCancellationRequested();
        return MoveBetweenAsync(WindowsDesktop.CursorPosition(), destination, duration, cancellationToken);
    }

    /// <summary>拖动鼠标；取消或异常时释放本操作按住的鼠标键。</summary>
    public async Task MouseDragAsync(Point start, Point end, int duration = 250, MouseButton button = MouseButton.Left,
        CancellationToken cancellationToken = default)
    {
        InputValidation.Delay(duration, nameof(duration));
        InputValidation.Button(button);
        cancellationToken.ThrowIfCancellationRequested();
        LocateTo(start);
        await WithHeldAsync(HoldMouseButton(button), () => MoveBetweenAsync(start, end, duration, cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>尝试逆序释放本控制器持有的所有键。任何一次释放失败也会继续释放其他键。</summary>
    /// <remarks>调用前请取消并等待仍在运行的宏或序列，否则后续步骤仍可能再次按键。</remarks>
    public void ReleaseAll()
    {
        lock (sync)
        {
            CheckDisposed();
            ReleaseAllCore();
        }
    }

    private async Task MoveBetweenAsync(Point start, Point end, int duration, CancellationToken token)
    {
        var timer = Stopwatch.StartNew();
        while (duration > 0 && timer.ElapsedMilliseconds < duration)
        {
            token.ThrowIfCancellationRequested();
            var progress = Math.Clamp(timer.Elapsed.TotalMilliseconds / duration, 0, 1);
            LocateTo(new Point((int)Math.Round(start.X + ((double)end.X - start.X) * progress),
                (int)Math.Round(start.Y + ((double)end.Y - start.Y) * progress)));
            await Task.Delay(Math.Min(8, Math.Max(1, duration - (int)timer.ElapsedMilliseconds)), token).ConfigureAwait(false);
        }
        token.ThrowIfCancellationRequested();
        LocateTo(end);
    }

    private void CheckTextSupport()
    {
        CheckDisposed();
        if (!backend.SupportsUnicodeText)
            throw new NotSupportedException($"后端 {backend.Name} 不支持 Unicode 文本。");
    }

    private void SendCodeUnit(char character)
    {
        backend.SendUnicode(character, false);
        backend.SendUnicode(character, true);
    }

    private IDisposable HoldKeys(IEnumerable<ScanCode> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var buttons = keys.Distinct().Select(Button.Key).ToArray();
        if (buttons.Length == 0)
            throw new ArgumentException("组合键不能为空。", nameof(keys));
        List<IDisposable> leases = [];
        lock (sync)
        {
            CheckDisposed();
            try
            {
                foreach (var button in buttons) leases.Add(Acquire(button));
            }
            catch (Exception error)
            {
                try { DisposeReverse(leases); }
                catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
                throw;
            }
        }
        return new Lease(() => DisposeReverse(leases));
    }

    private void SetManual(Button button, bool down)
    {
        lock (sync)
        {
            CheckDisposed();
            if (down)
            {
                var state = GetOrPress(button);
                state.Manual = true;
            }
            else if (held.TryGetValue(button, out var state))
            {
                state.Manual = false;
                if (state.Leases == 0) Release(button);
            }
            else
            {
                SendButton(button, true);
            }
        }
    }

    private IDisposable Acquire(Button button)
    {
        lock (sync)
        {
            CheckDisposed();
            var state = GetOrPress(button);
            state.Leases++;
            return new Lease(() =>
            {
                lock (sync)
                {
                    // ReleaseAll 或 Dispose 后，旧作用域不得释放后来新按下的同名按键。
                    if (!held.TryGetValue(button, out var current) || !ReferenceEquals(current, state)) return;
                    state.Leases--;
                    if (state.Leases == 0 && !state.Manual) Release(button);
                }
            });
        }
    }

    private HeldButton GetOrPress(Button button)
    {
        if (!held.TryGetValue(button, out var state))
        {
            SendButton(button, false);
            state = new HeldButton();
            held.Add(button, state);
            pressOrder.Add(button);
        }
        return state;
    }

    private void Release(Button button)
    {
        SendButton(button, true);
        held.Remove(button);
        pressOrder.Remove(button);
    }

    private void SendButton(Button button, bool up)
    {
        if (button.IsMouse) backend.SendMouseButton((MouseButton)button.Code, up);
        else backend.SendKey((ScanCode)button.Code, up);
    }

    private void ReleaseAllCore()
    {
        List<Exception> errors = [];
        foreach (var button in pressOrder.AsEnumerable().Reverse().ToArray())
        {
            try { Release(button); }
            catch (Exception error) { errors.Add(error); }
        }
        if (errors.Count > 0) throw new AggregateException("部分按键释放失败，可以再次调用 ReleaseAll 重试。", errors);
    }

    private static void DisposeReverse(List<IDisposable> leases)
    {
        List<Exception> errors = [];
        for (var i = leases.Count - 1; i >= 0; i--)
        {
            try { leases[i].Dispose(); }
            catch (Exception error) { errors.Add(error); }
        }
        if (errors.Count > 0) throw new AggregateException("部分按键释放失败。", errors);
    }

    private static async Task WithHeldAsync(IDisposable scope, Func<Task> operation)
    {
        Exception? operationError = null;
        try { await operation().ConfigureAwait(false); }
        catch (Exception error)
        {
            operationError = error;
            throw;
        }
        finally
        {
            try { scope.Dispose(); }
            catch (Exception releaseError) when (operationError is not null)
            {
                throw new AggregateException("操作和按键释放均未完成。", operationError, releaseError);
            }
        }
    }

    private void Send(Action action)
    {
        lock (sync)
        {
            CheckDisposed();
            action();
        }
    }

    private void EnsureAlive() { lock (sync) CheckDisposed(); }
    private void CheckDisposed() => ObjectDisposedException.ThrowIf(disposed, this);

    /// <summary>释放持有的按键并关闭后端；即使有释放失败，也会尝试其他按键和后端清理。</summary>
    public void Dispose()
    {
        lock (sync)
        {
            if (disposed) return;
            List<Exception> errors = [];
            try { ReleaseAllCore(); }
            catch (Exception error) { errors.Add(error); }
            disposed = true;
            held.Clear();
            pressOrder.Clear();
            try { backend.Dispose(); }
            catch (Exception error) { errors.Add(error); }
            if (errors.Count > 0) throw new AggregateException("控制器清理失败。", errors);
        }
    }

    private readonly record struct Button(bool IsMouse, ushort Code)
    {
        internal static Button Key(ScanCode key) { InputValidation.Key(key); return new(false, (ushort)key); }
        internal static Button Mouse(MouseButton button) { InputValidation.Button(button); return new(true, (ushort)button); }
    }

    private sealed class HeldButton
    {
        internal bool Manual;
        internal int Leases;
    }

    private sealed class Lease(Action release) : IDisposable
    {
        private Action? release = release;
        public void Dispose() => Interlocked.Exchange(ref release, null)?.Invoke();
    }
}

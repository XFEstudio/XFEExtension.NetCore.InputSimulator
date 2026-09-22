using XFEExtension.NetCore.InputSimulator.Native;

namespace XFEExtension.NetCore.InputSimulator;

/// <summary>可重复播放的输入序列。构建时不要并发修改；播放时使用步骤快照。</summary>
/// <remarks>每个按键步骤独立管理释放，不会释放调用者在序列外持有的按键。</remarks>
public sealed class InputSequence
{
    private readonly List<Func<InputController, CancellationToken, Task>> steps = [];

    /// <summary>步骤数。</summary>
    public int Count => steps.Count;

    /// <summary>添加物理按键步骤。</summary>
    public InputSequence PressKey(ScanCode key, int holdTime = 30)
    {
        InputValidation.Key(key);
        InputValidation.Delay(holdTime, nameof(holdTime));
        steps.Add((input, token) => input.PressKeyAsync(key, holdTime, token));
        return this;
    }

    /// <summary>添加组合键步骤；复制参数，后续修改原数组不会改变序列。</summary>
    public InputSequence PressCombination(IEnumerable<ScanCode> keys, int holdTime = 30)
    {
        ArgumentNullException.ThrowIfNull(keys);
        InputValidation.Delay(holdTime, nameof(holdTime));
        var copy = keys.Distinct().ToArray();
        if (copy.Length == 0) throw new ArgumentException("组合键不能为空。", nameof(keys));
        foreach (var key in copy) InputValidation.Key(key);
        steps.Add((input, token) => input.PressCombinationAsync(copy, holdTime, token));
        return this;
    }

    /// <summary>添加 Unicode 文本输入步骤。</summary>
    public InputSequence TypeText(string text, int delay = 0)
    {
        ArgumentNullException.ThrowIfNull(text);
        InputValidation.Delay(delay, nameof(delay));
        steps.Add((input, token) => input.TypeTextAsync(text, delay, token));
        return this;
    }

    /// <summary>添加鼠标点击步骤。</summary>
    public InputSequence MouseClick(MouseButton button, int holdTime = 30)
    {
        InputValidation.Button(button);
        InputValidation.Delay(holdTime, nameof(holdTime));
        steps.Add((input, token) => input.MouseClickAsync(button, holdTime, token));
        return this;
    }

    /// <summary>添加相对鼠标移动步骤。</summary>
    public InputSequence MouseMove(int x, int y)
    {
        steps.Add((input, _) => { input.MouseMove(x, y); return Task.CompletedTask; });
        return this;
    }

    /// <summary>添加垂直或水平滚动步骤；120 为一格。</summary>
    public InputSequence MouseWheel(int delta, bool horizontal = false)
    {
        steps.Add((input, _) =>
        {
            if (horizontal) input.MouseHorizontalWheelRoll(delta);
            else input.MouseWheelRoll(delta);
            return Task.CompletedTask;
        });
        return this;
    }

    /// <summary>添加毫秒延迟。</summary>
    public InputSequence Wait(int milliseconds)
    {
        InputValidation.Delay(milliseconds, nameof(milliseconds));
        steps.Add((_, token) => Task.Delay(milliseconds, token));
        return this;
    }

    /// <summary>顺序播放指定次数。取消或发送失败时停止，按键步骤负责释放自己持有的键。</summary>
    public async Task PlayAsync(InputController input, int repeat = 1, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentOutOfRangeException.ThrowIfLessThan(repeat, 1);
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = steps.ToArray();
        for (var iteration = 0; iteration < repeat; iteration++)
        {
            foreach (var step in snapshot)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await step(input, cancellationToken).ConfigureAwait(false);
            }
            if (iteration + 1 < repeat)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }
        }
    }
}

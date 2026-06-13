namespace LockPicker;

/// <summary>
/// Аргументы события прогресса выполнения последовательности.
/// </summary>
public sealed class ProgressEventArgs : EventArgs
{
    public int StepNumber { get; init; }      // номер текущего шага (1-based)
    public int TotalSteps { get; init; }      // всего шагов
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Движок выполнения взлома: переводит список <see cref="LockStep"/> в нажатия клавиш
/// с навигацией между пластинами. Текущая пластина отслеживается, чтобы корректно
/// перемещаться клавишами W (следующая) и S (предыдущая).
/// </summary>
public sealed class LockPickEngine
{
    // Виртуальные коды клавиш (WinUser.h).
    private const ushort VK_W = 0x57; // следующая пластина
    private const ushort VK_A = 0x41; // повернуть влево
    private const ushort VK_S = 0x53; // предыдущая пластина
    private const ushort VK_D = 0x44; // повернуть вправо

    private readonly IntPtr _gameWindow;
    private readonly int _delayMs;

    // Текущая позиция (индекс пластины, 1-based). При старте считаем, что
    // фокус на первой пластине.
    private int _currentPlate = 1;

    public event EventHandler<ProgressEventArgs>? Progress;

    public LockPickEngine(IntPtr gameWindow, int delayMs, int startPlate = 1)
    {
        _gameWindow = gameWindow;
        _delayMs = Math.Max(0, delayMs);
        _currentPlate = Math.Max(1, startPlate);
    }

    /// <summary>
    /// Выполняет последовательность шагов. Прерывается по токену отмены.
    /// </summary>
    public async Task RunAsync(IReadOnlyList<LockStep> steps, CancellationToken token)
    {
        // Активируем окно игры перед началом.
        NativeMethods.BringWindowToFront(_gameWindow);
        await DelayAsync(token); // даём окну время получить фокус

        for (int i = 0; i < steps.Count; i++)
        {
            token.ThrowIfCancellationRequested();

            LockStep step = steps[i];

            // 1. Навигация к нужной пластине.
            await NavigateToPlateAsync(step.PlateIndex, i + 1, steps.Count, token);

            // 2. Поворот пластины в нужную сторону.
            ushort turnKey = step.Direction == TurnDirection.Left ? VK_A : VK_D;
            string dirText = step.Direction == TurnDirection.Left ? "Влево ◀ (A)" : "Вправо ▶ (D)";

            ReportProgress(i + 1, steps.Count,
                $"Шаг {i + 1}: Пластина {step.PlateIndex} → поворот {dirText}");

            NativeMethods.TapKey(turnKey);
            await DelayAsync(token);
        }

        ReportProgress(steps.Count, steps.Count, "Последовательность завершена.");
    }

    /// <summary>
    /// Перемещает фокус на целевую пластину, нажимая W или S нужное число раз.
    /// </summary>
    private async Task NavigateToPlateAsync(
        int targetPlate, int stepNumber, int totalSteps, CancellationToken token)
    {
        int diff = targetPlate - _currentPlate;

        if (diff == 0)
        {
            return; // уже на нужной пластине
        }

        // diff > 0 — двигаемся вперёд (W), diff < 0 — назад (S).
        ushort navKey = diff > 0 ? VK_W : VK_S;
        string navText = diff > 0 ? "W (след)" : "S (пред)";
        int moves = Math.Abs(diff);

        for (int m = 0; m < moves; m++)
        {
            token.ThrowIfCancellationRequested();

            ReportProgress(stepNumber, totalSteps,
                $"Шаг {stepNumber}: переход к пластине {targetPlate} — нажатие {navText}");

            NativeMethods.TapKey(navKey);
            await DelayAsync(token);
        }

        _currentPlate = targetPlate;
    }

    private async Task DelayAsync(CancellationToken token)
    {
        if (_delayMs > 0)
        {
            await Task.Delay(_delayMs, token);
        }
    }

    private void ReportProgress(int step, int total, string message)
    {
        Progress?.Invoke(this, new ProgressEventArgs
        {
            StepNumber = step,
            TotalSteps = total,
            Message = message
        });
    }
}

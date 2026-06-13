namespace LockPicker;

/// <summary>
/// Направление поворота пластины.
/// </summary>
public enum TurnDirection
{
    Left,   // Влево  ◀  -> клавиша A
    Right   // Вправо ▶  -> клавиша D
}

/// <summary>
/// Один шаг взлома: к какой пластине перейти и в какую сторону её повернуть.
/// PlateIndex — 1-based (Пластина I = 1, II = 2 и т.д.).
/// RepeatCount — сколько раз подряд повернуть пластину (по умолчанию 1).
/// </summary>
public sealed class LockStep
{
    public int PlateIndex { get; init; }
    public TurnDirection Direction { get; init; }

    // Число повторений поворота (например, "× 3"). Минимум 1.
    public int RepeatCount { get; init; } = 1;

    public override string ToString()
    {
        string dir = Direction == TurnDirection.Left ? "Влево ◀" : "Вправо ▶";
        string repeat = RepeatCount > 1 ? $" × {RepeatCount}" : string.Empty;
        return $"Пластина {PlateIndex} → {dir}{repeat}";
    }
}

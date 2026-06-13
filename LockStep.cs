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
/// </summary>
public sealed class LockStep
{
    public int PlateIndex { get; init; }
    public TurnDirection Direction { get; init; }

    public override string ToString()
    {
        string dir = Direction == TurnDirection.Left ? "Влево ◀" : "Вправо ▶";
        return $"Пластина {PlateIndex} → {dir}";
    }
}

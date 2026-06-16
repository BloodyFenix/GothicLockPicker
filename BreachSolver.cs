namespace LockPicker;

/// <summary>
/// Логическая модель замка «Сундуки Готики» и BFS-решатель.
/// Порт с JavaScript-движка SystematicBreachGame.
///
/// Каждая пластина имеет позицию 0..6, цель — все пластины в центре (позиция 3).
/// Матрица связей matrix[i][j] описывает, как сдвиг пластины i влияет на пластину j:
///   1  — тянет в ту же сторону, -1 — в противоположную, 0 — не связаны.
/// Диагональ matrix[i][i] всегда равна 1 (пластина двигает сама себя).
/// </summary>
public sealed class BreachSolver
{
    /// <summary>Центральная (целевая) позиция каждой пластины.</summary>
    public const int Center = 3;

    /// <summary>Минимальная позиция паза.</summary>
    public const int MinPos = 0;

    /// <summary>Максимальная позиция паза.</summary>
    public const int MaxPos = 6;

    // Возможные элементарные ходы пластины: влево и вправо.
    private static readonly int[] Deltas = { -1, 1 };

    private readonly int _numPlates;
    private readonly int[,] _matrix;

    /// <summary>Количество пластин в замке.</summary>
    public int NumPlates => _numPlates;

    /// <param name="numPlates">Число пластин.</param>
    /// <param name="matrix">Квадратная матрица связей размером numPlates × numPlates.</param>
    public BreachSolver(int numPlates, int[,] matrix)
    {
        _numPlates = numPlates;
        _matrix = matrix;
    }

    /// <summary>
    /// Находит кратчайший путь от стартовых позиций к цели (все в центре)
    /// поиском в ширину. Возвращает null, если замок неразрешим.
    /// Каждый элемент пути — снимок позиций всех пластин.
    /// </summary>
    public List<int[]>? SolveBfs(int[] startPositions)
    {
        long startKey = EncodeState(startPositions);
        long targetKey = EncodeState(TargetState());

        // Ранний выход: замок уже открыт.
        if (startKey == targetKey)
        {
            return new List<int[]> { (int[])startPositions.Clone() };
        }

        var queue = new List<long> { startKey };
        int head = 0;
        var parent = new Dictionary<long, long>();
        var visited = new HashSet<long> { startKey };

        bool solved = false;

        while (head < queue.Count)
        {
            long currentKey = queue[head++];
            if (currentKey == targetKey)
            {
                solved = true;
                break;
            }

            int[] state = DecodeState(currentKey);

            for (int i = 0; i < _numPlates; i++)
            {
                foreach (int delta in Deltas)
                {
                    if (!TryApplyMove(state, i, delta, out int[] next))
                    {
                        continue;
                    }

                    long nextKey = EncodeState(next);
                    if (visited.Add(nextKey))
                    {
                        parent[nextKey] = currentKey;
                        queue.Add(nextKey);
                    }
                }
            }
        }

        if (!solved)
        {
            return null;
        }

        // Восстанавливаем путь от цели к старту.
        var path = new List<int[]>();
        long curr = targetKey;
        while (true)
        {
            path.Add(DecodeState(curr));
            if (curr == startKey)
            {
                break;
            }
            curr = parent[curr];
        }

        path.Reverse();
        return path;
    }

    /// <summary>
    /// Преобразует путь решения в список шагов <see cref="LockStep"/>.
    /// Подряд идущие одинаковые ходы объединяются в один шаг с RepeatCount.
    /// Возвращает пустой список, если путь отсутствует или замок уже открыт.
    /// </summary>
    public List<LockStep> GetSolutionSteps(int[] startPositions)
    {
        List<int[]>? path = SolveBfs(startPositions);
        if (path is null || path.Count < 2)
        {
            return new List<LockStep>();
        }

        var rawMoves = ExtractMoves(path);

        // Объединяем подряд идущие одинаковые ходы в шаги с повторами.
        var steps = new List<LockStep>();
        foreach ((int plateIdx, int delta) in rawMoves)
        {
            TurnDirection dir = delta > 0 ? TurnDirection.Right : TurnDirection.Left;

            if (steps.Count > 0)
            {
                LockStep last = steps[^1];
                if (last.PlateIndex == plateIdx + 1 && last.Direction == dir)
                {
                    steps[^1] = new LockStep
                    {
                        PlateIndex = last.PlateIndex,
                        Direction = last.Direction,
                        RepeatCount = last.RepeatCount + 1
                    };
                    continue;
                }
            }

            steps.Add(new LockStep
            {
                PlateIndex = plateIdx + 1, // 0-based -> 1-based
                Direction = dir,
                RepeatCount = 1
            });
        }

        return steps;
    }

    /// <summary>
    /// Восстанавливает по пути список элементарных ходов (индекс пластины, направление).
    /// </summary>
    private List<(int plateIdx, int delta)> ExtractMoves(List<int[]> path)
    {
        var moves = new List<(int, int)>();

        for (int k = 0; k < path.Count - 1; k++)
        {
            int[] current = path[k];
            int[] next = path[k + 1];

            int foundPlate = -1;
            int foundDelta = 0;

            // Ищем пластину, сдвиг которой по матрице объясняет переход.
            for (int i = 0; i < _numPlates; i++)
            {
                int delta = next[i] - current[i];
                if (delta == 0)
                {
                    continue;
                }

                bool matches = true;
                for (int j = 0; j < _numPlates; j++)
                {
                    int expectedShift = _matrix[i, j] * delta;
                    if (next[j] - current[j] != expectedShift)
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                {
                    foundPlate = i;
                    foundDelta = delta;
                    break;
                }
            }

            // Резервный вариант: берём первую сдвинувшуюся пластину.
            if (foundPlate == -1)
            {
                for (int i = 0; i < _numPlates; i++)
                {
                    int delta = next[i] - current[i];
                    if (delta != 0)
                    {
                        foundPlate = i;
                        foundDelta = delta > 0 ? 1 : -1;
                        break;
                    }
                }
            }

            moves.Add((foundPlate, foundDelta > 0 ? 1 : -1));
        }

        return moves;
    }

    /// <summary>
    /// Пытается сдвинуть пластину plateIdx на delta, распространяя влияние по матрице.
    /// Возвращает false, если любая связанная пластина выходит за границы [0..6].
    /// </summary>
    private bool TryApplyMove(int[] state, int plateIdx, int delta, out int[] next)
    {
        next = (int[])state.Clone();
        for (int j = 0; j < _numPlates; j++)
        {
            int influence = _matrix[plateIdx, j];
            if (influence == 0)
            {
                continue;
            }

            next[j] += influence * delta;
            if (next[j] < MinPos || next[j] > MaxPos)
            {
                return false;
            }
        }

        return true;
    }

    private int[] TargetState()
    {
        var target = new int[_numPlates];
        for (int i = 0; i < _numPlates; i++)
        {
            target[i] = Center;
        }
        return target;
    }

    /// <summary>Кодирует состояние в целочисленный ключ по основанию 7.</summary>
    private long EncodeState(int[] state)
    {
        long key = 0;
        long baseVal = 1;
        for (int i = 0; i < state.Length; i++)
        {
            key += state[i] * baseVal;
            baseVal *= 7;
        }
        return key;
    }

    private int[] DecodeState(long key)
    {
        var state = new int[_numPlates];
        long temp = key;
        for (int i = 0; i < _numPlates; i++)
        {
            state[i] = (int)(temp % 7);
            temp /= 7;
        }
        return state;
    }
}

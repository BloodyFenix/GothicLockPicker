namespace LockPicker;

/// <summary>
/// Логическая модель замка «Сундуки Готики» и решатель последовательности взлома.
///
/// Каждая пластина имеет позицию 0..6, цель — все пластины в центре (позиция 3).
/// Матрица связей matrix[i][j] описывает, как сдвиг пластины i влияет на пластину j:
///   1  — тянет в ту же сторону, -1 — в противоположную, 0 — не связаны.
/// Диагональ matrix[i][i] всегда равна 1 (пластина двигает сама себя).
///
/// Доступны два режима поиска:
///   • <see cref="GetSolutionSteps"/> — минимум поворотов (быстрый BFS, веса = 1);
///   • <see cref="GetSolutionStepsWeighted"/> — минимум суммарного числа нажатий
///     с учётом перемещений фокуса между пластинами. Чтобы повернуть пластину p,
///     нужно сначала перевести на неё фокус (|p − фокус| нажатий навигации) и затем
///     нажать поворот — итого |p − фокус| + 1 нажатие. Это и есть реальное время
///     взлома. Используется Дейкстра по состоянию «(позиции, фокус)» с отсечением
///     по верхней границе (стоимости BFS-решения), что резко сокращает перебор.
/// </summary>
public sealed class BreachSolver
{
    /// <summary>Центральная (целевая) позиция каждой пластины.</summary>
    public const int Center = 3;

    /// <summary>Минимальная позиция паза.</summary>
    public const int MinPos = 0;

    /// <summary>Максимальная позиция паза.</summary>
    public const int MaxPos = 6;

    // Число возможных позиций пластины (0..6) — основание кодирования состояния.
    private const int PosBase = MaxPos - MinPos + 1; // 7

    // Возможные элементарные ходы пластины: влево и вправо.
    private static readonly int[] Deltas = { -1, 1 };

    private readonly int _numPlates;

    // Предвычисленные степени основания 7 для быстрого кодирования позиций.
    private readonly long[] _pow;
    // Плоское представление матрицы влияний (быстрый доступ без двумерной индексации).
    private readonly int[] _flatMatrix;

    /// <summary>Количество пластин в замке.</summary>
    public int NumPlates => _numPlates;

    /// <param name="numPlates">Число пластин.</param>
    /// <param name="matrix">Квадратная матрица связей размером numPlates × numPlates.</param>
    public BreachSolver(int numPlates, int[,] matrix)
    {
        _numPlates = numPlates;

        _pow = new long[numPlates];
        long p = 1;
        for (int i = 0; i < numPlates; i++)
        {
            _pow[i] = p;
            p *= PosBase;
        }

        _flatMatrix = new int[numPlates * numPlates];
        for (int i = 0; i < numPlates; i++)
        {
            for (int j = 0; j < numPlates; j++)
            {
                _flatMatrix[i * numPlates + j] = matrix[i, j];
            }
        }
    }

    /// <summary>
    /// Находит последовательность шагов с минимальным числом поворотов (быстрый BFS).
    /// Подряд идущие одинаковые повороты объединяются в один шаг с RepeatCount.
    /// Возвращает пустой список, если замок уже открыт либо неразрешим.
    /// </summary>
    public List<LockStep> GetSolutionSteps(int[] startPositions)
    {
        long startKey = EncodeState(startPositions);
        long targetKey = TargetKey();
        if (startKey == targetKey)
        {
            return new List<LockStep>();
        }

        List<(int plateIdx, int delta)>? moves = SolveMinTurns(startKey, targetKey, default);
        return moves is null ? new List<LockStep>() : BuildSteps(moves);
    }

    /// <summary>
    /// Находит последовательность шагов, минимальную по суммарному числу нажатий
    /// (повороты + перемещения фокуса между пластинами). Может прерываться токеном.
    /// Возвращает пустой список, если замок уже открыт либо неразрешим.
    /// </summary>
    /// <param name="startPositions">Стартовые позиции пластин (0..6).</param>
    /// <param name="startFocus">Индекс пластины в фокусе на старте (0-based).</param>
    /// <param name="token">Токен отмены длительного расчёта.</param>
    public List<LockStep> GetSolutionStepsWeighted(
        int[] startPositions, int startFocus, CancellationToken token)
    {
        int focus0 = Math.Clamp(startFocus, 0, _numPlates - 1);

        long startKey = EncodeState(startPositions);
        long targetKey = TargetKey();
        if (startKey == targetKey)
        {
            return new List<LockStep>();
        }

        // Фаза 1: BFS — разрешимость и путь с минимумом поворотов.
        List<(int plateIdx, int delta)>? bfsMoves = SolveMinTurns(startKey, targetKey, token);
        if (bfsMoves is null)
        {
            return new List<LockStep>(); // неразрешим
        }

        // Реальная стоимость BFS-решения (с перемещениями фокуса) — верхняя граница.
        int upperBound = SequenceCost(bfsMoves, focus0);

        // Фаза 2: оптимизация по числу нажатий с отсечением по верхней границе.
        List<(int plateIdx, int delta)> best =
            SolveWeighted(startKey, targetKey, focus0, upperBound, token) ?? bfsMoves;

        return BuildSteps(best);
    }

    /// <summary>Суммарная стоимость последовательности ходов (нажатия фокуса + повороты).</summary>
    private static int SequenceCost(List<(int plateIdx, int delta)> moves, int focus0)
    {
        int cost = 0;
        int focus = focus0;
        foreach ((int plateIdx, _) in moves)
        {
            cost += Math.Abs(plateIdx - focus) + 1;
            focus = plateIdx;
        }
        return cost;
    }

    /// <summary>Объединяет элементарные ходы в шаги <see cref="LockStep"/> с повторами.</summary>
    private static List<LockStep> BuildSteps(List<(int plateIdx, int delta)> moves)
    {
        var steps = new List<LockStep>();
        foreach ((int plateIdx, int delta) in moves)
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
    /// Фаза 1: BFS по позициям (без фокуса). Путь с минимальным числом поворотов.
    /// Возвращает список ходов или null, если цель недостижима.
    /// </summary>
    private List<(int plateIdx, int delta)>? SolveMinTurns(
        long startKey, long targetKey, CancellationToken token)
    {
        int n = _numPlates;

        var queue = new Queue<long>();
        var prev = new Dictionary<long, (long Key, int Plate, int Delta)>();
        var visited = new HashSet<long> { startKey };
        queue.Enqueue(startKey);

        var state = new int[n];
        bool solved = false;
        int guard = 0;

        while (queue.Count > 0)
        {
            // Периодическая проверка отмены (не на каждой итерации — ради скорости).
            if ((++guard & 0x3FFF) == 0)
            {
                token.ThrowIfCancellationRequested();
            }

            long key = queue.Dequeue();
            if (key == targetKey)
            {
                solved = true;
                break;
            }

            DecodeInto(key, state);

            for (int i = 0; i < n; i++)
            {
                int rowBase = i * n;
                foreach (int delta in Deltas)
                {
                    if (!TryApplyToKey(state, key, rowBase, delta, out long nextKey))
                    {
                        continue;
                    }

                    if (visited.Add(nextKey))
                    {
                        prev[nextKey] = (key, i, delta);
                        queue.Enqueue(nextKey);
                    }
                }
            }
        }

        if (!solved)
        {
            return null;
        }

        var moves = new List<(int plateIdx, int delta)>();
        long cur = targetKey;
        while (prev.TryGetValue(cur, out (long Key, int Plate, int Delta) p))
        {
            moves.Add((p.Plate, p.Delta));
            cur = p.Key;
        }
        moves.Reverse();
        return moves;
    }

    /// <summary>
    /// Фаза 2: Дейкстра по состоянию «(позиции, фокус)» с отсечением по верхней
    /// границе стоимости. Возвращает оптимальную по числу нажатий последовательность
    /// либо null, если в пределах границы лучшего не нашлось.
    /// </summary>
    private List<(int plateIdx, int delta)>? SolveWeighted(
        long startPosKey, long targetPosKey, int focus0, int upperBound, CancellationToken token)
    {
        int n = _numPlates;

        var dist = new Dictionary<long, int>();
        var prev = new Dictionary<long, (long State, int Plate, int Delta)>();

        long startState = startPosKey * n + focus0;
        dist[startState] = 0;

        var pq = new PriorityQueue<long, int>();
        pq.Enqueue(startState, 0);

        long foundState = -1;
        var state = new int[n];
        int guard = 0;

        while (pq.Count > 0)
        {
            if ((++guard & 0x3FFF) == 0)
            {
                token.ThrowIfCancellationRequested();
            }

            pq.TryDequeue(out long currentState, out int d);

            if (!dist.TryGetValue(currentState, out int best) || d > best)
            {
                continue;
            }

            long posKey = currentState / n;
            int focus = (int)(currentState % n);

            if (posKey == targetPosKey)
            {
                foundState = currentState;
                break;
            }

            DecodeInto(posKey, state);

            for (int i = 0; i < n; i++)
            {
                int moveCost = Math.Abs(i - focus) + 1;
                int rowBase = i * n;

                foreach (int delta in Deltas)
                {
                    if (!TryApplyToKey(state, posKey, rowBase, delta, out long nextPosKey))
                    {
                        continue;
                    }

                    int nd = d + moveCost;
                    if (nd > upperBound)
                    {
                        continue; // отсечение: дороже заведомо допустимого решения
                    }

                    long nextState = nextPosKey * n + i; // после хода фокус на пластине i
                    if (!dist.TryGetValue(nextState, out int known) || nd < known)
                    {
                        dist[nextState] = nd;
                        prev[nextState] = (currentState, i, delta);
                        pq.Enqueue(nextState, nd);
                    }
                }
            }
        }

        if (foundState < 0)
        {
            return null;
        }

        var moves = new List<(int plateIdx, int delta)>();
        long cur = foundState;
        while (prev.TryGetValue(cur, out (long State, int Plate, int Delta) p))
        {
            moves.Add((p.Plate, p.Delta));
            cur = p.State;
        }
        moves.Reverse();
        return moves;
    }

    /// <summary>
    /// Применяет ход (поворот пластины строки rowBase на delta) прямо к ключу позиций
    /// без аллокаций. Возвращает false, если связанная пластина выходит за [0..6].
    /// </summary>
    private bool TryApplyToKey(int[] state, long posKey, int rowBase, int delta, out long nextKey)
    {
        nextKey = posKey;
        int n = _numPlates;
        for (int j = 0; j < n; j++)
        {
            int influence = _flatMatrix[rowBase + j];
            if (influence == 0)
            {
                continue;
            }

            int nv = state[j] + influence * delta;
            if (nv < MinPos || nv > MaxPos)
            {
                return false;
            }

            nextKey += (long)(nv - state[j]) * _pow[j];
        }

        return true;
    }

    /// <summary>Ключ целевого состояния (все пластины в центре).</summary>
    private long TargetKey()
    {
        long key = 0;
        for (int i = 0; i < _numPlates; i++)
        {
            key += (long)Center * _pow[i];
        }
        return key;
    }

    /// <summary>Кодирует позиции в целочисленный ключ по основанию 7.</summary>
    private long EncodeState(int[] state)
    {
        long key = 0;
        for (int i = 0; i < state.Length; i++)
        {
            key += state[i] * _pow[i];
        }
        return key;
    }

    /// <summary>Декодирует ключ позиций в переданный буфер (без аллокаций).</summary>
    private void DecodeInto(long key, int[] buffer)
    {
        long temp = key;
        for (int i = 0; i < _numPlates; i++)
        {
            buffer[i] = (int)(temp % PosBase);
            temp /= PosBase;
        }
    }
}

namespace RecursiveLab.Core.Hanoi;

/// <summary>
/// Состояние головоломки: три стержня и набор дисков.
///
/// Модель данных полностью независима от интерфейса: визуализация только читает
/// состояние, а все перемещения выполняются здесь же и проверяются по правилам игры.
/// Список каждого стержня хранит диски СНИЗУ ВВЕРХ, то есть последний элемент — верхний диск.
/// </summary>
public sealed class HanoiState
{
    /// <summary>Минимальное количество дисков в головоломке.</summary>
    public const int MinDiskCount = 1;

    /// <summary>
    /// Технический предел: для 20 дисков требуется 2^20 − 1 ≈ 1 млн перемещений,
    /// большее количество уже не имеет смысла для демонстрации.
    /// </summary>
    public const int MaxDiskCount = 20;

    private readonly List<int>[] _pegs = [[], [], []];

    /// <summary>Начальное состояние: все диски на стержне <paramref name="startPeg"/>.</summary>
    /// <param name="diskCount">Количество дисков (от 1 до <see cref="MaxDiskCount"/>).</param>
    /// <param name="startPeg">Индекс начального стержня (по умолчанию 0 — стержень A).</param>
    public HanoiState(int diskCount, int startPeg = 0)
    {
        if (diskCount < MinDiskCount || diskCount > MaxDiskCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(diskCount),
                diskCount,
                $"Количество дисков должно быть от {MinDiskCount} до {MaxDiskCount}.");
        }

        if (!HanoiPegs.IsValid(startPeg))
        {
            throw new ArgumentOutOfRangeException(nameof(startPeg), startPeg, "Индекс стержня должен быть 0, 1 или 2.");
        }

        DiskCount = diskCount;
        StartPeg = startPeg;
        Reset();
    }

    private HanoiState(HanoiState source)
    {
        DiskCount = source.DiskCount;
        StartPeg = source.StartPeg;
        MovesApplied = source.MovesApplied;
        for (int peg = 0; peg < HanoiPegs.Count; peg++)
        {
            _pegs[peg].AddRange(source._pegs[peg]);
        }
    }

    /// <summary>Количество дисков в головоломке.</summary>
    public int DiskCount { get; }

    /// <summary>Стержень, на котором собирается башня в начальном состоянии.</summary>
    public int StartPeg { get; }

    /// <summary>Сколько перемещений уже выполнено в этом состоянии.</summary>
    public int MovesApplied { get; private set; }

    /// <summary>
    /// Диски стержня <paramref name="pegIndex"/> СНИЗУ ВВЕРХ (первый элемент — самый большой).
    /// Коллекция доступна только для чтения и не должна изменяться вызывающим кодом.
    /// </summary>
    public IReadOnlyList<int> PegDisks(int pegIndex)
    {
        ValidatePeg(pegIndex);
        return _pegs[pegIndex];
    }

    /// <summary>Самый маленький диск — 1, самый большой — <see cref="DiskCount"/>.</summary>
    public int TopDiskSize(int pegIndex)
    {
        ValidatePeg(pegIndex);
        return _pegs[pegIndex].Count == 0 ? 0 : _pegs[pegIndex][^1];
    }

    /// <summary>Количество дисков на стержне.</summary>
    public int Height(int pegIndex)
    {
        ValidatePeg(pegIndex);
        return _pegs[pegIndex].Count;
    }

    /// <summary>Проверяет правило: можно ли выполнить перемещение.</summary>
    public bool CanMove(int from, int to)
        => CanMove(from, to, out _);

    /// <summary>Проверяет правило перемещения и возвращает причину отказа.</summary>
    public bool CanMove(int from, int to, out string? error)
    {
        error = null;

        if (!HanoiPegs.IsValid(from) || !HanoiPegs.IsValid(to))
        {
            error = "Индекс стержня должен быть 0, 1 или 2.";
            return false;
        }

        if (from == to)
        {
            error = "Нельзя переносить диск на тот же стержень.";
            return false;
        }

        if (_pegs[from].Count == 0)
        {
            error = $"На стержне {HanoiPegs.Name(from)} нет дисков.";
            return false;
        }

        int moving = _pegs[from][^1];
        int target = TopDiskSize(to);
        if (target != 0 && target < moving)
        {
            error = $"Нельзя положить диск {moving} на меньший диск {target}.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Выполняет перемещение, если оно допустимо.
    /// Дополнительно проверяется, что в описании хода указан правильный диск.
    /// </summary>
    public bool TryApply(HanoiMove move, out string? error)
    {
        if (!CanMove(move.From, move.To, out error))
        {
            return false;
        }

        int moving = _pegs[move.From][^1];
        if (move.Disk != 0 && move.Disk != moving)
        {
            error = $"Ход описывает диск {move.Disk}, но сверху лежит диск {moving}.";
            return false;
        }

        _pegs[move.From].RemoveAt(_pegs[move.From].Count - 1);
        _pegs[move.To].Add(moving);
        MovesApplied++;
        return true;
    }

    /// <summary>Выполняет перемещение; при нарушении правил бросает исключение.</summary>
    public void Apply(HanoiMove move)
    {
        if (!TryApply(move, out string? error))
        {
            throw new InvalidOperationException($"Недопустимое перемещение {move}: {error}");
        }
    }

    /// <summary>Все диски собраны на заданном стержне.</summary>
    public bool IsSolved(int targetPeg)
    {
        ValidatePeg(targetPeg);
        return _pegs[targetPeg].Count == DiskCount;
    }

    /// <summary>
    /// Инвариант корректности: на каждом стержне диски убывают по размеру снизу вверх.
    /// </summary>
    public bool IsOrderingValid()
    {
        for (int peg = 0; peg < HanoiPegs.Count; peg++)
        {
            List<int> stack = _pegs[peg];
            for (int i = 1; i < stack.Count; i++)
            {
                if (stack[i] >= stack[i - 1])
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Суммарное количество дисков на всех стержнях (контроль сохранности набора).</summary>
    public int TotalDisks()
    {
        int total = 0;
        for (int peg = 0; peg < HanoiPegs.Count; peg++)
        {
            total += _pegs[peg].Count;
        }

        return total;
    }

    /// <summary>Возвращает состояние к начальному положению.</summary>
    public void Reset()
    {
        for (int peg = 0; peg < HanoiPegs.Count; peg++)
        {
            _pegs[peg].Clear();
        }

        // Диски укладываются от большого к маленькому — так снизу оказывается самый большой.
        for (int size = DiskCount; size >= MinDiskCount; size--)
        {
            _pegs[StartPeg].Add(size);
        }

        MovesApplied = 0;
    }

    /// <summary>Независимая копия состояния (нужна для проверок и повторного воспроизведения).</summary>
    public HanoiState Clone() => new(this);

    /// <summary>Текстовое представление для журнала: A=[3,2,1] B=[] C=[].</summary>
    public override string ToString()
    {
        var parts = new string[HanoiPegs.Count];
        for (int peg = 0; peg < HanoiPegs.Count; peg++)
        {
            parts[peg] = $"{HanoiPegs.Name(peg)}=[{string.Join(",", _pegs[peg])}]";
        }

        return string.Join(" ", parts);
    }

    private static void ValidatePeg(int pegIndex)
    {
        if (!HanoiPegs.IsValid(pegIndex))
        {
            throw new ArgumentOutOfRangeException(nameof(pegIndex), pegIndex, "Индекс стержня должен быть 0, 1 или 2.");
        }
    }
}

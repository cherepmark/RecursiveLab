namespace RecursiveLab.Core.Hanoi;

/// <summary>
/// Готовое решение головоломки: последовательность перемещений и сведения об алгоритме.
/// Объект неизменяемый, поэтому его можно безопасно воспроизводить в интерфейсе сколько угодно раз.
/// </summary>
public sealed class HanoiSolution
{
    public HanoiSolution(
        HanoiSolverKind kind,
        string algorithmName,
        int diskCount,
        int fromPeg,
        int toPeg,
        IReadOnlyList<HanoiMove> moves)
    {
        ArgumentNullException.ThrowIfNull(algorithmName);
        ArgumentNullException.ThrowIfNull(moves);

        Kind = kind;
        AlgorithmName = algorithmName;
        DiskCount = diskCount;
        FromPeg = fromPeg;
        ToPeg = toPeg;
        Moves = moves;
    }

    public HanoiSolverKind Kind { get; }

    /// <summary>Название алгоритма для интерфейса, например «Рекурсия» или «Явный стек».</summary>
    public string AlgorithmName { get; }

    public int DiskCount { get; }

    public int FromPeg { get; }

    public int ToPeg { get; }

    /// <summary>Полная последовательность перемещений.</summary>
    public IReadOnlyList<HanoiMove> Moves { get; }

    public int MoveCount => Moves.Count;

    /// <summary>
    /// Теоретический минимум перемещений классической головоломки: 2^N − 1.
    /// Оба алгоритма (рекурсивный и стековый) дают именно столько ходов.
    /// </summary>
    public long TheoreticalMinimumMoves => (1L << DiskCount) - 1;

    /// <summary>Решение оптимально (использовано ровно 2^N − 1 перемещений).</summary>
    public bool IsOptimal => DiskCount >= 1 && MoveCount == TheoreticalMinimumMoves;

    /// <summary>Перемещение по его номеру (нумерация с 0).</summary>
    public HanoiMove MoveAt(int index) => Moves[index];

    public override string ToString()
        => $"{AlgorithmName}: {HanoiPegs.Name(FromPeg)} → {HanoiPegs.Name(ToPeg)}, " +
           $"{DiskCount} диск(ов), {MoveCount} перемещений (минимум {TheoreticalMinimumMoves})";
}

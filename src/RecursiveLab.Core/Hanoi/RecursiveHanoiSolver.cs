namespace RecursiveLab.Core.Hanoi;

/// <summary>
/// Классическое рекурсивное решение головоломки «Ханойская башня».
///
/// Алгоритм переноса n дисков со стержня <c>from</c> на стержень <c>to</c>:
/// 1) перенести n − 1 диск с <c>from</c> на вспомогательный стержень <c>via</c>;
/// 2) перенести самый большой (n-й) диск с <c>from</c> на <c>to</c>;
/// 3) перенести n − 1 диск с <c>via</c> на <c>to</c>.
///
/// Базовый случай: n == 1 — диск переносится напрямую одним ходом.
/// Количество ходов: 2^n − 1.
/// Глубина рекурсии равна количеству дисков — это наглядно показывает,
/// что системный стек вызовов растёт линейно по n.
/// </summary>
public sealed class RecursiveHanoiSolver : IHanoiSolver
{
    public HanoiSolverKind Kind => HanoiSolverKind.Recursive;

    public string Name => "Рекурсия";

    public string Description =>
        "Классический рекурсивный алгоритм. Для n дисков: перенести n − 1 на вспомогательный " +
        "стержень, перенести n-й диск на целевой, перенести n − 1 на целевой. " +
        "Базовый случай: n = 1. Всего ходов: 2^n − 1. Глубина рекурсии равна числу дисков.";

    public HanoiSolution Solve(int diskCount, int fromPeg = 0, int toPeg = HanoiPegs.Count - 1)
    {
        HanoiSolverArguments.Validate(diskCount, fromPeg, toPeg);

        // 2^n − 1 ходов — сразу резервируем память, чтобы избежать перевыделения.
        var moves = new List<HanoiMove>((1 << diskCount) - 1);

        MoveTower(diskCount, fromPeg, toPeg, HanoiPegs.Auxiliary(fromPeg, toPeg), moves);

        return new HanoiSolution(Kind, Name, diskCount, fromPeg, toPeg, moves);
    }

    /// <summary>
    /// Рекурсивный перенос башни из <paramref name="count"/> дисков.
    /// </summary>
    /// <param name="count">Сколько дисков переносим.</param>
    /// <param name="from">Стержень-источник.</param>
    /// <param name="to">Стержень-назначение.</param>
    /// <param name="via">Вспомогательный стержень.</param>
    /// <param name="moves">Журнал перемещений.</param>
    private static void MoveTower(int count, int from, int to, int via, List<HanoiMove> moves)
    {
        // --- Базовый случай рекурсии -------------------------------------------
        // Один диск всегда можно перенести напрямую: он гарантированно меньше
        // любого диска, который уже лежит на целевом стержне.
        if (count == 1)
        {
            moves.Add(new HanoiMove(from, to, 1));
            return;
        }

        // --- Шаг 1: освободить самый большой диск ------------------------------
        MoveTower(count - 1, from, via, to, moves);

        // --- Шаг 2: перенести самый большой диск -------------------------------
        moves.Add(new HanoiMove(from, to, count));

        // --- Шаг 3: вернуть освобождённую башню на целевой стержень ------------
        MoveTower(count - 1, via, to, from, moves);
    }
}

namespace RecursiveLab.Core.Hanoi;

/// <summary>
/// Контракт решателя головоломки «Ханойская башня».
/// Позволяет подменять рекурсивный алгоритм стековым без изменений в интерфейсе.
/// </summary>
public interface IHanoiSolver
{
    HanoiSolverKind Kind { get; }

    /// <summary>Название алгоритма для интерфейса.</summary>
    string Name { get; }

    /// <summary>Краткое описание алгоритма (для справки в интерфейсе).</summary>
    string Description { get; }

    /// <summary>
    /// Возвращает последовательность перемещений, переводящую все диски
    /// со стержня <paramref name="fromPeg"/> на стержень <paramref name="toPeg"/>.
    /// </summary>
    HanoiSolution Solve(int diskCount, int fromPeg = 0, int toPeg = HanoiPegs.Count - 1);
}

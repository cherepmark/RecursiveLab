namespace RecursiveLab.Core.Hanoi;

/// <summary>
/// Создаёт решатель по выбранному способу решения.
/// Интерфейсу достаточно передать вид алгоритма — он получит и алгоритм, и его описание.
/// </summary>
public static class HanoiSolverFactory
{
    private static readonly IHanoiSolver[] Solvers =
    [
        new RecursiveHanoiSolver(),
        new StackHanoiSolver(),
    ];

    public static IReadOnlyList<IHanoiSolver> All => Solvers;

    public static IHanoiSolver Create(HanoiSolverKind kind)
    {
        foreach (IHanoiSolver solver in Solvers)
        {
            if (solver.Kind == kind)
            {
                return solver;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(kind), kind, "Неизвестный способ решения.");
    }
}

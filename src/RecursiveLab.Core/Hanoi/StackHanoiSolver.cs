namespace RecursiveLab.Core.Hanoi;

/// <summary>
/// Нерекурсивное решение головоломки «Ханойская башня» с явным стеком.
///
/// Идея: системный стек вызовов заменяется собственным стеком
/// <see cref="Stack{T}"/> из кадров <see cref="Frame"/>. Каждый кадр описывает
/// один «вызов» рекурсивной функции: сколько дисков переносим и между какими стержнями.
/// При первом посещении кадра (Expanded == false) в стек в обратном порядке
/// помещаются три задачи, соответствующие трём шагам рекурсии; при повторном
/// (Expanded == true) выполняется сам перенос самого большого диска.
///
/// Рекурсивные вызовы в этом классе не используются: метод <see cref="Solve"/>
/// и весь его граф вызовов не содержат ни одной рекурсивной функции.
/// Порядок перемещений полностью совпадает с рекурсивным алгоритмом —
/// это проверяется автоматическими тестами.
/// </summary>
public sealed class StackHanoiSolver : IHanoiSolver
{
    public HanoiSolverKind Kind => HanoiSolverKind.ExplicitStack;

    public string Name => "Явный стек";

    public string Description =>
        "Тот же алгоритм, но без рекурсивных вызовов: кадры вида (n, from, to, via) " +
        "хранятся в собственном стеке. Кадр посещается дважды: при первом посещении " +
        "в стек кладутся три подзадачи в обратном порядке, при втором — выполняется " +
        "перенос большого диска. Порядок ходов идентичен рекурсивному решению.";

    public HanoiSolution Solve(int diskCount, int fromPeg = 0, int toPeg = HanoiPegs.Count - 1)
    {
        HanoiSolverArguments.Validate(diskCount, fromPeg, toPeg);

        var moves = new List<HanoiMove>((1 << diskCount) - 1);

        // Единственный явный стек — замена системного стека вызовов.
        var workStack = new Stack<Frame>();
        workStack.Push(new Frame(diskCount, fromPeg, toPeg, HanoiPegs.Auxiliary(fromPeg, toPeg), Expanded: false));

        while (workStack.Count > 0)
        {
            Frame frame = workStack.Pop();

            // --- Базовый случай (тот же, что и в рекурсии: n == 1) -------------
            // Один диск переносится напрямую, без добавления новых кадров.
            if (frame.Count == 1)
            {
                moves.Add(new HanoiMove(frame.From, frame.To, 1));
                continue;
            }

            if (!frame.Expanded)
            {
                // --- Первое посещение кадра: эмулируем три шага рекурсии --------
                // Кадры кладутся в порядке, обратном порядку выполнения,
                // потому что стек отдаёт элементы в обратном порядке (LIFO).
                // 3-й шаг: перенести n − 1 диск со вспомогательного на целевой.
                workStack.Push(new Frame(frame.Count - 1, frame.Via, frame.To, frame.From, Expanded: false));

                // 2-й шаг: перенести самый большой диск на целевой стержень.
                workStack.Push(new Frame(frame.Count, frame.From, frame.To, frame.Via, Expanded: true));

                // 1-й шаг: перенести n − 1 диск на вспомогательный стержень.
                workStack.Push(new Frame(frame.Count - 1, frame.From, frame.Via, frame.To, Expanded: false));
            }
            else
            {
                // --- Второе посещение кадра: перенос самого большого диска ------
                moves.Add(new HanoiMove(frame.From, frame.To, frame.Count));
            }
        }

        return new HanoiSolution(Kind, Name, diskCount, fromPeg, toPeg, moves);
    }

    /// <summary>
    /// Кадр явного стека — аналог одного кадра системного стека вызовов.
    /// </summary>
    /// <param name="Count">Сколько дисков переносится в этой задаче.</param>
    /// <param name="From">Стержень-источник.</param>
    /// <param name="To">Стержень-назначение.</param>
    /// <param name="Via">Вспомогательный стержень.</param>
    /// <param name="Expanded">true — кадр уже был раскрыт, нужно выполнить перенос большого диска.</param>
    private readonly record struct Frame(int Count, int From, int To, int Via, bool Expanded);
}

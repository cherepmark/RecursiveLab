namespace RecursiveLab.Core.Hanoi;

/// <summary>Результат проверки решения: где именно нарушены правила и достигнута ли цель.</summary>
public sealed class HanoiValidationResult
{
    private HanoiValidationResult(
        bool isValid,
        bool reachesGoal,
        bool isOptimalMoveCount,
        int movesApplied,
        int? firstIllegalMoveNumber,
        string? failureReason,
        HanoiState finalState)
    {
        IsValid = isValid;
        ReachesGoal = reachesGoal;
        IsOptimalMoveCount = isOptimalMoveCount;
        MovesApplied = movesApplied;
        FirstIllegalMoveNumber = firstIllegalMoveNumber;
        FailureReason = failureReason;
        FinalState = finalState;
    }

    /// <summary>Все ходы допустимы и цель достигнута.</summary>
    public bool IsValid { get; }

    /// <summary>Все диски оказались на целевом стержне.</summary>
    public bool ReachesGoal { get; }

    /// <summary>Количество ходов равно теоретическому минимуму 2^N − 1.</summary>
    public bool IsOptimalMoveCount { get; }

    public int MovesApplied { get; }

    /// <summary>Номер первого недопустимого хода (с 1) или null, если нарушений нет.</summary>
    public int? FirstIllegalMoveNumber { get; }

    public string? FailureReason { get; }

    /// <summary>Состояние после всех выполненных ходов.</summary>
    public HanoiState FinalState { get; }

    internal static HanoiValidationResult Valid(HanoiState finalState, int movesApplied, bool isOptimal)
        => new(true, true, isOptimal, movesApplied, null, null, finalState);

    internal static HanoiValidationResult GoalNotReached(HanoiState finalState, int movesApplied, bool isOptimal)
        => new(
            false,
            false,
            isOptimal,
            movesApplied,
            null,
            "Решение завершилось, но не все диски оказались на целевом стержне.",
            finalState);

    internal static HanoiValidationResult Invalid(HanoiState finalState, int movesApplied, int illegalMoveNumber, string reason)
        => new(false, false, false, movesApplied, illegalMoveNumber, reason, finalState);
}

/// <summary>
/// Независимый «судья» решения: воспроизводит все ходы на копии модели,
/// проверяя правила перекладывания и итоговое положение дисков.
/// Используется тестами и интерфейсом для контроля качества решения.
/// </summary>
public static class HanoiSolutionValidator
{
    /// <summary>
    /// Проверяет решение целиком: допустимость каждого хода, инвариант порядка дисков
    /// и достижение цели.
    /// </summary>
    public static HanoiValidationResult Validate(HanoiSolution solution)
    {
        ArgumentNullException.ThrowIfNull(solution);

        var state = new HanoiState(solution.DiskCount, solution.FromPeg);

        for (int i = 0; i < solution.Moves.Count; i++)
        {
            HanoiMove move = solution.Moves[i];
            if (!state.TryApply(move, out string? error))
            {
                return HanoiValidationResult.Invalid(state, i, i + 1, error ?? "Ход нарушает правила.");
            }

            // Инвариант: ни на одном стержне большой диск не должен лежать на меньшем.
            if (!state.IsOrderingValid())
            {
                return HanoiValidationResult.Invalid(
                    state,
                    i + 1,
                    i + 1,
                    "Нарушен порядок дисков: больший диск оказался над меньшим.");
            }

            // Дополнительный контроль сохранности набора дисков.
            if (state.TotalDisks() != solution.DiskCount)
            {
                return HanoiValidationResult.Invalid(
                    state,
                    i + 1,
                    i + 1,
                    "Количество дисков на стержнях изменилось — нарушен инвариант сохранности набора.");
            }
        }

        bool isOptimal = solution.Moves.Count == solution.TheoreticalMinimumMoves;
        int movesApplied = solution.Moves.Count;

        return state.IsSolved(solution.ToPeg)
            ? HanoiValidationResult.Valid(state, movesApplied, isOptimal)
            : HanoiValidationResult.GoalNotReached(state, movesApplied, isOptimal);
    }

    /// <summary>
    /// Проверка произвольной последовательности ходов (используется в тестах
    /// для доказательства того, что «судья» действительно ловит нарушения).
    /// </summary>
    public static HanoiValidationResult ValidateSequence(
        int diskCount,
        int fromPeg,
        int toPeg,
        IReadOnlyList<HanoiMove> moves)
    {
        ArgumentNullException.ThrowIfNull(moves);
        var solution = new HanoiSolution(HanoiSolverKind.Recursive, "Проверка", diskCount, fromPeg, toPeg, moves);
        return Validate(solution);
    }
}

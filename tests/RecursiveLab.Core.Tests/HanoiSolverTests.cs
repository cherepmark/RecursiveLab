using System.Reflection;
using RecursiveLab.Core.Hanoi;

namespace RecursiveLab.Core.Tests;

/// <summary>
/// Тесты Ханойской башни: оба алгоритма, модель данных и независимый «судья» решения.
/// </summary>
public class HanoiSolverTests
{
    private const int MaxComparedDisks = 12;

    public static TheoryData<HanoiSolverKind, int> AllSolverKindsAndSizes()
    {
        var data = new TheoryData<HanoiSolverKind, int>();
        foreach (HanoiSolverKind kind in Enum.GetValues<HanoiSolverKind>())
        {
            for (int disks = 1; disks <= MaxComparedDisks; disks++)
            {
                data.Add(kind, disks);
            }
        }

        return data;
    }

    // ---------------------------------------------------------------------
    // Базовый случай: один диск
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(HanoiSolverKind.Recursive)]
    [InlineData(HanoiSolverKind.ExplicitStack)]
    public void Solve_WithSingleDisk_PerformsExactlyOneMove(HanoiSolverKind kind)
    {
        IHanoiSolver solver = HanoiSolverFactory.Create(kind);

        HanoiSolution solution = solver.Solve(diskCount: 1);

        Assert.Single(solution.Moves);
        Assert.Equal(new HanoiMove(0, 2, 1), solution.Moves[0]);
        Assert.Equal(1, solution.TheoreticalMinimumMoves);
        Assert.True(solution.IsOptimal);
    }

    // ---------------------------------------------------------------------
    // Количество ходов 2^N − 1 и корректность всей последовательности
    // ---------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(AllSolverKindsAndSizes))]
    public void Solve_MoveCount_EqualsTwoPowerNMinusOne(HanoiSolverKind kind, int disks)
    {
        IHanoiSolver solver = HanoiSolverFactory.Create(kind);

        HanoiSolution solution = solver.Solve(disks);

        long expected = (1L << disks) - 1;
        Assert.Equal(expected, solution.MoveCount);
        Assert.Equal(expected, solution.TheoreticalMinimumMoves);
        Assert.True(solution.IsOptimal, "Оба алгоритма должны давать оптимальное решение.");
    }

    [Theory]
    [MemberData(nameof(AllSolverKindsAndSizes))]
    public void Solve_EveryMoveIsLegal_AndGoalIsReached(HanoiSolverKind kind, int disks)
    {
        IHanoiSolver solver = HanoiSolverFactory.Create(kind);

        HanoiSolution solution = solver.Solve(disks, fromPeg: 0, toPeg: 2);
        HanoiValidationResult validation = HanoiSolutionValidator.Validate(solution);

        Assert.True(validation.IsValid, validation.FailureReason);
        Assert.True(validation.ReachesGoal, "Все диски должны оказаться на целевом стержне.");
        Assert.Null(validation.FirstIllegalMoveNumber);
        Assert.True(validation.IsOptimalMoveCount);
        Assert.Equal(solution.MoveCount, validation.MovesApplied);
    }

    [Theory]
    [MemberData(nameof(AllSolverKindsAndSizes))]
    public void Solve_FinalState_HasAllDisksOnTargetInCorrectOrder(HanoiSolverKind kind, int disks)
    {
        IHanoiSolver solver = HanoiSolverFactory.Create(kind);

        HanoiSolution solution = solver.Solve(disks, fromPeg: 0, toPeg: 2);
        HanoiValidationResult validation = HanoiSolutionValidator.Validate(solution);
        HanoiState finalState = validation.FinalState;

        Assert.True(finalState.IsSolved(2));
        Assert.Equal(disks, finalState.Height(2));
        Assert.Equal(0, finalState.Height(0));
        Assert.Equal(0, finalState.Height(1));
        Assert.True(finalState.IsOrderingValid(), "Больший диск не должен лежать на меньшем.");
        Assert.Equal(disks, finalState.TotalDisks());

        // Диски лежат снизу вверх по убыванию размера: N, N-1, ..., 1.
        IReadOnlyList<int> target = finalState.PegDisks(2);
        for (int i = 0; i < disks; i++)
        {
            Assert.Equal(disks - i, target[i]);
        }
    }

    // ---------------------------------------------------------------------
    // Равенство рекурсивного и стекового решений
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    public void RecursiveAndStackSolvers_ProduceIdenticalMoveSequences(int disks)
    {
        HanoiSolution recursive = new RecursiveHanoiSolver().Solve(disks);
        HanoiSolution stack = new StackHanoiSolver().Solve(disks);

        Assert.Equal(recursive.MoveCount, stack.MoveCount);
        Assert.Equal(recursive.Moves, stack.Moves);
    }

    [Fact]
    public void Solvers_ReportDistinctKindsAndNames()
    {
        var recursive = new RecursiveHanoiSolver();
        var stack = new StackHanoiSolver();

        Assert.Equal(HanoiSolverKind.Recursive, recursive.Kind);
        Assert.Equal(HanoiSolverKind.ExplicitStack, stack.Kind);
        Assert.NotEqual(recursive.Name, stack.Name);
        Assert.All(HanoiSolverFactory.All, solver => Assert.False(string.IsNullOrWhiteSpace(solver.Description)));

        foreach (HanoiSolverKind kind in Enum.GetValues<HanoiSolverKind>())
        {
            Assert.Equal(kind, HanoiSolverFactory.Create(kind).Kind);
        }
    }

    // ---------------------------------------------------------------------
    // Недопустимые параметры
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(HanoiSolverKind.Recursive)]
    [InlineData(HanoiSolverKind.ExplicitStack)]
    public void Solve_RejectsZeroDisks(HanoiSolverKind kind)
    {
        IHanoiSolver solver = HanoiSolverFactory.Create(kind);
        Assert.Throws<ArgumentOutOfRangeException>(() => solver.Solve(0));
    }

    [Theory]
    [InlineData(HanoiSolverKind.Recursive)]
    [InlineData(HanoiSolverKind.ExplicitStack)]
    public void Solve_RejectsDisksAboveTechnicalLimit(HanoiSolverKind kind)
    {
        IHanoiSolver solver = HanoiSolverFactory.Create(kind);
        Assert.Throws<ArgumentOutOfRangeException>(() => solver.Solve(HanoiState.MaxDiskCount + 1));
    }

    [Theory]
    [InlineData(HanoiSolverKind.Recursive)]
    [InlineData(HanoiSolverKind.ExplicitStack)]
    public void Solve_RejectsInvalidPegIndexes(HanoiSolverKind kind)
    {
        IHanoiSolver solver = HanoiSolverFactory.Create(kind);

        Assert.Throws<ArgumentOutOfRangeException>(() => solver.Solve(3, fromPeg: 3, toPeg: 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => solver.Solve(3, fromPeg: 0, toPeg: -1));
        Assert.Throws<ArgumentException>(() => solver.Solve(3, fromPeg: 1, toPeg: 1));
    }

    // ---------------------------------------------------------------------
    // Свойства решения и вспомогательные функции
    // ---------------------------------------------------------------------

    [Fact]
    public void Solution_SupportsArbitraryPegPair()
    {
        HanoiSolution solution = new StackHanoiSolver().Solve(4, fromPeg: 1, toPeg: 0);

        Assert.Equal(1, solution.FromPeg);
        Assert.Equal(0, solution.ToPeg);
        Assert.True(HanoiSolutionValidator.Validate(solution).IsValid);
        Assert.Contains("B → A", solution.ToString());
    }

    [Fact]
    public void AuxiliaryPeg_ReturnsThirdPeg()
    {
        Assert.Equal(2, HanoiPegs.Auxiliary(0, 1));
        Assert.Equal(1, HanoiPegs.Auxiliary(0, 2));
        Assert.Equal(0, HanoiPegs.Auxiliary(1, 2));
    }

    [Fact]
    public void MoveFormatting_UsesPegLetters()
    {
        var move = new HanoiMove(0, 2, 3);
        Assert.Equal("A → C (диск 3)", move.ToString());
        Assert.Equal(new HanoiMove(2, 0, 3), move.Reversed());
        Assert.False(move.IsSelfMove);
        Assert.True(new HanoiMove(1, 1, 2).IsSelfMove);
    }
}

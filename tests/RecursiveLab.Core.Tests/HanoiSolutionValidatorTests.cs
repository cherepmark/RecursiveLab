using RecursiveLab.Core.Hanoi;

namespace RecursiveLab.Core.Tests;

/// <summary>
/// Тесты независимого «судьи»: он должен принимать корректные решения
/// и обязательно обнаруживать нарушения правил.
/// </summary>
public class HanoiSolutionValidatorTests
{
    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(8)]
    public void Validate_AcceptsCorrectSolutionsFromBothAlgorithms(int disks)
    {
        foreach (IHanoiSolver solver in HanoiSolverFactory.All)
        {
            HanoiValidationResult result = HanoiSolutionValidator.Validate(solver.Solve(disks));

            Assert.True(result.IsValid);
            Assert.True(result.ReachesGoal);
            Assert.True(result.IsOptimalMoveCount);
            Assert.Null(result.FirstIllegalMoveNumber);
            Assert.Null(result.FailureReason);
        }
    }

    [Fact]
    public void Validate_DetectsMoveThatBreaksDiskOrder()
    {
        // Диск 3 пытаемся положить на диск 1 — прямое нарушение правил на третьем ходу.
        HanoiMove[] moves = [new(0, 1, 1), new(0, 2, 2), new(0, 1, 3)];

        HanoiValidationResult result = HanoiSolutionValidator.ValidateSequence(3, 0, 2, moves);

        Assert.False(result.IsValid);
        Assert.False(result.ReachesGoal);
        Assert.Equal(3, result.FirstIllegalMoveNumber);
        Assert.Contains("меньший", result.FailureReason, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_DetectsMissingDiskInMoveDescription()
    {
        // Ход описывает диск 3, хотя сверху на стержне A лежит диск 1.
        HanoiMove[] moves = [new(0, 2, 3)];

        HanoiValidationResult result = HanoiSolutionValidator.ValidateSequence(3, 0, 2, moves);

        Assert.False(result.IsValid);
        Assert.Equal(1, result.FirstIllegalMoveNumber);
        Assert.Contains("сверху лежит диск 1", result.FailureReason, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_DetectsIncompleteSolution()
    {
        // Ходы допустимы, но цель не достигнута.
        HanoiMove[] moves = [new(0, 1, 1), new(0, 2, 2)];

        HanoiValidationResult result = HanoiSolutionValidator.ValidateSequence(2, 0, 2, moves);

        Assert.False(result.IsValid);
        Assert.False(result.ReachesGoal);
        Assert.Null(result.FirstIllegalMoveNumber);
        Assert.Contains("не все диски", result.FailureReason, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_DetectsNonOptimalButLegalSolution()
    {
        // Диск 1 «погулял» лишний раз: 5 ходов вместо 3 при двух дисках.
        HanoiMove[] moves =
        [
            new(0, 1, 1), new(0, 2, 2), new(1, 2, 1),
            new(2, 0, 1), new(0, 2, 1),
        ];

        HanoiValidationResult result = HanoiSolutionValidator.ValidateSequence(2, 0, 2, moves);

        Assert.True(result.IsValid);
        Assert.True(result.ReachesGoal);
        Assert.False(result.IsOptimalMoveCount);
        Assert.Equal(5, result.MovesApplied);
    }

    [Fact]
    public void Validate_RejectsNullSolution()
    {
        Assert.Throws<ArgumentNullException>(() => HanoiSolutionValidator.Validate(null!));
    }

    [Fact]
    public void Validate_UsesPassedStartingPeg()
    {
        HanoiMove[] moves = [new(1, 0, 1)];

        HanoiValidationResult result = HanoiSolutionValidator.ValidateSequence(1, 1, 0, moves);

        Assert.True(result.IsValid);
        Assert.True(result.ReachesGoal);
    }
}

using RecursiveLab.Core.Hanoi;

namespace RecursiveLab.Core.Tests;

/// <summary>Тесты модели данных: начальное состояние, правила ходов, инварианты.</summary>
public class HanoiStateTests
{
    [Fact]
    public void Constructor_PlacesAllDisksOnStartPeg_LargestAtBottom()
    {
        var state = new HanoiState(5);

        Assert.Equal(5, state.DiskCount);
        Assert.Equal(5, state.Height(0));
        Assert.Equal(0, state.MovesApplied);

        IReadOnlyList<int> disks = state.PegDisks(0);
        Assert.Equal(new[] { 5, 4, 3, 2, 1 }, disks);
        Assert.Equal(1, state.TopDiskSize(0));
        Assert.Equal(0, state.TopDiskSize(1));
        Assert.True(state.IsOrderingValid());
        Assert.Equal(5, state.TotalDisks());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(HanoiState.MaxDiskCount + 1)]
    public void Constructor_RejectsInvalidDiskCount(int diskCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new HanoiState(diskCount));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void Constructor_RejectsInvalidStartPeg(int startPeg)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new HanoiState(3, startPeg));
    }

    [Fact]
    public void CanMove_AllowsSmallerDiskOntoBigger_AndEmptyPeg()
    {
        var state = new HanoiState(3);

        Assert.True(state.CanMove(0, 1));   // диск 1 на пустой стержень
        state.Apply(new HanoiMove(0, 1, 1));
        Assert.True(state.CanMove(0, 2));   // диск 2 на пустой стержень
    }

    [Fact]
    public void CanMove_ForbidsLargerDiskOntoSmaller()
    {
        var state = new HanoiState(3);
        state.Apply(new HanoiMove(0, 1, 1));   // A -> B: диск 1
        state.Apply(new HanoiMove(0, 2, 2));   // A -> C: диск 2

        Assert.False(state.CanMove(0, 1));     // диск 3 на диск 1 — запрещено
        Assert.False(state.CanMove(0, 2));     // диск 3 на диск 2 — запрещено
        Assert.False(state.CanMove(0, 1, out string? error));
        Assert.Contains("меньший", error, StringComparison.Ordinal);
    }

    [Fact]
    public void CanMove_ReportsReasons()
    {
        var state = new HanoiState(2);

        Assert.False(state.CanMove(1, 2, out string? empty));
        Assert.Contains("нет дисков", empty, StringComparison.Ordinal);

        Assert.False(state.CanMove(0, 0, out string? same));
        Assert.Contains("тот же стержень", same, StringComparison.Ordinal);

        Assert.False(state.CanMove(0, 5, out string? range));
        Assert.Contains("0, 1 или 2", range, StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_IncrementsMoveCounter_AndThrowsOnIllegalMove()
    {
        var state = new HanoiState(2);
        state.Apply(new HanoiMove(0, 2, 1));

        Assert.Equal(1, state.MovesApplied);

        // Диск 2 нельзя положить на диск 1, который уже стоит на стержне C.
        var exception = Assert.Throws<InvalidOperationException>(() => state.Apply(new HanoiMove(0, 2, 2)));
        Assert.Contains("Недопустимое перемещение", exception.Message, StringComparison.Ordinal);
        Assert.Contains("меньший", exception.Message, StringComparison.Ordinal);
        Assert.Equal(1, state.MovesApplied);
    }

    [Fact]
    public void TryApply_RejectsMoveWithWrongDiskNumber()
    {
        var state = new HanoiState(3);

        Assert.False(state.TryApply(new HanoiMove(0, 1, 2), out string? error));
        Assert.Contains("сверху лежит диск 1", error, StringComparison.Ordinal);
        Assert.Equal(0, state.MovesApplied);
    }

    [Fact]
    public void Clone_IsIndependent()
    {
        var state = new HanoiState(3);
        HanoiState clone = state.Clone();

        clone.Apply(new HanoiMove(0, 2, 1));

        Assert.Equal(3, state.Height(0));
        Assert.Equal(0, state.Height(2));
        Assert.Equal(2, clone.Height(0));
        Assert.Equal(1, clone.Height(2));
        Assert.NotEqual(state.ToString(), clone.ToString());
    }

    [Fact]
    public void Reset_RestoresInitialPosition()
    {
        var state = new HanoiState(4);
        state.Apply(new HanoiMove(0, 1, 1));
        state.Apply(new HanoiMove(0, 2, 2));

        state.Reset();

        Assert.Equal(4, state.Height(0));
        Assert.Equal(0, state.MovesApplied);
        Assert.Equal(new[] { 4, 3, 2, 1 }, state.PegDisks(0));
    }

    [Fact]
    public void IsOrderingValid_HoldsAfterEveryMoveOfASolution()
    {
        const int disks = 6;
        var state = new HanoiState(disks);
        HanoiSolution solution = new StackHanoiSolver().Solve(disks);

        foreach (HanoiMove move in solution.Moves)
        {
            state.Apply(move);

            // Инвариант «снизу диск больше, чем сверху» не должен нарушаться никогда.
            Assert.True(state.IsOrderingValid());
            Assert.Equal(disks, state.TotalDisks());

            for (int peg = 0; peg < HanoiPegs.Count; peg++)
            {
                Assert.Equal(state.Height(peg), state.PegDisks(peg).Count);
            }
        }

        Assert.True(state.IsSolved(2));
        Assert.Equal(disks, state.PegDisks(2)[0]);
    }

    [Fact]
    public void IsSolved_IsTrueOnlyForCompletedTargetPeg()
    {
        var state = new HanoiState(2);
        Assert.False(state.IsSolved(2));

        state.Apply(new HanoiMove(0, 1, 1));
        state.Apply(new HanoiMove(0, 2, 2));
        state.Apply(new HanoiMove(1, 2, 1));

        Assert.True(state.IsSolved(2));
        Assert.False(state.IsSolved(0));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void PegAccessors_RejectInvalidIndexes(int peg)
    {
        var state = new HanoiState(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => state.PegDisks(peg));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.TopDiskSize(peg));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.Height(peg));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.IsSolved(peg));
    }
}

namespace RecursiveLab.Core.Hanoi;

/// <summary>Общая проверка аргументов для всех реализаций <see cref="IHanoiSolver"/>.</summary>
internal static class HanoiSolverArguments
{
    public static void Validate(int diskCount, int fromPeg, int toPeg)
    {
        if (diskCount < HanoiState.MinDiskCount || diskCount > HanoiState.MaxDiskCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(diskCount),
                diskCount,
                $"Количество дисков должно быть от {HanoiState.MinDiskCount} до {HanoiState.MaxDiskCount}.");
        }

        if (!HanoiPegs.IsValid(fromPeg))
        {
            throw new ArgumentOutOfRangeException(nameof(fromPeg), fromPeg, "Индекс стержня должен быть 0, 1 или 2.");
        }

        if (!HanoiPegs.IsValid(toPeg))
        {
            throw new ArgumentOutOfRangeException(nameof(toPeg), toPeg, "Индекс стержня должен быть 0, 1 или 2.");
        }

        if (fromPeg == toPeg)
        {
            throw new ArgumentException("Начальный и целевой стержни должны различаться.", nameof(toPeg));
        }
    }
}

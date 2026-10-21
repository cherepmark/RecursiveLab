namespace RecursiveLab.Core.Hanoi;

/// <summary>Имена стержней и вспомогательные операции над их индексами.</summary>
public static class HanoiPegs
{
    /// <summary>В классической головоломке всегда три стержня.</summary>
    public const int Count = 3;

    private static readonly string[] Names = ["A", "B", "C"];

    /// <summary>Проверяет, что индекс стержня существует.</summary>
    public static bool IsValid(int pegIndex) => pegIndex >= 0 && pegIndex < Count;

    /// <summary>Буквенное имя стержня (A, B, C) для интерфейса и журнала ходов.</summary>
    public static string Name(int pegIndex)
        => IsValid(pegIndex) ? Names[pegIndex] : $"#{pegIndex}";

    /// <summary>
    /// Вспомогательный стержень для переноса между <paramref name="from"/> и <paramref name="to"/>:
    /// это третий стержень, не совпадающий ни с одним из них.
    /// </summary>
    public static int Auxiliary(int from, int to)
    {
        for (int peg = 0; peg < Count; peg++)
        {
            if (peg != from && peg != to)
            {
                return peg;
            }
        }

        throw new ArgumentException($"Некорректная пара стержней: {from} → {to}.");
    }
}

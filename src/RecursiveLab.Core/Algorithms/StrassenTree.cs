using RecursiveLab.Core.Recursion;

namespace RecursiveLab.Core.Algorithms;

/// <summary>
/// Дерево рекурсии алгоритма Штрассена.
///
/// Каждый узел — умножение матриц размера n × n. Рекурсивный шаг даёт ровно семь
/// дочерних узлов половинного размера (M1…M7) вместо восьми у обычного блочного
/// умножения, поэтому число листьев дерева равно 7^k, а не 8^k.
///
/// Базовый случай — блок, размер которого не превышает выбранный порог: такой блок
/// перемножается обычным тройным циклом.
/// </summary>
public static class StrassenTree
{
    /// <summary>Число дочерних умножений на каждом шаге рекурсии.</summary>
    public const int MultiplicationCount = 7;

    /// <summary>Дерево рекурсии для умножения матриц заданного размера.</summary>
    public static RecursionTree Build(int size, int threshold, RecursionTreeLimits? limits = null)
    {
        if (size < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, "Размер матрицы должен быть положительным.");
        }

        if (threshold < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(threshold), threshold, "Порог базового случая должен быть не меньше 1.");
        }

        limits ??= RecursionTreeLimits.Default;
        var recorder = new RecursionTreeRecorder(limits);
        recorder.TryReserve(1);
        RecursionNode root = BuildNode(size, threshold, recorder, depth: 1);

        int splits = CountSplits(size, threshold);
        long theoretical = TheoreticalNodeCount(splits);

        string explanation =
            $"Каждый узел — умножение матриц, у него ровно {MultiplicationCount} дочерних умножений "
            + $"(M1…M7). Размер уменьшается вдвое, пока не достигнет порога {threshold}: "
            + $"уровней разбиения {splits}, листьев 7^{splits} = {Pow(7, splits):N0} "
            + $"против 8^{splits} = {Pow(8, splits):N0} у обычного блочного умножения.";

        return new RecursionTree(
            $"Дерево рекурсии Штрассена: {size}×{size}, порог {threshold}",
            explanation,
            root,
            theoretical,
            recorder.Truncated);
    }

    /// <summary>Полное число узлов дерева: 1 + 7 + … + 7^splits.</summary>
    public static long TheoreticalNodeCount(int splits)
    {
        if (splits < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(splits), splits, "Число разбиений не может быть отрицательным.");
        }

        long nodes = 1;
        long level = 1;
        for (int i = 0; i < splits; i++)
        {
            level *= MultiplicationCount;
            nodes += level;
        }

        return nodes;
    }

    /// <summary>Сколько раз размер делится пополам, пока не дойдёт до порога.</summary>
    public static int CountSplits(int size, int threshold)
    {
        int splits = 0;
        int current = size;

        while (current > threshold && current > 1)
        {
            current /= 2;
            splits++;
        }

        return splits;
    }

    private static RecursionNode BuildNode(int size, int threshold, RecursionTreeRecorder recorder, int depth)
    {
        bool isBaseCase = size <= threshold || size == 1;
        string title = $"{size}×{size}";

        // --- БАЗОВЫЙ СЛУЧАЙ РЕКУРСИИ: блок достаточно мал ---
        if (isBaseCase)
        {
            return new RecursionNode(
                title,
                RecursionNodeKind.BaseCase,
                "обычное умножение «в лоб»",
                children: null,
                truncated: false);
        }

        // Семь дочерних умножений бронируются вместе (иначе узел помечается обрезанным).
        if (!recorder.TryReserveGroup(depth + 1, MultiplicationCount))
        {
            recorder.MarkTruncated();
            return new RecursionNode(title, RecursionNodeKind.Call, "… ветвь не раскрыта", children: null, truncated: true);
        }

        // --- РЕКУРСИВНЫЙ ШАГ: семь умножений половинного размера ---
        var children = new List<RecursionNode>(MultiplicationCount);
        for (int i = 0; i < MultiplicationCount; i++)
        {
            children.Add(BuildNode(size / 2, threshold, recorder, depth + 1));
        }

        return new RecursionNode(title, RecursionNodeKind.Call, "M1…M7", children);
    }

    private static long Pow(long value, int exponent)
    {
        long result = 1;
        for (int i = 0; i < exponent; i++)
        {
            result *= value;
        }

        return result;
    }
}

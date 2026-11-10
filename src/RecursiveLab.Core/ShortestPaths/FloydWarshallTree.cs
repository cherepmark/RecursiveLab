using RecursiveLab.Core.Recursion;

namespace RecursiveLab.Core.ShortestPaths;

/// <summary>
/// Дерево вызовов алгоритма Флойда—Уоршелла.
///
/// Одна пара вершин даёт троичное дерево: каждый вызов D(k, i, j) порождает три вызова
/// уровня k−1 — «без промежуточной вершины», «путь до промежуточной» и «путь от
/// промежуточной». Именно поэтому без мемоизации получается 3^k вызовов на пару вершин.
///
/// При включённой мемоизации повторные состояния на схеме помечаются как «взято из
/// таблицы»: значение уже было вычислено, поэтому поддерево не раскрывается —
/// наглядно видно, за счёт чего экспоненциальная рекурсия превращается в O(n³).
/// </summary>
public static class FloydWarshallTree
{
    /// <summary>Дерево вызовов для пары вершин (для итеративного режима дерева нет).</summary>
    /// <param name="graph">Граф, для которого строится схема вызовов.</param>
    /// <param name="mode">Рекурсивный режим: без мемоизации или с мемоизацией.</param>
    /// <param name="from">Вершина-источник.</param>
    /// <param name="to">Вершина-назначение.</param>
    /// <param name="limits">Ограничения на размер схемы.</param>
    public static RecursionTree Build(
        WeightedGraph graph,
        FloydWarshallMode mode,
        int from,
        int to,
        RecursionTreeLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(graph);

        if (mode == FloydWarshallMode.Iterative)
        {
            throw new ArgumentException(
                "Итеративный алгоритм не использует рекурсию, поэтому дерева вызовов у него нет.",
                nameof(mode));
        }

        // Проверка индексов вершин выполняется графом.
        _ = graph.GetWeight(from, to);

        limits ??= RecursionTreeLimits.Default;
        var recorder = new RecursionTreeRecorder(limits);
        double[,] weights = graph.ToAdjacencyMatrix();
        var computed = new HashSet<(int Level, int From, int To)>();

        recorder.TryReserve(1);

        RecursionNode root = BuildNode(
            weights,
            level: graph.VertexCount,
            from,
            to,
            mode,
            computed,
            recorder,
            depth: 1);

        long theoretical = mode == FloydWarshallMode.NaiveRecursive
            ? TernaryCallCount(graph.VertexCount)
            : recorder.NodeCount;

        string explanation = mode == FloydWarshallMode.NaiveRecursive
            ? $"Полное дерево вызовов для одной пары вершин содержит {theoretical:N0} вызовов "
              + $"({graph.VertexCount} уровня по три вызова), поэтому на схеме показана только его часть."
            : "Повторные состояния не раскрываются: значение берётся из таблицы, "
              + "поэтому дерево остаётся линейным по числу состояний.";

        return new RecursionTree(
            $"Дерево вызовов D(k, {WeightedGraph.VertexName(from)}, {WeightedGraph.VertexName(to)})",
            explanation,
            root,
            theoretical,
            recorder.Truncated);
    }

    /// <summary>Полное число вызовов троичной рекурсии: T(k) = 1 + 3·T(k−1), T(0) = 1 → (3^(k+1) − 1)/2.</summary>
    public static long TernaryCallCount(int vertexCount)
    {
        if (vertexCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(vertexCount), vertexCount, "Количество вершин не может быть отрицательным.");
        }

        long result = 1;
        for (int level = 0; level < vertexCount; level++)
        {
            result = result * 3 + 1;
        }

        return result;
    }

    private static RecursionNode BuildNode(
        double[,] weights,
        int level,
        int from,
        int to,
        FloydWarshallMode mode,
        HashSet<(int Level, int From, int To)> computed,
        RecursionTreeRecorder recorder,
        int depth)
    {
        string title = $"D({level}, {WeightedGraph.VertexName(from)}, {WeightedGraph.VertexName(to)})";

        if (mode == FloydWarshallMode.MemoizedRecursive && !computed.Add((level, from, to)))
        {
            // Значение уже вычислено ранее — повторного раскрытия поддерева не происходит.
            return new RecursionNode(title, RecursionNodeKind.Cached, "взято из таблицы");
        }

        // --- БАЗОВЫЙ СЛУЧАЙ РЕКУРСИИ: промежуточные вершины не разрешены ---
        if (level == 0)
        {
            double weight = weights[from, to];
            string value = double.IsPositiveInfinity(weight) ? "∞" : weight.ToString("0.##");

            return new RecursionNode(title, RecursionNodeKind.BaseCase, $"прямое ребро = {value}");
        }

        // Три дочерних вызова бронируются вместе: либо ветвь видна целиком,
        // либо узел помечается как обрезанный (без узлов-заглушек).
        if (!recorder.TryReserveGroup(depth + 1, 3))
        {
            recorder.MarkTruncated();
            return new RecursionNode(
                title,
                RecursionNodeKind.Call,
                "… ветвь вызовов не раскрыта",
                children: null,
                truncated: true);
        }

        // --- РЕКУРСИВНЫЙ ШАГ: три вызова уровня k − 1 ---
        int intermediate = level - 1;
        var children = new List<RecursionNode>(3)
        {
            BuildNode(weights, level - 1, from, to, mode, computed, recorder, depth + 1),
            BuildNode(weights, level - 1, from, intermediate, mode, computed, recorder, depth + 1),
            BuildNode(weights, level - 1, intermediate, to, mode, computed, recorder, depth + 1),
        };

        return new RecursionNode(title, RecursionNodeKind.Call, "min(без k, через k)", children);
    }
}

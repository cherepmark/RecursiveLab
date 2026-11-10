using System.Diagnostics;
using RecursiveLab.Core.Algorithms;

namespace RecursiveLab.Core.ShortestPaths;

/// <summary>Способ вычисления кратчайших путей в приложении.</summary>
public enum FloydWarshallMode
{
    /// <summary>
    /// «Честная» рекурсия без мемоизации: D(k,i,j) = min(D(k−1,i,j), D(k−1,i,k−1) + D(k−1,k−1,j)).
    /// Троичная рекурсия: 3^k вызовов на каждую пару вершин — экспоненциальный пример.
    /// </summary>
    NaiveRecursive = 0,

    /// <summary>Та же рекурсия, но значения D(k,i,j) запоминаются: сложность O(n³).</summary>
    MemoizedRecursive = 1,

    /// <summary>Классические три вложенных цикла — эталон и одновременно источник путей.</summary>
    Iterative = 2,
}

/// <summary>Результат работы алгоритма Флойда—Уоршелла.</summary>
public sealed class FloydWarshallResult
{
    internal FloydWarshallResult(
        FloydWarshallMode mode,
        int vertexCount,
        double[,] distances,
        long recursiveCalls,
        int[,]? nextHop,
        TimeSpan elapsed,
        int? allowedIntermediates = null)
    {
        Mode = mode;
        VertexCount = vertexCount;
        Distances = distances;
        RecursiveCallCount = recursiveCalls;
        NextHop = nextHop;
        Elapsed = elapsed;
        AllowedIntermediates = allowedIntermediates ?? vertexCount;
    }

    public FloydWarshallMode Mode { get; }

    public string ModeName => FloydWarshall.ModeName(Mode);

    public int VertexCount { get; }

    /// <summary>
    /// Сколько первых вершин разрешено использовать как промежуточные.
    /// Для полностью решённой задачи равно <see cref="VertexCount"/>.
    /// </summary>
    public int AllowedIntermediates { get; }

    /// <summary>Задача решена полностью (обработаны все вершины как промежуточные).</summary>
    public bool IsComplete => AllowedIntermediates >= VertexCount;

    /// <summary>Матрица кратчайших расстояний (∞ — пути нет).</summary>
    public double[,] Distances { get; }

    /// <summary>Сколько раз была вызвана рекурсивная функция (для итеративного режима — 0).</summary>
    public long RecursiveCallCount { get; }

    /// <summary>Матрица «следующая вершина на пути» — заполняется только итеративным режимом.</summary>
    public int[,]? NextHop { get; }

    public TimeSpan Elapsed { get; }

    /// <summary>Расстояние между двумя вершинами.</summary>
    public double Distance(int from, int to) => Distances[from, to];

    /// <summary>Достижима ли вершина <paramref name="to"/> из <paramref name="from"/>.</summary>
    public bool IsReachable(int from, int to) => !double.IsPositiveInfinity(Distances[from, to]);

    /// <summary>Совпадают ли матрицы расстояний с другим результатом.</summary>
    public bool Matches(FloydWarshallResult other)
        => other is not null && Matrix.AreEqual(Distances, other.Distances);

    /// <summary>
    /// Вершины, достижимые из <paramref name="source"/> (сама вершина тоже входит в набор).
    /// </summary>
    public IReadOnlyCollection<int> ReachableVertices(int source)
    {
        var reachable = new List<int>();

        for (int vertex = 0; vertex < VertexCount; vertex++)
        {
            if (vertex == source || IsReachable(source, vertex))
            {
                reachable.Add(vertex);
            }
        }

        return reachable;
    }

    /// <summary>
    /// Рёбра, входящие в кратчайшие пути из вершины <paramref name="source"/> ко всем
    /// достижимым вершинам. Используется для подсветки путей на графе.
    /// </summary>
    public IReadOnlyList<(int From, int To)> ShortestPathEdges(int source)
    {
        var edges = new List<(int From, int To)>();

        if (NextHop is null || source < 0 || source >= VertexCount)
        {
            return edges;
        }

        var seen = new HashSet<(int From, int To)>();

        for (int target = 0; target < VertexCount; target++)
        {
            if (target == source || !IsReachable(source, target))
            {
                continue;
            }

            int current = source;
            int guard = 0;

            while (current != target && guard++ <= VertexCount)
            {
                int next = NextHop[current, target];
                if (next < 0)
                {
                    break;
                }

                if (seen.Add((current, next)))
                {
                    edges.Add((current, next));
                }

                current = next;
            }
        }

        return edges;
    }

    /// <summary>
    /// Восстанавливает кратчайший путь по матрице «следующая вершина»
    /// (доступно только для итеративного режима).
    /// </summary>
    public string DescribePath(int from, int to)
    {
        if (NextHop is null)
        {
            return "Пути не восстанавливались в этом режиме.";
        }

        string start = WeightedGraph.VertexName(from);
        string finish = WeightedGraph.VertexName(to);

        if (double.IsPositiveInfinity(Distances[from, to]))
        {
            return $"{start} → {finish}: пути нет";
        }

        if (from == to)
        {
            return $"{start}: расстояние 0";
        }

        var vertices = new List<int> { from };
        int current = from;

        while (current != to && vertices.Count <= VertexCount)
        {
            int next = NextHop[current, to];
            if (next < 0)
            {
                return $"{start} → {finish}: пути нет";
            }

            vertices.Add(next);
            current = next;
        }

        if (current != to)
        {
            return $"{start} → {finish}: ошибка восстановления пути";
        }

        string route = string.Join(" → ", vertices.ConvertAll(WeightedGraph.VertexName));
        return $"{route} = {Distances[from, to]:0.##}";
    }
}

/// <summary>
/// Алгоритм Флойда—Уоршелла для поиска кратчайших путей между всеми парами вершин.
///
/// Ключевая идея: путь из i в j либо не использует вершину k, либо использует её как
/// промежуточную. Отсюда рекуррентное правило
///   D(k, i, j) = min( D(k−1, i, j), D(k−1, i, k−1) + D(k−1, k−1, j) ),
/// где k — количество разрешённых промежуточных вершин. Базовый случай — k = 0:
/// расстояния равны прямым рёбрам графа.
///
/// В приложении реализованы три версии одного и того же правила:
/// без мемоизации (экспоненциальная, 3^k вызовов на пару), с мемоизацией (O(n³))
/// и классическая итеративная (O(n³)).
/// </summary>
public static class FloydWarshall
{
    /// <summary>
    /// Предел количества вершин для рекурсии без мемоизации: 3^n растёт слишком быстро,
    /// поэтому такая версия годится только для демонстрации экспоненциальности.
    /// </summary>
    public const int MaxNaiveVertexCount = 10;

    public static string ModeName(FloydWarshallMode mode) => mode switch
    {
        FloydWarshallMode.NaiveRecursive => "Рекурсия без мемоизации",
        FloydWarshallMode.MemoizedRecursive => "Рекурсия с мемоизацией",
        FloydWarshallMode.Iterative => "Итеративный алгоритм",
        _ => "Неизвестный режим",
    };

    public static string ModeDescription(FloydWarshallMode mode) => mode switch
    {
        FloydWarshallMode.NaiveRecursive =>
            "Троичная рекурсия: каждый вызов порождает три вызова предыдущего уровня, "
            + "поэтому на одну пару вершин приходится 3^k вызовов, а всего — n²·3^n. "
            + "Наглядно показывает, почему без запоминания результатов рекурсия становится экспоненциальной.",
        FloydWarshallMode.MemoizedRecursive =>
            "Та же рекурсивная формула, но значение D(k,i,j) вычисляется один раз и сохраняется "
            + "в таблице: число вычислений становится равным числу состояний (n+1)·n², то есть сложность — O(n³).",
        FloydWarshallMode.Iterative =>
            "Классические три вложенных цикла по k, i, j. Дополнительно заполняется матрица "
            + "«следующая вершина», по которой восстанавливаются сами кратчайшие пути, а не только расстояния.",
        _ => string.Empty,
    };

    /// <summary>Находит кратчайшие расстояния между всеми парами вершин.</summary>
    /// <param name="graph">Взвешенный граф с неотрицательными весами.</param>
    /// <param name="mode">Способ вычисления.</param>
    public static FloydWarshallResult Solve(WeightedGraph graph, FloydWarshallMode mode)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var stopwatch = Stopwatch.StartNew();
        long calls = 0;
        double[,] distances;
        int[,]? nextHop = null;

        switch (mode)
        {
            case FloydWarshallMode.NaiveRecursive:
                if (graph.VertexCount > MaxNaiveVertexCount)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(graph),
                        graph.VertexCount,
                        $"Рекурсия без мемоизации выполняется за n² · 3^n вызовов и допускается "
                        + $"не более чем для {MaxNaiveVertexCount} вершин (запрошено {graph.VertexCount}).");
                }

                distances = SolveNaive(graph, ref calls);
                break;

            case FloydWarshallMode.MemoizedRecursive:
                distances = SolveMemoized(graph, ref calls);
                break;

            case FloydWarshallMode.Iterative:
                distances = SolveIterative(graph, graph.VertexCount, out nextHop);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Неизвестный способ вычисления.");
        }

        stopwatch.Stop();
        return new FloydWarshallResult(mode, graph.VertexCount, distances, calls, nextHop, stopwatch.Elapsed);
    }

    // -----------------------------------------------------------------------
    // 1. Рекурсия без мемоизации (экспоненциальная)
    // -----------------------------------------------------------------------

    private static double[,] SolveNaive(WeightedGraph graph, ref long calls)
    {
        int n = graph.VertexCount;
        double[,] weights = graph.ToAdjacencyMatrix();
        var distances = new double[n, n];

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                distances[i, j] = DistanceWithoutMemo(weights, n, i, j, ref calls);
            }
        }

        return distances;
    }

    /// <summary>
    /// D(k, i, j). Каждый вызов порождает три вызова уровня k−1 — это и есть
    /// «троичная» рекурсия, дающая 3^k вызовов.
    /// </summary>
    private static double DistanceWithoutMemo(double[,] weights, int k, int i, int j, ref long calls)
    {
        calls++;

        // --- БАЗОВЫЙ СЛУЧАЙ РЕКУРСИИ ---------------------------------------
        // Промежуточные вершины не разрешены: остаются только прямые рёбра.
        if (k == 0)
        {
            return weights[i, j];
        }

        // --- РЕКУРСИВНЫЙ ШАГ -----------------------------------------------
        // Вариант 1: путь не проходит через вершину k−1.
        double withoutIntermediate = DistanceWithoutMemo(weights, k - 1, i, j, ref calls);

        // Вариант 2: путь проходит через вершину k−1.
        double throughIntermediate =
            DistanceWithoutMemo(weights, k - 1, i, k - 1, ref calls)
            + DistanceWithoutMemo(weights, k - 1, k - 1, j, ref calls);

        return Math.Min(withoutIntermediate, throughIntermediate);
    }

    // -----------------------------------------------------------------------
    // 2. Рекурсия с мемоизацией
    // -----------------------------------------------------------------------

    private static double[,] SolveMemoized(WeightedGraph graph, ref long calls)
    {
        int n = graph.VertexCount;
        double[,] weights = graph.ToAdjacencyMatrix();

        var table = new double[n + 1, n, n];
        var computed = new bool[n + 1, n, n];
        var distances = new double[n, n];

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                distances[i, j] = DistanceWithMemo(table, computed, weights, n, i, j, ref calls);
            }
        }

        return distances;
    }

    private static double DistanceWithMemo(
        double[,,] table,
        bool[,,] computed,
        double[,] weights,
        int k,
        int i,
        int j,
        ref long calls)
    {
        calls++;

        if (computed[k, i, j])
        {
            return table[k, i, j];
        }

        double value;
        if (k == 0)
        {
            // Базовый случай: только прямые рёбра.
            value = weights[i, j];
        }
        else
        {
            double withoutIntermediate = DistanceWithMemo(table, computed, weights, k - 1, i, j, ref calls);
            double throughIntermediate =
                DistanceWithMemo(table, computed, weights, k - 1, i, k - 1, ref calls)
                + DistanceWithMemo(table, computed, weights, k - 1, k - 1, j, ref calls);

            value = Math.Min(withoutIntermediate, throughIntermediate);
        }

        table[k, i, j] = value;
        computed[k, i, j] = true;
        return value;
    }

    // -----------------------------------------------------------------------
    // 3. Итеративный алгоритм (эталон + восстановление путей)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Промежуточное состояние алгоритма: как промежуточные разрешены только первые
    /// <paramref name="allowedIntermediates"/> вершин. При k = 0 расстояния равны прямым
    /// рёбрам, при k = n получается полное решение. Такой пошаговый просмотр удобно
    /// показывать на графе: видно, как появляются всё более короткие маршруты.
    /// </summary>
    public static FloydWarshallResult SolveStep(WeightedGraph graph, int allowedIntermediates)
    {
        ArgumentNullException.ThrowIfNull(graph);

        if (allowedIntermediates < 0 || allowedIntermediates > graph.VertexCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(allowedIntermediates),
                allowedIntermediates,
                $"Число промежуточных вершин должно быть от 0 до {graph.VertexCount}.");
        }

        var stopwatch = Stopwatch.StartNew();
        double[,] distances = SolveIterative(graph, allowedIntermediates, out int[,] nextHop);
        stopwatch.Stop();

        return new FloydWarshallResult(
            FloydWarshallMode.Iterative,
            graph.VertexCount,
            distances,
            recursiveCalls: 0,
            nextHop,
            stopwatch.Elapsed,
            allowedIntermediates);
    }

    private static double[,] SolveIterative(WeightedGraph graph, int allowedIntermediates, out int[,] nextHop)
    {
        int n = graph.VertexCount;
        double[,] distances = graph.ToAdjacencyMatrix();

        nextHop = new int[n, n];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                nextHop[i, j] = double.IsPositiveInfinity(distances[i, j]) ? -1 : j;
            }
        }

        for (int k = 0; k < allowedIntermediates; k++)
        {
            for (int i = 0; i < n; i++)
            {
                if (double.IsPositiveInfinity(distances[i, k]))
                {
                    continue;
                }

                for (int j = 0; j < n; j++)
                {
                    double candidate = distances[i, k] + distances[k, j];
                    if (candidate < distances[i, j])
                    {
                        distances[i, j] = candidate;
                        nextHop[i, j] = nextHop[i, k];
                    }
                }
            }
        }

        return distances;
    }
}

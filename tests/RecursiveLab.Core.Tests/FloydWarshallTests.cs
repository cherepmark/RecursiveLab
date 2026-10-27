using RecursiveLab.Core.Algorithms;
using RecursiveLab.Core.ShortestPaths;

namespace RecursiveLab.Core.Tests;

/// <summary>
/// Тесты алгоритма Флойда—Уоршелла: корректность всех трёх реализаций,
/// экспоненциальный рост рекурсии без мемоизации, эффект мемоизации
/// и восстановление самих кратчайших путей.
/// </summary>
public class FloydWarshallTests
{
    public static TheoryData<FloydWarshallMode> AllModes()
    {
        var data = new TheoryData<FloydWarshallMode>();
        foreach (FloydWarshallMode mode in Enum.GetValues<FloydWarshallMode>())
        {
            data.Add(mode);
        }

        return data;
    }

    // ---------------------------------------------------------------------
    // Корректность расстояний
    // ---------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(AllModes))]
    public void Solve_DemoGraph_ProducesKnownDistances(FloydWarshallMode mode)
    {
        WeightedGraph graph = WeightedGraph.CreateDemo();
        FloydWarshallResult result = FloydWarshall.Solve(graph, mode);

        // Значения посчитаны вручную по графу из CreateDemo.
        double[] expectedFromA = [0, 2, 3, 5, 8, 9];
        for (int j = 0; j < expectedFromA.Length; j++)
        {
            Assert.Equal(expectedFromA[j], result.Distance(0, j), 1e-9);
        }

        Assert.Equal(1, result.Distance(1, 2), 1e-9);
        Assert.Equal(3, result.Distance(1, 3), 1e-9);
        Assert.Equal(6, result.Distance(1, 4), 1e-9);
        Assert.Equal(7, result.Distance(1, 5), 1e-9);

        Assert.Equal(2, result.Distance(2, 3), 1e-9);
        Assert.Equal(5, result.Distance(2, 4), 1e-9);
        Assert.Equal(6, result.Distance(2, 5), 1e-9);

        Assert.Equal(3, result.Distance(3, 4), 1e-9);
        Assert.Equal(4, result.Distance(3, 5), 1e-9);
        Assert.Equal(1, result.Distance(4, 5), 1e-9);
    }

    [Theory]
    [MemberData(nameof(AllModes))]
    public void Solve_DiagonalIsAlwaysZero(FloydWarshallMode mode)
    {
        WeightedGraph graph = WeightedGraph.CreateRandom(8, seed: 7);
        FloydWarshallResult result = FloydWarshall.Solve(graph, mode);

        for (int i = 0; i < graph.VertexCount; i++)
        {
            Assert.Equal(0, result.Distance(i, i), 1e-9);
        }
    }

    [Fact]
    public void Solve_AllModes_ProduceIdenticalMatrices()
    {
        var graphs = new List<WeightedGraph> { WeightedGraph.CreateDemo() };

        for (int seed = 1; seed <= 6; seed++)
        {
            graphs.Add(WeightedGraph.CreateRandom(6, seed));
        }

        foreach (WeightedGraph graph in graphs)
        {
            FloydWarshallResult iterative = FloydWarshall.Solve(graph, FloydWarshallMode.Iterative);
            FloydWarshallResult memoized = FloydWarshall.Solve(graph, FloydWarshallMode.MemoizedRecursive);
            FloydWarshallResult naive = FloydWarshall.Solve(graph, FloydWarshallMode.NaiveRecursive);

            Assert.True(iterative.Matches(memoized), "Рекурсия с мемоизацией должна совпадать с итеративной версией.");
            Assert.True(iterative.Matches(naive), "Рекурсия без мемоизации должна совпадать с итеративной версией.");
            Assert.Equal(0, Matrix.MaxDifference(iterative.Distances, naive.Distances));
        }
    }

    [Theory]
    [MemberData(nameof(AllModes))]
    public void Solve_UnreachablePairsStayInfinite(FloydWarshallMode mode)
    {
        WeightedGraph graph = WeightedGraph.CreateDemo();
        FloydWarshallResult result = FloydWarshall.Solve(graph, mode);

        // Из вершины F нет исходящих рёбер, поэтому из неё достижима только она сама.
        Assert.False(result.IsReachable(5, 0));
        Assert.False(result.IsReachable(5, 1));
        Assert.False(result.IsReachable(5, 4));

        Assert.True(result.IsReachable(0, 5));
        Assert.True(result.IsReachable(5, 5));
        Assert.True(double.IsPositiveInfinity(result.Distance(5, 0)));
    }

    // ---------------------------------------------------------------------
    // Экспоненциальный рост без мемоизации и эффект мемоизации
    // ---------------------------------------------------------------------

    [Fact]
    public void Solve_NaiveRecursive_ExactCallCountGrowsThreefoldPerVertex()
    {
        // Каждый вызов порождает три вызова предыдущего уровня: T(k) = 1 + 3·T(k−1),
        // T(0) = 1, поэтому T(k) = (3^(k+1) − 1) / 2, а всего вызовов n² · T(n).
        for (int n = 1; n <= 5; n++)
        {
            WeightedGraph graph = CreateChainGraph(n);
            FloydWarshallResult result = FloydWarshall.Solve(graph, FloydWarshallMode.NaiveRecursive);

            long unit = (Pow(3, n + 1) - 1) / 2;
            long expected = unit * n * n;

            Assert.Equal(expected, result.RecursiveCallCount);
        }
    }

    [Fact]
    public void Solve_NaiveRecursive_CallCountTriplesBetweenNeighbourSizes()
    {
        long previous = 0;

        for (int n = 3; n <= 7; n++)
        {
            WeightedGraph graph = CreateChainGraph(n);
            long calls = FloydWarshall.Solve(graph, FloydWarshallMode.NaiveRecursive).RecursiveCallCount;

            if (previous > 0)
            {
                double ratio = calls / (double)previous;
                Assert.True(ratio > 2.5, $"Ожидался примерно трёхкратный рост, получено {ratio:0.##}.");
            }

            previous = calls;
        }
    }

    [Fact]
    public void Solve_MemoizedRecursive_CallCountIsFarSmallerThanNaive()
    {
        WeightedGraph graph = WeightedGraph.CreateRandom(6, seed: 11);

        long naive = FloydWarshall.Solve(graph, FloydWarshallMode.NaiveRecursive).RecursiveCallCount;
        long memoized = FloydWarshall.Solve(graph, FloydWarshallMode.MemoizedRecursive).RecursiveCallCount;

        Assert.True(memoized * 20 < naive, $"Мемоизация должна резко уменьшить число вызовов: {memoized} против {naive}.");

        // Число вычислений при мемоизации ограничено числом состояний (n+1)·n², умноженным на небольшой множитель.
        int n = graph.VertexCount;
        Assert.True(memoized < 4L * (n + 1) * n * n, $"Ожидался рост порядка O(n³), получено {memoized}.");
    }

    [Fact]
    public void Solve_Iterative_UsesNoRecursion()
    {
        WeightedGraph graph = WeightedGraph.CreateDemo();
        FloydWarshallResult result = FloydWarshall.Solve(graph, FloydWarshallMode.Iterative);

        Assert.Equal(0, result.RecursiveCallCount);
    }

    [Fact]
    public void Solve_NaiveRecursive_RejectsTooManyVertices()
    {
        var graph = new WeightedGraph(FloydWarshall.MaxNaiveVertexCount + 1);
        graph.AddEdge(0, 1, 1);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => FloydWarshall.Solve(graph, FloydWarshallMode.NaiveRecursive));

        Assert.Contains("3^n", exception.Message, StringComparison.Ordinal);

        // Верхняя допустимая граница работает.
        var allowed = CreateChainGraph(FloydWarshall.MaxNaiveVertexCount);
        Assert.Equal(allowed.VertexCount, FloydWarshall.Solve(allowed, FloydWarshallMode.NaiveRecursive).VertexCount);
    }

    // ---------------------------------------------------------------------
    // Восстановление путей
    // ---------------------------------------------------------------------

    [Fact]
    public void Solve_Iterative_BuildsPathsWithMatchingWeight()
    {
        WeightedGraph graph = WeightedGraph.CreateDemo();
        FloydWarshallResult result = FloydWarshall.Solve(graph, FloydWarshallMode.Iterative);

        Assert.Equal("A → B → C → D → E → F = 9", result.DescribePath(0, 5));
        Assert.Equal("B → C → D → E → F = 7", result.DescribePath(1, 5));

        // Вес восстановленного пути обязан совпадать с расстоянием в матрице.
        AssertPathWeightMatchesDistance(graph, result, 0, 5);
        AssertPathWeightMatchesDistance(graph, result, 2, 5);
    }

    [Fact]
    public void Solve_Iterative_PathsAreValidForEveryReachablePair()
    {
        for (int seed = 1; seed <= 5; seed++)
        {
            WeightedGraph graph = WeightedGraph.CreateRandom(7, seed);
            FloydWarshallResult result = FloydWarshall.Solve(graph, FloydWarshallMode.Iterative);

            for (int from = 0; from < graph.VertexCount; from++)
            {
                for (int to = 0; to < graph.VertexCount; to++)
                {
                    if (from == to || !result.IsReachable(from, to))
                    {
                        continue;
                    }

                    AssertPathWeightMatchesDistance(graph, result, from, to);
                }
            }
        }
    }

    [Fact]
    public void Solve_Iterative_PathDescriptionHandlesSpecialCases()
    {
        WeightedGraph graph = WeightedGraph.CreateDemo();
        FloydWarshallResult result = FloydWarshall.Solve(graph, FloydWarshallMode.Iterative);

        Assert.Contains("пути нет", result.DescribePath(5, 0), StringComparison.Ordinal);
        Assert.Contains("расстояние 0", result.DescribePath(2, 2), StringComparison.Ordinal);
    }

    [Fact]
    public void Solve_RecursiveModes_DoNotBuildPaths()
    {
        WeightedGraph graph = WeightedGraph.CreateDemo();

        FloydWarshallResult naive = FloydWarshall.Solve(graph, FloydWarshallMode.NaiveRecursive);
        FloydWarshallResult memoized = FloydWarshall.Solve(graph, FloydWarshallMode.MemoizedRecursive);

        Assert.Null(naive.NextHop);
        Assert.Null(memoized.NextHop);
        Assert.Contains("не восстанавливались", naive.DescribePath(0, 5), StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------
    // Служебные свойства результата
    // ---------------------------------------------------------------------

    [Fact]
    public void Solve_ResultCarriesMetadata()
    {
        WeightedGraph graph = WeightedGraph.CreateDemo();

        foreach (FloydWarshallMode mode in Enum.GetValues<FloydWarshallMode>())
        {
            FloydWarshallResult result = FloydWarshall.Solve(graph, mode);

            Assert.Equal(mode, result.Mode);
            Assert.Equal(graph.VertexCount, result.VertexCount);
            Assert.Equal(FloydWarshall.ModeName(mode), result.ModeName);
            Assert.False(string.IsNullOrWhiteSpace(FloydWarshall.ModeDescription(mode)));
            Assert.True(result.Elapsed >= TimeSpan.Zero);
        }
    }

    [Fact]
    public void Solve_DifferentGraphs_DoNotMatch()
    {
        FloydWarshallResult first = FloydWarshall.Solve(WeightedGraph.CreateDemo(), FloydWarshallMode.Iterative);
        var other = new WeightedGraph(6);
        other.AddEdge(0, 5, 100);
        FloydWarshallResult second = FloydWarshall.Solve(other, FloydWarshallMode.Iterative);

        Assert.False(first.Matches(second));
        Assert.True(first.Matches(first));
    }

    [Fact]
    public void DistanceMatrixDisplay_ShowsInfinityForUnreachablePairs()
    {
        WeightedGraph graph = WeightedGraph.CreateDemo();
        FloydWarshallResult result = FloydWarshall.Solve(graph, FloydWarshallMode.Iterative);

        string display = Matrix.ToDisplayString(result.Distances);
        Assert.Contains("∞", display, StringComparison.Ordinal);
    }

    [Fact]
    public void Solve_RejectsUnknownMode()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => FloydWarshall.Solve(WeightedGraph.CreateDemo(), (FloydWarshallMode)99));
    }

    [Fact]
    public void Solve_RejectsNullGraph()
    {
        Assert.Throws<ArgumentNullException>(() => FloydWarshall.Solve(null!, FloydWarshallMode.Iterative));
    }

    // ---------------------------------------------------------------------
    // Вспомогательные методы
    // ---------------------------------------------------------------------

    private static WeightedGraph CreateChainGraph(int vertexCount)
    {
        var graph = new WeightedGraph(vertexCount);
        for (int i = 0; i + 1 < vertexCount; i++)
        {
            graph.AddEdge(i, i + 1, i + 1);
        }

        return graph;
    }

    private static void AssertPathWeightMatchesDistance(
        WeightedGraph graph,
        FloydWarshallResult result,
        int from,
        int to)
    {
        string description = result.DescribePath(from, to);
        string route = description.Split('=')[0].Trim();
        string[] names = route.Split('→', StringSplitOptions.TrimEntries);

        Assert.True(names.Length >= 2, $"Не удалось разобрать путь: {description}");

        double total = 0;
        for (int i = 1; i < names.Length; i++)
        {
            int previous = names[i - 1][0] - 'A';
            int current = names[i][0] - 'A';

            Assert.True(graph.HasEdge(previous, current), $"Ребро {names[i - 1]} → {names[i]} отсутствует в графе.");
            total += graph.GetWeight(previous, current);
        }

        Assert.Equal(result.Distance(from, to), total, 1e-9);
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

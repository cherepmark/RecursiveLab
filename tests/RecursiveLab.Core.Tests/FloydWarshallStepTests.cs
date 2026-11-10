using RecursiveLab.Core.ShortestPaths;

namespace RecursiveLab.Core.Tests;

/// <summary>
/// Тесты пошагового состояния алгоритма Флойда—Уоршелла (для отображения на графе)
/// и выделения рёбер кратчайших путей.
/// </summary>
public class FloydWarshallStepTests
{
    [Fact]
    public void SolveStep_NoIntermediates_EqualsAdjacencyMatrix()
    {
        WeightedGraph graph = WeightedGraph.CreateDemo();

        FloydWarshallResult step = FloydWarshall.SolveStep(graph, allowedIntermediates: 0);

        Assert.Equal(graph.ToAdjacencyMatrix(), step.Distances);
        Assert.Equal(0, step.AllowedIntermediates);
        Assert.False(step.IsComplete);

        // Пока промежуточные вершины не разрешены, пути совпадают с прямыми рёбрами.
        Assert.Equal(5, step.Distance(0, 2));
        Assert.True(double.IsPositiveInfinity(step.Distance(0, 5)));
    }

    [Fact]
    public void SolveStep_AllIntermediates_EqualsFullSolution()
    {
        WeightedGraph graph = WeightedGraph.CreateDemo();

        FloydWarshallResult step = FloydWarshall.SolveStep(graph, graph.VertexCount);
        FloydWarshallResult full = FloydWarshall.Solve(graph, FloydWarshallMode.Iterative);

        Assert.True(step.IsComplete);
        Assert.True(step.Matches(full));
        Assert.Equal(graph.VertexCount, step.AllowedIntermediates);
    }

    [Fact]
    public void SolveStep_ShowsHowIntermediateVerticesImprovePaths()
    {
        WeightedGraph graph = WeightedGraph.CreateDemo();

        // k = 2: как промежуточные разрешены A и B, поэтому A → C становится равным 3.
        FloydWarshallResult twoVertices = FloydWarshall.SolveStep(graph, 2);
        Assert.Equal(2, twoVertices.Distance(0, 1));
        Assert.Equal(3, twoVertices.Distance(0, 2));

        // k = 3: добавление C открывает маршрут до D длиной 5.
        FloydWarshallResult threeVertices = FloydWarshall.SolveStep(graph, 3);
        Assert.Equal(5, threeVertices.Distance(0, 3));
        Assert.Equal(5, threeVertices.Distance(0, 2) + 2);

        // k = n: полное решение, из A доходит и до F.
        FloydWarshallResult complete = FloydWarshall.SolveStep(graph, graph.VertexCount);
        Assert.Equal(9, complete.Distance(0, 5));
    }

    [Fact]
    public void SolveStep_DistancesOnlyDecreaseAsIntermediateVerticesAreAdded()
    {
        WeightedGraph graph = WeightedGraph.CreateRandom(7, seed: 5);

        double[,]? previous = null;
        for (int k = 0; k <= graph.VertexCount; k++)
        {
            double[,] current = FloydWarshall.SolveStep(graph, k).Distances;

            if (previous is not null)
            {
                for (int i = 0; i < graph.VertexCount; i++)
                {
                    for (int j = 0; j < graph.VertexCount; j++)
                    {
                        Assert.True(
                            current[i, j] <= previous[i, j] + 1e-9,
                            $"Расстояние {i}→{j} выросло при добавлении вершины {k - 1}.");
                    }
                }
            }

            previous = current;
        }
    }

    [Fact]
    public void SolveStep_PathsStayValidAtEveryStep()
    {
        WeightedGraph graph = WeightedGraph.CreateDemo();

        for (int k = 0; k <= graph.VertexCount; k++)
        {
            FloydWarshallResult step = FloydWarshall.SolveStep(graph, k);

            for (int from = 0; from < graph.VertexCount; from++)
            {
                for (int to = 0; to < graph.VertexCount; to++)
                {
                    if (from == to || !step.IsReachable(from, to))
                    {
                        continue;
                    }

                    Assert.NotEmpty(step.ShortestPathEdges(from));

                    // Все рёбра восстановленных путей должны быть рёбрами графа.
                    foreach ((int edgeFrom, int edgeTo) in step.ShortestPathEdges(from))
                    {
                        Assert.True(graph.HasEdge(edgeFrom, edgeTo), $"Ребра {edgeFrom}→{edgeTo} нет в графе.");
                    }
                }
            }
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    public void SolveStep_RejectsInvalidIntermediateCount(int allowedIntermediates)
    {
        WeightedGraph graph = WeightedGraph.CreateDemo();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => FloydWarshall.SolveStep(graph, allowedIntermediates));
    }

    [Fact]
    public void SolveStep_RejectsNullGraph()
    {
        Assert.Throws<ArgumentNullException>(() => FloydWarshall.SolveStep(null!, 0));
    }

    // ---------------------------------------------------------------------
    // Рёбра кратчайших путей и достижимые вершины
    // ---------------------------------------------------------------------

    [Fact]
    public void ShortestPathEdges_ReturnsEdgesUsedByShortestPaths()
    {
        WeightedGraph graph = WeightedGraph.CreateDemo();
        FloydWarshallResult result = FloydWarshall.Solve(graph, FloydWarshallMode.Iterative);

        IReadOnlyList<(int From, int To)> edges = result.ShortestPathEdges(0);

        Assert.Equal(5, edges.Count);
        Assert.Contains((0, 1), edges);
        Assert.Contains((1, 2), edges);
        Assert.Contains((2, 3), edges);
        Assert.Contains((3, 4), edges);
        Assert.Contains((4, 5), edges);
    }

    [Fact]
    public void ShortestPathEdges_DiffersForDifferentSources()
    {
        WeightedGraph graph = WeightedGraph.CreateDemo();
        FloydWarshallResult result = FloydWarshall.Solve(graph, FloydWarshallMode.Iterative);

        IReadOnlyList<(int From, int To)> fromA = result.ShortestPathEdges(0);
        IReadOnlyList<(int From, int To)> fromC = result.ShortestPathEdges(2);

        Assert.Equal(5, fromA.Count);
        Assert.Equal(3, fromC.Count);          // C → D → E → F
        Assert.DoesNotContain((0, 1), fromC);
        Assert.Contains((2, 3), fromC);
    }

    [Fact]
    public void ShortestPathEdges_AreEmptyWithoutPathMatrix()
    {
        WeightedGraph graph = WeightedGraph.CreateDemo();

        FloydWarshallResult naive = FloydWarshall.Solve(graph, FloydWarshallMode.NaiveRecursive);
        FloydWarshallResult memoized = FloydWarshall.Solve(graph, FloydWarshallMode.MemoizedRecursive);

        Assert.Empty(naive.ShortestPathEdges(0));
        Assert.Empty(memoized.ShortestPathEdges(0));
    }

    [Fact]
    public void ShortestPathEdges_RejectInvalidSource()
    {
        WeightedGraph graph = WeightedGraph.CreateDemo();
        FloydWarshallResult result = FloydWarshall.Solve(graph, FloydWarshallMode.Iterative);

        Assert.Empty(result.ShortestPathEdges(-1));
        Assert.Empty(result.ShortestPathEdges(6));
    }

    [Fact]
    public void ReachableVertices_IncludesSourceAndItsTargets()
    {
        WeightedGraph graph = WeightedGraph.CreateDemo();
        FloydWarshallResult result = FloydWarshall.Solve(graph, FloydWarshallMode.Iterative);

        IReadOnlyCollection<int> fromA = result.ReachableVertices(0);
        IReadOnlyCollection<int> fromF = result.ReachableVertices(5);

        Assert.Equal(6, fromA.Count);
        Assert.Contains(0, fromA);

        // Из вершины F нет исходящих рёбер.
        Assert.Single(fromF);
        Assert.Contains(5, fromF);
    }
}

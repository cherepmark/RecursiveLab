using RecursiveLab.Core.Algorithms;
using RecursiveLab.Core.ShortestPaths;

namespace RecursiveLab.Core.Tests;

/// <summary>Тесты модели взвешенного графа.</summary>
public class WeightedGraphTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(WeightedGraph.MaxVertexCount + 1)]
    public void Constructor_RejectsInvalidVertexCount(int vertexCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WeightedGraph(vertexCount));
    }

    [Fact]
    public void NewGraph_HasNoEdgesAndZeroDiagonal()
    {
        var graph = new WeightedGraph(4);

        Assert.Equal(4, graph.VertexCount);
        Assert.Equal(0, graph.EdgeCount);

        for (int i = 0; i < 4; i++)
        {
            Assert.Equal(0, graph.GetWeight(i, i));
            for (int j = 0; j < 4; j++)
            {
                if (i != j)
                {
                    Assert.False(graph.HasEdge(i, j));
                    Assert.True(double.IsPositiveInfinity(graph.GetWeight(i, j)));
                }
            }
        }
    }

    [Fact]
    public void AddEdge_StoresWeightAndCountsEdge()
    {
        var graph = new WeightedGraph(3);
        graph.AddEdge(0, 1, 2.5);

        Assert.True(graph.HasEdge(0, 1));
        Assert.False(graph.HasEdge(1, 0));
        Assert.Equal(2.5, graph.GetWeight(0, 1));
        Assert.Equal(1, graph.EdgeCount);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void AddEdge_RejectsInvalidWeight(double weight)
    {
        var graph = new WeightedGraph(3);
        Assert.Throws<ArgumentOutOfRangeException>(() => graph.AddEdge(0, 1, weight));
    }

    [Fact]
    public void AddEdge_RejectsNegativeWeightAndSelfLoop()
    {
        var graph = new WeightedGraph(3);

        Assert.Throws<ArgumentOutOfRangeException>(() => graph.AddEdge(0, 1, -3));
        Assert.Throws<ArgumentException>(() => graph.AddEdge(2, 2, 1));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void Indexers_RejectInvalidVertex(int vertex)
    {
        var graph = new WeightedGraph(3);

        Assert.Throws<ArgumentOutOfRangeException>(() => graph.GetWeight(vertex, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => graph.GetWeight(0, vertex));
        Assert.Throws<ArgumentOutOfRangeException>(() => graph.AddEdge(vertex, 0, 1));
    }

    [Fact]
    public void DemoGraph_HasExpectedShape()
    {
        WeightedGraph graph = WeightedGraph.CreateDemo();

        Assert.Equal(6, graph.VertexCount);
        Assert.Equal(9, graph.EdgeCount);
        Assert.Equal(2, graph.GetWeight(0, 1));
        Assert.False(graph.HasEdge(1, 0));
    }

    [Fact]
    public void EnumerateEdges_ReturnsEveryEdge()
    {
        WeightedGraph graph = WeightedGraph.CreateDemo();
        var edges = graph.EnumerateEdges().ToList();

        Assert.Equal(9, edges.Count);
        Assert.Contains(edges, edge => edge is { From: 0, To: 1, Weight: 2 });
        Assert.Contains(edges, edge => edge is { From: 4, To: 5, Weight: 1 });
    }

    [Fact]
    public void CreateRandom_IsDeterministicForSameSeed()
    {
        WeightedGraph first = WeightedGraph.CreateRandom(7, seed: 42);
        WeightedGraph second = WeightedGraph.CreateRandom(7, seed: 42);

        Assert.Equal(first.EdgeCount, second.EdgeCount);
        Assert.Equal(first.ToAdjacencyMatrix(), second.ToAdjacencyMatrix());
    }

    [Fact]
    public void CreateRandom_RejectsInvalidProbability()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WeightedGraph.CreateRandom(4, 1, edgeProbability: 1.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => WeightedGraph.CreateRandom(4, 1, edgeProbability: -0.1));
    }

    [Fact]
    public void ToAdjacencyMatrix_ReturnsIndependentCopy()
    {
        WeightedGraph graph = WeightedGraph.CreateDemo();
        double[,] matrix = graph.ToAdjacencyMatrix();

        matrix[0, 1] = 999;

        Assert.Equal(2, graph.GetWeight(0, 1));
    }

    [Theory]
    [InlineData(0, "A")]
    [InlineData(1, "B")]
    [InlineData(25, "Z")]
    public void VertexName_UsesLetters(int index, string expected)
    {
        Assert.Equal(expected, WeightedGraph.VertexName(index));
    }
}

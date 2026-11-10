using RecursiveLab.Core.Algorithms;
using RecursiveLab.Core.Fractals;
using RecursiveLab.Core.Recursion;
using RecursiveLab.Core.ShortestPaths;

namespace RecursiveLab.Core.Tests;

/// <summary>
/// Тесты деревьев рекурсии для трёх индивидуальных алгоритмов: форма дерева,
/// базовые случаи, формулы числа узлов, ограничение размера схемы и пометка
/// повторных состояний при мемоизации.
/// </summary>
public class RecursionTreeTests
{
    // ---------------------------------------------------------------------
    // Флойд — Уоршелл: троичное дерево
    // ---------------------------------------------------------------------

    [Fact]
    public void FloydTree_Naive_HasTernaryBranchingAndPowerOfThreeBaseCases()
    {
        WeightedGraph graph = CreateChainGraph(3);

        RecursionTree tree = FloydWarshallTree.Build(graph, FloydWarshallMode.NaiveRecursive, from: 0, to: 2);

        // T(3) = 1 + 3 + 9 + 27 = 40 вызовов, из них 27 — базовые случаи.
        Assert.Equal(40, tree.NodeCount);
        Assert.Equal(40, tree.TheoreticalNodeCount);
        Assert.Equal(27, tree.BaseCaseCount);
        Assert.Equal(4, tree.Depth);
        Assert.False(tree.Truncated);
        Assert.Equal(0, tree.CachedCount);

        Assert.Equal("D(3, A, C)", tree.Root.Title);
        Assert.Equal(3, tree.Root.Children.Count);
        Assert.All(tree.Root.Children, child => Assert.Equal(3, child.Children.Count));
    }

    [Fact]
    public void FloydTree_Naive_LimitedDepthMarksBranchAsTruncated()
    {
        WeightedGraph graph = CreateChainGraph(3);

        RecursionTree tree = FloydWarshallTree.Build(
            graph,
            FloydWarshallMode.NaiveRecursive,
            0,
            2,
            new RecursionTreeLimits().With(2, 100));

        // Корень и его три потомка изображаются, дальше ветви обрезаны.
        Assert.True(tree.Truncated);
        Assert.Equal(4, tree.NodeCount);
        Assert.Equal(3, tree.Root.Children.Count);
        Assert.All(tree.Root.Children, child =>
        {
            Assert.True(child.Truncated);
            Assert.Contains("не раскрыта", child.Detail!, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void FloydTree_Naive_BaseCaseShowsDirectEdgeWeight()
    {
        WeightedGraph graph = CreateChainGraph(3);

        RecursionTree tree = FloydWarshallTree.Build(graph, FloydWarshallMode.NaiveRecursive, 0, 1, new RecursionTreeLimits().With(5, 400));

        RecursionNode? baseCase = FindFirst(tree.Root, RecursionNodeKind.BaseCase);

        Assert.NotNull(baseCase);
        Assert.Contains("прямое ребро = 1", baseCase!.Detail!, StringComparison.Ordinal);
    }

    [Fact]
    public void FloydTree_Memoized_MarksRepeatedStatesAsCached()
    {
        WeightedGraph graph = CreateChainGraph(4);

        RecursionTree naive = FloydWarshallTree.Build(graph, FloydWarshallMode.NaiveRecursive, 0, 3);
        RecursionTree memoized = FloydWarshallTree.Build(graph, FloydWarshallMode.MemoizedRecursive, 0, 3);

        Assert.True(memoized.CachedCount > 0, "При мемоизации должны появляться повторно используемые состояния.");
        Assert.True(
            memoized.NodeCount < naive.NodeCount,
            $"Дерево с мемоизацией ({memoized.NodeCount}) должно быть меньше дерева без неё ({naive.NodeCount}).");
    }

    [Fact]
    public void FloydTree_LargeGraph_IsTruncatedToLimits()
    {
        WeightedGraph graph = CreateChainGraph(10);
        var limits = new RecursionTreeLimits().With(3, 60);

        RecursionTree tree = FloydWarshallTree.Build(graph, FloydWarshallMode.NaiveRecursive, 0, 9, limits);

        Assert.True(tree.Truncated);
        Assert.True(tree.NodeCount <= limits.MaxNodes, $"Узлов на схеме: {tree.NodeCount}.");
        Assert.True(tree.TheoreticalNodeCount > 1000);
        Assert.Contains("обрезано", tree.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void FloydTree_IterativeMode_HasNoRecursionTree()
    {
        WeightedGraph graph = CreateChainGraph(4);

        var exception = Assert.Throws<ArgumentException>(
            () => FloydWarshallTree.Build(graph, FloydWarshallMode.Iterative, 0, 3));

        Assert.Contains("не использует рекурсию", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FloydTree_RejectsInvalidVertices()
    {
        WeightedGraph graph = CreateChainGraph(4);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => FloydWarshallTree.Build(graph, FloydWarshallMode.NaiveRecursive, 0, 9));
        Assert.Throws<ArgumentNullException>(
            () => FloydWarshallTree.Build(null!, FloydWarshallMode.NaiveRecursive, 0, 1));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 4)]
    [InlineData(2, 13)]
    [InlineData(3, 40)]
    [InlineData(4, 121)]
    [InlineData(5, 364)]
    public void FloydTree_TernaryCallCountFollowsFormula(int vertexCount, long expected)
    {
        Assert.Equal(expected, FloydWarshallTree.TernaryCallCount(vertexCount));

        // Формула совпадает с фактическим числом узлов полного дерева.
        WeightedGraph graph = CreateChainGraph(Math.Max(1, vertexCount));
        if (vertexCount >= 1)
        {
            RecursionTree tree = FloydWarshallTree.Build(
                graph,
                FloydWarshallMode.NaiveRecursive,
                0,
                vertexCount - 1,
                new RecursionTreeLimits().With(vertexCount + 1, 1000));

            Assert.Equal(expected, tree.TheoreticalNodeCount);
        }
    }

    // ---------------------------------------------------------------------
    // Штрассен: семеричное дерево
    // ---------------------------------------------------------------------

    [Fact]
    public void StrassenTree_HasSevenChildrenAndPowerOfSevenBaseCases()
    {
        RecursionTree tree = StrassenTree.Build(size: 8, threshold: 2);

        // 8 → 4 → 2: два разбиения, узлов 1 + 7 + 49 = 57, базовых случаев 49.
        Assert.Equal(57, tree.NodeCount);
        Assert.Equal(57, tree.TheoreticalNodeCount);
        Assert.Equal(49, tree.BaseCaseCount);
        Assert.Equal(3, tree.Depth);
        Assert.False(tree.Truncated);
        Assert.Equal(7, tree.Root.Children.Count);
        Assert.Equal("8×8", tree.Root.Title);
    }

    [Fact]
    public void StrassenTree_BaseCaseWhenSizeFitsThreshold()
    {
        RecursionTree tree = StrassenTree.Build(size: 4, threshold: 4);

        Assert.Equal(1, tree.NodeCount);
        Assert.Equal(RecursionNodeKind.BaseCase, tree.Root.Kind);
        Assert.Equal(0, StrassenTree.CountSplits(4, 4));
    }

    [Fact]
    public void StrassenTree_LargeSizeIsTruncated()
    {
        var limits = new RecursionTreeLimits().With(5, 200);

        RecursionTree tree = StrassenTree.Build(size: 32, threshold: 1, limits);

        Assert.True(tree.Truncated);
        Assert.True(tree.NodeCount <= limits.MaxNodes, $"Узлов на схеме: {tree.NodeCount}.");
        Assert.True(tree.TheoreticalNodeCount > 10_000);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 8)]
    [InlineData(2, 57)]
    [InlineData(3, 400)]
    public void StrassenTree_TheoreticalNodeCountIsSumOfSevenPowers(int splits, long expected)
    {
        Assert.Equal(expected, StrassenTree.TheoreticalNodeCount(splits));

        if (splits > 0)
        {
            // На последнем уровне ровно 7^splits листьев.
            Assert.Equal(
                (long)Math.Pow(7, splits),
                StrassenTree.TheoreticalNodeCount(splits) - StrassenTree.TheoreticalNodeCount(splits - 1));
        }
    }

    [Theory]
    [InlineData(8, 2, 2)]
    [InlineData(8, 1, 3)]
    [InlineData(32, 1, 5)]
    [InlineData(16, 16, 0)]
    public void StrassenTree_CountSplitsHalvesSizeUntilThreshold(int size, int threshold, int expectedSplits)
    {
        Assert.Equal(expectedSplits, StrassenTree.CountSplits(size, threshold));
    }

    [Fact]
    public void StrassenTree_RejectsInvalidArguments()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StrassenTree.Build(0, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => StrassenTree.Build(8, 0));
    }

    [Fact]
    public void StrassenTree_LeafCountMatchesSevenPowerForFullTree()
    {
        RecursionTree tree = StrassenTree.Build(size: 16, threshold: 1, new RecursionTreeLimits().With(6, 4000));

        // 16 → 8 → 4 → 2 → 1: четыре разбиения, листьев 7^4 = 2401.
        Assert.Equal(4, StrassenTree.CountSplits(16, 1));
        Assert.Equal(2401, tree.BaseCaseCount);
        Assert.False(tree.Truncated);
    }

    // ---------------------------------------------------------------------
    // Кривая Гильберта: четверное дерево с ориентациями
    // ---------------------------------------------------------------------

    [Fact]
    public void HilbertTree_HasFourChildrenAndBaseCasesAtOrderOne()
    {
        RecursionTree tree = HilbertCurveTree.Build(order: 4);

        // 1 + 4 + 16 + 64 = 85 узлов, базовых случаев 64.
        Assert.Equal(85, tree.NodeCount);
        Assert.Equal(85, tree.TheoreticalNodeCount);
        Assert.Equal(64, tree.BaseCaseCount);
        Assert.Equal(4, tree.Depth);
        Assert.False(tree.Truncated);
        Assert.Equal(4, tree.Root.Children.Count);
        Assert.Equal("порядок 4", tree.Root.Title);
    }

    [Fact]
    public void HilbertTree_ChildrenOrientationsFollowXorRule()
    {
        RecursionTree fromBase = HilbertCurveTree.Build(order: 2, orientation: 0);
        RecursionTree fromRotated = HilbertCurveTree.Build(order: 2, orientation: 1);

        // Для ориентации 0 подкривые получают коды 1, 0, 0, 2 (порядок обхода подквадратов).
        int[] expectedFromBase = [.. HilbertCurve.VisitOrder.Select(sub => sub.Orientation)];
        Assert.Equal(expectedFromBase, ChildrenOrientations(fromBase.Root));

        // Ориентация родителя складывается с кодом подквадрата по модулю 2.
        int[] expectedFromRotated = [.. expectedFromBase.Select(code => code ^ 1)];
        Assert.Equal(expectedFromRotated, ChildrenOrientations(fromRotated.Root));
    }

    [Fact]
    public void HilbertTree_OrientationDoesNotChangeTreeSize()
    {
        RecursionTree reference = HilbertCurveTree.Build(order: 4, orientation: 0);

        for (int orientation = 1; orientation < HilbertCurve.OrientationCount; orientation++)
        {
            RecursionTree other = HilbertCurveTree.Build(order: 4, orientation);

            Assert.Equal(reference.NodeCount, other.NodeCount);
            Assert.Equal(reference.BaseCaseCount, other.BaseCaseCount);
        }
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 5)]
    [InlineData(3, 21)]
    [InlineData(4, 85)]
    [InlineData(7, 5461)]
    public void HilbertTree_TheoreticalNodeCountFollowsFormula(int order, long expected)
    {
        Assert.Equal(expected, HilbertCurveTree.TheoreticalNodeCount(order));
    }

    [Fact]
    public void HilbertTree_DepthLimitTruncatesPicture()
    {
        var limits = new RecursionTreeLimits().With(3, 1000);

        RecursionTree tree = HilbertCurveTree.Build(order: 7, orientation: 0, limits);

        Assert.True(tree.Truncated);
        Assert.Equal(21, tree.NodeCount);   // 1 + 4 + 16
        Assert.Equal(5461, tree.TheoreticalNodeCount);
    }

    [Fact]
    public void HilbertTree_RejectsInvalidArguments()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HilbertCurveTree.Build(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => HilbertCurveTree.Build(3, orientation: 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => HilbertCurveTree.TheoreticalNodeCount(0));
    }

    // ---------------------------------------------------------------------
    // Общие свойства модели
    // ---------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(AllTrees))]
    public void EveryTree_ReportsConsistentSummary(string name, RecursionTree tree)
    {
        _ = name;

        Assert.True(tree.NodeCount >= 1);
        Assert.True(tree.Depth >= 1);
        Assert.True(tree.TheoreticalNodeCount >= tree.NodeCount);
        Assert.Contains("узлов", tree.Summary, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(tree.Title));
        Assert.False(string.IsNullOrWhiteSpace(tree.Explanation));
        Assert.Equal(tree.NodeCount, CountNodes(tree.Root));
    }

    public static TheoryData<string, RecursionTree> AllTrees()
    {
        var data = new TheoryData<string, RecursionTree>
        {
            { "Флойд (без мемоизации)", FloydWarshallTree.Build(CreateChainGraph(4), FloydWarshallMode.NaiveRecursive, 0, 3) },
            { "Флойд (мемоизация)", FloydWarshallTree.Build(CreateChainGraph(4), FloydWarshallMode.MemoizedRecursive, 0, 3) },
            { "Штрассен", StrassenTree.Build(8, 2) },
            { "Гильберт", HilbertCurveTree.Build(3) },
        };

        return data;
    }

    [Fact]
    public void RecursionTreeLimits_DefaultsAreReasonable()
    {
        RecursionTreeLimits limits = RecursionTreeLimits.Default;

        Assert.True(limits.MaxDepth >= 3);
        Assert.True(limits.MaxNodes >= 100);
        Assert.Equal(7, limits.With(7, 42).MaxDepth);
    }

    [Fact]
    public void RecursionNode_RejectsEmptyTitle()
    {
        Assert.Throws<ArgumentException>(() => new RecursionNode("  "));
    }

    private static WeightedGraph CreateChainGraph(int vertexCount)
    {
        var graph = new WeightedGraph(vertexCount);
        for (int i = 0; i + 1 < vertexCount; i++)
        {
            graph.AddEdge(i, i + 1, i + 1);
        }

        return graph;
    }

    private static RecursionNode? FindFirst(RecursionNode node, RecursionNodeKind kind)
    {
        if (node.Kind == kind)
        {
            return node;
        }

        foreach (RecursionNode child in node.Children)
        {
            RecursionNode? found = FindFirst(child, kind);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static int[] ChildrenOrientations(RecursionNode node)
        => [.. node.Children.Select(child => int.Parse(
            child.Detail!.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1],
            System.Globalization.CultureInfo.InvariantCulture))];

    private static int CountNodes(RecursionNode node)
        => 1 + node.Children.Sum(CountNodes);
}

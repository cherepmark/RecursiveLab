namespace RecursiveLab.Core.ShortestPaths;

/// <summary>
/// Взвешенный граф, заданный матрицей смежности.
///
/// Отсутствие ребра обозначается <see cref="NoEdge"/> (плюс бесконечность), поэтому
/// формулы поиска кратчайших путей работают с ней напрямую: <c>min(∞, ∞ + ∞) = ∞</c>.
/// Допускаются только неотрицательные веса — это условие корректности классической
/// формулировки алгоритма Флойда—Уоршелла без анализа отрицательных циклов.
/// </summary>
public sealed class WeightedGraph
{
    /// <summary>Признак отсутствия ребра (пути).</summary>
    public const double NoEdge = double.PositiveInfinity;

    /// <summary>Технический предел количества вершин: матрица — кубическая структура данных.</summary>
    public const int MaxVertexCount = 12;

    private readonly double[,] _weights;

    /// <summary>Создаёт граф без рёбер с заданным числом вершин.</summary>
    public WeightedGraph(int vertexCount)
    {
        if (vertexCount < 1 || vertexCount > MaxVertexCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(vertexCount),
                vertexCount,
                $"Количество вершин должно быть от 1 до {MaxVertexCount}.");
        }

        VertexCount = vertexCount;
        _weights = new double[vertexCount, vertexCount];

        for (int i = 0; i < vertexCount; i++)
        {
            for (int j = 0; j < vertexCount; j++)
            {
                _weights[i, j] = i == j ? 0 : NoEdge;
            }
        }
    }

    public int VertexCount { get; }

    /// <summary>Количество рёбер графа.</summary>
    public int EdgeCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < VertexCount; i++)
            {
                for (int j = 0; j < VertexCount; j++)
                {
                    if (i != j && !double.IsPositiveInfinity(_weights[i, j]))
                    {
                        count++;
                    }
                }
            }

            return count;
        }
    }

    /// <summary>Вес ребра или <see cref="NoEdge"/>, если ребра нет.</summary>
    public double GetWeight(int from, int to)
    {
        ValidateVertex(from, nameof(from));
        ValidateVertex(to, nameof(to));
        return _weights[from, to];
    }

    public bool HasEdge(int from, int to) => !double.IsPositiveInfinity(GetWeight(from, to));

    /// <summary>Добавляет (или заменяет) ребро графа.</summary>
    public void AddEdge(int from, int to, double weight)
    {
        ValidateVertex(from, nameof(from));
        ValidateVertex(to, nameof(to));

        if (from == to)
        {
            throw new ArgumentException("Петли (ребро из вершины в саму себя) не поддерживаются.", nameof(to));
        }

        if (double.IsNaN(weight) || double.IsInfinity(weight))
        {
            throw new ArgumentOutOfRangeException(nameof(weight), weight, "Вес ребра должен быть конечным числом.");
        }

        if (weight < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(weight),
                weight,
                "Вес ребра не может быть отрицательным: алгоритм Флойда—Уоршелла рассматривается без отрицательных циклов.");
        }

        _weights[from, to] = weight;
    }

    /// <summary>Копия матрицы смежности (значения вне рёбер — <see cref="NoEdge"/>).</summary>
    public double[,] ToAdjacencyMatrix()
    {
        var copy = new double[VertexCount, VertexCount];
        for (int i = 0; i < VertexCount; i++)
        {
            for (int j = 0; j < VertexCount; j++)
            {
                copy[i, j] = _weights[i, j];
            }
        }

        return copy;
    }

    /// <summary>Перечисление всех рёбер графа.</summary>
    public IEnumerable<(int From, int To, double Weight)> EnumerateEdges()
    {
        for (int i = 0; i < VertexCount; i++)
        {
            for (int j = 0; j < VertexCount; j++)
            {
                if (i != j && !double.IsPositiveInfinity(_weights[i, j]))
                {
                    yield return (i, j, _weights[i, j]);
                }
            }
        }
    }

    /// <summary>Имя вершины для интерфейса: A, B, C, …</summary>
    public static string VertexName(int vertexIndex)
        => vertexIndex is >= 0 and < 26 ? ((char)('A' + vertexIndex)).ToString() : $"#{vertexIndex}";

    /// <summary>
    /// Учебный граф из шести вершин с известными кратчайшими расстояниями
    /// (используется как демонстрационный пример).
    /// </summary>
    public static WeightedGraph CreateDemo()
    {
        var graph = new WeightedGraph(6);

        graph.AddEdge(0, 1, 2);   // A → B
        graph.AddEdge(0, 2, 5);   // A → C
        graph.AddEdge(1, 2, 1);   // B → C
        graph.AddEdge(1, 3, 4);   // B → D
        graph.AddEdge(1, 5, 10);  // B → F
        graph.AddEdge(2, 3, 2);   // C → D
        graph.AddEdge(2, 4, 7);   // C → E
        graph.AddEdge(3, 4, 3);   // D → E
        graph.AddEdge(4, 5, 1);   // E → F

        return graph;
    }

    /// <summary>Случайный граф с заданной плотностью рёбер.</summary>
    public static WeightedGraph CreateRandom(
        int vertexCount,
        int seed,
        double edgeProbability = 0.45,
        int maxWeight = 20)
    {
        if (edgeProbability is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(edgeProbability), edgeProbability, "Вероятность должна быть от 0 до 1.");
        }

        var graph = new WeightedGraph(vertexCount);
        var random = new Random(seed);

        for (int i = 0; i < vertexCount; i++)
        {
            for (int j = 0; j < vertexCount; j++)
            {
                if (i == j || random.NextDouble() > edgeProbability)
                {
                    continue;
                }

                graph.AddEdge(i, j, random.Next(1, maxWeight + 1));
            }
        }

        return graph;
    }

    public override string ToString()
        => $"Граф: {VertexCount} вершин, {EdgeCount} рёбер";

    private void ValidateVertex(int vertex, string parameterName)
    {
        if (vertex < 0 || vertex >= VertexCount)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                vertex,
                $"Индекс вершины должен быть от 0 до {VertexCount - 1}.");
        }
    }
}

namespace RecursiveLab.Core.Recursion;

/// <summary>Вид узла дерева рекурсии.</summary>
public enum RecursionNodeKind
{
    /// <summary>Обычный рекурсивный вызов.</summary>
    Call = 0,

    /// <summary>Базовый случай рекурсии — вызов, который не порождает новых вызовов.</summary>
    BaseCase = 1,

    /// <summary>
    /// Результат взят из таблицы (мемоизация): значение уже было вычислено ранее,
    /// поэтому повторных вызовов не происходит.
    /// </summary>
    Cached = 2,
}

/// <summary>
/// Узел дерева рекурсии: подпись вызова, вид узла и дочерние вызовы.
/// Модель не зависит от графической библиотеки — её можно проверять тестами.
/// </summary>
public sealed class RecursionNode
{
    public RecursionNode(
        string title,
        RecursionNodeKind kind = RecursionNodeKind.Call,
        string? detail = null,
        IReadOnlyList<RecursionNode>? children = null,
        bool truncated = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        Title = title;
        Kind = kind;
        Detail = detail;
        Children = children ?? [];
        Truncated = truncated;
    }

    /// <summary>Подпись вызова, например «D(3, A, C)» или «4×4».</summary>
    public string Title { get; }

    /// <summary>Дополнительная информация: значение базового случая, пометка об ориентации и т. п.</summary>
    public string? Detail { get; }

    public RecursionNodeKind Kind { get; }

    public IReadOnlyList<RecursionNode> Children { get; }

    /// <summary>Узел не раскрыт: сработало ограничение на глубину или число узлов.</summary>
    public bool Truncated { get; }

    public bool IsLeaf => Children.Count == 0;

    public override string ToString()
        => Detail is null ? Title : $"{Title} ({Detail})";
}

/// <summary>Ограничения на размер изображаемого дерева рекурсии.</summary>
public sealed class RecursionTreeLimits
{
    /// <summary>Максимальная высота дерева (число уровней рекурсии на картинке).</summary>
    public int MaxDepth { get; init; } = 5;

    /// <summary>Максимальное число изображаемых узлов.</summary>
    public int MaxNodes { get; init; } = 300;

    /// <summary>Значения по умолчанию.</summary>
    public static RecursionTreeLimits Default => new();

    public RecursionTreeLimits With(int maxDepth, int maxNodes) => new() { MaxDepth = maxDepth, MaxNodes = maxNodes };
}

/// <summary>
/// Счётчик узлов дерева рекурсии. Полное дерево реального алгоритма может содержать
/// миллионы вызовов (например, 3^k у Флойда—Уоршелла), поэтому для картинки дерево
/// записывается с ограничениями, а фактическое число узлов считается по формуле.
/// </summary>
public sealed class RecursionTreeRecorder
{
    private readonly int _maxNodes;
    private readonly int _maxDepth;

    public RecursionTreeRecorder(RecursionTreeLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);

        _maxDepth = Math.Max(1, limits.MaxDepth);
        _maxNodes = Math.Max(1, limits.MaxNodes);
    }

    /// <summary>Сколько узлов уже записано.</summary>
    public int NodeCount { get; private set; }

    /// <summary>Дерево оказалось обрезано из-за ограничений.</summary>
    public bool Truncated { get; private set; }

    /// <summary>Занимает место под узел на заданной глубине.</summary>
    public bool TryReserve(int depth)
    {
        if (depth > _maxDepth || NodeCount >= _maxNodes)
        {
            Truncated = true;
            return false;
        }

        NodeCount++;
        return true;
    }

    /// <summary>
    /// Пытается занять место сразу под группу узлов одного уровня (все дети или ни одного).
    /// Так на схеме не появляются «половинчатые» ветви, а счётчик узлов остаётся точным.
    /// </summary>
    public bool TryReserveGroup(int depth, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (depth > _maxDepth || NodeCount + count > _maxNodes)
        {
            Truncated = true;
            return false;
        }

        NodeCount += count;
        return true;
    }

    /// <summary>
    /// Проверяет, поместится ли группа узлов на заданной глубине, но место не занимает.
    /// </summary>
    public bool CanReserve(int depth) => depth <= _maxDepth && NodeCount < _maxNodes;

    /// <summary>Помечает дерево как обрезанное (например, когда дети не раскрыты).</summary>
    public void MarkTruncated() => Truncated = true;
}

/// <summary>Готовое дерево рекурсии для отображения на холсте.</summary>
public sealed class RecursionTree
{
    public RecursionTree(
        string title,
        string explanation,
        RecursionNode root,
        long theoreticalNodeCount,
        bool truncated)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(root);

        Title = title;
        Explanation = explanation ?? string.Empty;
        Root = root;
        TheoreticalNodeCount = theoreticalNodeCount;
        Truncated = truncated;
        NodeCount = Count(root);
        Depth = MeasureDepth(root);
        BaseCaseCount = CountOf(root, RecursionNodeKind.BaseCase);
        CachedCount = CountOf(root, RecursionNodeKind.Cached);
    }

    public string Title { get; }

    /// <summary>Пояснение, что именно изображено.</summary>
    public string Explanation { get; }

    public RecursionNode Root { get; }

    /// <summary>Число узлов на картинке.</summary>
    public int NodeCount { get; }

    /// <summary>Высота дерева на картинке.</summary>
    public int Depth { get; }

    /// <summary>Полное число вызовов у настоящего алгоритма (может быть очень большим).</summary>
    public long TheoreticalNodeCount { get; }

    /// <summary>Дерево изображено не полностью.</summary>
    public bool Truncated { get; }

    public int BaseCaseCount { get; }

    public int CachedCount { get; }

    /// <summary>Строка со сведениями для интерфейса.</summary>
    public string Summary
    {
        get
        {
            string truncation = Truncated ? " · дерево обрезано по размеру картинки" : string.Empty;
            string theoretical = TheoreticalNodeCount > NodeCount
                ? $"из {TheoreticalNodeCount:N0} вызовов"
                : "все вызовы";

            return $"узлов на схеме: {NodeCount:N0} {theoretical} · уровней: {Depth} · "
                + $"базовых случаев: {BaseCaseCount:N0}"
                + (CachedCount > 0 ? $" · взято из таблицы: {CachedCount:N0}" : string.Empty)
                + truncation;
        }
    }

    private static int Count(RecursionNode node)
    {
        int total = 1;
        foreach (RecursionNode child in node.Children)
        {
            total += Count(child);
        }

        return total;
    }

    private static int MeasureDepth(RecursionNode node)
    {
        int deepest = 0;
        foreach (RecursionNode child in node.Children)
        {
            deepest = Math.Max(deepest, MeasureDepth(child));
        }

        return deepest + 1;
    }

    private static int CountOf(RecursionNode node, RecursionNodeKind kind)
    {
        int total = node.Kind == kind ? 1 : 0;
        foreach (RecursionNode child in node.Children)
        {
            total += CountOf(child, kind);
        }

        return total;
    }
}

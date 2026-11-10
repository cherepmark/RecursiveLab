using RecursiveLab.Core.Recursion;

namespace RecursiveLab.Core.Fractals;

/// <summary>
/// Дерево рекурсии построения кривой Гильберта.
///
/// Каждый узел — построение кривой заданного порядка с заданной ориентацией. Узел имеет
/// ровно четыре дочерних: квадрат делится на четыре подквадрата, и каждый обходится кривой
/// на единицу меньшего порядка со своей ориентацией.
///
/// Второй параметр (ориентация) на схеме видно явно: ориентация подкривой вычисляется как
/// «ориентация родителя XOR код подквадрата», поэтому дерево показывает, как поворот
/// распространяется по уровням рекурсии.
/// </summary>
public static class HilbertCurveTree
{
    /// <summary>Дерево рекурсии для кривой заданного порядка.</summary>
    public static RecursionTree Build(int order, int orientation = 0, RecursionTreeLimits? limits = null)
    {
        if (order < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(order), order, "Порядок кривой должен быть не меньше 1.");
        }

        if (orientation < 0 || orientation >= HilbertCurve.OrientationCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(orientation),
                orientation,
                $"Ориентация должна быть от 0 до {HilbertCurve.OrientationCount - 1}.");
        }

        limits ??= RecursionTreeLimits.Default;
        var recorder = new RecursionTreeRecorder(limits);
        recorder.TryReserve(1);
        RecursionNode root = BuildNode(order, orientation, recorder, depth: 1);

        long theoretical = TheoreticalNodeCount(order);

        string explanation =
            $"Каждый узел — построение кривой порядка n: четыре дочерних узла соответствуют "
            + $"четырём подквадратам порядка n − 1. Базовый случай — порядок 1 (заготовка из "
            + $"четырёх точек). Всего узлов: 1 + 4 + … + 4^(n−1) = {theoretical:N0}.";

        return new RecursionTree(
            $"Дерево рекурсии кривой Гильберта: порядок {order}, ориентация {orientation}",
            explanation,
            root,
            theoretical,
            recorder.Truncated);
    }

    /// <summary>Полное число узлов дерева: 1 + 4 + … + 4^(order−1) = (4^order − 1) / 3.</summary>
    public static long TheoreticalNodeCount(int order)
    {
        if (order < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(order), order, "Порядок кривой должен быть не меньше 1.");
        }

        long nodes = 0;
        long level = 1;
        for (int i = 0; i < order; i++)
        {
            nodes += level;
            level *= HilbertCurve.OrientationCount;
        }

        return nodes;
    }

    private static RecursionNode BuildNode(int order, int orientation, RecursionTreeRecorder recorder, int depth)
    {
        string title = $"порядок {order}";

        // --- БАЗОВЫЙ СЛУЧАЙ РЕКУРСИИ: порядок 1 — заготовка из четырёх точек ---
        if (order == 1)
        {
            return new RecursionNode(
                title,
                RecursionNodeKind.BaseCase,
                $"ориентация {orientation} · базовый случай: 4 точки",
                children: null,
                truncated: false);
        }

        // Четыре подкривые бронируются вместе (иначе узел помечается обрезанным).
        if (!recorder.TryReserveGroup(depth + 1, HilbertCurve.OrientationCount))
        {
            recorder.MarkTruncated();
            return new RecursionNode(
                title,
                RecursionNodeKind.Call,
                $"ориентация {orientation} · … ветвь не раскрыта",
                children: null,
                truncated: true);
        }

        // --- РЕКУРСИВНЫЙ ШАГ: четыре подквадрата с собственными ориентациями ---
        var children = new List<RecursionNode>(HilbertCurve.OrientationCount);
        var childOrientations = new List<int>(HilbertCurve.OrientationCount);

        foreach (HilbertCurve.SubSquareOrder subSquare in HilbertCurve.VisitOrder)
        {
            int childOrientation = HilbertCurve.ComposeOrientation(orientation, subSquare.Orientation);
            childOrientations.Add(childOrientation);
            children.Add(BuildNode(order - 1, childOrientation, recorder, depth + 1));
        }

        string detail = $"ориентация {orientation} → {string.Join(", ", childOrientations)}";
        return new RecursionNode(title, RecursionNodeKind.Call, detail, children);
    }
}

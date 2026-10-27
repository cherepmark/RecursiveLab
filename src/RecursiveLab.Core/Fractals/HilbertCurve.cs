using RecursiveLab.Core.Geometry;

namespace RecursiveLab.Core.Fractals;

/// <summary>
/// Рекурсивное построение кривой Гильберта.
///
/// Кривая Гильберта — пространственная фрактальная кривая: она проходит через каждую
/// клетку квадрата 2^n × 2^n ровно один раз и при этом никогда не пересекает себя.
/// Строится рекурсией «четвёртого порядка»: квадрат делится на четыре подквадрата,
/// каждый обходится такой же кривой меньшего порядка, а направление обхода и поворот
/// каждой из четырёх копий задаются параметром <b>ориентации</b>.
///
/// Ориентация — элемент группы симметрий квадрата из четырёх преобразований:
/// <list type="number">
///   <item>0 — тождественное преобразование;</item>
///   <item>1 — отражение относительно главной диагонали (x,y) → (y,x);</item>
///   <item>2 — отражение относительно побочной диагонали (x,y) → (1−y,1−x);</item>
///   <item>3 — поворот на 180° (x,y) → (1−x,1−y).</item>
/// </list>
/// Все четыре преобразования — инволюции, а их композиция задаётся побитовым
/// сложением по модулю 2 (группа Клейна), поэтому ориентации подкривых
/// вычисляются как <c>orientation XOR subOrientation</c>.
///
/// Базовый случай рекурсии — порядок 1: рисуется «П»-образная заготовка из четырёх
/// точек, повёрнутая согласно ориентации.
/// </summary>
public static class HilbertCurve
{
    /// <summary>
    /// Базовый случай — заготовка из четырёх точек (порядок 1). Она начинается в
    /// левом верхнем углу квадрата и заканчивается в правом верхнем: именно такое
    /// расположение начала и конца делает базовый случай согласованным с рекурсией
    /// на всех порядках (проверяется тестами).
    /// </summary>
    private static readonly (int X, int Y)[] BaseShape =
    [
        (0, 0),
        (0, 1),
        (1, 1),
        (1, 0),
    ];

    /// <summary>
    /// Канонический порядок обхода четырёх подквадратов и их ориентации.
    /// Значения — смещения подквадрата в единицах половины стороны (0 или 1)
    /// и код ориентации подкривой, то есть симметрии, которая переводит каноническую
    /// кривую порядка n в соответствующую подкривую:
    /// верхний левый → код 1, нижний левый → 0, нижний правый → 0, верхний правый → 2.
    /// Такой порядок обеспечивает единичные шаги между подкривыми и совпадение
    /// начала и конца кривой с левым верхним и правым верхним углами квадрата.
    /// </summary>
    private static readonly (int OffsetX, int OffsetY, int SubOrientation)[] SubSquares =
    [
        (0, 0, 1),   // верхний левый
        (0, 1, 0),   // нижний левый
        (1, 1, 0),   // нижний правый
        (1, 0, 2),   // верхний правый
    ];

    /// <summary>Количество допустимых ориентаций.</summary>
    public const int OrientationCount = 4;

    /// <summary>Число точек кривой: 4^order.</summary>
    public static int PointCount(int order)
    {
        if (order < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(order), order, "Порядок кривой должен быть не меньше 1.");
        }

        return 1 << (2 * order);
    }

    /// <summary>Точки кривой Гильберта заданного порядка (рекурсивное построение).</summary>
    public static IReadOnlyList<PointD> BuildPoints(int order) => BuildPoints(order, orientation: 0);

    /// <summary>
    /// Точки кривой Гильберта заданного порядка и ориентации.
    /// Координаты — целые числа в диапазоне 0 .. 2^order − 1.
    /// </summary>
    public static IReadOnlyList<PointD> BuildPoints(int order, int orientation)
    {
        if (order < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(order), order, "Порядок кривой должен быть не меньше 1.");
        }

        if (orientation < 0 || orientation >= OrientationCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(orientation),
                orientation,
                $"Ориентация должна быть от 0 до {OrientationCount - 1}.");
        }

        var points = new List<PointD>(PointCount(order));
        Build(order, orientation, 0, 0, points);
        return points;
    }

    /// <summary>
    /// Эталонная (классическая) реализация «индекс → точка» — алгоритм d2xy.
    /// Используется тестами для независимой проверки рекурсивного построения.
    /// </summary>
    public static IReadOnlyList<PointD> ReferencePoints(int order)
    {
        if (order < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(order), order, "Порядок кривой должен быть не меньше 1.");
        }

        int side = 1 << order;
        var points = new List<PointD>(side * side);

        for (int index = 0; index < side * side; index++)
        {
            int x = 0;
            int y = 0;
            int t = index;

            for (int step = 1; step < side; step *= 2)
            {
                int rx = 1 & (t / 2);
                int ry = 1 & (t ^ rx);
                Rotate(step, ref x, ref y, rx, ry);
                x += step * rx;
                y += step * ry;
                t /= 4;
            }

            points.Add(new PointD(x, y));
        }

        return points;
    }

    /// <summary>
    /// Один шаг рекурсии: либо базовый случай (порядок 1), либо четыре подквадрата.
    /// </summary>
    private static void Build(int order, int orientation, int originX, int originY, List<PointD> points)
    {
        // --- БАЗОВЫЙ СЛУЧАЙ РЕКУРСИИ -------------------------------------------
        // Порядок 1: четыре точки заготовки, повёрнутые согласно ориентации.
        if (order == 1)
        {
            foreach ((int x, int y) in BaseShape)
            {
                (int tx, int ty) = Transform(orientation, x, y);
                points.Add(new PointD(originX + tx, originY + ty));
            }

            return;
        }

        // --- РЕКУРСИВНЫЙ ШАГ ---------------------------------------------------
        // Квадрат делится на четыре подквадрата; каждый обходится кривой на единицу
        // меньшего порядка, а его положение и ориентация зависят от ориентации родителя.
        int half = 1 << (order - 1);

        foreach ((int offsetX, int offsetY, int subOrientation) in SubSquares)
        {
            (int dx, int dy) = Transform(orientation, offsetX, offsetY);

            Build(
                order - 1,
                orientation ^ subOrientation,
                originX + dx * half,
                originY + dy * half,
                points);
        }
    }

    /// <summary>
    /// Симметрия квадрата по коду ориентации. Координаты задаются в долях квадрата: 0 или 1.
    /// </summary>
    private static (int X, int Y) Transform(int orientation, int x, int y) => orientation switch
    {
        0 => (x, y),
        1 => (y, x),
        2 => (1 - y, 1 - x),
        3 => (1 - x, 1 - y),
        _ => throw new ArgumentOutOfRangeException(nameof(orientation), orientation, "Неизвестная ориентация."),
    };

    /// <summary>Вспомогательное преобразование классического алгоритма d2xy.</summary>
    private static void Rotate(int size, ref int x, ref int y, int rx, int ry)
    {
        if (ry != 0)
        {
            return;
        }

        if (rx == 1)
        {
            x = size - 1 - x;
            y = size - 1 - y;
        }

        (x, y) = (y, x);
    }
}

/// <summary>
/// Кривая Гильберта как фрактальный алгоритм: тот же контракт <see cref="IFractal"/>,
/// что и у остальных фракталов, поэтому она отображается общим холстом.
///
/// Число отрезков: 4^depth − 1.
/// </summary>
public sealed class HilbertCurveFractal : FractalAlgorithmBase
{
    /// <summary>4^7 − 1 = 16383 отрезка — предел для интерактивной отрисовки.</summary>
    public const int MaximumDepth = 7;

    public HilbertCurveFractal()
        : base(MaximumDepth)
    {
    }

    public override FractalKind Kind => FractalKind.HilbertCurve;

    public override string DisplayName => "Кривая Гильберта";

    public override string AlgorithmDescription =>
        "Квадрат делится на четыре подквадрата, каждый обходится такой же кривой на единицу "
        + "меньшего порядка; ориентация каждой копии задаётся параметром поворота. "
        + "Базовый случай: порядок 1 — заготовка из четырёх точек. Число отрезков: 4^depth − 1.";

    /// <summary>Порядок 0 вырожден (одна точка), поэтому минимальная глубина — 1.</summary>
    public override int MinDepth => 1;

    public override string ToString() => DisplayName;

    protected override FractalGeometry BuildCore(int depth) => BuildGeometry(depth, orientation: 0);

    /// <summary>
    /// Строит геометрию кривой с явно заданной ориентацией — это иллюстрирует,
    /// что ориентация является ещё одним параметром рекурсии.
    /// </summary>
    public FractalGeometry BuildGeometry(int depth, int orientation)
    {
        ValidateDepth(depth);

        IReadOnlyList<PointD> points = HilbertCurve.BuildPoints(depth, orientation);
        var segments = new List<Segment>(Math.Max(0, points.Count - 1));

        for (int i = 1; i < points.Count; i++)
        {
            segments.Add(new Segment(points[i - 1], points[i]));
        }

        return new FractalGeometry(Kind, depth, segments: segments).FitToUnitBox();
    }

    /// <summary>Описание ориентации для интерфейса.</summary>
    public static string DescribeOrientation(int orientation) => orientation switch
    {
        0 => "0 — базовое построение",
        1 => "1 — отражение по главной диагонали",
        2 => "2 — отражение по побочной диагонали",
        3 => "3 — поворот на 180°",
        _ => $"Неизвестная ориентация {orientation}",
    };
}

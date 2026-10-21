using RecursiveLab.Core.Geometry;

namespace RecursiveLab.Core.Fractals;

/// <summary>
/// Снежинка Коха.
///
/// Рекурсивный алгоритм: каждый отрезок (сторона) заменяется на четыре отрезка —
/// две трети исходной длины заменяются «горбом» из равностороннего треугольника,
/// построенного на средней трети. Базовый случай — глубина 0: сторона остаётся отрезком.
///
/// Количество отрезков растёт как 3 * 4^depth, длина каждого уменьшается в 3 раза,
/// а суммарная длина — увеличивается в 4/3 раза на каждом уровне (фрактальная кривая).
/// </summary>
public sealed class KochSnowflakeFractal : FractalAlgorithmBase
{
    /// <summary>3 * 4^7 = 49152 отрезка — предел для комфортной отрисовки в реальном времени.</summary>
    public const int MaximumDepth = 7;

    private const double Sqrt3Over2 = 0.8660254037844386;

    public KochSnowflakeFractal()
        : base(MaximumDepth)
    {
    }

    public override FractalKind Kind => FractalKind.KochSnowflake;

    public override string DisplayName => "Снежинка Коха";

    public override string AlgorithmDescription =>
        "Каждая сторона рекурсивно заменяется на 4 звена: средняя треть превращается в " +
        "«горб» равностороннего треугольника. Базовый случай: depth = 0 — сторона остаётся отрезком. " +
        "Число звеньев: 3 · 4^depth.";

    public override string ToString() => DisplayName;

    protected override FractalGeometry BuildCore(int depth)
    {
        // Правильный (равносторонний) треугольник — основа снежинки.
        const double radius = 0.45;
        var center = new PointD(0.5, 0.5);
        PointD top = new(center.X, center.Y - radius);
        PointD right = new(center.X + radius * Sqrt3Over2, center.Y + radius * 0.5);
        PointD left = new(center.X - radius * Sqrt3Over2, center.Y + radius * 0.5);

        // Направление «горба» вычисляется автоматически: он должен смотреть наружу,
        // то есть в сторону от центра фигуры.
        int outwardSign = DetermineOutwardSign(top, right, left, center);

        var segments = new List<Segment>();
        BuildEdge(top, right, depth, outwardSign, segments);
        BuildEdge(right, left, depth, outwardSign, segments);
        BuildEdge(left, top, depth, outwardSign, segments);

        return new FractalGeometry(Kind, depth, segments: segments);
    }

    /// <summary>
    /// Один шаг рекурсии над стороной многоугольника.
    /// </summary>
    private static void BuildEdge(PointD a, PointD b, int depth, int outwardSign, List<Segment> output)
    {
        // --- Базовый случай рекурсии -------------------------------------------
        // Дробить нечего: сторона остаётся одним отрезком.
        if (depth == 0)
        {
            output.Add(new Segment(a, b));
            return;
        }

        // --- Рекурсивный шаг ---------------------------------------------------
        // Делим сторону на три равные части и на средней строим «горб».
        PointD oneThird = PointD.Lerp(a, b, 1.0 / 3.0);
        PointD twoThirds = PointD.Lerp(a, b, 2.0 / 3.0);
        PointD peak = Peak(oneThird, twoThirds, outwardSign);

        // Четыре звена заменяют одну сторону; каждое обрабатывается рекурсивно.
        BuildEdge(a, oneThird, depth - 1, outwardSign, output);
        BuildEdge(oneThird, peak, depth - 1, outwardSign, output);
        BuildEdge(peak, twoThirds, depth - 1, outwardSign, output);
        BuildEdge(twoThirds, b, depth - 1, outwardSign, output);
    }

    /// <summary>Вершина «горба»: вектор средней трети поворачивается на 60° наружу.</summary>
    private static PointD Peak(PointD oneThird, PointD twoThirds, int outwardSign)
    {
        double dx = twoThirds.X - oneThird.X;
        double dy = twoThirds.Y - oneThird.Y;

        double cos = 0.5;
        double sin = outwardSign * Sqrt3Over2;

        // Поворот вектора на ±60° (система координат — экранная, ось Y вниз).
        double rotatedX = dx * cos - dy * sin;
        double rotatedY = dx * sin + dy * cos;

        return new PointD(oneThird.X + rotatedX, oneThird.Y + rotatedY);
    }

    /// <summary>
    /// Подбирает знак поворота так, чтобы «горб» выходил за пределы треугольника
    /// (в сторону от центра), а не внутрь него.
    /// </summary>
    private static int DetermineOutwardSign(PointD top, PointD right, PointD left, PointD center)
    {
        double DistanceFromCenter(int sign)
            => Peak(PointD.Lerp(top, right, 1.0 / 3.0), PointD.Lerp(top, right, 2.0 / 3.0), sign)
                .DistanceTo(center);

        return DistanceFromCenter(+1) >= DistanceFromCenter(-1) ? +1 : -1;
    }
}

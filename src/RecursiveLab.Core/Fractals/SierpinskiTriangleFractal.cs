using RecursiveLab.Core.Geometry;

namespace RecursiveLab.Core.Fractals;

/// <summary>
/// Треугольник Серпинского.
///
/// Рекурсивный алгоритм: треугольник делится средними линиями на четыре равных,
/// центральный отбрасывается, а три угловых обрабатываются рекурсивно с глубиной на
/// единицу меньше. Базовый случай — глубина 0: треугольник добавляется в результат целиком.
///
/// Количество треугольников растёт как 3^depth, поэтому глубина ограничена.
/// </summary>
public sealed class SierpinskiTriangleFractal : FractalAlgorithmBase
{
    /// <summary>3^12 ≈ 531 тыс. треугольников — предел, после которого отрисовка становится слишком тяжёлой.</summary>
    public const int MaximumDepth = 10;

    public SierpinskiTriangleFractal()
        : base(MaximumDepth)
    {
    }

    public override FractalKind Kind => FractalKind.SierpinskiTriangle;

    public override string DisplayName => "Треугольник Серпинского";

    public override string AlgorithmDescription =>
        "Треугольник делится средними линиями на 4 части; центральная отбрасывается, " +
        "три угловые обрабатываются рекурсивно с depth − 1. Базовый случай: depth = 0 — " +
        "треугольник рисуется целиком.";

    public override string ToString() => DisplayName;

    protected override FractalGeometry BuildCore(int depth)
    {
        var triangles = new List<Triangle>();

        // Нулевой уровень: равносторонний треугольник вершиной вверх.
        PointD top = new(0.5, 0.0);
        PointD right = new(1.0, 0.8660254037844386);
        PointD left = new(0.0, 0.8660254037844386);

        Subdivide(new Triangle(top, right, left), depth, triangles);

        return new FractalGeometry(Kind, depth, segments: null, triangles: triangles);
    }

    /// <summary>
    /// Один шаг рекурсии: дробление треугольника на три угловых.
    /// </summary>
    private static void Subdivide(Triangle triangle, int depth, List<Triangle> output)
    {
        // --- Базовый случай рекурсии -------------------------------------------
        // Глубина исчерпана: дальнейшее дробление не увеличит детализацию
        // (пиксель уже не различит более мелкие треугольники).
        if (depth == 0)
        {
            output.Add(triangle);
            return;
        }

        // --- Рекурсивный шаг ---------------------------------------------------
        // Средние линии разрезают треугольник на 4 равных; центральный «перевёрнутый»
        // треугольник принадлежит «дырке» и не рисуется.
        Triangle cornerTop = new(triangle.A, triangle.MidAB, triangle.MidCA);
        Triangle cornerLeft = new(triangle.MidCA, triangle.MidBC, triangle.C);
        Triangle cornerRight = new(triangle.MidAB, triangle.B, triangle.MidBC);

        Subdivide(cornerTop, depth - 1, output);
        Subdivide(cornerLeft, depth - 1, output);
        Subdivide(cornerRight, depth - 1, output);
    }
}

using RecursiveLab.Core.Geometry;

namespace RecursiveLab.Core.Fractals;

/// <summary>
/// Результат работы рекурсивного алгоритма фрактала, полностью независимый от графической
/// библиотеки. Содержит либо отрезки (контурные фракталы), либо треугольники (площадные),
/// либо и то и другое.
///
/// Гарантия: после <see cref="FitToUnitBox"/> вся геометрия лежит в единичном квадрате
/// [0;1] x [0;1] с сохранением пропорций, поэтому слой отрисовки выполняет только
/// линейное масштабирование и не знает ничего о конкретном фрактале.
/// </summary>
public sealed class FractalGeometry
{
    private static readonly Segment[] NoSegments = [];
    private static readonly Triangle[] NoTriangles = [];

    public FractalGeometry(
        FractalKind kind,
        int depth,
        IReadOnlyList<Segment>? segments = null,
        IReadOnlyList<Triangle>? triangles = null)
    {
        Kind = kind;
        Depth = depth;
        Segments = segments ?? NoSegments;
        Triangles = triangles ?? NoTriangles;
    }

    public FractalKind Kind { get; }

    /// <summary>Глубина рекурсии, с которой построена данная геометрия.</summary>
    public int Depth { get; }

    public IReadOnlyList<Segment> Segments { get; }

    public IReadOnlyList<Triangle> Triangles { get; }

    /// <summary>Суммарное количество примитивов (отрезков и треугольников).</summary>
    public int PrimitiveCount => Segments.Count + Triangles.Count;

    /// <summary>Суммарная длина всех отрезков — метрика «детализации» линейных фракталов.</summary>
    public double TotalSegmentLength
    {
        get
        {
            double total = 0;
            for (int i = 0; i < Segments.Count; i++)
            {
                total += Segments[i].Length;
            }

            return total;
        }
    }

    /// <summary>Границы всей геометрии.</summary>
    public Bounds Bounds => Bounds.FromPoints(EnumeratePoints());

    /// <summary>
    /// Приводит геометрию к единичному квадрату: равномерное (сохраняющее пропорции)
    /// масштабирование и центрирование. Вырожденную геометрию оставляет как есть.
    /// </summary>
    public FractalGeometry FitToUnitBox()
    {
        Bounds bounds = Bounds;
        double extent = Math.Max(bounds.Width, bounds.Height);
        if (!double.IsFinite(extent) || extent <= 1e-12)
        {
            return this;
        }

        double scale = 1.0 / extent;
        double offsetX = (1.0 - bounds.Width * scale) * 0.5 - bounds.MinX * scale;
        double offsetY = (1.0 - bounds.Height * scale) * 0.5 - bounds.MinY * scale;

        PointD Transform(PointD p) => new(p.X * scale + offsetX, p.Y * scale + offsetY);

        Segment[] segments = new Segment[Segments.Count];
        for (int i = 0; i < Segments.Count; i++)
        {
            segments[i] = new Segment(Transform(Segments[i].A), Transform(Segments[i].B));
        }

        Triangle[] triangles = new Triangle[Triangles.Count];
        for (int i = 0; i < Triangles.Count; i++)
        {
            triangles[i] = new Triangle(
                Transform(Triangles[i].A),
                Transform(Triangles[i].B),
                Transform(Triangles[i].C));
        }

        return new FractalGeometry(Kind, Depth, segments, triangles);
    }

    private IEnumerable<PointD> EnumeratePoints()
    {
        foreach (Segment s in Segments)
        {
            yield return s.A;
            yield return s.B;
        }

        foreach (Triangle t in Triangles)
        {
            yield return t.A;
            yield return t.B;
            yield return t.C;
        }
    }
}

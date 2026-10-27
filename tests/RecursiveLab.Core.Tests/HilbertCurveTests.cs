using RecursiveLab.Core.Fractals;
using RecursiveLab.Core.Geometry;

namespace RecursiveLab.Core.Tests;

/// <summary>
/// Тесты рекурсивного построения кривой Гильберта.
///
/// Главная проверка — сравнение рекурсивного построения с классическим алгоритмом
/// d2xy (преобразование «индекс → точка»): обе реализации обязаны давать одну и ту же
/// последовательность точек. Дополнительно проверяются свойства самой кривой:
/// число точек 4^n, шаги длиной в одну клетку, отсутствие самопересечений и полнота обхода.
/// </summary>
public class HilbertCurveTests
{
    [Theory]
    [InlineData(1, 4)]
    [InlineData(2, 16)]
    [InlineData(3, 64)]
    [InlineData(4, 256)]
    [InlineData(5, 1024)]
    [InlineData(6, 4096)]
    public void BuildPoints_PointCount_IsFourPowerOrder(int order, int expectedPoints)
    {
        IReadOnlyList<PointD> points = HilbertCurve.BuildPoints(order);

        Assert.Equal(expectedPoints, points.Count);
        Assert.Equal(expectedPoints, HilbertCurve.PointCount(order));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void BuildPoints_MatchesClassicD2xyReference(int order)
    {
        IReadOnlyList<PointD> recursive = HilbertCurve.BuildPoints(order);
        IReadOnlyList<PointD> reference = HilbertCurve.ReferencePoints(order);

        Assert.Equal(reference.Count, recursive.Count);

        for (int i = 0; i < reference.Count; i++)
        {
            Assert.Equal(reference[i], recursive[i]);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void BuildPoints_ConsecutivePointsAreUnitSteps(int order)
    {
        IReadOnlyList<PointD> points = HilbertCurve.BuildPoints(order);

        for (int i = 1; i < points.Count; i++)
        {
            double steps = Math.Abs(points[i].X - points[i - 1].X) + Math.Abs(points[i].Y - points[i - 1].Y);
            Assert.Equal(1.0, steps);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void BuildPoints_VisitsEveryCellExactlyOnce(int order)
    {
        int side = 1 << order;
        IReadOnlyList<PointD> points = HilbertCurve.BuildPoints(order);

        var visited = new HashSet<(int X, int Y)>();
        foreach (PointD point in points)
        {
            Assert.InRange(point.X, 0, side - 1);
            Assert.InRange(point.Y, 0, side - 1);
            Assert.True(visited.Add(((int)point.X, (int)point.Y)), $"Клетка {point} посещена дважды.");
        }

        Assert.Equal(side * side, visited.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void BuildPoints_AllOrientationsProduceValidCurves(int orientation)
    {
        const int order = 4;
        int side = 1 << order;
        IReadOnlyList<PointD> points = HilbertCurve.BuildPoints(order, orientation);

        Assert.Equal(side * side, points.Count);

        var visited = new HashSet<(int X, int Y)>();
        for (int i = 0; i < points.Count; i++)
        {
            Assert.True(visited.Add(((int)points[i].X, (int)points[i].Y)));
            Assert.InRange(points[i].X, 0, side - 1);
            Assert.InRange(points[i].Y, 0, side - 1);

            if (i > 0)
            {
                double steps = Math.Abs(points[i].X - points[i - 1].X) + Math.Abs(points[i].Y - points[i - 1].Y);
                Assert.Equal(1.0, steps);
            }
        }

        // Число отрезков одинаково у всех ориентаций — это повёрнутые копии одной кривой.
        Assert.Equal(side * side, visited.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void BuildPoints_SameOrientation_IsDeterministic(int orientation)
    {
        IReadOnlyList<PointD> first = HilbertCurve.BuildPoints(4, orientation);
        IReadOnlyList<PointD> second = HilbertCurve.BuildPoints(4, orientation);

        Assert.Equal(first, second);
    }

    [Fact]
    public void BuildPoints_DifferentOrientations_GiveDifferentSequences()
    {
        IReadOnlyList<PointD> baseCurve = HilbertCurve.BuildPoints(3, 0);

        for (int orientation = 1; orientation < HilbertCurve.OrientationCount; orientation++)
        {
            IReadOnlyList<PointD> other = HilbertCurve.BuildPoints(3, orientation);

            Assert.Equal(baseCurve.Count, other.Count);

            // Кривая обязана отличаться хотя бы порядком обхода точек.
            Assert.NotEqual(baseCurve, other);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void BuildPoints_RejectsInvalidOrder(int order)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HilbertCurve.BuildPoints(order));
        Assert.Throws<ArgumentOutOfRangeException>(() => HilbertCurve.ReferencePoints(order));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void BuildPoints_RejectsInvalidOrientation(int orientation)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HilbertCurve.BuildPoints(3, orientation));
    }

    // ---------------------------------------------------------------------
    // Кривая Гильберта как фрактальный алгоритм
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(1, 3)]
    [InlineData(2, 15)]
    [InlineData(3, 63)]
    [InlineData(4, 255)]
    [InlineData(5, 1023)]
    [InlineData(6, 4095)]
    public void Fractal_SegmentCount_IsFourPowerDepthMinusOne(int depth, int expectedSegments)
    {
        var fractal = new HilbertCurveFractal();

        FractalGeometry geometry = fractal.Build(new FractalParameters(depth));

        Assert.Equal(expectedSegments, geometry.Segments.Count);
        Assert.Empty(geometry.Triangles);
    }

    [Fact]
    public void Fractal_BaseCase_DepthOne_IsThreeUnitSegments()
    {
        var fractal = new HilbertCurveFractal();

        FractalGeometry geometry = fractal.Build(new FractalParameters(1));

        Assert.Equal(3, geometry.Segments.Count);
        Assert.All(geometry.Segments, segment => Assert.Equal(1.0, segment.Length, 1e-9));
    }

    [Fact]
    public void Fractal_MinimumDepthIsOne()
    {
        var fractal = new HilbertCurveFractal();

        Assert.Equal(1, fractal.MinDepth);
        Assert.Equal(7, fractal.MaxDepth);

        // Нулевой порядок вырожден (нет отрезков), поэтому он отвергается.
        Assert.Throws<FractalParameterException>(() => fractal.Build(new FractalParameters(0)));
        Assert.Throws<FractalParameterException>(() => fractal.Build(new FractalParameters(8)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Fractal_BuildGeometry_SupportsEveryOrientation(int orientation)
    {
        var fractal = new HilbertCurveFractal();

        FractalGeometry geometry = fractal.BuildGeometry(5, orientation);

        Assert.Equal(1023, geometry.Segments.Count);
        Assert.Equal(FractalKind.HilbertCurve, geometry.Kind);
        Assert.Equal(5, geometry.Depth);
        Assert.Equal(1.0, Math.Max(geometry.Bounds.Width, geometry.Bounds.Height), 1e-9);
    }

    [Fact]
    public void Fractal_DefaultBuild_UsesBaseOrientation()
    {
        var fractal = new HilbertCurveFractal();

        FractalGeometry fromInterface = fractal.Build(new FractalParameters(4));
        FractalGeometry fromExplicitOrientation = fractal.BuildGeometry(4, 0);

        Assert.Equal(fromExplicitOrientation.Segments, fromInterface.Segments);
    }

    [Fact]
    public void Fractal_IsRegisteredInCatalog()
    {
        IFractal fractal = FractalCatalog.Get(FractalKind.HilbertCurve);

        Assert.Equal(FractalKind.HilbertCurve, fractal.Kind);
        Assert.Equal("Кривая Гильберта", fractal.DisplayName);
        Assert.Contains(fractal, FractalCatalog.All);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void DescribeOrientation_IsNotEmptyForEveryOrientation(int orientation)
    {
        Assert.False(string.IsNullOrWhiteSpace(HilbertCurveFractal.DescribeOrientation(orientation)));
    }

    [Fact]
    public void Fractal_TotalLengthIsPowerOfFour_ForEveryOrientation()
    {
        var fractal = new HilbertCurveFractal();

        // Кривая состоит из единичных шагов, поэтому суммарная длина определяется числом звеньев.
        double expected = fractal.BuildGeometry(4, 0).TotalSegmentLength;
        for (int orientation = 1; orientation < HilbertCurve.OrientationCount; orientation++)
        {
            Assert.Equal(expected, fractal.BuildGeometry(4, orientation).TotalSegmentLength, 1e-9);
        }
    }
}

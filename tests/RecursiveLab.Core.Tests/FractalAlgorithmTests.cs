using RecursiveLab.Core.Fractals;
using RecursiveLab.Core.Geometry;

namespace RecursiveLab.Core.Tests;

/// <summary>
/// Тесты рекурсивных алгоритмов построения фракталов: базовые случаи, количество
/// примитивов, рост детализации, ограничения глубины и корректная геометрия.
/// </summary>
public class FractalAlgorithmTests
{
    private const double Tolerance = 1e-9;

    public static TheoryData<FractalKind> AllKinds()
    {
        var data = new TheoryData<FractalKind>();
        foreach (FractalKind kind in Enum.GetValues<FractalKind>())
        {
            data.Add(kind);
        }

        return data;
    }

    // ---------------------------------------------------------------------
    // Треугольник Серпинского
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 3)]
    [InlineData(2, 9)]
    [InlineData(3, 27)]
    [InlineData(4, 81)]
    [InlineData(5, 243)]
    public void Sierpinski_TriangleCount_IsPowerOfThree(int depth, int expectedCount)
    {
        FractalGeometry geometry = new SierpinskiTriangleFractal().Build(new FractalParameters(depth));

        Assert.Equal(expectedCount, geometry.Triangles.Count);
        Assert.Empty(geometry.Segments);
        Assert.Equal(expectedCount, geometry.PrimitiveCount);
    }

    [Fact]
    public void Sierpinski_BaseCase_DepthZero_IsSingleEquilateralTriangle()
    {
        FractalGeometry geometry = new SierpinskiTriangleFractal().Build(new FractalParameters(0));

        Assert.Single(geometry.Triangles);
        Triangle triangle = geometry.Triangles[0];

        // Базовый случай: один равносторонний треугольник с тремя равными сторонами.
        Assert.Equal(triangle.A.DistanceTo(triangle.B), triangle.B.DistanceTo(triangle.C), Tolerance * 10);
        Assert.Equal(triangle.B.DistanceTo(triangle.C), triangle.C.DistanceTo(triangle.A), Tolerance * 10);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Sierpinski_TotalArea_ShrinksAsThreeQuartersPerLevel(int depth)
    {
        var fractal = new SierpinskiTriangleFractal();
        double area0 = TotalTriangleArea(fractal.Build(new FractalParameters(0)));
        double area = TotalTriangleArea(fractal.Build(new FractalParameters(depth)));

        // Классическое свойство: на каждом уровне остаётся 3/4 площади.
        Assert.Equal(area0 * Math.Pow(0.75, depth), area, 1e-9);
    }

    // ---------------------------------------------------------------------
    // Снежинка Коха
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 12)]
    [InlineData(2, 48)]
    [InlineData(3, 192)]
    [InlineData(4, 768)]
    [InlineData(5, 3072)]
    public void Koch_SegmentCount_IsThreeTimesFourPowerDepth(int depth, int expectedCount)
    {
        FractalGeometry geometry = new KochSnowflakeFractal().Build(new FractalParameters(depth));

        Assert.Equal(expectedCount, geometry.Segments.Count);
        Assert.Empty(geometry.Triangles);
    }

    [Fact]
    public void Koch_BaseCase_DepthZero_IsClosedTriangleOfEqualSides()
    {
        FractalGeometry geometry = new KochSnowflakeFractal().Build(new FractalParameters(0));

        Assert.Equal(3, geometry.Segments.Count);

        double side = geometry.Segments[0].Length;
        Assert.All(geometry.Segments, segment => Assert.Equal(side, segment.Length, 1e-12));
        Assert.Equal(3 * side, geometry.TotalSegmentLength, 1e-12);
        AssertClosedChain(geometry.Segments);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Koch_Segments_StayChainedAndUniform(int depth)
    {
        FractalGeometry geometry = new KochSnowflakeFractal().Build(new FractalParameters(depth));

        // Все звенья одной длины и образуют замкнутую ломаную без разрывов.
        double expectedLength = geometry.Segments[0].Length;
        Assert.All(geometry.Segments, segment => Assert.Equal(expectedLength, segment.Length, 1e-12));
        AssertClosedChain(geometry.Segments);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Koch_Perimeter_GrowsWithEveryLevel(int depth)
    {
        var fractal = new KochSnowflakeFractal();
        double length = fractal.Build(new FractalParameters(depth)).TotalSegmentLength;
        double next = fractal.Build(new FractalParameters(depth + 1)).TotalSegmentLength;

        // Замена отрезка длины L на четыре отрезка длины L/3 увеличивает суммарную
        // длину в 4/3 раза; после приведения к единичному квадрату масштаб меняется,
        // но рост детализации обязан сохраниться.
        Assert.True(next > length, "Суммарная длина ломаной должна расти с каждым уровнем рекурсии.");
        Assert.True(next / length < 4.0 / 3.0 + 1e-9, "Рост ограничен теоретическим множителем 4/3.");
    }

    // ---------------------------------------------------------------------
    // Фрактальное дерево
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 3)]
    [InlineData(2, 7)]
    [InlineData(3, 15)]
    [InlineData(4, 31)]
    [InlineData(5, 63)]
    [InlineData(6, 127)]
    public void FractalTree_SegmentCount_IsTwoPowerDepthPlusOneMinusOne(int depth, int expectedCount)
    {
        FractalGeometry geometry = new FractalTreeFractal().Build(new FractalParameters(depth));

        Assert.Equal(expectedCount, geometry.Segments.Count);
        Assert.Equal(expectedCount, geometry.PrimitiveCount);
    }

    [Fact]
    public void FractalTree_BaseCase_DepthZero_IsSingleVerticalTrunk()
    {
        FractalGeometry geometry = new FractalTreeFractal().Build(new FractalParameters(0));

        Assert.Single(geometry.Segments);
        Segment trunk = geometry.Segments[0];

        // Ствол строго вертикальный, растёт вверх от нижней границы холста.
        Assert.Equal(trunk.A.X, trunk.B.X, Tolerance);
        Assert.True(trunk.B.Y < trunk.A.Y, "Ствол должен расти вверх (экранная ось Y направлена вниз).");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    public void FractalTree_BranchLengths_ShrinkGeometrically(int depth)
    {
        FractalGeometry geometry = new FractalTreeFractal().Build(new FractalParameters(depth));

        double longest = geometry.Segments.Max(s => s.Length);
        double shortest = geometry.Segments.Min(s => s.Length);

        // Длина ветви каждого следующего уровня умножается на 0.72.
        Assert.Equal(Math.Pow(0.72, depth), shortest / longest, 1e-9);
    }

    // ---------------------------------------------------------------------
    // Общие свойства всех фракталов
    // ---------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void EveryFractal_LevelIncreasesDetail(FractalKind kind)
    {
        IFractal fractal = FractalCatalog.Get(kind);

        int previous = -1;
        for (int depth = fractal.MinDepth; depth <= Math.Min(fractal.MaxDepth, 6); depth++)
        {
            FractalGeometry geometry = fractal.Build(new FractalParameters(depth));
            Assert.True(geometry.PrimitiveCount > previous, "Рост детализации должен увеличивать число примитивов.");
            previous = geometry.PrimitiveCount;
        }
    }

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void EveryFractal_GeometryIsFiniteAndFitsUnitBox(FractalKind kind)
    {
        IFractal fractal = FractalCatalog.Get(kind);

        foreach (int depth in new[] { fractal.MinDepth, 1, 3, 5, fractal.MaxDepth })
        {
            if (depth < fractal.MinDepth || depth > fractal.MaxDepth)
            {
                continue;
            }

            FractalGeometry geometry = fractal.Build(new FractalParameters(depth));
            Bounds bounds = geometry.Bounds;

            Assert.True(geometry.PrimitiveCount > 0);
            Assert.True(double.IsFinite(bounds.MinX) && double.IsFinite(bounds.MaxY));

            // После приведения к единичному квадрату вся геометрия обязана
            // оказаться внутри [0;1] x [0;1] — на этом строится слой отрисовки.
            Assert.InRange(bounds.MinX, -1e-9, 1.0);
            Assert.InRange(bounds.MinY, -1e-9, 1.0);
            Assert.InRange(bounds.MaxX, 0.0, 1.0 + 1e-9);
            Assert.InRange(bounds.MaxY, 0.0, 1.0 + 1e-9);

            // Наибольшая сторона фигуры занимает весь единичный квадрат:
            // пропорции при этом сохраняются (масштаб равномерный).
            Assert.Equal(1.0, Math.Max(bounds.Width, bounds.Height), 1e-9);

            foreach (Segment segment in geometry.Segments)
            {
                Assert.True(segment.IsFinite);
                Assert.True(bounds.Contains(segment.A, 1e-9) && bounds.Contains(segment.B, 1e-9));
            }

            foreach (Triangle triangle in geometry.Triangles)
            {
                Assert.True(triangle.IsFinite);
            }
        }
    }

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void EveryFractal_IsDeterministic(FractalKind kind)
    {
        IFractal fractal = FractalCatalog.Get(kind);
        var parameters = new FractalParameters(Math.Min(4, fractal.MaxDepth));

        FractalGeometry first = fractal.Build(parameters);
        FractalGeometry second = fractal.Build(parameters);

        Assert.Equal(first.PrimitiveCount, second.PrimitiveCount);
        Assert.Equal(first.Segments, second.Segments);
        Assert.Equal(first.Triangles, second.Triangles);
    }

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void EveryFractal_MaxDepthIsBuildable(FractalKind kind)
    {
        IFractal fractal = FractalCatalog.Get(kind);

        FractalGeometry geometry = fractal.Build(new FractalParameters(fractal.MaxDepth));

        Assert.Equal(fractal.MaxDepth, geometry.Depth);
        Assert.True(geometry.PrimitiveCount > 0);
    }

    // ---------------------------------------------------------------------
    // Проверка параметров и границы глубины
    // ---------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void EveryFractal_RejectsNegativeDepth(FractalKind kind)
    {
        IFractal fractal = FractalCatalog.Get(kind);

        var exception = Assert.Throws<FractalParameterException>(
            () => fractal.Build(new FractalParameters(-1)));

        Assert.Equal("Depth", exception.ParameterName);
        Assert.Equal(-1, exception.ActualValue);
        Assert.Equal(fractal.MinDepth, exception.MinValue);
        Assert.Equal(fractal.MaxDepth, exception.MaxValue);
    }

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void EveryFractal_RejectsDepthAboveLimit(FractalKind kind)
    {
        IFractal fractal = FractalCatalog.Get(kind);
        int tooDeep = fractal.MaxDepth + 1;

        var exception = Assert.Throws<FractalParameterException>(
            () => fractal.Build(new FractalParameters(tooDeep)));

        Assert.Equal(tooDeep, exception.ActualValue);
        Assert.Contains(tooDeep.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Contains(fractal.MaxDepth.ToString(), exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void EveryFractal_RejectsVeryLargeDepthWithoutStackOverflow(FractalKind kind)
    {
        IFractal fractal = FractalCatalog.Get(kind);

        // Даже абсурдно большая глубина должна давать понятное исключение,
        // а не переполнение стека.
        Assert.Throws<FractalParameterException>(() => fractal.Build(new FractalParameters(10_000)));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(7)]
    [InlineData(14)]
    public void DepthLimits_AreReasonable(int expectedMaximum)
    {
        // Ограничения подобраны так, чтобы отрисовка оставалась интерактивной.
        Assert.Contains(
            FractalCatalog.All,
            fractal => fractal.MaxDepth == expectedMaximum);
    }

    // ---------------------------------------------------------------------
    // Каталог фракталов
    // ---------------------------------------------------------------------

    [Fact]
    public void Catalog_ExposesEveryFractalKind()
    {
        FractalKind[] kinds = Enum.GetValues<FractalKind>();

        Assert.Equal(kinds.Length, FractalCatalog.All.Count);

        foreach (FractalKind kind in kinds)
        {
            IFractal fractal = FractalCatalog.Get(kind);

            Assert.Equal(kind, fractal.Kind);
            Assert.False(string.IsNullOrWhiteSpace(fractal.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(fractal.AlgorithmDescription));
            Assert.Equal(0, fractal.MinDepth);
            Assert.True(fractal.MaxDepth > 0);
        }
    }

    [Fact]
    public void Catalog_RejectsUnknownKind()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FractalCatalog.Get((FractalKind)99));
    }

    // ---------------------------------------------------------------------
    // Вспомогательные проверки
    // ---------------------------------------------------------------------

    private static double TotalTriangleArea(FractalGeometry geometry)
    {
        double total = 0;
        foreach (Triangle triangle in geometry.Triangles)
        {
            total += Math.Abs(
                (triangle.B.X - triangle.A.X) * (triangle.C.Y - triangle.A.Y)
                - (triangle.C.X - triangle.A.X) * (triangle.B.Y - triangle.A.Y)) / 2.0;
        }

        return total;
    }

    private static void AssertClosedChain(IReadOnlyList<Segment> segments)
    {
        Assert.NotEmpty(segments);

        for (int i = 1; i < segments.Count; i++)
        {
            Assert.Equal(segments[i - 1].B.X, segments[i].A.X, 1e-12);
            Assert.Equal(segments[i - 1].B.Y, segments[i].A.Y, 1e-12);
        }

        Assert.Equal(segments[0].A.X, segments[^1].B.X, 1e-12);
        Assert.Equal(segments[0].A.Y, segments[^1].B.Y, 1e-12);
    }
}

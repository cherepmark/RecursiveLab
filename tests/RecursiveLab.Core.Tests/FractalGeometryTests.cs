using RecursiveLab.Core.Fractals;
using RecursiveLab.Core.Geometry;

namespace RecursiveLab.Core.Tests;

/// <summary>Тесты вспомогательной геометрии: границы и приведение к единичному квадрату.</summary>
public class FractalGeometryTests
{
    [Fact]
    public void Bounds_FromEmptySet_AreZero()
    {
        var bounds = Bounds.FromPoints([]);

        Assert.Equal(0, bounds.Width);
        Assert.Equal(0, bounds.Height);
        Assert.Equal(0, bounds.MinX);
        Assert.Equal(0, bounds.MaxY);
    }

    [Fact]
    public void Bounds_FromPoints_ComputeExtremes()
    {
        PointD[] points = [new(3, -1), new(-2, 5), new(0, 0)];

        var bounds = Bounds.FromPoints(points);

        Assert.Equal(-2, bounds.MinX);
        Assert.Equal(-1, bounds.MinY);
        Assert.Equal(3, bounds.MaxX);
        Assert.Equal(5, bounds.MaxY);
        Assert.Equal(5, bounds.Width);
        Assert.Equal(6, bounds.Height);
    }

    [Fact]
    public void FitToUnitBox_PreservesProportions()
    {
        var geometry = new FractalGeometry(
            FractalKind.KochSnowflake,
            depth: 1,
            segments:
            [
                new Segment(new PointD(0, 0), new PointD(10, 0)),
                new Segment(new PointD(10, 0), new PointD(10, 2)),
            ]);

        FractalGeometry fitted = geometry.FitToUnitBox();
        Bounds bounds = fitted.Bounds;

        Assert.Equal(0, bounds.MinX, 1e-12);
        Assert.Equal(1, bounds.Width, 1e-12);
        Assert.Equal(0.2, bounds.Height, 1e-12);

        // Соотношение длин отрезков не изменилось (масштаб равномерный).
        Assert.Equal(5.0, fitted.Segments[0].Length / fitted.Segments[1].Length, 1e-12);
    }

    [Fact]
    public void FitToUnitBox_CentersSmallerDimension()
    {
        var geometry = new FractalGeometry(
            FractalKind.FractalTree,
            depth: 0,
            segments: [new Segment(new PointD(0, 0), new PointD(10, 0))]);

        Bounds bounds = geometry.FitToUnitBox().Bounds;

        Assert.Equal(0.5, bounds.MinY, 1e-12);
        Assert.Equal(0.5, bounds.MaxY, 1e-12);
    }

    [Fact]
    public void FitToUnitBox_KeepsDegenerateGeometryUnchanged()
    {
        var geometry = new FractalGeometry(
            FractalKind.FractalTree,
            depth: 0,
            segments: [new Segment(new PointD(1, 1), new PointD(1, 1))]);

        FractalGeometry fitted = geometry.FitToUnitBox();

        Assert.Equal(1, fitted.Segments[0].A.X, 1e-12);
        Assert.Equal(1, fitted.Segments[0].A.Y, 1e-12);
        Assert.Equal(0, fitted.Bounds.Width, 1e-12);
    }

    [Fact]
    public void TotalSegmentLength_SumsAllSegments()
    {
        var geometry = new FractalGeometry(
            FractalKind.KochSnowflake,
            depth: 0,
            segments:
            [
                new Segment(new PointD(0, 0), new PointD(3, 0)),
                new Segment(new PointD(0, 0), new PointD(0, 4)),
            ]);

        Assert.Equal(7, geometry.TotalSegmentLength, 1e-12);
    }

    [Fact]
    public void GeometryWithoutPrimitives_HasZeroCounts()
    {
        var geometry = new FractalGeometry(FractalKind.SierpinskiTriangle, depth: 0);

        Assert.Equal(0, geometry.PrimitiveCount);
        Assert.Empty(geometry.Segments);
        Assert.Empty(geometry.Triangles);
        Assert.Equal(0, geometry.Bounds.Width);
    }
}

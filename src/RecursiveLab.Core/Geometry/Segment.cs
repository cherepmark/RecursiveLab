namespace RecursiveLab.Core.Geometry;

/// <summary>Отрезок прямой — базовый примитив линейной геометрии фракталов.</summary>
public readonly record struct Segment(PointD A, PointD B)
{
    public double Length => A.DistanceTo(B);

    public bool IsFinite => A.IsFinite && B.IsFinite;
}

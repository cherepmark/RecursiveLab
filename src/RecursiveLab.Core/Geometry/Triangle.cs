namespace RecursiveLab.Core.Geometry;

/// <summary>Треугольник — базовый примитив площадной геометрии (треугольник Серпинского).</summary>
public readonly record struct Triangle(PointD A, PointD B, PointD C)
{
    public PointD MidAB => PointD.Midpoint(A, B);

    public PointD MidBC => PointD.Midpoint(B, C);

    public PointD MidCA => PointD.Midpoint(C, A);

    public bool IsFinite => A.IsFinite && B.IsFinite && C.IsFinite;

    /// <summary>Периметр треугольника (используется в тестах для сравнения детализации).</summary>
    public double Perimeter => A.DistanceTo(B) + B.DistanceTo(C) + C.DistanceTo(A);

    public override string ToString() => $"Triangle {A} {B} {C}";
}

namespace RecursiveLab.Core.Geometry;

/// <summary>
/// Точка в двумерном пространстве (дробные координаты).
/// Используется экранная система координат: ось Y направлена вниз.
/// </summary>
public readonly record struct PointD(double X, double Y)
{
    /// <summary>Линейная интерполяция между двумя точками (t = 0 — a, t = 1 — b).</summary>
    public static PointD Lerp(PointD a, PointD b, double t)
        => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

    /// <summary>Середина отрезка.</summary>
    public static PointD Midpoint(PointD a, PointD b)
        => new((a.X + b.X) * 0.5, (a.Y + b.Y) * 0.5);

    public double DistanceTo(PointD other)
    {
        double dx = other.X - X;
        double dy = other.Y - Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>Все координаты конечны (не NaN и не бесконечность).</summary>
    public bool IsFinite => double.IsFinite(X) && double.IsFinite(Y);

    public override string ToString() => string.Create(
        System.Globalization.CultureInfo.InvariantCulture, $"({X:0.####}; {Y:0.####})");
}

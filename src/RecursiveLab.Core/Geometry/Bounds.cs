namespace RecursiveLab.Core.Geometry;

/// <summary>
/// Ограничивающий прямоугольник множества точек. Нужен для приведения
/// геометрии фрактала к единичному квадрату и для авто-масштаба при отрисовке.
/// </summary>
public readonly record struct Bounds(double MinX, double MinY, double MaxX, double MaxY)
{
    public double Width => MaxX - MinX;

    public double Height => MaxY - MinY;

    public double CenterX => (MinX + MaxX) * 0.5;

    public double CenterY => (MinY + MaxY) * 0.5;

    /// <summary>Строит границы по набору точек. Для пустого набора возвращает точечные границы в начале координат.</summary>
    public static Bounds FromPoints(IEnumerable<PointD> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
        double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
        bool any = false;

        foreach (PointD p in points)
        {
            minX = Math.Min(minX, p.X);
            minY = Math.Min(minY, p.Y);
            maxX = Math.Max(maxX, p.X);
            maxY = Math.Max(maxY, p.Y);
            any = true;
        }

        return any ? new Bounds(minX, minY, maxX, maxY) : new Bounds(0, 0, 0, 0);
    }

    public bool Contains(PointD p, double tolerance = 0)
        => p.X >= MinX - tolerance && p.X <= MaxX + tolerance
        && p.Y >= MinY - tolerance && p.Y <= MaxY + tolerance;

    public override string ToString() => string.Create(
        System.Globalization.CultureInfo.InvariantCulture,
        $"[{MinX:0.####}; {MinY:0.####}] .. [{MaxX:0.####}; {MaxY:0.####}]");
}

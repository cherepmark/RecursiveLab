using RecursiveLab.Core.Geometry;

namespace RecursiveLab.Core.Fractals;

/// <summary>
/// Фрактальное дерево.
///
/// Рекурсивный алгоритм: из конца текущей ветви выпускаются две ветви —
/// влево и вправо на фиксированный угол, каждая короче предыдущей в
/// <see cref="LengthRatio"/> раз. Базовый случай — глубина 0: ветвь рисуется и не делится.
///
/// Количество ветвей: 2^(depth + 1) − 1.
/// </summary>
public sealed class FractalTreeFractal : FractalAlgorithmBase
{
    /// <summary>2^15 − 1 = 32767 отрезков — предел для комфортной отрисовки.</summary>
    public const int MaximumDepth = 14;

    private const double TrunkLength = 0.22;
    private const double LengthRatio = 0.72;
    private const double SpreadDegrees = 26.0;

    /// <summary>Ограничение наклона ветви от вертикали, чтобы крона не «заваливалась» вниз.</summary>
    private const double MaxTiltDegrees = 75.0;

    public FractalTreeFractal()
        : base(MaximumDepth)
    {
    }

    public override FractalKind Kind => FractalKind.FractalTree;

    public override string DisplayName => "Фрактальное дерево";

    public override string AlgorithmDescription =>
        "Из конца каждой ветви рекурсивно выпускаются две ветви под углом ±26° к ней, " +
        "каждая короче предыдущей в 0,72 раза. Базовый случай: depth = 0 — ветвь не делится. " +
        "Число ветвей: 2^(depth + 1) − 1.";

    public override string ToString() => DisplayName;

    protected override FractalGeometry BuildCore(int depth)
    {
        var segments = new List<Segment>();

        // Ствол растёт из нижней точки холста строго вверх (угол 0°).
        var root = new PointD(0.5, 1.0);
        Branch(root, tiltDegrees: 0.0, length: TrunkLength, depth, segments);

        return new FractalGeometry(Kind, depth, segments: segments);
    }

    /// <summary>
    /// Один шаг рекурсии: рисование ветви и порождение двух дочерних.
    /// </summary>
    /// <param name="origin">Начало ветви.</param>
    /// <param name="tiltDegrees">Наклон от вертикали: 0° — вверх, положительный — вправо.</param>
    /// <param name="length">Длина ветви.</param>
    /// <param name="depth">Оставшаяся глубина рекурсии.</param>
    /// <param name="output">Накопитель отрезков.</param>
    private static void Branch(PointD origin, double tiltDegrees, double length, int depth, List<Segment> output)
    {
        double radians = tiltDegrees * Math.PI / 180.0;
        var end = new PointD(
            origin.X + Math.Sin(radians) * length,
            origin.Y - Math.Cos(radians) * length);

        output.Add(new Segment(origin, end));

        // --- Базовый случай рекурсии -------------------------------------------
        // Глубина исчерпана: ветвь — «лист», она больше не делится.
        if (depth == 0)
        {
            return;
        }

        // --- Рекурсивный шаг ---------------------------------------------------
        double childLength = length * LengthRatio;
        double leftTilt = Math.Clamp(tiltDegrees - SpreadDegrees, -MaxTiltDegrees, MaxTiltDegrees);
        double rightTilt = Math.Clamp(tiltDegrees + SpreadDegrees, -MaxTiltDegrees, MaxTiltDegrees);

        Branch(end, leftTilt, childLength, depth - 1, output);
        Branch(end, rightTilt, childLength, depth - 1, output);
    }
}

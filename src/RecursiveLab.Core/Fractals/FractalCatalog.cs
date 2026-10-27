namespace RecursiveLab.Core.Fractals;

/// <summary>
/// Реестр доступных фракталов: единая точка получения алгоритма по виду.
/// Интерфейс получает отсюда и алгоритм, и допустимые границы глубины,
/// поэтому добавление нового фрактала не требует правок в формах.
/// </summary>
public static class FractalCatalog
{
    private static readonly IFractal[] Algorithms =
    [
        new SierpinskiTriangleFractal(),
        new KochSnowflakeFractal(),
        new FractalTreeFractal(),
        new HilbertCurveFractal(),
    ];

    /// <summary>Все доступные фракталы в порядке отображения в интерфейсе.</summary>
    public static IReadOnlyList<IFractal> All => Algorithms;

    /// <summary>Возвращает алгоритм по виду фрактала.</summary>
    public static IFractal Get(FractalKind kind)
    {
        foreach (IFractal fractal in Algorithms)
        {
            if (fractal.Kind == kind)
            {
                return fractal;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(kind), kind, "Неизвестный вид фрактала.");
    }
}

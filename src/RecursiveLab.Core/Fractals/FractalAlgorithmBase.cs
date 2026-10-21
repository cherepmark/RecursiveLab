namespace RecursiveLab.Core.Fractals;

/// <summary>
/// Базовый класс рекурсивных алгоритмов фракталов.
/// Берёт на себя проверку параметров и приведение геометрии к единичному квадрату,
/// поэтому конкретные фракталы содержат только саму рекурсию.
/// </summary>
public abstract class FractalAlgorithmBase : IFractal
{
    protected FractalAlgorithmBase(int maxDepth)
    {
        if (maxDepth < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDepth), maxDepth, "Максимальная глубина должна быть положительной.");
        }

        MaxDepth = maxDepth;
    }

    public abstract FractalKind Kind { get; }

    public abstract string DisplayName { get; }

    public abstract string AlgorithmDescription { get; }

    /// <summary>Нулевая глубина — это всегда базовый случай рекурсии.</summary>
    public virtual int MinDepth => 0;

    public int MaxDepth { get; }

    public FractalGeometry Build(FractalParameters parameters)
    {
        ValidateDepth(parameters.Depth);
        FractalGeometry geometry = BuildCore(parameters.Depth);
        return geometry.FitToUnitBox();
    }

    /// <summary>Проверка пользовательских параметров перед запуском рекурсии.</summary>
    public void ValidateDepth(int depth)
    {
        if (depth < MinDepth || depth > MaxDepth)
        {
            throw FractalParameterException.DepthOutOfRange(depth, MinDepth, MaxDepth);
        }
    }

    /// <summary>Рекурсивное построение геометрии для уже проверенной глубины.</summary>
    protected abstract FractalGeometry BuildCore(int depth);
}

namespace RecursiveLab.Core.Fractals;

/// <summary>
/// Параметры построения фрактала. <see cref="Depth"/> — глубина рекурсии:
/// 0 означает базовый случай (минимальная детализация).
/// </summary>
/// <param name="Depth">Глубина рекурсии (число рекурсивных уровней дробления).</param>
public readonly record struct FractalParameters(int Depth)
{
    public override string ToString() => $"Depth={Depth}";
}

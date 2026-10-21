namespace RecursiveLab.Core.Fractals;

/// <summary>
/// Контракт рекурсивного алгоритма построения фрактала.
/// Реализации не знают ничего о графическом интерфейсе, поэтому легко тестируются.
/// </summary>
public interface IFractal
{
    FractalKind Kind { get; }

    /// <summary>Название для интерфейса.</summary>
    string DisplayName { get; }

    /// <summary>Краткое описание алгоритма (для справки в интерфейсе).</summary>
    string AlgorithmDescription { get; }

    /// <summary>Минимально допустимая глубина рекурсии (базовый случай).</summary>
    int MinDepth { get; }

    /// <summary>Максимально допустимая глубина рекурсии (ограничение ресурсов).</summary>
    int MaxDepth { get; }

    /// <summary>
    /// Строит геометрию фрактала. Бросает <see cref="FractalParameterException"/>,
    /// если глубина вне диапазона [<see cref="MinDepth"/>; <see cref="MaxDepth"/>].
    /// </summary>
    FractalGeometry Build(FractalParameters parameters);
}

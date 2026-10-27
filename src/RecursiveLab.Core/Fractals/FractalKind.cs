namespace RecursiveLab.Core.Fractals;

/// <summary>Виды фракталов, поддерживаемые приложением.</summary>
public enum FractalKind
{
    /// <summary>Треугольник Серпинского — рекурсивное деление треугольника на 3 части.</summary>
    SierpinskiTriangle = 0,

    /// <summary>Снежинка Коха — рекурсивное дробление каждой стороны на 4 звена.</summary>
    KochSnowflake = 1,

    /// <summary>Фрактальное дерево — рекурсивное ветвление с уменьшением длины.</summary>
    FractalTree = 2,

    /// <summary>Кривая Гильберта — рекурсия с параметром глубины и ориентации.</summary>
    HilbertCurve = 3,
}

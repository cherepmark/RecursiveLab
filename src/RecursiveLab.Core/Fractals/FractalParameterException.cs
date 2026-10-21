namespace RecursiveLab.Core.Fractals;

/// <summary>
/// Исключение некорректных параметров построения фрактала.
/// Содержит допустимый диапазон, чтобы интерфейс мог показать понятное сообщение,
/// не дублируя знания об ограничениях алгоритма.
/// </summary>
public sealed class FractalParameterException : Exception
{
    public FractalParameterException(string parameterName, int actualValue, int minValue, int maxValue, string message)
        : base(message)
    {
        ParameterName = parameterName;
        ActualValue = actualValue;
        MinValue = minValue;
        MaxValue = maxValue;
    }

    public string ParameterName { get; }

    public int ActualValue { get; }

    public int MinValue { get; }

    public int MaxValue { get; }

    public static FractalParameterException DepthOutOfRange(int actual, int min, int max)
        => new(
            nameof(FractalParameters.Depth),
            actual,
            min,
            max,
            $"Глубина рекурсии {actual} вне допустимого диапазона [{min}; {max}]. " +
            $"Укажите значение от {min} до {max}.");
}

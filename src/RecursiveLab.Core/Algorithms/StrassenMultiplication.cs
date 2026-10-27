using System.Diagnostics;

namespace RecursiveLab.Core.Algorithms;

/// <summary>Статистика работы алгоритма умножения — нужна для сравнения классического и рекурсивного способов.</summary>
public sealed class MatrixMultiplicationStats
{
    /// <summary>Сколько раз применялся рекурсивный шаг (разбиение на блоки).</summary>
    public int RecursiveCalls { get; internal set; }

    /// <summary>Сколько базовых блоков перемножено «в лоб».</summary>
    public int BaseCaseMultiplications { get; internal set; }

    /// <summary>
    /// Сколько скалярных умножений выполнено в базовых случаях (размер базового блока³).
    /// Именно это число показывает выигрыш Штрассена: 7^k вместо 8^k у классического алгоритма.
    /// </summary>
    public long ScalarMultiplications { get; internal set; }

    /// <summary>Наибольшая достигнутая глубина рекурсии (число уровней разбиения).</summary>
    public int MaxDepthReached { get; internal set; }
}

/// <summary>Результат умножения матриц.</summary>
public sealed class MatrixMultiplicationResult
{
    internal MatrixMultiplicationResult(string algorithmName, double[,] product, MatrixMultiplicationStats stats, TimeSpan elapsed)
    {
        AlgorithmName = algorithmName;
        Product = product;
        Stats = stats;
        Elapsed = elapsed;
    }

    /// <summary>Название алгоритма для интерфейса.</summary>
    public string AlgorithmName { get; }

    /// <summary>Матрица-произведение.</summary>
    public double[,] Product { get; }

    public MatrixMultiplicationStats Stats { get; }

    public TimeSpan Elapsed { get; }

    public int Size => Product.GetLength(0);
}

/// <summary>
/// Умножение матриц алгоритмом Штрассена — рекурсивное разбиение на блоки по принципу
/// «разделяй и властвуй».
///
/// Обычное блочное умножение требует 8 рекурсивных умножений половинного размера:
///   C11 = A11·B11 + A12·B21 и т. д.
/// Штрассен находит 7 промежуточных произведений M1…M7, из которых собираются блоки
/// результата, поэтому рекуррентность имеет вид T(n) = 7·T(n/2) + O(n²), что даёт
/// сложность O(n^log2 7) ≈ O(n^2.807) вместо O(n³).
///
/// Базовый случай рекурсии: блок размером не больше <c>threshold</c> перемножается
/// обычным тройным циклом.
/// </summary>
public static class StrassenMultiplication
{
    /// <summary>Порог по умолчанию: блоки такого размера и меньше считаются «в лоб».</summary>
    public const int DefaultThreshold = 32;

    /// <summary>Наибольший размер, поддерживаемый интерфейсом (ограничение ресурсов).</summary>
    public const int MaxSupportedSize = 64;

    /// <summary>Классическое умножение «в лоб» — эталон для сравнения.</summary>
    public static MatrixMultiplicationResult MultiplyClassic(double[,] left, double[,] right)
    {
        ValidateCompatible(left, right);

        int rows = left.GetLength(0);
        int inner = left.GetLength(1);
        int columns = right.GetLength(1);

        var stats = new MatrixMultiplicationStats();
        var stopwatch = Stopwatch.StartNew();

        var product = new double[rows, columns];
        for (int i = 0; i < rows; i++)
        {
            for (int k = 0; k < inner; k++)
            {
                double leftValue = left[i, k];

                // Теоретическая оценка: классическому алгоритму нужно n³ скалярных умножений.
                stats.ScalarMultiplications += columns;

                // Умножение на ноль пропускается — это оптимизация, не влияющая на оценку.
                if (leftValue == 0)
                {
                    continue;
                }

                for (int j = 0; j < columns; j++)
                {
                    product[i, j] += leftValue * right[k, j];
                }
            }
        }

        stopwatch.Stop();
        stats.BaseCaseMultiplications = 1;

        return new MatrixMultiplicationResult("Классический алгоритм O(n³)", product, stats, stopwatch.Elapsed);
    }

    /// <summary>Умножение матриц алгоритмом Штрассена (7 рекурсивных вызовов вместо 8).</summary>
    public static MatrixMultiplicationResult MultiplyStrassen(double[,] left, double[,] right, int threshold = DefaultThreshold)
    {
        ValidateCompatible(left, right);

        if (threshold < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(threshold), threshold, "Порог базового случая должен быть не меньше 1.");
        }

        int rows = left.GetLength(0);
        int inner = left.GetLength(1);
        int columns = right.GetLength(1);

        // Разбиение пополам возможно только для чётных размеров, поэтому матрицы
        // дополняются нулями до ближайшей степени двойки.
        int padded = NextPowerOfTwo(Math.Max(Math.Max(rows, inner), columns));
        if (padded > MaxSupportedSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(left),
                padded,
                $"После дополнения размер достигает {padded}, что больше поддерживаемого {MaxSupportedSize}.");
        }

        double[,] a = Pad(left, padded, padded);
        double[,] b = Pad(right, padded, padded);

        var stats = new MatrixMultiplicationStats();
        var stopwatch = Stopwatch.StartNew();

        double[,] c = MultiplyCore(a, b, padded, threshold, stats, depth: 0);

        stopwatch.Stop();

        // Отрезаем служебное дополнение.
        var product = new double[rows, columns];
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < columns; j++)
            {
                product[i, j] = c[i, j];
            }
        }

        return new MatrixMultiplicationResult("Алгоритм Штрассена O(n^2.807)", product, stats, stopwatch.Elapsed);
    }

    /// <summary>
    /// Один шаг рекурсии: либо базовый случай (обычное умножение блока),
    /// либо разбиение на 4 блока и 7 рекурсивных умножений.
    /// </summary>
    private static double[,] MultiplyCore(
        double[,] a,
        double[,] b,
        int size,
        int threshold,
        MatrixMultiplicationStats stats,
        int depth)
    {
        if (depth > stats.MaxDepthReached)
        {
            stats.MaxDepthReached = depth;
        }

        // --- БАЗОВЫЙ СЛУЧАЙ РЕКУРСИИ -----------------------------------------
        // Блок достаточно мал: перемножаем обычным способом, дальнейшее дробление
        // только увеличит накладные расходы.
        if (size <= threshold || size == 1)
        {
            stats.BaseCaseMultiplications++;
            stats.ScalarMultiplications += (long)size * size * size;
            return MultiplyBlock(a, b, size);
        }

        // --- РЕКУРСИВНЫЙ ШАГ -------------------------------------------------
        stats.RecursiveCalls++;
        int half = size / 2;

        double[,] a11 = SubMatrix(a, 0, 0, half);
        double[,] a12 = SubMatrix(a, 0, half, half);
        double[,] a21 = SubMatrix(a, half, 0, half);
        double[,] a22 = SubMatrix(a, half, half, half);

        double[,] b11 = SubMatrix(b, 0, 0, half);
        double[,] b12 = SubMatrix(b, 0, half, half);
        double[,] b21 = SubMatrix(b, half, 0, half);
        double[,] b22 = SubMatrix(b, half, half, half);

        // Семь произведений Штрассена вместо восьми обычных.
        double[,] m1 = MultiplyCore(Add(a11, a22), Add(b11, b22), half, threshold, stats, depth + 1);
        double[,] m2 = MultiplyCore(Add(a21, a22), b11, half, threshold, stats, depth + 1);
        double[,] m3 = MultiplyCore(a11, Subtract(b12, b22), half, threshold, stats, depth + 1);
        double[,] m4 = MultiplyCore(a22, Subtract(b21, b11), half, threshold, stats, depth + 1);
        double[,] m5 = MultiplyCore(Add(a11, a12), b22, half, threshold, stats, depth + 1);
        double[,] m6 = MultiplyCore(Subtract(a21, a11), Add(b11, b12), half, threshold, stats, depth + 1);
        double[,] m7 = MultiplyCore(Subtract(a12, a22), Add(b21, b22), half, threshold, stats, depth + 1);

        // Сборка блоков результата.
        double[,] c11 = Add(Subtract(Add(m1, m4), m5), m7);
        double[,] c12 = Add(m3, m5);
        double[,] c21 = Add(m2, m4);
        double[,] c22 = Add(Subtract(Add(m1, m3), m2), m6);

        return Combine(c11, c12, c21, c22, half);
    }

    private static double[,] MultiplyBlock(double[,] a, double[,] b, int size)
    {
        var result = new double[size, size];
        for (int i = 0; i < size; i++)
        {
            for (int k = 0; k < size; k++)
            {
                double value = a[i, k];
                if (value == 0)
                {
                    continue;
                }

                for (int j = 0; j < size; j++)
                {
                    result[i, j] += value * b[k, j];
                }
            }
        }

        return result;
    }

    private static void ValidateCompatible(double[,] left, double[,] right)
    {
        Matrix.Validate(left, nameof(left));
        Matrix.Validate(right, nameof(right));

        if (left.GetLength(1) != right.GetLength(0))
        {
            throw new ArgumentException(
                $"Число столбцов первой матрицы ({left.GetLength(1)}) должно совпадать " +
                $"с числом строк второй ({right.GetLength(0)}).",
                nameof(right));
        }
    }

    private static int NextPowerOfTwo(int value)
    {
        int result = 1;
        while (result < value)
        {
            result <<= 1;
        }

        return result;
    }

    private static double[,] Pad(double[,] source, int rows, int columns)
    {
        var result = new double[rows, columns];
        int sourceRows = Math.Min(source.GetLength(0), rows);
        int sourceColumns = Math.Min(source.GetLength(1), columns);

        for (int i = 0; i < sourceRows; i++)
        {
            for (int j = 0; j < sourceColumns; j++)
            {
                result[i, j] = source[i, j];
            }
        }

        return result;
    }

    private static double[,] SubMatrix(double[,] source, int row, int column, int size)
    {
        var result = new double[size, size];
        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                result[i, j] = source[row + i, column + j];
            }
        }

        return result;
    }

    private static double[,] Combine(double[,] c11, double[,] c12, double[,] c21, double[,] c22, int half)
    {
        var result = new double[half * 2, half * 2];
        for (int i = 0; i < half; i++)
        {
            for (int j = 0; j < half; j++)
            {
                result[i, j] = c11[i, j];
                result[i, j + half] = c12[i, j];
                result[i + half, j] = c21[i, j];
                result[i + half, j + half] = c22[i, j];
            }
        }

        return result;
    }

    private static double[,] Add(double[,] left, double[,] right)
    {
        int rows = left.GetLength(0);
        int columns = left.GetLength(1);
        var result = new double[rows, columns];

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < columns; j++)
            {
                result[i, j] = left[i, j] + right[i, j];
            }
        }

        return result;
    }

    private static double[,] Subtract(double[,] left, double[,] right)
    {
        int rows = left.GetLength(0);
        int columns = left.GetLength(1);
        var result = new double[rows, columns];

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < columns; j++)
            {
                result[i, j] = left[i, j] - right[i, j];
            }
        }

        return result;
    }
}

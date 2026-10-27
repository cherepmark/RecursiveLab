namespace RecursiveLab.Core.Algorithms;

/// <summary>
/// Вспомогательные операции над матрицами <see cref="double"/>[,].
/// Используются алгоритмом Штрассена и алгоритмом Флойда—Уоршелла.
/// </summary>
public static class Matrix
{
    /// <summary>Проверяет, что матрица не пустая и прямоугольная.</summary>
    public static void Validate(double[,] matrix, string parameterName = "matrix")
    {
        ArgumentNullException.ThrowIfNull(matrix, parameterName);

        if (matrix.GetLength(0) == 0 || matrix.GetLength(1) == 0)
        {
            throw new ArgumentException("Матрица не должна быть пустой.", parameterName);
        }
    }

    /// <summary>Проверяет, что матрица квадратная, и возвращает её размер.</summary>
    public static int ValidateSquare(double[,] matrix, string parameterName = "matrix")
    {
        Validate(matrix, parameterName);

        int rows = matrix.GetLength(0);
        int columns = matrix.GetLength(1);
        if (rows != columns)
        {
            throw new ArgumentException($"Матрица должна быть квадратной, а получено {rows}×{columns}.", parameterName);
        }

        return rows;
    }

    public static double[,] Create(int rows, int columns, double defaultValue = 0)
    {
        if (rows < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rows), rows, "Количество строк не может быть отрицательным.");
        }

        if (columns < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(columns), columns, "Количество столбцов не может быть отрицательным.");
        }

        var result = new double[rows, columns];
        if (defaultValue != 0)
        {
            Fill(result, defaultValue);
        }

        return result;
    }

    public static void Fill(double[,] matrix, double value)
    {
        ArgumentNullException.ThrowIfNull(matrix);

        for (int i = 0; i < matrix.GetLength(0); i++)
        {
            for (int j = 0; j < matrix.GetLength(1); j++)
            {
                matrix[i, j] = value;
            }
        }
    }

    public static double[,] Clone(double[,] source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return (double[,])source.Clone();
    }

    public static double[,] Identity(int size)
    {
        var result = new double[size, size];
        for (int i = 0; i < size; i++)
        {
            result[i, i] = 1;
        }

        return result;
    }

    /// <summary>Случайная квадратная матрица с целыми значениями из заданного диапазона.</summary>
    public static double[,] RandomSquare(int size, int seed, int minValue = -9, int maxValue = 9)
    {
        if (size < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, "Размер матрицы должен быть положительным.");
        }

        if (minValue > maxValue)
        {
            throw new ArgumentException("Минимальное значение не может быть больше максимального.", nameof(minValue));
        }

        var random = new Random(seed);
        var result = new double[size, size];
        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                result[i, j] = random.Next(minValue, maxValue + 1);
            }
        }

        return result;
    }

    /// <summary>Поэлементное сравнение матриц с допуском (учитываются и размеры).</summary>
    public static bool AreEqual(double[,] left, double[,] right, double tolerance = 1e-9)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (left.GetLength(0) != right.GetLength(0) || left.GetLength(1) != right.GetLength(1))
        {
            return false;
        }

        for (int i = 0; i < left.GetLength(0); i++)
        {
            for (int j = 0; j < left.GetLength(1); j++)
            {
                double a = left[i, j];
                double b = right[i, j];

                // Бесконечности (нет пути) считаются равными только сами себе.
                if (double.IsInfinity(a) || double.IsInfinity(b))
                {
                    if (a != b)
                    {
                        return false;
                    }

                    continue;
                }

                if (Math.Abs(a - b) > tolerance)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Максимальное по модулю отклонение между матрицами (для отчёта).</summary>
    public static double MaxDifference(double[,] left, double[,] right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (left.GetLength(0) != right.GetLength(0) || left.GetLength(1) != right.GetLength(1))
        {
            return double.PositiveInfinity;
        }

        double maximum = 0;
        for (int i = 0; i < left.GetLength(0); i++)
        {
            for (int j = 0; j < left.GetLength(1); j++)
            {
                double difference = Math.Abs(left[i, j] - right[i, j]);
                if (difference > maximum)
                {
                    maximum = difference;
                }
            }
        }

        return maximum;
    }

    /// <summary>Текстовое представление матрицы (для журнала и отладки).</summary>
    public static string ToDisplayString(double[,] matrix, int digits = 0)
    {
        ArgumentNullException.ThrowIfNull(matrix);

        int rows = matrix.GetLength(0);
        int columns = matrix.GetLength(1);
        var lines = new string[rows];

        for (int i = 0; i < rows; i++)
        {
            var cells = new string[columns];
            for (int j = 0; j < columns; j++)
            {
                double value = matrix[i, j];
                cells[j] = double.IsPositiveInfinity(value)
                    ? "∞"
                    : value.ToString(digits == 0 ? "0" : "0." + new string('#', digits),
                        System.Globalization.CultureInfo.InvariantCulture);
            }

            lines[i] = string.Join(' ', cells);
        }

        return string.Join(Environment.NewLine, lines);
    }
}

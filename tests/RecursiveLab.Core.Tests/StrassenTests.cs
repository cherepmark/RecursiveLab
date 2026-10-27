using RecursiveLab.Core.Algorithms;

namespace RecursiveLab.Core.Tests;

/// <summary>
/// Тесты умножения матриц алгоритмом Штрассена: совпадение с классическим алгоритмом,
/// число рекурсивных вызовов и главный показатель — 7^k базовых умножений вместо 8^k.
/// </summary>
public class StrassenTests
{
    public static TheoryData<int, int> SizesAndThresholds()
    {
        var data = new TheoryData<int, int>();
        foreach (int size in new[] { 1, 2, 3, 4, 5, 7, 8, 9, 16, 17 })
        {
            foreach (int threshold in new[] { 1, 2, 4, 8, 32 })
            {
                data.Add(size, threshold);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(SizesAndThresholds))]
    public void MultiplyStrassen_MatchesClassicAlgorithm(int size, int threshold)
    {
        double[,] left = Matrix.RandomSquare(size, seed: size * 100 + threshold);
        double[,] right = Matrix.RandomSquare(size, seed: size * 1000 + threshold);

        MatrixMultiplicationResult classic = StrassenMultiplication.MultiplyClassic(left, right);
        MatrixMultiplicationResult strassen = StrassenMultiplication.MultiplyStrassen(left, right, threshold);

        Assert.True(
            Matrix.AreEqual(classic.Product, strassen.Product, 1e-9),
            $"Результаты различаются: максимальное отклонение {Matrix.MaxDifference(classic.Product, strassen.Product)}.");
    }

    [Theory]
    [InlineData(2, 3, 5)]
    [InlineData(3, 3, 3)]
    [InlineData(5, 1, 4)]
    public void MultiplyStrassen_SupportsRectangularMatrices(int rows, int inner, int columns)
    {
        var left = new double[rows, inner];
        var right = new double[inner, columns];

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < inner; j++)
            {
                left[i, j] = (i + 1) * (j + 2) % 7;
            }
        }

        for (int i = 0; i < inner; i++)
        {
            for (int j = 0; j < columns; j++)
            {
                right[i, j] = (i + j + 1) % 5;
            }
        }

        MatrixMultiplicationResult classic = StrassenMultiplication.MultiplyClassic(left, right);
        MatrixMultiplicationResult strassen = StrassenMultiplication.MultiplyStrassen(left, right, threshold: 1);

        Assert.Equal(rows, strassen.Product.GetLength(0));
        Assert.Equal(columns, strassen.Product.GetLength(1));
        Assert.True(Matrix.AreEqual(classic.Product, strassen.Product));
    }

    [Fact]
    public void MultiplyStrassen_RecursiveCallCountFollowsSevenPowers()
    {
        // Для размера 2^k и порога 1 число рекурсивных вызовов равно 1 + 7 + … + 7^(k−1) = (7^k − 1) / 6.
        for (int k = 1; k <= 5; k++)
        {
            int size = 1 << k;
            double[,] left = Matrix.RandomSquare(size, seed: k);
            double[,] right = Matrix.RandomSquare(size, seed: k + 100);

            MatrixMultiplicationResult result = StrassenMultiplication.MultiplyStrassen(left, right, threshold: 1);

            long expectedCalls = (Pow(7, k) - 1) / 6;
            Assert.Equal(expectedCalls, result.Stats.RecursiveCalls);
            Assert.Equal(k, result.Stats.MaxDepthReached);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void MultiplyStrassen_UsesSevenPowerBaseMultiplications_InsteadOfEightPower(int k)
    {
        int size = 1 << k;
        double[,] left = Matrix.RandomSquare(size, seed: k * 3);
        double[,] right = Matrix.RandomSquare(size, seed: k * 5);

        MatrixMultiplicationResult classic = StrassenMultiplication.MultiplyClassic(left, right);
        MatrixMultiplicationResult strassen = StrassenMultiplication.MultiplyStrassen(left, right, threshold: 1);

        long classicMultiplications = Pow(8, k);   // n³ у классического алгоритма
        long strassenMultiplications = Pow(7, k);  // 7^k у Штрассена

        Assert.Equal(classicMultiplications, classic.Stats.ScalarMultiplications);
        Assert.Equal(strassenMultiplications, strassen.Stats.ScalarMultiplications);

        // Главное утверждение: 7 рекурсивных умножений вместо 8 дают меньше работы.
        if (k >= 1)
        {
            Assert.True(strassenMultiplications < classicMultiplications);
        }
    }

    [Fact]
    public void MultiplyStrassen_LargerThresholdMeansFewerRecursiveCalls()
    {
        const int size = 16;
        double[,] left = Matrix.RandomSquare(size, seed: 1);
        double[,] right = Matrix.RandomSquare(size, seed: 2);

        MatrixMultiplicationResult deep = StrassenMultiplication.MultiplyStrassen(left, right, threshold: 1);
        MatrixMultiplicationResult shallow = StrassenMultiplication.MultiplyStrassen(left, right, threshold: 8);

        Assert.True(shallow.Stats.RecursiveCalls < deep.Stats.RecursiveCalls);
        Assert.True(shallow.Stats.BaseCaseMultiplications < deep.Stats.BaseCaseMultiplications);
        Assert.True(Matrix.AreEqual(deep.Product, shallow.Product));

        // Порог, равный размеру матрицы, вообще отключает рекурсию.
        MatrixMultiplicationResult plain = StrassenMultiplication.MultiplyStrassen(left, right, threshold: size);
        Assert.Equal(0, plain.Stats.RecursiveCalls);
        Assert.Equal(1, plain.Stats.BaseCaseMultiplications);
    }

    [Fact]
    public void MultiplyStrassen_RecordsRecursionDepth()
    {
        double[,] left = Matrix.RandomSquare(8, seed: 4);
        double[,] right = Matrix.RandomSquare(8, seed: 5);

        MatrixMultiplicationResult result = StrassenMultiplication.MultiplyStrassen(left, right, threshold: 1);

        // 8 → 4 → 2 → 1: три уровня разбиения.
        Assert.Equal(3, result.Stats.MaxDepthReached);
    }

    [Fact]
    public void MultiplyStrassen_HandlesIdentityAndZeroMatrices()
    {
        double[,] values = Matrix.RandomSquare(6, seed: 9);
        double[,] identity = Matrix.Identity(6);
        double[,] zero = new double[6, 6];

        MatrixMultiplicationResult byIdentity = StrassenMultiplication.MultiplyStrassen(values, identity, threshold: 2);
        MatrixMultiplicationResult byZero = StrassenMultiplication.MultiplyStrassen(values, zero, threshold: 2);

        Assert.True(Matrix.AreEqual(values, byIdentity.Product));
        Assert.True(Matrix.AreEqual(zero, byZero.Product));
    }

    [Fact]
    public void MultiplyClassic_RejectsMismatchedDimensions()
    {
        var left = new double[2, 3];
        var right = new double[2, 3];

        Assert.Throws<ArgumentException>(() => StrassenMultiplication.MultiplyClassic(left, right));
        Assert.Throws<ArgumentException>(() => StrassenMultiplication.MultiplyStrassen(left, right));
    }

    [Fact]
    public void Multiply_RejectsEmptyMatrices()
    {
        var empty = new double[0, 0];
        var square = new double[2, 2];

        Assert.Throws<ArgumentException>(() => StrassenMultiplication.MultiplyClassic(empty, square));
        Assert.Throws<ArgumentException>(() => StrassenMultiplication.MultiplyStrassen(square, empty));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public void MultiplyStrassen_RejectsInvalidThreshold(int threshold)
    {
        double[,] matrix = Matrix.RandomSquare(4, seed: 1);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => StrassenMultiplication.MultiplyStrassen(matrix, matrix, threshold));
    }

    [Fact]
    public void MultiplyStrassen_RejectsTooLargeMatrices()
    {
        int tooLarge = StrassenMultiplication.MaxSupportedSize * 2;
        var matrix = new double[tooLarge, tooLarge];

        Assert.Throws<ArgumentOutOfRangeException>(
            () => StrassenMultiplication.MultiplyStrassen(matrix, matrix, threshold: 1));
    }

    [Fact]
    public void MultiplyStrassen_SupportsMaximumSupportedSize()
    {
        const int size = StrassenMultiplication.MaxSupportedSize;
        double[,] left = Matrix.RandomSquare(size, seed: 1);
        double[,] right = Matrix.RandomSquare(size, seed: 2);

        MatrixMultiplicationResult result = StrassenMultiplication.MultiplyStrassen(left, right, threshold: 16);

        Assert.Equal(size, result.Size);
        Assert.True(Matrix.AreEqual(
            StrassenMultiplication.MultiplyClassic(left, right).Product,
            result.Product));
    }

    [Fact]
    public void MultiplyStrassen_ReportsAlgorithmName()
    {
        double[,] matrix = Matrix.RandomSquare(4, seed: 1);

        Assert.Contains("Штрассена", StrassenMultiplication.MultiplyStrassen(matrix, matrix).AlgorithmName, StringComparison.Ordinal);
        Assert.Contains("O(n³)", StrassenMultiplication.MultiplyClassic(matrix, matrix).AlgorithmName, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------
    // Вспомогательный класс Matrix
    // ---------------------------------------------------------------------

    [Fact]
    public void Matrix_AreEqual_ComparesSizesToleranceAndInfinity()
    {
        var first = new double[2, 2];
        var second = new double[2, 2];

        Assert.True(Matrix.AreEqual(first, second));
        Assert.True(Matrix.AreEqual(first, new double[2, 3]) == false);

        second[0, 0] = 1e-12;
        Assert.True(Matrix.AreEqual(first, second, tolerance: 1e-9));
        Assert.False(Matrix.AreEqual(first, second, tolerance: 1e-15));

        second[0, 0] = double.PositiveInfinity;
        Assert.False(Matrix.AreEqual(first, second));
        Assert.True(Matrix.AreEqual(second, second));
    }

    [Fact]
    public void Matrix_MaxDifference_ReportsLargestDeviation()
    {
        var first = new double[2, 2];
        var second = new double[2, 2];
        second[1, 1] = 7;

        Assert.Equal(7, Matrix.MaxDifference(first, second));
        Assert.True(double.IsPositiveInfinity(Matrix.MaxDifference(first, new double[3, 3])));
    }

    [Fact]
    public void Matrix_ValidateSquare_RejectsNonSquareAndEmpty()
    {
        Assert.Throws<ArgumentException>(() => Matrix.ValidateSquare(new double[2, 3]));
        Assert.Throws<ArgumentException>(() => Matrix.ValidateSquare(new double[0, 0]));
        Assert.Equal(3, Matrix.ValidateSquare(new double[3, 3]));
    }

    [Fact]
    public void Matrix_ValidateSquare_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => Matrix.ValidateSquare(null!));
    }

    [Fact]
    public void Matrix_RandomSquare_IsDeterministicForSameSeed()
    {
        Assert.True(Matrix.AreEqual(Matrix.RandomSquare(5, 42), Matrix.RandomSquare(5, 42)));
        Assert.False(Matrix.AreEqual(Matrix.RandomSquare(5, 42), Matrix.RandomSquare(5, 43)));
    }

    [Fact]
    public void Matrix_ToDisplayString_ShowsInfinityAndIntegers()
    {
        var matrix = new double[2, 2] { { 1, double.PositiveInfinity }, { 0, -3 } };

        string display = Matrix.ToDisplayString(matrix);

        Assert.Contains("∞", display, StringComparison.Ordinal);
        Assert.Contains("-3", display, StringComparison.Ordinal);
        Assert.Equal(2, display.Split(Environment.NewLine).Length);
    }

    private static long Pow(long value, int exponent)
    {
        long result = 1;
        for (int i = 0; i < exponent; i++)
        {
            result *= value;
        }

        return result;
    }
}

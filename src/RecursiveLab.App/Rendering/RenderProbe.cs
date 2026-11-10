using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace RecursiveLab.App.Rendering;

/// <summary>
/// Числовой анализ готового изображения холста.
///
/// Нужен для объективной проверки отрисовки без участия человека: считается
/// количество закрашенных пикселей, центр тяжести изображения и расхождение
/// с зеркальным отражением относительно вертикальной оси холста.
/// </summary>
internal static class RenderProbe
{
    /// <summary>Результат анализа изображения холста.</summary>
    /// <param name="PaintedPixels">Количество пикселей, отличающихся от фона.</param>
    /// <param name="CenterOfMassX">Центр тяжести закрашенных пикселей по горизонтали (доля ширины, 0..1).</param>
    /// <param name="HalfBalance">Дисбаланс половин: (левая − правая) / всего, от −1 до 1.</param>
    /// <param name="MirrorMismatch">Доля закрашенных пикселей без зеркального «двойника».</param>
    /// <param name="PaintedWidthFraction">Ширина закрашенной области (доля ширины холста).</param>
    /// <param name="PaintedHeightFraction">Высота закрашенной области (доля высоты холста).</param>
    public readonly record struct Analysis(
        int PaintedPixels,
        double CenterOfMassX,
        double HalfBalance,
        double MirrorMismatch,
        double PaintedWidthFraction,
        double PaintedHeightFraction);

    /// <summary>Считает закрашенным пиксель, отличающийся от фона заметнее этого порога.</summary>
    private const int PaintThreshold = 24;

    /// <summary>
    /// Числовой анализ изображения. Верхние <paramref name="skipTopRows"/> строк исключаются:
    /// там находятся служебные подписи холста, которые не являются частью визуализации.
    /// </summary>
    public static Analysis Analyze(Bitmap bitmap, Color background, int skipTopRows = 0)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        int width = bitmap.Width;
        int height = bitmap.Height;
        int firstRow = Math.Clamp(skipTopRows, 0, height);
        var data = bitmap.LockBits(
            new Rectangle(0, 0, width, height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);

        try
        {
            int stride = data.Stride;
            byte[] buffer = new byte[stride * height];
            Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);

            bool[] painted = new bool[width * height];
            int paintedCount = 0;
            int paintedLeft = 0;
            double weightedX = 0;
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;

            for (int y = firstRow; y < height; y++)
            {
                int row = y * stride;
                for (int x = 0; x < width; x++)
                {
                    int offset = row + x * 4;
                    int b = buffer[offset];
                    int g = buffer[offset + 1];
                    int r = buffer[offset + 2];

                    int difference = Math.Abs(b - background.B)
                                     + Math.Abs(g - background.G)
                                     + Math.Abs(r - background.R);

                    if (difference > PaintThreshold)
                    {
                        painted[y * width + x] = true;
                        paintedCount++;
                        weightedX += x;

                        if (x < width / 2)
                        {
                            paintedLeft++;
                        }

                        minX = Math.Min(minX, x);
                        maxX = Math.Max(maxX, x);
                        minY = Math.Min(minY, y);
                        maxY = Math.Max(maxY, y);
                    }
                }
            }

            if (paintedCount == 0)
            {
                return new Analysis(0, 0, 0, 0, 0, 0);
            }

            int mismatches = 0;
            for (int y = firstRow; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (painted[y * width + x] != painted[y * width + (width - 1 - x)])
                    {
                        mismatches++;
                    }
                }
            }

            // Каждая несимметричная пара считается дважды, поэтому делим на удвоенное
            // количество закрашенных пикселей: получаем долю пикселей без зеркального «двойника».
            double mismatchRatio = mismatches / (2.0 * paintedCount);

            return new Analysis(
                paintedCount,
                weightedX / paintedCount / width,
                (2.0 * paintedLeft - paintedCount) / paintedCount,
                mismatchRatio,
                (maxX - minX + 1) / (double)width,
                (maxY - minY + 1) / (double)height);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    /// <summary>
    /// Доля различающихся пикселей двух изображений одного размера.
    /// Нужна самопроверке, чтобы убедиться, что смена вида (масштаб, сдвиг) действительно
    /// изменила картинку, а сброс вида вернул её к исходной.
    /// </summary>
    public static double DifferenceRatio(Bitmap left, Bitmap right, int threshold = PaintThreshold)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (left.Width != right.Width || left.Height != right.Height)
        {
            return 1.0;
        }

        int width = left.Width;
        int height = left.Height;

        var leftData = left.LockBits(
            new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var rightData = right.LockBits(
            new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

        try
        {
            var leftBuffer = new byte[leftData.Stride * height];
            var rightBuffer = new byte[rightData.Stride * height];
            Marshal.Copy(leftData.Scan0, leftBuffer, 0, leftBuffer.Length);
            Marshal.Copy(rightData.Scan0, rightBuffer, 0, rightBuffer.Length);

            long different = 0;
            long total = 0;

            for (int y = 0; y < height; y++)
            {
                int leftRow = y * leftData.Stride;
                int rightRow = y * rightData.Stride;

                for (int x = 0; x < width; x++)
                {
                    int offset = x * 4;
                    int difference = Math.Abs(leftBuffer[leftRow + offset] - rightBuffer[rightRow + offset])
                                     + Math.Abs(leftBuffer[leftRow + offset + 1] - rightBuffer[rightRow + offset + 1])
                                     + Math.Abs(leftBuffer[leftRow + offset + 2] - rightBuffer[rightRow + offset + 2]);

                    if (difference > threshold)
                    {
                        different++;
                    }

                    total++;
                }
            }

            return total == 0 ? 0 : different / (double)total;
        }
        finally
        {
            left.UnlockBits(leftData);
            right.UnlockBits(rightData);
        }
    }
}

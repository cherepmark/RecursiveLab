using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace RecursiveLab.App.Rendering;

/// <summary>
/// Холст для отображения матрицы в виде «тепловой карты».
///
/// Используется страницами самостоятельных алгоритмов: матрица смежности графа,
/// матрица кратчайших расстояний и результат умножения матриц. Значения отображаются
/// цветом, а для небольших размеров — ещё и числом.
/// </summary>
internal sealed class MatrixCanvas : Control, IRenderable
{
    private double[,]? _matrix;

    public MatrixCanvas()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw,
            true);

        BackColor = Theme.Surface;
        DoubleBuffered = true;
        Dock = DockStyle.Fill;
    }

    /// <summary>Отображаемая матрица (null — холст пуст).</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public double[,]? Matrix
    {
        get => _matrix;
        set
        {
            _matrix = value;
            Invalidate();
        }
    }

    /// <summary>Заголовок над сеткой.</summary>
    public string Caption { get; set; } = string.Empty;

    /// <summary>Печатать ли числа внутри клеток.</summary>
    public bool ShowValues { get; set; } = true;

    /// <summary>Размер матрицы, до которого числа ещё читаются.</summary>
    public int NumberLimit { get; set; } = 12;

    /// <summary>Нулевая матрица подсвечивается как «всё верно» (для матрицы разности).</summary>
    public bool HighlightZeroMatrix { get; set; }

    public void Clear() => Matrix = null;

    public void Render(Graphics graphics, Size size)
    {
        graphics.Clear(BackColor);
        graphics.SmoothingMode = SmoothingMode.None;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        const int captionHeight = 26;
        DrawCaption(graphics, size);

        if (_matrix is null)
        {
            DrawPlaceholder(graphics, size, captionHeight);
            return;
        }

        int rows = _matrix.GetLength(0);
        int columns = _matrix.GetLength(1);
        int available = Math.Min(size.Height - captionHeight, size.Width) - 8;

        if (available <= 0 || rows == 0 || columns == 0)
        {
            return;
        }

        float cell = Math.Min(available / (float)rows, available / (float)columns);
        cell = Math.Max(cell, 1f);

        float offsetX = (size.Width - cell * columns) / 2f;
        float offsetY = captionHeight + (available - cell * rows) / 2f;

        (double min, double max) = ValueRange(out bool allZero);

        if (HighlightZeroMatrix && allZero)
        {
            DrawZeroMatrixMessage(graphics, size, offsetX, offsetY, cell, rows, columns);
            return;
        }

        bool drawNumbers = ShowValues && rows <= NumberLimit && columns <= NumberLimit && cell >= 20f;

        using var gridPen = new Pen(Color.FromArgb(220, 226, 236));
        using var textBrush = new SolidBrush(Theme.Text);
        using var infinityBrush = new SolidBrush(Color.White);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
        };

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < columns; j++)
            {
                double value = _matrix[i, j];
                var rectangle = new RectangleF(
                    offsetX + j * cell,
                    offsetY + i * cell,
                    cell,
                    cell);

                using (var brush = new SolidBrush(CellColor(value, min, max, i == j)))
                {
                    graphics.FillRectangle(brush, rectangle);
                }

                graphics.DrawRectangle(gridPen, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);

                if (cell < 12f)
                {
                    continue;
                }

                bool isInfinity = double.IsPositiveInfinity(value);
                if (!drawNumbers && !isInfinity)
                {
                    continue;
                }

                string text = isInfinity
                    ? "∞"
                    : value.ToString(value == Math.Floor(value) ? "0" : "0.#");

                // Кисти общие для всего холста, поэтому здесь нельзя использовать using:
                // освобождение затронуло бы кисть для остальных клеток.
                Brush currentBrush = isInfinity ? infinityBrush : textBrush;

                graphics.DrawString(text, Theme.HintFont, currentBrush, rectangle, format);
            }
        }
    }

    protected override void OnPaint(PaintEventArgs e) => Render(e.Graphics, ClientSize);

    private void DrawCaption(Graphics graphics, Size size)
    {
        if (string.IsNullOrWhiteSpace(Caption))
        {
            return;
        }

        TextRenderer.DrawText(
            graphics,
            Caption,
            Theme.BoldFont,
            new Point(6, 6),
            Theme.Text,
            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPadding);

        if (_matrix is not null)
        {
            TextRenderer.DrawText(
                graphics,
                $"{_matrix.GetLength(0)}×{_matrix.GetLength(1)}",
                Theme.HintFont,
                new Point(size.Width - 48, 8),
                Theme.Muted,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPadding);
        }
    }

    private void DrawZeroMatrixMessage(Graphics graphics, Size size, float offsetX, float offsetY, float cell, int rows, int columns)
    {
        var area = new RectangleF(offsetX, offsetY, cell * columns, cell * rows);

        using (var brush = new SolidBrush(Color.FromArgb(223, 245, 231)))
        {
            graphics.FillRectangle(brush, area);
        }

        using var pen = new Pen(Color.FromArgb(150, 200, 170));
        graphics.DrawRectangle(pen, area.X, area.Y, area.Width, area.Height);

        const string message = "Матрицы совпадают: все элементы нулевые";
        Size textSize = TextRenderer.MeasureText(message, Theme.BaseFont);

        TextRenderer.DrawText(
            graphics,
            message,
            Theme.BaseFont,
            new Point(
                (int)(area.X + (area.Width - textSize.Width) / 2),
                (int)(area.Y + (area.Height - textSize.Height) / 2)),
            Theme.Success);
    }

    private void DrawPlaceholder(Graphics graphics, Size size, int captionHeight)
    {
        const string hint = "Нажмите «Вычислить»";
        Size textSize = TextRenderer.MeasureText(hint, Theme.BaseFont);

        TextRenderer.DrawText(
            graphics,
            hint,
            Theme.BaseFont,
            new Point((size.Width - textSize.Width) / 2, captionHeight + (size.Height - captionHeight - textSize.Height) / 2),
            Theme.Muted);
    }

    private (double Min, double Max) ValueRange(out bool allZero)
    {
        double min = double.PositiveInfinity;
        double max = double.NegativeInfinity;
        allZero = true;

        foreach (double value in _matrix!)
        {
            if (double.IsPositiveInfinity(value) || double.IsNegativeInfinity(value))
            {
                allZero = false;
                continue;
            }

            if (Math.Abs(value) > 1e-12)
            {
                allZero = false;
            }

            min = Math.Min(min, value);
            max = Math.Max(max, value);
        }

        if (double.IsPositiveInfinity(min))
        {
            return (0, 0);
        }

        return (min, max);
    }

    /// <summary>Цвет клетки: синий → жёлтый → красный по возрастанию значения.</summary>
    private static Color CellColor(double value, double min, double max, bool diagonal)
    {
        if (double.IsPositiveInfinity(value))
        {
            return Color.FromArgb(86, 92, 104);
        }

        if (max - min <= 1e-12)
        {
            return diagonal ? Color.FromArgb(226, 232, 240) : Color.FromArgb(236, 240, 247);
        }

        double t = Math.Clamp((value - min) / (max - min), 0, 1);

        Color from = t < 0.5 ? Color.FromArgb(198, 222, 250) : Color.FromArgb(252, 240, 190);
        Color to = t < 0.5 ? Color.FromArgb(252, 240, 190) : Color.FromArgb(246, 190, 180);
        double local = t < 0.5 ? t * 2 : (t - 0.5) * 2;

        return Lerp(from, to, local);
    }

    private static Color Lerp(Color from, Color to, double t)
        => Color.FromArgb(
            (int)(from.R + (to.R - from.R) * t),
            (int)(from.G + (to.G - from.G) * t),
            (int)(from.B + (to.B - from.B) * t));
}

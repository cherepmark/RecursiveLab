using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using RecursiveLab.Core.Fractals;
using RecursiveLab.Core.Geometry;

namespace RecursiveLab.App.Rendering;

/// <summary>
/// Холст для отображения фракталов.
///
/// Слой отрисовки ничего не знает об алгоритмах: он получает готовую геометрию
/// в единичном квадрате и переносит её на экран равномерным масштабированием
/// с сохранением пропорций. Поэтому один и тот же холст рисует все фракталы.
/// </summary>
internal sealed class FractalCanvas : Control, IRenderable
{
    private FractalGeometry? _geometry;

    public FractalCanvas()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw,
            true);

        BackColor = Theme.FractalCanvasBackground;
        DoubleBuffered = true;
        Dock = DockStyle.Fill;
    }

    /// <summary>Построенная геометрия или null, если холст пуст.</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public FractalGeometry? Geometry
    {
        get => _geometry;
        set
        {
            _geometry = value;
            Invalidate();
        }
    }

    /// <summary>Очищает холст.</summary>
    public void Clear() => Geometry = null;

    /// <summary>
    /// Градиентная раскраска отрезков по порядку обхода — делает видимым путь
    /// фрактальной кривой (используется для кривой Гильберта).
    /// </summary>
    public bool UseGradientStroke { get; set; }

    /// <summary>
    /// Отрисовывает текущую геометрию в произвольный Graphics заданного размера.
    /// Отдельный метод нужен и для Control.OnPaint, и для самопроверки (сохранение PNG).
    /// </summary>
    public void Render(Graphics graphics, Size size)
    {
        graphics.Clear(BackColor);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        if (_geometry is null || _geometry.PrimitiveCount == 0)
        {
            DrawPlaceholder(graphics, size);
            return;
        }

        // Равномерный масштаб: самая длинная сторона фигуры занимает 92% холста.
        const double fill = 0.92;
        double scale = Math.Min(size.Width, size.Height) * fill;
        double offsetX = (size.Width - scale) / 2.0;
        double offsetY = (size.Height - scale) / 2.0;

        PointF Map(PointD p) => new(
            (float)(offsetX + p.X * scale),
            (float)(offsetY + p.Y * scale));

        if (_geometry.Triangles.Count > 0)
        {
            DrawTriangles(graphics, Map);
        }

        if (_geometry.Segments.Count > 0)
        {
            DrawSegments(graphics, Map);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Render(e.Graphics, ClientSize);

        if (_geometry is not null)
        {
            TextRenderer.DrawText(
                e.Graphics,
                $"{FractalCatalog.Get(_geometry.Kind).DisplayName} · глубина {_geometry.Depth}",
                Theme.HintFont,
                new Point(12, 10),
                Color.FromArgb(150, 170, 200));
        }
    }

    private void DrawTriangles(Graphics graphics, Func<PointD, PointF> map)
    {
        // Все треугольники объединяются в один GraphicsPath: так отрисовка
        // десятков тысяч примитивов остаётся быстрой (один вызов FillPath).
        using var path = new GraphicsPath();
        foreach (Triangle triangle in _geometry!.Triangles)
        {
            path.AddPolygon([map(triangle.A), map(triangle.B), map(triangle.C)]);
        }

        // Треугольники только заливаются: на больших глубинах лист становится меньше
        // пикселя, и обводка «замазала» бы структуру фрактала.
        using var brush = new SolidBrush(Theme.FractalFill);
        graphics.FillPath(brush, path);
    }

    private void DrawSegments(Graphics graphics, Func<PointD, PointF> map)
    {
        if (UseGradientStroke)
        {
            DrawSegmentsWithGradient(graphics, map);
            return;
        }

        using var path = new GraphicsPath();
        foreach (Segment segment in _geometry!.Segments)
        {
            // StartFigure обязателен: иначе GraphicsPath соединит конец предыдущего
            // отрезка с началом следующего (у дерева ветви не связаны подряд).
            path.StartFigure();
            path.AddLine(map(segment.A), map(segment.B));
        }

        using var pen = new Pen(Theme.FractalStroke, 1.2f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round,
        };

        graphics.DrawPath(pen, path);
    }

    /// <summary>
    /// Рисует отрезки группами, меняя оттенок по порядку обхода: начало кривой — синее,
    /// конец — красно-оранжевое. Так видно, как кривая заполняет квадрат.
    /// </summary>
    private void DrawSegmentsWithGradient(Graphics graphics, Func<PointD, PointF> map)
    {
        const int groups = 60;
        IReadOnlyList<Segment> segments = _geometry!.Segments;
        int perGroup = Math.Max(1, (segments.Count + groups - 1) / groups);

        for (int group = 0; group * perGroup < segments.Count; group++)
        {
            using var path = new GraphicsPath();
            int start = group * perGroup;
            int end = Math.Min(segments.Count, start + perGroup);

            for (int i = start; i < end; i++)
            {
                path.StartFigure();
                path.AddLine(map(segments[i].A), map(segments[i].B));
            }

            double hue = 230.0 * group / Math.Max(1, groups - 1);
            using var pen = new Pen(ColorFromHue(hue), 1.5f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round,
            };

            graphics.DrawPath(pen, path);
        }
    }

    /// <summary>Цвет по оттенку HSV (насыщенность и яркость подобраны для тёмного фона).</summary>
    private static Color ColorFromHue(double hue)
    {
        double h = ((hue % 360) + 360) % 360 / 60.0;
        const double saturation = 0.75;
        const double value = 0.98;

        int sector = (int)Math.Floor(h);
        double fraction = h - sector;
        double p = value * (1 - saturation);
        double q = value * (1 - saturation * fraction);
        double t = value * (1 - saturation * (1 - fraction));

        (double r, double g, double b) = sector switch
        {
            0 => (value, t, p),
            1 => (q, value, p),
            2 => (p, value, t),
            3 => (p, q, value),
            4 => (t, p, value),
            _ => (value, p, q),
        };

        return Color.FromArgb((int)(r * 255), (int)(g * 255), (int)(b * 255));
    }

    private void DrawPlaceholder(Graphics graphics, Size size)
    {
        const string hint = "Выберите фрактал и нажмите «Построить»";
        Size textSize = TextRenderer.MeasureText(hint, Theme.BaseFont);
        TextRenderer.DrawText(
            graphics,
            hint,
            Theme.BaseFont,
            new Point((size.Width - textSize.Width) / 2, (size.Height - textSize.Height) / 2),
            Color.FromArgb(110, 126, 150));
    }
}

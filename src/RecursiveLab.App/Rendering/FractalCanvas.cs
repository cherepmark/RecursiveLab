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

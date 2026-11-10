using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using RecursiveLab.Core.ShortestPaths;

namespace RecursiveLab.App.Rendering;

/// <summary>
/// Холст для изображения взвешенного графа.
///
/// Вершины располагаются по окружности, рёбра рисуются стрелками с весами.
/// Рёбра, входящие в кратчайшие пути из выбранной вершины, подсвечиваются,
/// а недостижимые вершины приглушаются — так алгоритм Флойда—Уоршелла видно
/// именно на графе, а не только в матрице.
/// </summary>
internal sealed class GraphCanvas : Control, IRenderable
{
    private WeightedGraph? _graph;

    public GraphCanvas()
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

    /// <summary>Изображаемый граф.</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public WeightedGraph? Graph
    {
        get => _graph;
        set
        {
            _graph = value;
            Invalidate();
        }
    }

    /// <summary>Рёбра, которые нужно подсветить (кратчайшие пути из источника).</summary>
    public IReadOnlyList<(int From, int To)> HighlightedEdges { get; set; } = [];

    /// <summary>Вершина-источник подсветки (null — подсветки нет).</summary>
    public int? SourceVertex { get; set; }

    /// <summary>Вершины, достижимые из источника; остальные приглушаются (null — все считаются достижимыми).</summary>
    public IReadOnlyCollection<int>? ReachableVertices { get; set; }

    /// <summary>Пояснение под заголовком, например «промежуточные вершины 1..3».</summary>
    public string? Note { get; set; }

    /// <summary>Подпись режима в правом верхнем углу.</summary>
    public string? Badge { get; set; }

    public void Refresh(WeightedGraph? graph, IReadOnlyList<(int From, int To)> highlighted, int? source, string? note)
    {
        Graph = graph;
        HighlightedEdges = highlighted;
        SourceVertex = source;
        Note = note;
        ReachableVertices = source is null
            ? null
            : [.. Enumerable.Range(0, graph?.VertexCount ?? 0)];

        Invalidate();
    }

    public void Render(Graphics graphics, Size size)
    {
        graphics.Clear(BackColor);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        const int headerHeight = 46;
        DrawHeader(graphics, size, headerHeight);

        if (_graph is null || _graph.VertexCount == 0)
        {
            DrawPlaceholder(graphics, size, headerHeight);
            return;
        }

        (PointF[] positions, float radius) = ComputePositions(size, headerHeight, _graph.VertexCount);

        // Рёбра рисуются первыми, чтобы стрелки уходили под круги вершин.
        foreach ((int from, int to, double weight) in _graph.EnumerateEdges())
        {
            bool highlighted = HighlightedEdges.Contains((from, to));
            DrawEdge(graphics, positions, radius, from, to, weight, highlighted);
        }

        for (int vertex = 0; vertex < _graph.VertexCount; vertex++)
        {
            DrawVertex(graphics, positions[vertex], radius, vertex);
        }
    }

    protected override void OnPaint(PaintEventArgs e) => Render(e.Graphics, ClientSize);

    private void DrawHeader(Graphics graphics, Size size, int headerHeight)
    {
        if (_graph is null)
        {
            return;
        }

        string title = $"Граф: {_graph.VertexCount} вершин, {_graph.EdgeCount} рёбер";
        if (SourceVertex is { } source)
        {
            title += $" · пути из {WeightedGraph.VertexName(source)}";
        }

        TextRenderer.DrawText(
            graphics,
            title,
            Theme.BoldFont,
            new Point(12, 8),
            Theme.Text,
            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPadding);

        if (!string.IsNullOrWhiteSpace(Note))
        {
            TextRenderer.DrawText(
                graphics,
                Note,
                Theme.HintFont,
                new Rectangle(12, 26, size.Width - 24, headerHeight - 26),
                Theme.Muted,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPadding);
        }

        if (!string.IsNullOrWhiteSpace(Badge))
        {
            Size badgeSize = TextRenderer.MeasureText(Badge, Theme.HintFont);
            TextRenderer.DrawText(
                graphics,
                Badge,
                Theme.HintFont,
                new Point(Math.Max(12, size.Width - badgeSize.Width - 12), 10),
                Theme.Accent);
        }
    }

    private void DrawPlaceholder(Graphics graphics, Size size, int headerHeight)
    {
        const string hint = "Выберите граф и нажмите «Вычислить»";
        Size textSize = TextRenderer.MeasureText(hint, Theme.BaseFont);

        TextRenderer.DrawText(
            graphics,
            hint,
            Theme.BaseFont,
            new Point((size.Width - textSize.Width) / 2, headerHeight + (size.Height - headerHeight) / 2),
            Theme.Muted);
    }

    private static (PointF[] Positions, float Radius) ComputePositions(Size size, int headerHeight, int vertexCount)
    {
        float centerX = size.Width / 2f;
        float centerY = headerHeight + (size.Height - headerHeight) / 2f;
        float radius = Math.Min(size.Width, size.Height - headerHeight) * 0.36f;
        radius = Math.Max(radius, 40f);

        var positions = new PointF[vertexCount];
        for (int i = 0; i < vertexCount; i++)
        {
            double angle = -Math.PI / 2 + 2 * Math.PI * i / vertexCount;
            positions[i] = new PointF(
                centerX + (float)(radius * Math.Cos(angle)),
                centerY + (float)(radius * Math.Sin(angle)));
        }

        float vertexRadius = Math.Clamp(radius * 0.2f, 12f, 24f);
        return (positions, vertexRadius);
    }

    private void DrawEdge(
        Graphics graphics,
        PointF[] positions,
        float vertexRadius,
        int from,
        int to,
        double weight,
        bool highlighted)
    {
        PointF start = positions[from];
        PointF end = positions[to];

        // Рёбра между одной и той же парой вершин разводятся в разные стороны.
        bool reverseExists = _graph!.HasEdge(to, from);
        float curvature = reverseExists ? 0.18f : 0f;

        (PointF lineStart, PointF lineEnd) = TrimSegment(start, end, vertexRadius + 2f);
        PointF control = MidPoint(lineStart, lineEnd, curvature);

        Color color = highlighted ? Theme.Accent : Theme.GraphEdge;
        float width = highlighted ? 2.4f : 1.2f;

        using var pen = new Pen(color, width);
        using var path = new GraphicsPath();
        path.AddBezier(lineStart, control, control, lineEnd);
        graphics.DrawPath(pen, path);

        DrawArrowHead(graphics, control, lineEnd, color, vertexRadius);
        DrawWeightLabel(graphics, control, lineEnd, weight, highlighted);
    }

    private static void DrawArrowHead(Graphics graphics, PointF control, PointF tip, Color color, float vertexRadius)
    {
        double angle = Math.Atan2(tip.Y - control.Y, tip.X - control.X);
        const float length = 9f;
        const double spread = 0.42;

        var p1 = new PointF(
            tip.X - (float)(length * Math.Cos(angle - spread)),
            tip.Y - (float)(length * Math.Sin(angle - spread)));
        var p2 = new PointF(
            tip.X - (float)(length * Math.Cos(angle + spread)),
            tip.Y - (float)(length * Math.Sin(angle + spread)));

        using var brush = new SolidBrush(color);
        graphics.FillPolygon(brush, [tip, p1, p2]);

        _ = vertexRadius;
    }

    private static void DrawWeightLabel(Graphics graphics, PointF control, PointF end, double weight, bool highlighted)
    {
        // Подпись веса смещается к середине дуги, но не заезжает на вершину.
        float x = control.X * 0.75f + end.X * 0.25f;
        float y = control.Y * 0.75f + end.Y * 0.25f;

        string text = weight.ToString("0.##");
        Size textSize = TextRenderer.MeasureText(text, Theme.NodeFont);
        var rectangle = new Rectangle(
            (int)x - textSize.Width / 2 - 3,
            (int)y - textSize.Height / 2 - 1,
            textSize.Width + 6,
            textSize.Height + 2);

        using (var background = new SolidBrush(Color.FromArgb(235, 255, 255, 255)))
        {
            graphics.FillRectangle(background, rectangle);
        }

        TextRenderer.DrawText(
            graphics,
            text,
            Theme.NodeFont,
            rectangle,
            highlighted ? Theme.Accent : Theme.Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    private void DrawVertex(Graphics graphics, PointF center, float radius, int vertex)
    {
        bool isSource = SourceVertex == vertex;
        bool isReachable = ReachableVertices is null || ReachableVertices.Contains(vertex);

        var rectangle = new RectangleF(center.X - radius, center.Y - radius, radius * 2, radius * 2);

        Color border = !isReachable
            ? Theme.GraphMuted
            : isSource ? Theme.Accent : Color.FromArgb(96, 106, 124);
        Color fill = isSource ? Theme.Accent : Theme.GraphVertexFill;

        using (var brush = new SolidBrush(fill))
        {
            graphics.FillEllipse(brush, rectangle);
        }

        using (var pen = new Pen(border, isSource ? 2.4f : 1.6f))
        {
            graphics.DrawEllipse(pen, rectangle);
        }

        string name = WeightedGraph.VertexName(vertex);
        using var textBrush = new SolidBrush(isSource ? Color.White : isReachable ? Theme.Text : Theme.GraphMuted);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        graphics.DrawString(name, Theme.GraphFont, textBrush, rectangle, format);
    }

    private static (PointF Start, PointF End) TrimSegment(PointF start, PointF end, float trim)
    {
        float dx = end.X - start.X;
        float dy = end.Y - start.Y;
        double length = Math.Sqrt(dx * dx + dy * dy);

        if (length <= 1e-6)
        {
            return (start, end);
        }

        float ux = (float)(dx / length);
        float uy = (float)(dy / length);

        var trimmedStart = new PointF(start.X + ux * trim * 0.6f, start.Y + uy * trim * 0.6f);
        var trimmedEnd = new PointF(end.X - ux * trim, end.Y - uy * trim);
        return (trimmedStart, trimmedEnd);
    }

    private static PointF MidPoint(PointF start, PointF end, float curvature)
    {
        var middle = new PointF((start.X + end.X) / 2f, (start.Y + end.Y) / 2f);
        if (Math.Abs(curvature) < 1e-6)
        {
            return middle;
        }

        float dx = end.X - start.X;
        float dy = end.Y - start.Y;
        double length = Math.Sqrt(dx * dx + dy * dy);
        if (length <= 1e-6)
        {
            return middle;
        }

        // Смещение середины перпендикулярно отрезку превращает прямую в дугу.
        float offset = (float)(length / 2 * curvature);
        return new PointF(
            middle.X - (float)(dy / length) * offset,
            middle.Y + (float)(dx / length) * offset);
    }
}

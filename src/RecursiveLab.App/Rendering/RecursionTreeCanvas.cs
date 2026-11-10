using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using RecursiveLab.Core.Recursion;

namespace RecursiveLab.App.Rendering;

/// <summary>
/// Холст для изображения дерева рекурсии.
///
/// Дерево раскладывается «классическим» способом: листья получают последовательные
/// позиции по горизонтали, а каждый внутренний узел — середину между своими детьми;
/// вертикаль соответствует уровню рекурсии. Схема масштабируется так, чтобы целиком
/// помещаться в холст.
///
/// Вид можно приближать и сдвигать: колесо мыши меняет масштаб (относительно курсора),
/// перетаскивание левой кнопкой сдвигает изображение, двойной щелчок возвращает
/// «вписанный» вид. Так схему можно рассмотреть вблизи, даже если она большая.
///
/// Цвет узла показывает его роль: обычный вызов, базовый случай рекурсии или значение,
/// взятое из таблицы при мемоизации.
/// </summary>
internal sealed class RecursionTreeCanvas : Control, IRenderable
{
    private const float NodeWidth = 104f;
    private const float NodeHeight = 36f;
    private const float ColumnGap = 12f;
    private const float LevelGap = 30f;

    /// <summary>Пределы масштаба относительно «вписанного» вида.</summary>
    private const double MinZoom = 0.6;
    private const double MaxZoom = 12.0;

    /// <summary>Минимальная доля изображения, которая обязана оставаться в окне при сдвиге (пиксели).</summary>
    private const float PanMargin = 48f;

    private RecursionTree? _tree;
    private double _zoom = 1.0;
    private PointF _pan = PointF.Empty;

    private bool _isPanning;
    private Point _panStart;
    private PointF _panOrigin;

    // Параметры последней отрисовки — нужны, чтобы зум привязывался к курсору.
    private double _fitScale = 1.0;
    private double _contentWidth = 1;
    private double _contentHeight = 1;
    private PointF _fitOrigin = PointF.Empty;
    private int _headerHeight = 52;
    private bool _hasFitInfo;

    public RecursionTreeCanvas()
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
        Cursor = Cursors.Default;
    }

    /// <summary>Изображаемое дерево (null — дерево не построено).</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public RecursionTree? Tree
    {
        get => _tree;
        set
        {
            if (!ReferenceEquals(_tree, value))
            {
                // Новое дерево показывается целиком, прежний масштаб сбрасывается.
                FitToWindow();
            }

            _tree = value;
            Invalidate();
        }
    }

    /// <summary>Текущий масштаб относительно «вписанного» вида: 1 — дерево видно целиком.</summary>
    public double Zoom => _zoom;

    /// <summary>Текст, который показывается, когда дерева нет (например, для итеративного режима).</summary>
    public string PlaceholderText { get; set; } = "Дерево рекурсии не построено";

    /// <summary>Вид изменён (приближен или сдвинут) относительно «вписанного».</summary>
    public bool IsZoomed => _zoom > 1.0001 || Math.Abs(_pan.X) > 0.5f || Math.Abs(_pan.Y) > 0.5f;

    /// <summary>Возвращает «вписанный» вид: дерево целиком, без сдвига.</summary>
    public void FitToWindow()
    {
        _zoom = 1.0;
        _pan = PointF.Empty;
        Invalidate();
    }

    /// <summary>
    /// Меняет масштаб относительно указанной точки холста: точка под курсором
    /// остаётся на месте, поэтому приближение идёт «в место интереса».
    /// </summary>
    public void ZoomBy(double factor, Point anchor)
    {
        if (factor <= 0)
        {
            return;
        }

        double oldScale = _fitScale * _zoom;
        double newZoom = Math.Clamp(_zoom * factor, MinZoom, MaxZoom);

        if (Math.Abs(newZoom - _zoom) < 1e-6 || oldScale <= 0)
        {
            return;
        }

        // Если холст ещё не отрисован, «вписанных» параметров нет:
        // масштаб всё равно меняется, но привязка к курсору выполняется только при наличии данных.
        if (!_hasFitInfo)
        {
            _zoom = newZoom;
            Invalidate();
            return;
        }

        double newScale = _fitScale * newZoom;

        // Координаты содержимого под курсором до и после изменения масштаба.
        double contentX = (anchor.X - (_fitOrigin.X + _pan.X)) / oldScale;
        double contentY = (anchor.Y - (_fitOrigin.Y + _pan.Y)) / oldScale;

        _zoom = newZoom;
        _pan = new PointF(
            (float)(anchor.X - contentX * newScale - _fitOrigin.X),
            (float)(anchor.Y - contentY * newScale - _fitOrigin.Y));

        ClampPan();
        Invalidate();
    }

    /// <summary>Сдвигает изображение на заданное число пикселей.</summary>
    public void PanBy(float deltaX, float deltaY)
    {
        _pan = new PointF(_pan.X + deltaX, _pan.Y + deltaY);
        ClampPan();
        Invalidate();
    }

    /// <summary>Подпись с подсказками по управлению видом.</summary>
    public static string NavigationHint => "колесо — масштаб · перетаскивание — сдвиг · двойной щелчок — вписать";

    public void Clear() => Tree = null;

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);

        if (_tree is not null)
        {
            ZoomBy(e.Delta > 0 ? 1.15 : 1 / 1.15, e.Location);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button == MouseButtons.Left && _tree is not null)
        {
            _isPanning = true;
            _panStart = e.Location;
            _panOrigin = _pan;
            Cursor = Cursors.SizeAll;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (!_isPanning)
        {
            return;
        }

        _pan = new PointF(
            _panOrigin.X + (e.X - _panStart.X),
            _panOrigin.Y + (e.Y - _panStart.Y));

        ClampPan();
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (_isPanning)
        {
            _isPanning = false;
            Cursor = Cursors.Default;
        }
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);

        if (_tree is not null)
        {
            FitToWindow();
        }
    }

    /// <summary>Не даёт изображению уйти за пределы холста целиком.</summary>
    private void ClampPan()
    {
        double scale = _fitScale * _zoom;
        double drawnWidth = _contentWidth * scale;
        double drawnHeight = _contentHeight * scale;

        float minX = (float)(Math.Min(_fitOrigin.X + drawnWidth, ClientSize.Width) - drawnWidth - _fitOrigin.X);
        float maxX = (float)(ClientSize.Width - _fitOrigin.X - PanMargin);

        float minY = (float)(Math.Min(_fitOrigin.Y + drawnHeight, ClientSize.Height) - drawnHeight - _fitOrigin.Y);
        float maxY = (float)(ClientSize.Height - _fitOrigin.Y - PanMargin);

        if (drawnWidth <= ClientSize.Width)
        {
            // Изображение уже помещается по ширине — центрируем, не давая уводить его в сторону.
            minX = Math.Min(minX, 0);
            maxX = Math.Max(maxX, 0);
        }

        if (drawnHeight <= ClientSize.Height)
        {
            minY = Math.Min(minY, 0);
            maxY = Math.Max(maxY, 0);
        }

        _pan = new PointF(
            Math.Clamp(_pan.X, Math.Min(minX, maxX), Math.Max(minX, maxX)),
            Math.Clamp(_pan.Y, Math.Min(minY, maxY), Math.Max(minY, maxY)));
    }

    public void Render(Graphics graphics, Size size)
    {
        graphics.Clear(BackColor);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        _headerHeight = 52;
        DrawHeader(graphics, size, _headerHeight);

        if (_tree is null)
        {
            DrawPlaceholder(graphics, size, _headerHeight);
            return;
        }

        if (_tree.Depth == 0 || _tree.NodeCount == 0)
        {
            return;
        }

        var positions = new Dictionary<RecursionNode, (double X, int Depth)>(ReferenceEqualityComparer.Instance);
        int leaves = 0;
        LayoutTree(_tree.Root, depth: 0, positions, ref leaves);

        _contentWidth = Math.Max(1, leaves) * (NodeWidth + ColumnGap);
        _contentHeight = _tree.Depth * (NodeHeight + LevelGap) + NodeHeight;

        float availableWidth = Math.Max(1, size.Width - 24);
        float availableHeight = Math.Max(1, size.Height - _headerHeight - 24);

        _fitScale = Math.Clamp(
            Math.Min(availableWidth / _contentWidth, availableHeight / _contentHeight),
            0.12,
            1.1);

        double scale = _fitScale * _zoom;
        double drawnWidth = _contentWidth * scale;

        _fitOrigin = new PointF(
            (float)((size.Width - drawnWidth) / 2),
            _headerHeight + 10);

        _hasFitInfo = true;

        double originX = _fitOrigin.X + _pan.X;
        double originY = _fitOrigin.Y + _pan.Y;

        var clip = new Rectangle(0, _headerHeight, size.Width, Math.Max(1, size.Height - _headerHeight));

        using var edgePen = new Pen(Theme.TreeEdge, (float)Math.Max(1, 1.4 * scale));
        DrawEdges(graphics, _tree.Root, positions, originX, originY, scale, edgePen);
        DrawNodes(graphics, _tree.Root, positions, originX, originY, scale, clip);
        DrawLegend(graphics, size);
    }

    protected override void OnPaint(PaintEventArgs e) => Render(e.Graphics, ClientSize);

    private void DrawHeader(Graphics graphics, Size size, int headerHeight)
    {
        string title = _tree?.Title ?? "Дерево рекурсии";
        TextRenderer.DrawText(
            graphics,
            title,
            Theme.BoldFont,
            new Point(12, 8),
            Theme.Text,
            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPadding);

        if (_tree is not null && IsZoomed)
        {
            string zoom = $"масштаб ×{_zoom:0.##}";
            Size zoomSize = TextRenderer.MeasureText(zoom, Theme.HintFont);
            TextRenderer.DrawText(
                graphics,
                zoom,
                Theme.HintFont,
                new Point(Math.Max(12, size.Width - zoomSize.Width - 12), 8),
                Theme.Accent);
        }

        string summary = _tree?.Summary ?? string.Empty;
        if (summary.Length == 0)
        {
            return;
        }

        TextRenderer.DrawText(
            graphics,
            summary,
            Theme.HintFont,
            new Rectangle(12, 26, size.Width - 24, headerHeight - 26),
            Theme.Muted,
            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
    }

    private void DrawPlaceholder(Graphics graphics, Size size, int headerHeight)
    {
        Size textSize = TextRenderer.MeasureText(PlaceholderText, Theme.BaseFont);

        TextRenderer.DrawText(
            graphics,
            PlaceholderText,
            Theme.BaseFont,
            new Point(
                Math.Max(12, (size.Width - textSize.Width) / 2),
                headerHeight + Math.Max(12, (size.Height - headerHeight - textSize.Height) / 2)),
            Theme.Muted);
    }

    /// <summary>Раскладка дерева: листья слева направо, внутренние узлы — по центру детей.</summary>
    private static void LayoutTree(
        RecursionNode node,
        int depth,
        Dictionary<RecursionNode, (double X, int Depth)> positions,
        ref int leaves)
    {
        double x;

        if (node.IsLeaf)
        {
            x = leaves;
            leaves++;
        }
        else
        {
            double sum = 0;
            foreach (RecursionNode child in node.Children)
            {
                LayoutTree(child, depth + 1, positions, ref leaves);
                sum += positions[child].X;
            }

            x = sum / node.Children.Count;
        }

        positions[node] = (x, depth);
    }

    private static void DrawEdges(
        Graphics graphics,
        RecursionNode node,
        Dictionary<RecursionNode, (double X, int Depth)> positions,
        double originX,
        double originY,
        double scale,
        Pen pen)
    {
        if (node.IsLeaf)
        {
            return;
        }

        (double parentX, int parentDepth) = positions[node];

        double parentCenterX = originX + (parentX + 0.5) * (NodeWidth + ColumnGap) * scale;
        double parentBottom = originY + ((parentDepth + 1) * NodeHeight + parentDepth * LevelGap) * scale;
        double busY = parentBottom + LevelGap * scale / 2;

        using var path = new GraphicsPath();
        path.StartFigure();
        path.AddLine((float)parentCenterX, (float)parentBottom, (float)parentCenterX, (float)busY);

        double minChildX = double.MaxValue;
        double maxChildX = double.MinValue;

        foreach (RecursionNode child in node.Children)
        {
            (double childX, int childDepth) = positions[child];

            double childCenterX = originX + (childX + 0.5) * (NodeWidth + ColumnGap) * scale;
            double childTop = originY + childDepth * (NodeHeight + LevelGap) * scale;

            path.AddLine((float)childCenterX, (float)busY, (float)childCenterX, (float)childTop);
            minChildX = Math.Min(minChildX, childCenterX);
            maxChildX = Math.Max(maxChildX, childCenterX);

            DrawEdges(graphics, child, positions, originX, originY, scale, pen);
        }

        if (minChildX < maxChildX)
        {
            path.AddLine((float)minChildX, (float)busY, (float)maxChildX, (float)busY);
        }

        graphics.DrawPath(pen, path);
    }

    private void DrawNodes(
        Graphics graphics,
        RecursionNode node,
        Dictionary<RecursionNode, (double X, int Depth)> positions,
        double originX,
        double originY,
        double scale,
        Rectangle clip)
    {
        (double x, int depth) = positions[node];

        float width = (float)(NodeWidth * scale);
        float height = (float)(NodeHeight * scale);
        float left = (float)(originX + x * (NodeWidth + ColumnGap) * scale);
        float top = (float)(originY + depth * (NodeHeight + LevelGap) * scale);
        var rectangle = new RectangleF(left, top, width, height);

        // За пределами окна узел не рисуется, но рекурсия продолжается:
        // так увеличенное изображение отрисовывается без лишней работы.
        if (rectangle.Bottom >= clip.Top && rectangle.Top <= clip.Bottom
            && rectangle.Right >= clip.Left && rectangle.Left <= clip.Right)
        {
            (Color fill, Color border) = ColorsFor(node);
            using var path = RoundedRect(rectangle, Math.Min(6f * (float)scale, height / 2f));

            using (var brush = new SolidBrush(fill))
            {
                graphics.FillPath(brush, path);
            }

            using (var pen = new Pen(border, node.Truncated ? 1.6f : 1f))
            {
                if (node.Truncated)
                {
                    pen.DashStyle = DashStyle.Dash;
                }

                graphics.DrawPath(pen, path);
            }

            DrawNodeText(graphics, node, rectangle, border);
        }

        foreach (RecursionNode child in node.Children)
        {
            DrawNodes(graphics, child, positions, originX, originY, scale, clip);
        }
    }

    private static void DrawNodeText(Graphics graphics, RecursionNode node, RectangleF rectangle, Color border)
    {
        if (rectangle.Width < 34f || rectangle.Height < 14f)
        {
            return;
        }

        using var textBrush = new SolidBrush(border);

        if (rectangle.Width >= 62f && rectangle.Height >= 26f && node.Detail is not null)
        {
            // Подпись вызова и пояснение в две строки.
            var titleArea = new RectangleF(rectangle.X + 2, rectangle.Y + 2, rectangle.Width - 4, rectangle.Height * 0.55f);
            var detailArea = new RectangleF(rectangle.X + 2, rectangle.Y + rectangle.Height * 0.5f, rectangle.Width - 4, rectangle.Height * 0.45f);

            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            graphics.DrawString(node.Title, Theme.NodeFont, textBrush, titleArea, format);

            using var detailBrush = new SolidBrush(Theme.Muted);
            graphics.DrawString(node.Detail, Theme.NodeDetailFont, detailBrush, detailArea, format);
            return;
        }

        using var singleFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        graphics.DrawString(node.Title, Theme.NodeFont, textBrush, rectangle, singleFormat);
    }

    private static (Color Fill, Color Border) ColorsFor(RecursionNode node) => node.Kind switch
    {
        RecursionNodeKind.BaseCase => (Theme.NodeBaseFill, Theme.NodeBaseBorder),
        RecursionNodeKind.Cached => (Theme.NodeCachedFill, Theme.NodeCachedBorder),
        _ => (Theme.NodeCallFill, Theme.NodeCallBorder),
    };

    private void DrawLegend(Graphics graphics, Size size)
    {
        if (_tree is null)
        {
            return;
        }

        var items = new List<(Color Fill, Color Border, string Text)>
        {
            (Theme.NodeCallFill, Theme.NodeCallBorder, "рекурсивный вызов"),
        };

        if (_tree.BaseCaseCount > 0)
        {
            items.Add((Theme.NodeBaseFill, Theme.NodeBaseBorder, "базовый случай"));
        }

        if (_tree.CachedCount > 0)
        {
            items.Add((Theme.NodeCachedFill, Theme.NodeCachedBorder, "взято из таблицы (мемоизация)"));
        }

        if (_tree.Truncated)
        {
            items.Add((Color.White, Theme.Muted, "пунктир — ветвь не раскрыта"));
        }

        int x = 12;
        int y = size.Height - 20;

        foreach ((Color fill, Color border, string text) in items)
        {
            using (var brush = new SolidBrush(fill))
            {
                graphics.FillRectangle(brush, x, y + 3, 12, 10);
            }

            using (var pen = new Pen(border))
            {
                graphics.DrawRectangle(pen, x, y + 3, 12, 10);
            }

            Size textSize = TextRenderer.MeasureText(text, Theme.NodeDetailFont);
            TextRenderer.DrawText(graphics, text, Theme.NodeDetailFont, new Point(x + 16, y), Theme.Muted);
            x += 16 + textSize.Width + 16;
        }

        // Подсказка по управлению видом выводится справа от легенды.
        Size hintSize = TextRenderer.MeasureText(NavigationHint, Theme.NodeDetailFont);
        if (x + hintSize.Width + 12 < size.Width)
        {
            TextRenderer.DrawText(
                graphics,
                NavigationHint,
                Theme.NodeDetailFont,
                new Point(size.Width - hintSize.Width - 12, y),
                Theme.Muted);
        }
    }

    private static GraphicsPath RoundedRect(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        float diameter = Math.Max(1f, radius * 2f);

        if (rect.Width <= diameter || rect.Height <= diameter)
        {
            path.AddRectangle(rect);
            return path;
        }

        path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

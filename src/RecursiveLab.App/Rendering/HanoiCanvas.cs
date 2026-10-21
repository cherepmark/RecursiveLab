using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using RecursiveLab.Core.Hanoi;

namespace RecursiveLab.App.Rendering;

/// <summary>
/// Холст для визуализации Ханойской башни.
///
/// Холст только ЧИТАЕТ модель <see cref="HanoiState"/>: любое перемещение сначала
/// выполняется в модели, а затем холст перерисовывается. Поэтому картинка всегда
/// соответствует фактическому состоянию алгоритма.
/// </summary>
internal sealed class HanoiCanvas : Control, IRenderable
{
    private HanoiState? _state;
    private HanoiMove? _lastMove;
    private HanoiMove? _inFlightMove;
    private double _progress;

    public HanoiCanvas()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw,
            true);

        BackColor = Theme.HanoiCanvasBackground;
        DoubleBuffered = true;
        Dock = DockStyle.Fill;
    }

    /// <summary>Текущее отображаемое состояние головоломки.</summary>
    public HanoiState? State => _state;

    /// <summary>Последний выполненный ход (для подсветки).</summary>
    public HanoiMove? LastMove => _lastMove;

    /// <summary>Ход, который сейчас проигрывается анимацией, или null.</summary>
    public HanoiMove? InFlightMove => _inFlightMove;

    /// <summary>Устанавливает состояние и последний ход, после чего перерисовывает холст.</summary>
    public void Update(HanoiState? state, HanoiMove? lastMove)
    {
        _state = state;
        _lastMove = lastMove;
        Invalidate();
    }

    /// <summary>Начинает анимацию переноса диска (модель ещё не изменена).</summary>
    public void BeginFlight(HanoiMove move)
    {
        _inFlightMove = move;
        _progress = 0;
        Invalidate();
    }

    /// <summary>Обновляет положение летящего диска: 0 — старт, 1 — финиш.</summary>
    public void UpdateFlight(double progress)
    {
        _progress = Math.Clamp(progress, 0, 1);
        Invalidate();
    }

    /// <summary>Прекращает анимацию переноса.</summary>
    public void EndFlight()
    {
        _inFlightMove = null;
        _progress = 0;
        Invalidate();
    }

    public void Render(Graphics graphics, Size size)
    {
        graphics.Clear(BackColor);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        if (_state is null)
        {
            DrawPlaceholder(graphics, size);
            return;
        }

        CanvasLayout layout = ComputeLayout(size, _state.DiskCount);

        DrawBase(graphics, layout);
        DrawPegs(graphics, layout);
        DrawPegLabels(graphics, layout);
        DrawDisks(graphics, layout);
        DrawFlyingDisk(graphics, layout);
        DrawMoveCaption(graphics, size);
    }

    protected override void OnPaint(PaintEventArgs e) => Render(e.Graphics, ClientSize);

    // -----------------------------------------------------------------------
    // Разметка
    // -----------------------------------------------------------------------

    /// <summary>Геометрическая разметка холста для текущего количества дисков.</summary>
    private readonly record struct CanvasLayout(
        float BaseY,
        float PegHeight,
        float PegSpacing,
        float DiskHeight,
        float DiskGap,
        float MinDiskWidth,
        float MaxDiskWidth,
        float[] PegX)
    {
        public float DiskWidth(int diskSize, int diskCount)
        {
            double t = diskCount <= 1 ? 1.0 : (diskSize - 1) / (double)(diskCount - 1);
            return (float)(MinDiskWidth + (MaxDiskWidth - MinDiskWidth) * t);
        }

        /// <summary>Координата верхнего края диска с индексом снизу.</summary>
        public float DiskTop(int stackIndex) => BaseY - (stackIndex + 1) * DiskHeight - stackIndex * DiskGap;

        public float DiskCenterY(int stackIndex) => DiskTop(stackIndex) + DiskHeight / 2f;
    }

    private static CanvasLayout ComputeLayout(Size size, int diskCount)
    {
        float width = Math.Max(size.Width, 1);
        float height = Math.Max(size.Height, 1);

        float baseY = height * 0.84f;
        float pegHeight = height * 0.60f;
        float pegSpacing = width / HanoiPegs.Count;

        float[] pegX =
        [
            pegSpacing * 0.5f,
            pegSpacing * 1.5f,
            pegSpacing * 2.5f,
        ];

        float available = pegHeight - 8f;
        float gap = Math.Min(3f, available / Math.Max(1, diskCount) * 0.14f);
        float diskHeight = (available - gap * (diskCount - 1)) / Math.Max(1, diskCount);
        diskHeight = Math.Clamp(diskHeight, 4f, height * 0.075f);

        float minDiskWidth = pegSpacing * 0.24f;
        float maxDiskWidth = pegSpacing * 0.86f;

        return new CanvasLayout(baseY, pegHeight, pegSpacing, diskHeight, gap, minDiskWidth, maxDiskWidth, pegX);
    }

    // -----------------------------------------------------------------------
    // Отрисовка
    // -----------------------------------------------------------------------

    private static void DrawBase(Graphics graphics, CanvasLayout layout)
    {
        float left = layout.PegSpacing * 0.15f;
        float width = layout.PegSpacing * (HanoiPegs.Count - 0.3f);

        using var brush = new SolidBrush(Theme.HanoiBase);
        graphics.FillRectangle(brush, left, layout.BaseY, width, 10f);
    }

    private void DrawPegs(Graphics graphics, CanvasLayout layout)
    {
        using var pegBrush = new SolidBrush(Theme.HanoiPeg);

        for (int peg = 0; peg < HanoiPegs.Count; peg++)
        {
            // Подсветка стержня-назначения последнего хода.
            if (_lastMove is { } move && move.To == peg)
            {
                using var glow = new SolidBrush(Color.FromArgb(38, Theme.HanoiHighlight));
                var glowRect = new RectangleF(
                    layout.PegX[peg] - layout.PegSpacing * 0.44f,
                    layout.BaseY - layout.PegHeight - 12f,
                    layout.PegSpacing * 0.88f,
                    layout.PegHeight + 12f);
                graphics.FillRectangle(glow, glowRect);
            }

            var pegRect = new RectangleF(
                layout.PegX[peg] - 4f,
                layout.BaseY - layout.PegHeight,
                8f,
                layout.PegHeight);

            using var path = RoundedRect(pegRect, 4f);
            graphics.FillPath(pegBrush, path);
        }
    }

    private static void DrawPegLabels(Graphics graphics, CanvasLayout layout)
    {
        using var brush = new SolidBrush(Theme.Muted);

        for (int peg = 0; peg < HanoiPegs.Count; peg++)
        {
            string name = HanoiPegs.Name(peg);
            Size textSize = TextRenderer.MeasureText(name, Theme.BoldFont);
            graphics.DrawString(
                name,
                Theme.BoldFont,
                brush,
                layout.PegX[peg] - textSize.Width / 2f,
                layout.BaseY + 14f);
        }
    }

    private void DrawDisks(Graphics graphics, CanvasLayout layout)
    {
        if (_state is null)
        {
            return;
        }

        for (int peg = 0; peg < HanoiPegs.Count; peg++)
        {
            IReadOnlyList<int> disks = _state.PegDisks(peg);
            for (int index = 0; index < disks.Count; index++)
            {
                // Диск «в полёте» рисуется отдельно, поэтому с исходного стержня
                // его временно убираем.
                bool isFlying = _inFlightMove is { } flight
                                && flight.From == peg
                                && index == disks.Count - 1;

                if (isFlying)
                {
                    continue;
                }

                DrawDisk(graphics, layout, disks[index], _state.DiskCount, layout.PegX[peg], layout.DiskTop(index), false);
            }
        }
    }

    private void DrawFlyingDisk(Graphics graphics, CanvasLayout layout)
    {
        if (_state is null || _inFlightMove is not { } move)
        {
            return;
        }

        int sourceHeight = _state.Height(move.From);
        int targetHeight = _state.Height(move.To);

        float startY = layout.DiskTop(Math.Max(0, sourceHeight - 1));
        float endY = layout.DiskTop(targetHeight);
        float fromX = layout.PegX[move.From];
        float toX = layout.PegX[move.To];
        float cruiseY = layout.BaseY - layout.PegHeight - layout.DiskHeight * 0.6f;

        // Траектория из трёх фаз: подъём, перенос по горизонтали, опускание.
        float x;
        float y;

        const double riseEnd = 0.35;
        const double travelEnd = 0.65;

        if (_progress < riseEnd)
        {
            double t = _progress / riseEnd;
            x = fromX;
            y = (float)(startY + (cruiseY - startY) * t);
        }
        else if (_progress < travelEnd)
        {
            double t = (_progress - riseEnd) / (travelEnd - riseEnd);
            x = (float)(fromX + (toX - fromX) * t);
            y = cruiseY;
        }
        else
        {
            double t = (_progress - travelEnd) / (1 - travelEnd);
            x = toX;
            y = (float)(cruiseY + (endY - cruiseY) * t);
        }

        DrawDisk(graphics, layout, move.Disk, _state.DiskCount, x, y, highlighted: true);
    }

    private static void DrawDisk(
        Graphics graphics,
        CanvasLayout layout,
        int diskSize,
        int diskCount,
        float centerX,
        float top,
        bool highlighted)
    {
        float width = layout.DiskWidth(diskSize, diskCount);
        var rect = new RectangleF(centerX - width / 2f, top, width, layout.DiskHeight);

        Color color = Theme.DiskColor(diskSize, diskCount);
        using var brush = new LinearGradientBrush(
            rect,
            ControlPaint.Light(color, 0.18f),
            ControlPaint.Dark(color, 0.12f),
            LinearGradientMode.Vertical);

        using var path = RoundedRect(rect, Math.Min(6f, layout.DiskHeight / 2f));
        graphics.FillPath(brush, path);

        using var pen = new Pen(highlighted ? Theme.HanoiHighlight : ControlPaint.Dark(color, 0.35f), highlighted ? 2f : 1f);
        graphics.DrawPath(pen, path);

        // Номер диска печатаем только там, где он действительно читается.
        if (layout.DiskHeight >= 11f && width >= 26f)
        {
            string label = diskSize.ToString();
            Size textSize = TextRenderer.MeasureText(label, Theme.HintFont);
            using var textBrush = new SolidBrush(Color.White);
            graphics.DrawString(
                label,
                Theme.HintFont,
                textBrush,
                centerX - textSize.Width / 2f,
                top + (layout.DiskHeight - textSize.Height) / 2f);
        }
    }

    private void DrawMoveCaption(Graphics graphics, Size size)
    {
        string caption;
        if (_inFlightMove is { } move)
        {
            caption = $"Ход {HanoiPegs.Name(move.From)} → {HanoiPegs.Name(move.To)} (диск {move.Disk})";
        }
        else if (_lastMove is { } last)
        {
            caption = $"Последний ход: {last}";
        }
        else
        {
            caption = "Нажмите «Решить», чтобы построить последовательность ходов";
        }

        TextRenderer.DrawText(
            graphics,
            caption,
            Theme.HintFont,
            new Point(12, 10),
            Theme.Muted);

        TextRenderer.DrawText(
            graphics,
            $"Минимум ходов: 2^{_state?.DiskCount ?? 0} − 1 = {(1L << (_state?.DiskCount ?? 0)) - 1}",
            Theme.HintFont,
            new Point(12, 30),
            Theme.Muted);
    }

    private void DrawPlaceholder(Graphics graphics, Size size)
    {
        const string hint = "Выберите количество дисков и способ решения";
        Size textSize = TextRenderer.MeasureText(hint, Theme.BaseFont);
        TextRenderer.DrawText(
            graphics,
            hint,
            Theme.BaseFont,
            new Point((size.Width - textSize.Width) / 2, (size.Height - textSize.Height) / 2),
            Theme.Muted);
    }

    /// <summary>Прямоугольник со скруглёнными углами.</summary>
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

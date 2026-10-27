using System.Diagnostics;
using System.Windows.Forms;
using RecursiveLab.App.Rendering;
using RecursiveLab.Core.Fractals;
using RecursiveLab.Core.Geometry;

namespace RecursiveLab.App.Views.Individual;

/// <summary>
/// Страница индивидуального алгоритма «Рекурсивное построение кривой Гильберта».
///
/// Демонстрирует фрактальную рекурсию с двумя параметрами: порядком (глубиной)
/// и ориентацией каждой из четырёх копий кривой.
/// </summary>
internal sealed class HilbertCurveView : UserControl
{
    private readonly NumericUpDown _orderBox = new();
    private readonly ComboBox _orientationBox = new();
    private readonly FractalCanvas _canvas = new() { UseGradientStroke = true };
    private readonly Label _metricsLabel = Theme.CreateLabel(string.Empty, font: Theme.BoldFont);
    private readonly Label _checkLabel = Theme.CreateLabel(string.Empty);
    private readonly Label _statusLabel = Theme.CreateLabel(string.Empty);
    private readonly Label _descriptionLabel = Theme.CreateLabel(string.Empty, muted: true, Theme.HintFont);
    private readonly Button _buildButton = Theme.CreateButton("Построить", primary: true);
    private readonly Button _clearButton = Theme.CreateButton("Очистить");
    private readonly HilbertCurveFractal _fractal = new();

    public HilbertCurveView()
    {
        Dock = DockStyle.Fill;
        BackColor = Theme.Background;
        Font = Theme.BaseFont;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Theme.Background,
            Padding = new Padding(12, 10, 12, 12),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(BuildToolbar(), 0, 0);

        var canvasHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.FractalCanvasBackground,
            Padding = new Padding(1),
            Margin = new Padding(0, 8, 0, 8),
        };
        canvasHost.Controls.Add(_canvas);
        root.Controls.Add(canvasHost, 0, 1);
        root.Controls.Add(BuildInfoPanel(), 0, 2);

        Controls.Add(root);

        ConfigureInputs();
        BuildCurve();
    }

    internal FractalCanvas Canvas => _canvas;

    internal NumericUpDown OrderInput => _orderBox;

    internal ComboBox OrientationSelector => _orientationBox;

    internal string StatusText => _statusLabel.Text;

    internal string MetricsText => _metricsLabel.Text;

    internal string CheckText => _checkLabel.Text;

    internal void SelectOrientation(int orientation) => _orientationBox.SelectedIndex = orientation;

    private Control BuildToolbar()
    {
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            BackColor = Theme.Surface,
            Padding = new Padding(12, 8, 12, 8),
            Margin = new Padding(0),
        };

        toolbar.Controls.Add(Theme.CreateCaption("Порядок кривой (глубина рекурсии):"));
        toolbar.Controls.Add(_orderBox);
        toolbar.Controls.Add(Theme.CreateLabel(
            $"допустимо: {_fractal.MinDepth}–{_fractal.MaxDepth}",
            muted: true,
            Theme.HintFont));

        toolbar.Controls.Add(Theme.CreateCaption("Ориентация:"));
        toolbar.Controls.Add(_orientationBox);
        toolbar.Controls.Add(Theme.CreateSeparator());

        _buildButton.Click += (_, _) => BuildCurve();
        _clearButton.Click += (_, _) => ClearCurve();
        _clearButton.Margin = new Padding(0, 2, 8, 0);

        toolbar.Controls.Add(_buildButton);
        toolbar.Controls.Add(_clearButton);
        return toolbar;
    }

    private Control BuildInfoPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Theme.Surface,
            Padding = new Padding(12, 8, 12, 10),
            Margin = new Padding(0),
        };

        _metricsLabel.Margin = new Padding(0, 0, 0, 2);
        _checkLabel.Margin = new Padding(0, 0, 0, 2);
        _statusLabel.Margin = new Padding(0, 0, 0, 4);
        _descriptionLabel.AutoSize = true;
        _descriptionLabel.MaximumSize = new Size(1100, 0);
        _descriptionLabel.Margin = new Padding(0);

        panel.Controls.Add(_metricsLabel, 0, 0);
        panel.Controls.Add(_checkLabel, 0, 1);
        panel.Controls.Add(_statusLabel, 0, 2);
        panel.Controls.Add(_descriptionLabel, 0, 3);
        return panel;
    }

    private void ConfigureInputs()
    {
        _orderBox.Width = 60;
        _orderBox.Minimum = _fractal.MinDepth;
        _orderBox.Maximum = _fractal.MaxDepth;
        _orderBox.Value = 3;
        _orderBox.Margin = new Padding(0, 2, 8, 0);
        _orderBox.TextAlign = HorizontalAlignment.Center;

        _orientationBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _orientationBox.Width = 260;
        _orientationBox.Margin = new Padding(0, 2, 8, 0);

        for (int orientation = 0; orientation < HilbertCurve.OrientationCount; orientation++)
        {
            _orientationBox.Items.Add(HilbertCurveFractal.DescribeOrientation(orientation));
        }

        _orientationBox.SelectedIndex = 0;
        _orientationBox.SelectedIndexChanged += (_, _) =>
        {
            if (_canvas.Geometry is not null)
            {
                BuildCurve();
            }
        };

        _descriptionLabel.Text = _fractal.AlgorithmDescription;
    }

    /// <summary>Строит кривую заданного порядка и ориентации.</summary>
    internal bool BuildCurve(int? orderOverride = null)
    {
        int order = orderOverride ?? (int)_orderBox.Value;
        int orientation = Math.Max(0, _orientationBox.SelectedIndex);

        try
        {
            var stopwatch = Stopwatch.StartNew();
            FractalGeometry geometry = _fractal.BuildGeometry(order, orientation);
            stopwatch.Stop();

            _canvas.Geometry = geometry;

            int points = HilbertCurve.PointCount(order);
            _metricsLabel.Text =
                $"Порядок {order}: точек {points:N0} · отрезков {geometry.Segments.Count:N0} = 4^{order} − 1 · "
                + $"клеток {1 << order}×{1 << order} · время {stopwatch.Elapsed.TotalMilliseconds:0.#} мс";

            // Независимая сверка с классическим алгоритмом «индекс → точка» (d2xy).
            IReadOnlyList<PointD> recursive = HilbertCurve.BuildPoints(order, orientation: 0);
            IReadOnlyList<PointD> reference = HilbertCurve.ReferencePoints(order);
            bool matches = recursive.Count == reference.Count;

            for (int i = 0; matches && i < recursive.Count; i++)
            {
                matches = recursive[i] == reference[i];
            }

            _checkLabel.Text = matches
                ? "Сверка с классическим алгоритмом d2xy: последовательность точек совпадает."
                : "Сверка с классическим алгоритмом d2xy: обнаружено расхождение!";
            _checkLabel.ForeColor = matches ? Theme.Success : Theme.Danger;

            SetStatus(
                $"Готово: порядок {order} ({HilbertCurveFractal.DescribeOrientation(orientation)}).",
                StatusKind.Success);
            return true;
        }
        catch (FractalParameterException exception)
        {
            _canvas.Clear();
            _metricsLabel.Text = string.Empty;
            _checkLabel.Text = string.Empty;
            SetStatus(exception.Message, StatusKind.Error);
            return false;
        }
        catch (Exception exception)
        {
            _canvas.Clear();
            _metricsLabel.Text = string.Empty;
            _checkLabel.Text = string.Empty;
            SetStatus($"Не удалось построить кривую: {exception.Message}", StatusKind.Error);
            return false;
        }
    }

    internal void ClearCurve()
    {
        _canvas.Clear();
        _metricsLabel.Text = string.Empty;
        _checkLabel.Text = string.Empty;
        SetStatus("Холст очищен. Нажмите «Построить», чтобы построить кривую заново.", StatusKind.Hint);
    }

    private enum StatusKind
    {
        Hint,
        Success,
        Error,
    }

    private void SetStatus(string text, StatusKind kind)
    {
        _statusLabel.Text = text;
        _statusLabel.ForeColor = kind switch
        {
            StatusKind.Success => Theme.Success,
            StatusKind.Error => Theme.Danger,
            _ => Theme.Muted,
        };
    }
}

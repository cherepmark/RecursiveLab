using System.Diagnostics;
using System.Windows.Forms;
using RecursiveLab.App.Rendering;
using RecursiveLab.Core.Algorithms;

namespace RecursiveLab.App.Views.Individual;

/// <summary>
/// Страница индивидуального алгоритма «Штрассен».
///
/// Показывает результат классического умножения, результат алгоритма Штрассена
/// и матрицу разности (она обязана быть нулевой). Отдельно выводятся числа
/// скалярных умножений: n³ у классического алгоритма против 7^k у Штрассена.
/// </summary>
internal sealed class StrassenView : UserControl
{
    private static readonly int[] Thresholds = [1, 2, 4, 8, 16, 32];

    private readonly NumericUpDown _sizeBox = new();
    private readonly ComboBox _thresholdBox = new();
    private readonly Button _randomButton = Theme.CreateButton("Случайные матрицы");
    private readonly Button _multiplyButton = Theme.CreateButton("Перемножить", primary: true);
    private readonly MatrixCanvas _classicCanvas = new();
    private readonly MatrixCanvas _strassenCanvas = new();
    private readonly MatrixCanvas _differenceCanvas = new() { HighlightZeroMatrix = true };
    private readonly Label _infoLabel = Theme.CreateLabel(string.Empty, font: Theme.BoldFont);
    private readonly Label _statusLabel = Theme.CreateLabel(string.Empty);
    private readonly Label _descriptionLabel = Theme.CreateLabel(string.Empty, muted: true, Theme.HintFont);

    private double[,] _left = Matrix.RandomSquare(8, seed: 1);
    private double[,] _right = Matrix.RandomSquare(8, seed: 2);
    private int _seed = 2;

    public StrassenView()
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
        root.Controls.Add(BuildContentPanel(), 0, 1);
        root.Controls.Add(BuildStatusPanel(), 0, 2);

        Controls.Add(root);

        ConfigureInputs();
        Multiply();
    }

    internal NumericUpDown SizeInput => _sizeBox;

    internal ComboBox ThresholdSelector => _thresholdBox;

    internal MatrixCanvas ClassicCanvas => _classicCanvas;

    internal MatrixCanvas StrassenCanvas => _strassenCanvas;

    internal MatrixCanvas DifferenceCanvas => _differenceCanvas;

    internal string InfoText => _infoLabel.Text;

    internal string StatusText => _statusLabel.Text;

    internal double[,] LeftMatrix => _left;

    internal double[,] RightMatrix => _right;

    internal int Threshold => Thresholds[Math.Max(0, _thresholdBox.SelectedIndex)];

    internal double[,] ClassicProduct { get; private set; } = new double[0, 0];

    internal double[,] StrassenProduct { get; private set; } = new double[0, 0];

    internal long StrassenScalarMultiplications { get; private set; }

    internal int StrassenRecursiveCalls { get; private set; }

    /// <summary>Все элементы матрицы разности нулевые — значит результаты совпали.</summary>
    internal bool DifferenceCanvasAllZero
        => _differenceCanvas.Matrix is { } matrix && matrix.Cast<double>().All(value => Math.Abs(value) < 1e-9);

    /// <summary>Создаёт новые случайные матрицы заданного размера.</summary>
    internal void FillRandomMatrices()
    {
        _seed++;
        int size = (int)_sizeBox.Value;

        _left = Matrix.RandomSquare(size, seed: _seed);
        _right = Matrix.RandomSquare(size, seed: _seed + 1000);
        Multiply();
    }

    /// <summary>Перемножает матрицы классическим алгоритмом и алгоритмом Штрассена.</summary>
    internal bool Multiply()
    {
        int threshold = Threshold;

        try
        {
            var classicWatch = Stopwatch.StartNew();
            MatrixMultiplicationResult classic = StrassenMultiplication.MultiplyClassic(_left, _right);
            classicWatch.Stop();

            var strassenWatch = Stopwatch.StartNew();
            MatrixMultiplicationResult strassen = StrassenMultiplication.MultiplyStrassen(_left, _right, threshold);
            strassenWatch.Stop();

            ClassicProduct = classic.Product;
            StrassenProduct = strassen.Product;
            StrassenScalarMultiplications = strassen.Stats.ScalarMultiplications;
            StrassenRecursiveCalls = strassen.Stats.RecursiveCalls;

            double[,] difference = Subtract(classic.Product, strassen.Product);

            _classicCanvas.Matrix = classic.Product;
            _strassenCanvas.Matrix = strassen.Product;
            _differenceCanvas.Matrix = difference;

            bool equal = Matrix.AreEqual(classic.Product, strassen.Product);
            int size = (int)_sizeBox.Value;
            long classicTheoretical = (long)size * size * size;

            _infoLabel.Text =
                $"Размер {size}×{size}, порог базового случая {threshold} · "
                + $"классический: {classicWatch.Elapsed.TotalMilliseconds:0.###} мс, скалярных умножений {classicTheoretical:N0} = n³ · "
                + $"Штрассен: {strassenWatch.Elapsed.TotalMilliseconds:0.###} мс, скалярных умножений {strassen.Stats.ScalarMultiplications:N0}, "
                + $"рекурсивных вызовов {strassen.Stats.RecursiveCalls:N0}, глубина {strassen.Stats.MaxDepthReached} · "
                + $"результаты {(equal ? "совпадают" : "РАЗЛИЧАЮТСЯ")}";
            _infoLabel.ForeColor = equal ? Theme.Text : Theme.Danger;

            SetStatus(
                equal
                    ? "Готово: 7 рекурсивных умножений вместо 8 дали тот же результат. Матрица разности нулевая."
                    : "Внимание: результаты умножения различаются!",
                equal ? StatusKind.Success : StatusKind.Error);

            return true;
        }
        catch (ArgumentOutOfRangeException exception)
        {
            _classicCanvas.Clear();
            _strassenCanvas.Clear();
            _differenceCanvas.Clear();
            _infoLabel.Text = string.Empty;
            SetStatus(exception.Message, StatusKind.Error);
            return false;
        }
        catch (Exception exception)
        {
            _classicCanvas.Clear();
            _strassenCanvas.Clear();
            _differenceCanvas.Clear();
            _infoLabel.Text = string.Empty;
            SetStatus($"Не удалось выполнить умножение: {exception.Message}", StatusKind.Error);
            return false;
        }
    }

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

        toolbar.Controls.Add(Theme.CreateCaption("Размер матриц:"));
        _sizeBox.Width = 60;
        _sizeBox.Margin = new Padding(0, 2, 8, 0);
        _sizeBox.TextAlign = HorizontalAlignment.Center;
        _sizeBox.ValueChanged += (_, _) => FillRandomMatrices();
        toolbar.Controls.Add(_sizeBox);

        toolbar.Controls.Add(Theme.CreateCaption("Порог базового случая:"));
        _thresholdBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _thresholdBox.Width = 70;
        _thresholdBox.Margin = new Padding(0, 2, 8, 0);
        foreach (int value in Thresholds)
        {
            _thresholdBox.Items.Add(value);
        }

        _thresholdBox.SelectedIndexChanged += (_, _) =>
        {
            if (_classicCanvas.Matrix is not null)
            {
                Multiply();
            }
        };
        toolbar.Controls.Add(_thresholdBox);
        toolbar.Controls.Add(Theme.CreateLabel(
            "(меньше порог — глубже рекурсия)",
            muted: true,
            Theme.HintFont));
        toolbar.Controls.Add(Theme.CreateSeparator());

        _randomButton.Click += (_, _) => FillRandomMatrices();
        _multiplyButton.Click += (_, _) => Multiply();
        _randomButton.Margin = new Padding(0, 2, 8, 0);

        toolbar.Controls.Add(_multiplyButton);
        toolbar.Controls.Add(_randomButton);
        return toolbar;
    }

    private Control BuildContentPanel()
    {
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Theme.Background,
            Margin = new Padding(0),
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));

        _classicCanvas.Caption = "Классический алгоритм O(n³)";
        _strassenCanvas.Caption = "Алгоритм Штрассена (7 рекурсивных умножений)";
        _differenceCanvas.Caption = "Разность результатов (должна быть нулевой)";

        content.Controls.Add(Wrap(_classicCanvas), 0, 0);
        content.Controls.Add(Wrap(_strassenCanvas), 1, 0);
        content.Controls.Add(Wrap(_differenceCanvas, last: true), 2, 0);
        return content;
    }

    private static Control Wrap(Control child, bool last = false)
    {
        var host = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Surface,
            Padding = new Padding(1),
            Margin = new Padding(0, 0, last ? 0 : 8, 0),
        };
        host.Controls.Add(child);
        return host;
    }

    private Control BuildStatusPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Theme.Surface,
            Padding = new Padding(12, 8, 12, 10),
            Margin = new Padding(0, 8, 0, 0),
        };

        _infoLabel.Margin = new Padding(0, 0, 0, 2);
        _statusLabel.Margin = new Padding(0, 0, 0, 4);
        _descriptionLabel.AutoSize = true;
        _descriptionLabel.MaximumSize = new Size(1100, 0);
        _descriptionLabel.Margin = new Padding(0);

        panel.Controls.Add(_infoLabel, 0, 0);
        panel.Controls.Add(_statusLabel, 0, 1);
        panel.Controls.Add(_descriptionLabel, 0, 2);
        return panel;
    }

    private void ConfigureInputs()
    {
        _sizeBox.Minimum = 2;
        _sizeBox.Maximum = 32;
        _sizeBox.Value = 8;

        _thresholdBox.SelectedIndex = 1;   // порог 2

        _descriptionLabel.Text =
            "Классическое умножение требует 8 рекурсивных умножений половинного размера, "
            + "Штрассен обходится семью (M1…M7), поэтому T(n) = 7·T(n/2) + O(n²) и сложность равна O(n^2.807). "
            + "Базовый случай рекурсии — блок размером не больше выбранного порога: он перемножается обычным тройным циклом. "
            + "Матрицы дополняются нулями до степени двойки, поэтому поддерживаются и нечётные размеры.";
    }

    private static double[,] Subtract(double[,] left, double[,] right)
    {
        var result = new double[left.GetLength(0), left.GetLength(1)];
        for (int i = 0; i < left.GetLength(0); i++)
        {
            for (int j = 0; j < left.GetLength(1); j++)
            {
                result[i, j] = left[i, j] - right[i, j];
            }
        }

        return result;
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

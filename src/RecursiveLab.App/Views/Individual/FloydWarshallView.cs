using System.Diagnostics;
using System.Windows.Forms;
using RecursiveLab.App.Rendering;
using RecursiveLab.Core.ShortestPaths;

namespace RecursiveLab.App.Views.Individual;

/// <summary>
/// Страница индивидуального алгоритма «Флойд — Уоршелл».
///
/// Показывает матрицу смежности графа, матрицу кратчайших расстояний и сами пути.
/// Переключатель режимов демонстрирует переход от экспоненциальной троичной рекурсии
/// (3^k вызовов на пару вершин) к рекурсии с мемоизацией и к классическим трём циклам.
/// </summary>
internal sealed class FloydWarshallView : UserControl
{
    private readonly NumericUpDown _verticesBox = new();
    private readonly RadioButton _naiveRadio = new();
    private readonly RadioButton _memoizedRadio = new();
    private readonly RadioButton _iterativeRadio = new();
    private readonly Button _demoButton = Theme.CreateButton("Демо-граф");
    private readonly Button _randomButton = Theme.CreateButton("Случайный граф");
    private readonly Button _solveButton = Theme.CreateButton("Вычислить", primary: true);
    private readonly Button _compareButton = Theme.CreateButton("Сравнить все режимы");
    private readonly MatrixCanvas _adjacencyCanvas = new();
    private readonly MatrixCanvas _distanceCanvas = new();
    private readonly ListBox _pathsList = new();
    private readonly Label _infoLabel = Theme.CreateLabel(string.Empty, font: Theme.BoldFont);
    private readonly Label _statusLabel = Theme.CreateLabel(string.Empty);
    private readonly Label _comparisonLabel = Theme.CreateLabel(string.Empty, font: Theme.MonoFont);
    private readonly Label _descriptionLabel = Theme.CreateLabel(string.Empty, muted: true, Theme.HintFont);

    private WeightedGraph _graph = WeightedGraph.CreateDemo();
    private int _randomSeed = 1;

    public FloydWarshallView()
    {
        Dock = DockStyle.Fill;
        BackColor = Theme.Background;
        Font = Theme.BaseFont;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Theme.Background,
            Padding = new Padding(12, 10, 12, 12),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(BuildSettingsPanel(), 0, 0);
        root.Controls.Add(BuildActionsPanel(), 0, 1);
        root.Controls.Add(BuildContentPanel(), 0, 2);
        root.Controls.Add(BuildStatusPanel(), 0, 3);

        Controls.Add(root);

        ConfigureInputs();
        LoadGraph(_graph);
        SolveSelected();
    }

    internal MatrixCanvas DistanceCanvas => _distanceCanvas;

    internal MatrixCanvas AdjacencyCanvas => _adjacencyCanvas;

    internal ListBox PathsList => _pathsList;

    internal NumericUpDown VerticesInput => _verticesBox;

    internal string InfoText => _infoLabel.Text;

    internal string StatusText => _statusLabel.Text;

    internal string ComparisonText => _comparisonLabel.Text;

    internal void SelectMode(FloydWarshallMode mode)
    {
        switch (mode)
        {
            case FloydWarshallMode.NaiveRecursive:
                _naiveRadio.Checked = true;
                break;
            case FloydWarshallMode.MemoizedRecursive:
                _memoizedRadio.Checked = true;
                break;
            default:
                _iterativeRadio.Checked = true;
                break;
        }
    }

    internal FloydWarshallMode SelectedMode
    {
        get
        {
            if (_naiveRadio.Checked)
            {
                return FloydWarshallMode.NaiveRecursive;
            }

            return _memoizedRadio.Checked ? FloydWarshallMode.MemoizedRecursive : FloydWarshallMode.Iterative;
        }
    }

    /// <summary>Загружает демонстрационный граф.</summary>
    internal void LoadDemoGraph()
    {
        _verticesBox.Value = WeightedGraph.CreateDemo().VertexCount;
        LoadGraph(WeightedGraph.CreateDemo());
    }

    /// <summary>Строит случайный граф с текущим числом вершин.</summary>
    internal void LoadRandomGraph()
    {
        _randomSeed++;
        LoadGraph(WeightedGraph.CreateRandom((int)_verticesBox.Value, _randomSeed));
    }

    /// <summary>Вычисляет кратчайшие пути выбранным способом.</summary>
    internal bool SolveSelected()
    {
        FloydWarshallMode mode = SelectedMode;

        try
        {
            var stopwatch = Stopwatch.StartNew();
            FloydWarshallResult result = FloydWarshall.Solve(_graph, mode);
            stopwatch.Stop();

            // Пути восстанавливаются по итеративной версии — она же служит эталоном.
            FloydWarshallResult reference = mode == FloydWarshallMode.Iterative
                ? result
                : FloydWarshall.Solve(_graph, FloydWarshallMode.Iterative);

            _distanceCanvas.Matrix = result.Distances;
            FillPaths(reference);

            bool matches = result.Matches(reference);

            _infoLabel.Text =
                $"{_graph.VertexCount} вершин · {_graph.EdgeCount} рёбер · способ: {result.ModeName} · "
                + $"рекурсивных вызовов: {result.RecursiveCallCount:N0} · время: {stopwatch.Elapsed.TotalMilliseconds:0.##} мс · "
                + $"совпадение с итеративным алгоритмом: {(matches ? "да" : "НЕТ")}";
            _infoLabel.ForeColor = matches ? Theme.Text : Theme.Danger;

            SetStatus(
                matches
                    ? $"Готово. Режим «{result.ModeName}» дал ту же матрицу, что и классические три цикла."
                    : "Внимание: матрицы расстояний различаются!",
                matches ? StatusKind.Success : StatusKind.Error);

            UpdateDescription();
            return true;
        }
        catch (ArgumentOutOfRangeException exception)
        {
            _distanceCanvas.Clear();
            _pathsList.Items.Clear();
            _infoLabel.Text = string.Empty;
            SetStatus(exception.Message, StatusKind.Error);
            return false;
        }
        catch (Exception exception)
        {
            _distanceCanvas.Clear();
            _pathsList.Items.Clear();
            _infoLabel.Text = string.Empty;
            SetStatus($"Не удалось вычислить расстояния: {exception.Message}", StatusKind.Error);
            return false;
        }
    }

    /// <summary>Запускает все три режима и показывает сравнение времени и числа вызовов.</summary>
    internal void CompareAllModes()
    {
        var lines = new List<string>();

        foreach (FloydWarshallMode mode in Enum.GetValues<FloydWarshallMode>())
        {
            if (mode == FloydWarshallMode.NaiveRecursive && _graph.VertexCount > FloydWarshall.MaxNaiveVertexCount)
            {
                lines.Add($"{FloydWarshall.ModeName(mode),-28} — пропущено: слишком много вершин для 3^n");
                continue;
            }

            var stopwatch = Stopwatch.StartNew();
            FloydWarshallResult result = FloydWarshall.Solve(_graph, mode);
            stopwatch.Stop();

            FloydWarshallResult reference = FloydWarshall.Solve(_graph, FloydWarshallMode.Iterative);
            lines.Add(
                $"{result.ModeName,-28} вызовов: {result.RecursiveCallCount,12:N0} · "
                + $"время: {stopwatch.Elapsed.TotalMilliseconds,8:0.###} мс · "
                + $"совпадение: {(result.Matches(reference) ? "да" : "нет")}");
        }

        _comparisonLabel.Text = string.Join(Environment.NewLine, lines);
    }

    private Control BuildSettingsPanel()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            BackColor = Theme.Surface,
            Padding = new Padding(12, 8, 12, 8),
            Margin = new Padding(0),
        };

        panel.Controls.Add(Theme.CreateCaption("Вершин:"));
        _verticesBox.Width = 60;
        _verticesBox.Margin = new Padding(0, 2, 8, 0);
        _verticesBox.TextAlign = HorizontalAlignment.Center;
        _verticesBox.ValueChanged += (_, _) => LoadRandomGraph();
        panel.Controls.Add(_verticesBox);
        panel.Controls.Add(Theme.CreateLabel(
            $"от 3 до {WeightedGraph.MaxVertexCount} (без мемоизации — не больше {FloydWarshall.MaxNaiveVertexCount})",
            muted: true,
            Theme.HintFont));

        panel.Controls.Add(Theme.CreateSeparator());
        panel.Controls.Add(Theme.CreateCaption("Способ:"));
        ConfigureRadio(_naiveRadio, "Рекурсия без мемоизации", check: true);
        ConfigureRadio(_memoizedRadio, "Рекурсия с мемоизацией", check: false);
        ConfigureRadio(_iterativeRadio, "Итеративный O(n³)", check: false);
        panel.Controls.Add(_naiveRadio);
        panel.Controls.Add(_memoizedRadio);
        panel.Controls.Add(_iterativeRadio);

        return panel;
    }

    private void ConfigureRadio(RadioButton radio, string text, bool check)
    {
        radio.Text = text;
        radio.AutoSize = true;
        radio.Checked = check;
        radio.Font = Theme.BaseFont;
        radio.ForeColor = Theme.Text;
        radio.Margin = new Padding(0, 8, 14, 0);
        radio.CheckedChanged += (_, _) =>
        {
            if (radio.Checked)
            {
                UpdateDescription();
            }
        };
    }

    private Control BuildActionsPanel()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            BackColor = Theme.Surface,
            Padding = new Padding(12, 8, 12, 8),
            Margin = new Padding(0, 8, 0, 8),
        };

        _demoButton.Click += (_, _) => LoadDemoGraph();
        _randomButton.Click += (_, _) => LoadRandomGraph();
        _solveButton.Click += (_, _) => SolveSelected();
        _compareButton.Click += (_, _) => CompareAllModes();

        panel.Controls.Add(_solveButton);
        panel.Controls.Add(_compareButton);
        panel.Controls.Add(Theme.CreateSeparator());
        panel.Controls.Add(_demoButton);
        panel.Controls.Add(_randomButton);
        return panel;
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
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

        _adjacencyCanvas.Caption = "Матрица смежности (∞ — ребра нет)";
        _distanceCanvas.Caption = "Кратчайшие расстояния";

        content.Controls.Add(Wrap(_adjacencyCanvas), 0, 0);
        content.Controls.Add(Wrap(_distanceCanvas), 1, 0);
        content.Controls.Add(BuildPathPanel(), 2, 0);
        return content;
    }

    private static Control Wrap(Control child)
    {
        var host = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Surface,
            Padding = new Padding(1),
            Margin = new Padding(0, 0, 8, 0),
        };
        host.Controls.Add(child);
        return host;
    }

    private Control BuildPathPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Theme.Surface,
            Padding = new Padding(10),
            Margin = new Padding(0),
        };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var caption = Theme.CreateCaption("Кратчайшие пути (восстановлены по матрице «следующая вершина»)");
        caption.Margin = new Padding(0, 0, 0, 6);

        _pathsList.Dock = DockStyle.Fill;
        _pathsList.Font = Theme.MonoFont;
        _pathsList.BorderStyle = BorderStyle.FixedSingle;
        _pathsList.IntegralHeight = false;
        _pathsList.SelectionMode = SelectionMode.None;

        _comparisonLabel.AutoSize = true;
        _comparisonLabel.MaximumSize = new Size(520, 0);
        _comparisonLabel.Margin = new Padding(0, 8, 0, 0);

        panel.Controls.Add(caption, 0, 0);
        panel.Controls.Add(_pathsList, 0, 1);
        panel.Controls.Add(_comparisonLabel, 0, 2);
        return panel;
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
            Margin = new Padding(0),
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
        _verticesBox.Minimum = 3;
        _verticesBox.Maximum = WeightedGraph.MaxVertexCount;
        _verticesBox.Value = 6;

        UpdateDescription();
    }

    private void LoadGraph(WeightedGraph graph)
    {
        _graph = graph;
        _adjacencyCanvas.Matrix = graph.ToAdjacencyMatrix();
        _distanceCanvas.Clear();
        _pathsList.Items.Clear();
        _comparisonLabel.Text = string.Empty;
        _infoLabel.Text = string.Empty;
        SetStatus("Граф загружен. Нажмите «Вычислить».", StatusKind.Hint);
    }

    private void FillPaths(FloydWarshallResult result)
    {
        _pathsList.BeginUpdate();
        _pathsList.Items.Clear();

        for (int from = 0; from < _graph.VertexCount; from++)
        {
            for (int to = 0; to < _graph.VertexCount; to++)
            {
                if (from == to)
                {
                    continue;
                }

                string name = $"{WeightedGraph.VertexName(from)} → {WeightedGraph.VertexName(to)}: ";
                _pathsList.Items.Add(name + (result.IsReachable(from, to)
                    ? result.DescribePath(from, to)
                    : "пути нет"));
            }
        }

        _pathsList.EndUpdate();
    }

    private void UpdateDescription()
    {
        _descriptionLabel.Text = FloydWarshall.ModeDescription(SelectedMode);
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

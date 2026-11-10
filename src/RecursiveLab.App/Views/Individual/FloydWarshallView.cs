using System.Diagnostics;
using System.Windows.Forms;
using RecursiveLab.App.Rendering;
using RecursiveLab.Core.Recursion;
using RecursiveLab.Core.ShortestPaths;

namespace RecursiveLab.App.Views.Individual;

/// <summary>
/// Страница индивидуального алгоритма «Флойд — Уоршелл».
///
/// Алгоритм показан на графе: вершины и взвешенные рёбра, а также подсвеченные
/// рёбра кратчайших путей из выбранной вершины. Переключатель «промежуточные
/// вершины» показывает состояние алгоритма после обработки k вершин — видно,
/// как постепенно появляются всё более короткие маршруты.
///
/// На второй вкладке — дерево рекурсии для выбранной пары вершин: троичное без
/// мемоизации (3^k вызовов) и с пометкой «взято из таблицы» при мемоизации.
/// </summary>
internal sealed class FloydWarshallView : UserControl
{
    /// <summary>Ограничения схемы дерева: не больше 90 узлов, чтобы подписи читались.</summary>
    private static readonly RecursionTreeLimits TreeLimits = new RecursionTreeLimits().With(4, 90);
    private readonly NumericUpDown _verticesBox = new();
    private readonly NumericUpDown _stepBox = new();
    private readonly ComboBox _sourceBox = new();
    private readonly RadioButton _naiveRadio = new();
    private readonly RadioButton _memoizedRadio = new();
    private readonly RadioButton _iterativeRadio = new();
    private readonly Button _demoButton = Theme.CreateButton("Демо-граф");
    private readonly Button _randomButton = Theme.CreateButton("Случайный граф");
    private readonly Button _solveButton = Theme.CreateButton("Вычислить", primary: true);
    private readonly Button _compareButton = Theme.CreateButton("Сравнить все режимы");
    private readonly GraphCanvas _graphCanvas = new();
    private readonly MatrixCanvas _distanceCanvas = new();
    private readonly RecursionTreeCanvas _treeCanvas = new();
    private readonly ListBox _pathsList = new();
    private readonly TabControl _sideTabs = new();
    private readonly Label _infoLabel = Theme.CreateLabel(string.Empty, font: Theme.BoldFont);
    private readonly Label _statusLabel = Theme.CreateLabel(string.Empty);
    private readonly Label _comparisonLabel = Theme.CreateLabel(string.Empty, font: Theme.MonoFont);
    private readonly Label _descriptionLabel = Theme.CreateLabel(string.Empty, muted: true, Theme.HintFont);
    private readonly List<(int From, int To)> _pathPairs = [];
    private readonly List<string> _pathTexts = [];

    private WeightedGraph _graph = WeightedGraph.CreateDemo();
    private FloydWarshallResult? _stepResult;
    private int _randomSeed = 1;
    private bool _updatingControls;

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

    internal GraphCanvas GraphView => _graphCanvas;

    internal MatrixCanvas DistanceCanvas => _distanceCanvas;

    internal RecursionTreeCanvas TreeView => _treeCanvas;

    internal ListBox PathsList => _pathsList;

    internal NumericUpDown VerticesInput => _verticesBox;

    internal NumericUpDown StepInput => _stepBox;

    internal ComboBox SourceSelector => _sourceBox;

    internal string InfoText => _infoLabel.Text;

    internal string StatusText => _statusLabel.Text;

    internal string ComparisonText => _comparisonLabel.Text;

    internal FloydWarshallResult? StepResult => _stepResult;

    internal void SelectMode(FloydWarshallMode mode)
    {
        _updatingControls = true;

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

        _updatingControls = false;
        UpdateDescription();
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
        LoadGraph(WeightedGraph.CreateDemo());
    }

    /// <summary>Строит случайный граф с текущим числом вершин.</summary>
    internal void LoadRandomGraph()
    {
        _randomSeed++;
        LoadGraph(WeightedGraph.CreateRandom((int)_verticesBox.Value, _randomSeed));
    }

    /// <summary>
    /// Вычисляет полное решение выбранным способом (для дерева рекурсии и сравнения)
    /// и показывает итоговое состояние графа.
    /// </summary>
    internal bool SolveSelected()
    {
        FloydWarshallMode mode = SelectedMode;

        try
        {
            var stopwatch = Stopwatch.StartNew();
            FloydWarshallResult result = FloydWarshall.Solve(_graph, mode);
            stopwatch.Stop();

            FloydWarshallResult reference = mode == FloydWarshallMode.Iterative
                ? result
                : FloydWarshall.Solve(_graph, FloydWarshallMode.Iterative);

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
            UpdateRecursionTree();

            // После полного решения показываем итоговое состояние алгоритма (k = n).
            _stepBox.Value = _graph.VertexCount;
            UpdateStepState();

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

    /// <summary>Пересчитывает состояние алгоритма после k промежуточных вершин.</summary>
    internal bool UpdateStepState()
    {
        try
        {
            _stepResult = FloydWarshall.SolveStep(_graph, (int)_stepBox.Value);

            _distanceCanvas.Matrix = _stepResult.Distances;
            _distanceCanvas.Caption = _stepResult.IsComplete
                ? "Кратчайшие расстояния (все вершины разрешены)"
                : $"Расстояния при промежуточных вершинах 1..{_stepResult.AllowedIntermediates}";

            FillPaths();
            HighlightGraph();

            return true;
        }
        catch (Exception exception)
        {
            SetStatus($"Не удалось показать шаг алгоритма: {exception.Message}", StatusKind.Error);
            return false;
        }
    }

    /// <summary>Строит дерево рекурсии для выбранной пары вершин.</summary>
    internal void UpdateRecursionTree()
    {
        FloydWarshallMode mode = SelectedMode;

        if (mode == FloydWarshallMode.Iterative)
        {
            _treeCanvas.PlaceholderText = "Итеративный алгоритм не использует рекурсию: дерева вызовов нет.";
            _treeCanvas.Clear();
            return;
        }

        (int from, int to) = SelectedPair();

        // Глубина схемы подбирается так, чтобы для небольших графов были видны
        // даже базовые случаи, а для больших схема не превращалась в «кашу».
        RecursionTreeLimits limits = new RecursionTreeLimits().With(
            Math.Min(_graph.VertexCount + 1, 5),
            TreeLimits.MaxNodes);

        try
        {
            _treeCanvas.Tree = FloydWarshallTree.Build(_graph, mode, from, to, limits);
        }
        catch (Exception exception)
        {
            _treeCanvas.PlaceholderText = $"Дерево не построено: {exception.Message}";
            _treeCanvas.Clear();
        }
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
        _verticesBox.ValueChanged += (_, _) => OnVerticesChanged();
        panel.Controls.Add(_verticesBox);
        panel.Controls.Add(Theme.CreateLabel(
            $"от 3 до {WeightedGraph.MaxVertexCount} (без мемоизации — не больше {FloydWarshall.MaxNaiveVertexCount})",
            muted: true,
            Theme.HintFont));

        panel.Controls.Add(Theme.CreateSeparator());
        panel.Controls.Add(Theme.CreateCaption("Способ решения:"));
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
        radio.CheckedChanged += (_, _) => OnModeChanged();
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
        panel.Controls.Add(Theme.CreateSeparator());

        panel.Controls.Add(Theme.CreateCaption("Промежуточные вершины 1..k:"));
        _stepBox.Width = 60;
        _stepBox.Margin = new Padding(0, 2, 8, 0);
        _stepBox.TextAlign = HorizontalAlignment.Center;
        _stepBox.ValueChanged += (_, _) => OnStepChanged();
        panel.Controls.Add(_stepBox);

        panel.Controls.Add(Theme.CreateCaption("Пути из:"));
        _sourceBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _sourceBox.Width = 60;
        _sourceBox.Margin = new Padding(0, 2, 8, 0);
        _sourceBox.SelectedIndexChanged += (_, _) => OnSourceChanged();
        panel.Controls.Add(_sourceBox);

        return panel;
    }

    private Control BuildContentPanel()
    {
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Background,
            Margin = new Padding(0),
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));

        var graphHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Surface,
            Padding = new Padding(1),
            Margin = new Padding(0, 0, 8, 0),
        };
        graphHost.Controls.Add(_graphCanvas);
        content.Controls.Add(graphHost, 0, 0);
        content.Controls.Add(BuildSidePanel(), 1, 0);
        return content;
    }

    private Control BuildSidePanel()
    {
        _sideTabs.Dock = DockStyle.Fill;
        _sideTabs.Font = Theme.BaseFont;
        _sideTabs.Padding = new Point(12, 5);
        _sideTabs.Margin = new Padding(0);

        _distanceCanvas.Caption = "Кратчайшие расстояния";
        _pathsList.Dock = DockStyle.Fill;
        _pathsList.Font = Theme.MonoFont;
        _pathsList.BorderStyle = BorderStyle.FixedSingle;
        _pathsList.IntegralHeight = false;
        _pathsList.SelectionMode = SelectionMode.One;
        _pathsList.SelectedIndexChanged += (_, _) => OnPathSelected();

        _comparisonLabel.AutoSize = true;
        _comparisonLabel.MaximumSize = new Size(420, 0);
        _comparisonLabel.Margin = new Padding(6, 6, 0, 0);

        var matricesPage = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Theme.Surface,
            Padding = new Padding(8),
        };
        matricesPage.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        matricesPage.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        matricesPage.RowStyles.Add(new RowStyle(SizeType.Percent, 45));

        var matrixHost = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(1) };
        matrixHost.Controls.Add(_distanceCanvas);

        var pathsCaption = Theme.CreateCaption("Кратчайшие пути (выберите строку — построится дерево рекурсии)");
        pathsCaption.Margin = new Padding(0, 8, 0, 4);

        var pathsHost = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(1) };
        pathsHost.Controls.Add(_pathsList);

        matricesPage.Controls.Add(matrixHost, 0, 0);
        matricesPage.Controls.Add(pathsCaption, 0, 1);
        matricesPage.Controls.Add(pathsHost, 0, 2);

        _descriptionLabel.AutoSize = true;
        _descriptionLabel.MaximumSize = new Size(500, 0);
        _descriptionLabel.Margin = new Padding(0, 0, 0, 0);
        _comparisonLabel.AutoSize = true;
        _comparisonLabel.MaximumSize = new Size(500, 0);
        _comparisonLabel.Margin = new Padding(0, 6, 0, 0);

        var notes = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Theme.Surface,
            Padding = new Padding(10, 6, 10, 6),
            Margin = new Padding(0),
        };
        notes.Controls.Add(_descriptionLabel);
        notes.Controls.Add(_comparisonLabel);

        var treePage = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.Surface,
            Padding = new Padding(0),
        };
        treePage.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        treePage.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        treePage.Controls.Add(_treeCanvas, 0, 0);
        treePage.Controls.Add(notes, 0, 1);

        _sideTabs.TabPages.Add(CreatePage("Матрица и пути", matricesPage));
        _sideTabs.TabPages.Add(CreatePage("Дерево рекурсии", treePage));
        return _sideTabs;
    }

    private static TabPage CreatePage(string title, Control content)
    {
        var page = new TabPage(title)
        {
            BackColor = Theme.Surface,
            Padding = new Padding(0),
            UseVisualStyleBackColor = false,
        };

        content.Dock = DockStyle.Fill;
        page.Controls.Add(content);
        return page;
    }

    private Control BuildStatusPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.Surface,
            Padding = new Padding(12, 8, 12, 10),
            Margin = new Padding(0),
        };

        _infoLabel.Margin = new Padding(0, 0, 0, 2);
        _statusLabel.Margin = new Padding(0, 0, 0, 0);

        panel.Controls.Add(_infoLabel, 0, 0);
        panel.Controls.Add(_statusLabel, 0, 1);
        return panel;
    }

    private void ConfigureInputs()
    {
        _verticesBox.Minimum = 3;
        _verticesBox.Maximum = WeightedGraph.MaxVertexCount;
        _verticesBox.Value = 6;

        _stepBox.Minimum = 0;
        _stepBox.Maximum = WeightedGraph.MaxVertexCount;
        _stepBox.Value = 0;

        UpdateDescription();
    }

    private void LoadGraph(WeightedGraph graph)
    {
        _graph = graph;
        _graphCanvas.Graph = graph;
        _distanceCanvas.Clear();
        _pathsList.Items.Clear();
        _pathPairs.Clear();
        _pathTexts.Clear();
        _comparisonLabel.Text = string.Empty;
        _infoLabel.Text = string.Empty;

        _stepBox.Value = Math.Min(graph.VertexCount, _stepBox.Maximum);

        _sourceBox.Items.Clear();
        for (int vertex = 0; vertex < graph.VertexCount; vertex++)
        {
            _sourceBox.Items.Add(WeightedGraph.VertexName(vertex));
        }

        _sourceBox.SelectedIndex = 0;
        SetStatus("Граф загружен. Нажмите «Вычислить».", StatusKind.Hint);
    }

    private void FillPaths()
    {
        if (_stepResult is null)
        {
            return;
        }

        int selected = _pathsList.SelectedIndex;

        _pathsList.BeginUpdate();
        _pathsList.Items.Clear();
        _pathPairs.Clear();

        for (int from = 0; from < _graph.VertexCount; from++)
        {
            for (int to = 0; to < _graph.VertexCount; to++)
            {
                if (from == to)
                {
                    continue;
                }

                string name = $"{WeightedGraph.VertexName(from)} → {WeightedGraph.VertexName(to)}: ";
                _pathPairs.Add((from, to));

                if (_stepResult.IsReachable(from, to))
                {
                    _pathsList.Items.Add(name + _stepResult.DescribePath(from, to));
                }
                else
                {
                    _pathsList.Items.Add(name + "пути нет (k = " + _stepResult.AllowedIntermediates + ")");
                }
            }
        }

        _pathsList.EndUpdate();

        if (_pathPairs.Count > 0)
        {
            _pathsList.SelectedIndex = Math.Clamp(selected, 0, _pathPairs.Count - 1);
        }
    }

    private void HighlightGraph()
    {
        if (_stepResult is null)
        {
            return;
        }

        int source = Math.Max(0, _sourceBox.SelectedIndex);
        IReadOnlyList<(int From, int To)> edges = _stepResult.ShortestPathEdges(source);

        _graphCanvas.HighlightedEdges = edges;
        _graphCanvas.SourceVertex = source;
        _graphCanvas.ReachableVertices = _stepResult.ReachableVertices(source);
        _graphCanvas.Note = _stepResult.IsComplete
            ? $"Подсвечены рёбра кратчайших путей из {WeightedGraph.VertexName(source)} · решены все k = {_stepResult.AllowedIntermediates} вершин"
            : $"Промежуточные вершины 1..{_stepResult.AllowedIntermediates} · подсвечены пути из {WeightedGraph.VertexName(source)} (рёбер: {edges.Count})";
        _graphCanvas.Invalidate();
    }

    private (int From, int To) SelectedPair()
    {
        int index = _pathsList.SelectedIndex;
        if (index >= 0 && index < _pathPairs.Count)
        {
            return _pathPairs[index];
        }

        return (0, Math.Max(1, Math.Min(1, _graph.VertexCount - 1)));
    }

    private void OnVerticesChanged()
    {
        if (_updatingControls)
        {
            return;
        }

        LoadGraph(WeightedGraph.CreateRandom((int)_verticesBox.Value, ++_randomSeed));
        SolveSelected();
    }

    private void OnModeChanged()
    {
        if (_updatingControls)
        {
            return;
        }

        UpdateDescription();
        UpdateRecursionTree();
    }

    private void OnStepChanged()
    {
        if (_updatingControls)
        {
            return;
        }

        UpdateStepState();
    }

    private void OnSourceChanged()
    {
        if (_updatingControls)
        {
            return;
        }

        HighlightGraph();
    }

    private void OnPathSelected()
    {
        if (_updatingControls)
        {
            return;
        }

        UpdateRecursionTree();
        HighlightGraph();
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

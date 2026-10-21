using System.Windows.Forms;
using RecursiveLab.App.Rendering;
using RecursiveLab.Core.Hanoi;

namespace RecursiveLab.App.Views;

/// <summary>
/// Страница головоломки «Ханойская башня».
///
/// Страница не содержит собственной логики решения: она получает готовую
/// последовательность ходов от выбранного алгоритма (<see cref="IHanoiSolver"/>)
/// и воспроизводит её. Оба способа решения — рекурсивный и стековый — используют
/// один и тот же интерфейс и одну и ту же визуализацию.
/// </summary>
internal sealed class HanoiView : UserControl
{
    private const int GuiMaxDisks = 12;

    /// <summary>Задержка автоигры в миллисекундах для позиций ползунка 1..10.</summary>
    private static readonly int[] AutoDelays = [1600, 1100, 750, 520, 360, 250, 180, 130, 90, 55];

    private readonly NumericUpDown _disksBox = new();
    private readonly RadioButton _recursiveRadio = new();
    private readonly RadioButton _stackRadio = new();
    private readonly TrackBar _speedBar = new();
    private readonly Label _speedLabel = Theme.CreateLabel(string.Empty, muted: true, Theme.HintFont);
    private readonly Label _algorithmLabel = Theme.CreateLabel(string.Empty, font: Theme.BoldFont);
    private readonly Label _progressLabel = Theme.CreateLabel(string.Empty, font: Theme.BoldFont);
    private readonly Label _statusLabel = Theme.CreateLabel(string.Empty);
    private readonly Label _descriptionLabel = Theme.CreateLabel(string.Empty, muted: true, Theme.HintFont);
    private readonly ListBox _journal = new();
    private readonly Button _solveButton = Theme.CreateButton("Решить", primary: true);
    private readonly Button _stepButton = Theme.CreateButton("Следующий ход");
    private readonly Button _autoButton = Theme.CreateButton("Авто");
    private readonly Button _runAllButton = Theme.CreateButton("Выполнить всё");
    private readonly Button _resetButton = Theme.CreateButton("Сброс");
    private readonly HanoiCanvas _canvas = new();
    private readonly MoveAnimator _animator = new();
    private readonly System.Windows.Forms.Timer _autoTimer = new();

    private HanoiState _state;
    private HanoiSolution? _solution;
    private int _nextMoveIndex;
    private HanoiMove? _lastMove;
    private bool _animationActive;
    private int _targetPeg = HanoiPegs.Count - 1;

    public HanoiView()
    {
        Dock = DockStyle.Fill;
        BackColor = Theme.Background;
        Font = Theme.BaseFont;

        _state = new HanoiState(4);

        _autoTimer.Tick += (_, _) => OnAutoTick();
        _animator.ProgressChanged += progress => _canvas.UpdateFlight(progress);
        _animator.Completed += OnMoveAnimationCompleted;

        Controls.Add(BuildLayout());

        ConfigureInputs();
        ResetPuzzle();
    }

    /// <summary>Фактическое состояние головоломки (используется самопроверкой).</summary>
    internal HanoiState? PuzzleState => _state;

    internal HanoiSolution? Solution => _solution;

    internal int AppliedMoveCount => _nextMoveIndex;

    internal HanoiCanvas Canvas => _canvas;

    internal string StatusText => _statusLabel.Text;

    internal string ProgressText => _progressLabel.Text;

    internal NumericUpDown DisksInput => _disksBox;

    internal RadioButton RecursiveOption => _recursiveRadio;

    internal RadioButton StackOption => _stackRadio;

    internal TrackBar SpeedBar => _speedBar;

    internal Button AutoButton => _autoButton;

    /// <summary>Выбирает решатель в интерфейсе (используется самопроверкой).</summary>
    internal void SelectSolver(HanoiSolverKind kind)
    {
        if (kind == HanoiSolverKind.ExplicitStack)
        {
            _stackRadio.Checked = true;
        }
        else
        {
            _recursiveRadio.Checked = true;
        }
    }

    // -----------------------------------------------------------------------
    // Разметка страницы
    // -----------------------------------------------------------------------

    private Control BuildLayout()
    {
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
        return root;
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

        panel.Controls.Add(Theme.CreateCaption("Количество дисков:"));
        _disksBox.Width = 60;
        _disksBox.Margin = new Padding(0, 2, 12, 0);
        _disksBox.TextAlign = HorizontalAlignment.Center;
        _disksBox.ValueChanged += (_, _) => OnDisksChanged();
        panel.Controls.Add(_disksBox);

        panel.Controls.Add(Theme.CreateCaption("Способ решения:"));
        ConfigureRadio(_recursiveRadio, "Рекурсия", check: true);
        ConfigureRadio(_stackRadio, "Явный стек", check: false);
        panel.Controls.Add(_recursiveRadio);
        panel.Controls.Add(_stackRadio);
        panel.Controls.Add(Theme.CreateSeparator());

        panel.Controls.Add(Theme.CreateCaption("Скорость:"));
        _speedBar.Minimum = 1;
        _speedBar.Maximum = AutoDelays.Length;
        _speedBar.Value = 5;
        _speedBar.TickStyle = TickStyle.None;
        _speedBar.Width = 180;
        _speedBar.Height = 28;
        _speedBar.Margin = new Padding(0, 2, 6, 0);
        _speedBar.ValueChanged += (_, _) => UpdateSpeedLabel();
        panel.Controls.Add(_speedBar);
        panel.Controls.Add(_speedLabel);

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
        radio.CheckedChanged += (_, _) => OnSolverChanged();
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

        _solveButton.Click += (_, _) => PrepareSolution();
        _stepButton.Click += (_, _) => StepOnce();
        _autoButton.Click += (_, _) => ToggleAutoPlay();
        _runAllButton.Click += (_, _) => RunToEnd();
        _resetButton.Click += (_, _) => ResetPuzzle();

        panel.Controls.Add(_solveButton);
        panel.Controls.Add(_stepButton);
        panel.Controls.Add(_autoButton);
        panel.Controls.Add(_runAllButton);
        panel.Controls.Add(_resetButton);
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
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 290));

        var canvasHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.HanoiCanvasBackground,
            Padding = new Padding(1),
            Margin = new Padding(0, 0, 8, 0),
        };
        canvasHost.Controls.Add(_canvas);
        content.Controls.Add(canvasHost, 0, 0);

        var side = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Theme.Surface,
            Padding = new Padding(10),
            Margin = new Padding(0),
        };
        side.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        side.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        side.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        side.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var journalCaption = Theme.CreateCaption("Журнал выполненных ходов");
        journalCaption.Margin = new Padding(0, 0, 0, 6);

        _journal.Dock = DockStyle.Fill;
        _journal.Font = Theme.MonoFont;
        _journal.BorderStyle = BorderStyle.FixedSingle;
        _journal.IntegralHeight = false;
        _journal.SelectionMode = SelectionMode.None;
        _journal.HorizontalScrollbar = false;

        var descriptionCaption = Theme.CreateCaption("Как работает алгоритм");
        descriptionCaption.Margin = new Padding(0, 10, 0, 4);

        _descriptionLabel.AutoSize = true;
        _descriptionLabel.MaximumSize = new Size(258, 0);
        _descriptionLabel.Margin = new Padding(0);

        side.Controls.Add(journalCaption, 0, 0);
        side.Controls.Add(_journal, 0, 1);
        side.Controls.Add(descriptionCaption, 0, 2);
        side.Controls.Add(_descriptionLabel, 0, 3);

        content.Controls.Add(side, 1, 0);
        return content;
    }

    private Control BuildStatusPanel()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            BackColor = Theme.Surface,
            Padding = new Padding(12, 8, 12, 8),
            Margin = new Padding(0, 8, 0, 0),
        };

        _progressLabel.Margin = new Padding(0, 8, 20, 0);
        _algorithmLabel.Margin = new Padding(0, 8, 20, 0);
        _statusLabel.Margin = new Padding(0, 8, 0, 0);

        panel.Controls.Add(_progressLabel);
        panel.Controls.Add(_algorithmLabel);
        panel.Controls.Add(_statusLabel);
        return panel;
    }

    private void ConfigureInputs()
    {
        _disksBox.Minimum = HanoiState.MinDiskCount;
        _disksBox.Maximum = GuiMaxDisks;
        _disksBox.Value = 4;

        UpdateSpeedLabel();
        UpdateAlgorithmLabel();
    }

    // -----------------------------------------------------------------------
    // Вспомогательные свойства
    // -----------------------------------------------------------------------

    private HanoiSolverKind SelectedSolverKind
        => _stackRadio.Checked ? HanoiSolverKind.ExplicitStack : HanoiSolverKind.Recursive;

    private int SelectedDiskCount => (int)_disksBox.Value;

    private int AutoDelayMs => AutoDelays[Math.Clamp(_speedBar.Value - 1, 0, AutoDelays.Length - 1)];

    /// <summary>Длительность анимации одного хода: половина задержки автоигры, но в разумных границах.</summary>
    private int AnimationDurationMs => Math.Clamp(AutoDelayMs / 2, 70, 400);

    // -----------------------------------------------------------------------
    // Управление воспроизведением
    // -----------------------------------------------------------------------

    /// <summary>
    /// Строит решение выбранным алгоритмом. Состояние головоломки приводится
    /// к начальному положению, чтобы показ всегда начинался с одного и того же места.
    /// </summary>
    internal bool PrepareSolution()
    {
        StopAutoPlay();
        _animator.Stop();
        _animationActive = false;
        _canvas.EndFlight();

        try
        {
            IHanoiSolver solver = HanoiSolverFactory.Create(SelectedSolverKind);
            int diskCount = SelectedDiskCount;

            _state = new HanoiState(diskCount, startPeg: 0);
            _solution = solver.Solve(diskCount, fromPeg: 0, toPeg: _targetPeg);
            _nextMoveIndex = 0;
            _lastMove = null;

            _journal.Items.Clear();
            _canvas.Update(_state, null);

            UpdateProgressLabel();
            UpdateButtons();
            SetStatus(
                $"Решение построено алгоритмом «{solver.Name}»: {_solution.MoveCount} ходов. " +
                "Используйте «Следующий ход» или «Авто».",
                StatusKind.Hint);
            return true;
        }
        catch (ArgumentOutOfRangeException exception)
        {
            _solution = null;
            SetStatus(exception.Message, StatusKind.Error);
            UpdateButtons();
            return false;
        }
        catch (Exception exception)
        {
            _solution = null;
            SetStatus($"Не удалось построить решение: {exception.Message}", StatusKind.Error);
            UpdateButtons();
            return false;
        }
    }

    /// <summary>
    /// Выполняет один ход. При <paramref name="animate"/> = false ход применяется
    /// мгновенно (используется самопроверкой).
    /// </summary>
    internal bool StepOnce(bool animate = true)
    {
        if (_animationActive)
        {
            return false;
        }

        if (_solution is null && !PrepareSolution())
        {
            return false;
        }

        if (_solution is null || _nextMoveIndex >= _solution.MoveCount)
        {
            SetStatus("Все ходы уже выполнены.", StatusKind.Hint);
            UpdateButtons();
            return false;
        }

        HanoiMove move = _solution.Moves[_nextMoveIndex];

        if (!animate)
        {
            ApplyMoveToModel(move);
            return true;
        }

        // Показ одного хода: модель меняется по завершении анимации,
        // поэтому картинка и модель всегда согласованы.
        _animationActive = true;
        UpdateButtons();
        _canvas.BeginFlight(move);
        _animator.Start(AnimationDurationMs);
        return true;
    }

    /// <summary>Немедленно выполняет все оставшиеся ходы.</summary>
    internal void RunToEnd()
    {
        StopAutoPlay();
        _animator.Stop();
        _animationActive = false;
        _canvas.EndFlight();

        if (_solution is null && !PrepareSolution())
        {
            return;
        }

        if (_solution is null)
        {
            return;
        }

        while (_nextMoveIndex < _solution.MoveCount)
        {
            ApplyMoveToModel(_solution.Moves[_nextMoveIndex]);
        }
    }

    /// <summary>Переключает автоматическое воспроизведение решения.</summary>
    internal void ToggleAutoPlay()
    {
        if (_autoTimer.Enabled)
        {
            StopAutoPlay();
            SetStatus("Автоматическое воспроизведение приостановлено.", StatusKind.Hint);
            return;
        }

        if (_solution is null && !PrepareSolution())
        {
            return;
        }

        if (_solution is null || _nextMoveIndex >= _solution.MoveCount)
        {
            SetStatus("Решение уже завершено — нажмите «Сброс», чтобы начать заново.", StatusKind.Hint);
            return;
        }

        _autoButton.Text = "Пауза";
        SetStatus($"Автоматическое воспроизведение: {AutoDelayMs} мс на ход.", StatusKind.Hint);
        _autoTimer.Interval = AutoDelayMs;
        _autoTimer.Start();
        OnAutoTick();
    }

    /// <summary>Приводит головоломку к начальному положению.</summary>
    internal void ResetPuzzle()
    {
        StopAutoPlay();
        _animator.Stop();
        _animationActive = false;
        _canvas.EndFlight();

        _state = new HanoiState(SelectedDiskCount, startPeg: 0);
        _solution = null;
        _nextMoveIndex = 0;
        _lastMove = null;

        _journal.Items.Clear();
        _canvas.Update(_state, null);
        UpdateProgressLabel();
        UpdateButtons();
        SetStatus("Готово к работе: выберите способ решения и нажмите «Решить».", StatusKind.Hint);
    }

    private void StopAutoPlay()
    {
        _autoTimer.Stop();
        _autoButton.Text = "Авто";
    }

    private void OnAutoTick()
    {
        if (_animationActive)
        {
            return;
        }

        if (_solution is null || _nextMoveIndex >= _solution.MoveCount)
        {
            StopAutoPlay();
            return;
        }

        StepOnce();
    }

    private void OnMoveAnimationCompleted()
    {
        _animationActive = false;

        if (_solution is null || _nextMoveIndex >= _solution.MoveCount)
        {
            _canvas.EndFlight();
            UpdateButtons();
            return;
        }

        HanoiMove move = _solution.Moves[_nextMoveIndex];
        _canvas.EndFlight();
        ApplyMoveToModel(move);
    }

    /// <summary>
    /// Единственная точка изменения модели: ход выполняется в <see cref="HanoiState"/>,
    /// после чего обновляются холст, журнал и счётчики.
    /// </summary>
    private void ApplyMoveToModel(HanoiMove move)
    {
        try
        {
            _state.Apply(move);
        }
        catch (InvalidOperationException exception)
        {
            StopAutoPlay();
            SetStatus(exception.Message, StatusKind.Error);
            UpdateButtons();
            return;
        }

        _nextMoveIndex++;
        _lastMove = move;

        _journal.Items.Add($"{_nextMoveIndex,5}.  {move}");
        _journal.TopIndex = Math.Max(0, _journal.Items.Count - 1);

        _canvas.Update(_state, move);
        UpdateProgressLabel();

        if (_state.IsSolved(_targetPeg))
        {
            StopAutoPlay();
            UpdateButtons();
            SetStatus(
                $"Задача решена за {_nextMoveIndex} ходов (минимум 2^{_state.DiskCount} − 1 = " +
                $"{_solution?.TheoreticalMinimumMoves}). Способ решения: {HanoiSolverFactory.Create(SelectedSolverKind).Name}.",
                StatusKind.Success);
            return;
        }

        UpdateButtons();
    }

    // -----------------------------------------------------------------------
    // Обновление интерфейса
    // -----------------------------------------------------------------------

    private void UpdateProgressLabel()
    {
        int total = _solution?.MoveCount ?? 0;
        _progressLabel.Text = $"Ход: {_nextMoveIndex} из {total} · осталось {Math.Max(0, total - _nextMoveIndex)}";

        if (_solution is not null && _nextMoveIndex >= _solution.MoveCount)
        {
            _progressLabel.ForeColor = Theme.Success;
        }
        else
        {
            _progressLabel.ForeColor = Theme.Text;
        }
    }

    private void UpdateAlgorithmLabel()
    {
        IHanoiSolver solver = HanoiSolverFactory.Create(SelectedSolverKind);
        long minimum = (1L << SelectedDiskCount) - 1;
        _algorithmLabel.Text = $"Алгоритм: {solver.Name} · минимум ходов 2^{SelectedDiskCount} − 1 = {minimum}";
        _descriptionLabel.Text = solver.Description;
    }

    private void UpdateSpeedLabel()
    {
        _speedLabel.Text = $"{AutoDelayMs} мс/ход";

        if (_autoTimer.Enabled)
        {
            _autoTimer.Interval = AutoDelayMs;
        }
    }

    private void UpdateButtons()
    {
        bool hasMovesLeft = _solution is not null && _nextMoveIndex < _solution.MoveCount;
        bool canInteract = hasMovesLeft && !_animationActive;

        _stepButton.Enabled = canInteract;
        _autoButton.Enabled = hasMovesLeft;
        _runAllButton.Enabled = canInteract;
        _solveButton.Enabled = !_animationActive;
        _resetButton.Enabled = !_animationActive;
    }

    private void OnDisksChanged()
    {
        UpdateAlgorithmLabel();

        // Количество дисков изменилось — прежнее решение больше не соответствует
        // головоломке, поэтому состояние сбрасывается.
        ResetPuzzle();
    }

    private void OnSolverChanged()
    {
        UpdateAlgorithmLabel();

        if (_solution is not null)
        {
            SetStatus("Способ решения изменён — нажмите «Решить», чтобы построить новую последовательность ходов.", StatusKind.Hint);
            _solution = null;
            _nextMoveIndex = 0;
            _journal.Items.Clear();
            _state = new HanoiState(SelectedDiskCount, startPeg: 0);
            _canvas.Update(_state, null);
            UpdateProgressLabel();
            UpdateButtons();
        }
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _autoTimer.Stop();
            _autoTimer.Dispose();
            _animator.Stop();
            _animator.Dispose();
        }

        base.Dispose(disposing);
    }
}

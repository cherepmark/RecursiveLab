using System.Diagnostics;
using System.Drawing.Imaging;
using System.Windows.Forms;
using RecursiveLab.App.Rendering;
using RecursiveLab.Core.Fractals;

namespace RecursiveLab.App.Views;

/// <summary>
/// Страница построения фракталов.
///
/// Панель отвечает только за ввод параметров и показ результата: сами вычисления
/// выполняет рекурсивный алгоритм из <c>RecursiveLab.Core</c>.
/// </summary>
internal sealed class FractalView : UserControl
{
    private readonly ComboBox _fractalBox = new();
    private readonly NumericUpDown _depthBox = new();
    private readonly Label _depthHint = Theme.CreateLabel(string.Empty, muted: true, Theme.HintFont);
    private readonly FractalCanvas _canvas = new();
    private readonly Label _metricsLabel = Theme.CreateLabel(string.Empty, font: Theme.BoldFont);
    private readonly Label _statusLabel = Theme.CreateLabel(string.Empty);
    private readonly Label _descriptionLabel = Theme.CreateLabel(string.Empty, muted: true, Theme.HintFont);
    private readonly Button _buildButton = Theme.CreateButton("Построить", primary: true);
    private readonly Button _clearButton = Theme.CreateButton("Очистить");
    private readonly Button _saveButton = Theme.CreateButton("Сохранить PNG");

    public FractalView()
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

        ConfigureFractalList();
        ConfigureDepthInput();
    }

    /// <summary>Холст — используется самопроверкой для сохранения изображения.</summary>
    internal FractalCanvas Canvas => _canvas;

    internal NumericUpDown DepthInput => _depthBox;

    internal ComboBox FractalSelector => _fractalBox;

    /// <summary>Текст текущего сообщения панели (успех/ошибка/подсказка).</summary>
    internal string StatusText => _statusLabel.Text;

    /// <summary>Выбирает фрактал в списке (используется самопроверкой).</summary>
    internal void SelectFractal(FractalKind kind)
    {
        for (int i = 0; i < _fractalBox.Items.Count; i++)
        {
            if (_fractalBox.Items[i] is IFractal fractal && fractal.Kind == kind)
            {
                _fractalBox.SelectedIndex = i;
                return;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(kind), kind, "Фрактал отсутствует в списке интерфейса.");
    }

    // -----------------------------------------------------------------------
    // Построение интерфейса
    // -----------------------------------------------------------------------

    private Control BuildToolbar()
    {
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            BackColor = Theme.Surface,
            Padding = new Padding(12, 8, 12, 8),
            Margin = new Padding(0),
        };

        toolbar.Controls.Add(Theme.CreateCaption("Фрактал:"));

        _fractalBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _fractalBox.Width = 240;
        _fractalBox.Height = 28;
        _fractalBox.Margin = new Padding(0, 2, 12, 0);
        _fractalBox.SelectedIndexChanged += OnFractalChanged;
        toolbar.Controls.Add(_fractalBox);

        toolbar.Controls.Add(Theme.CreateCaption("Глубина рекурсии:"));
        toolbar.Controls.Add(_depthBox);
        toolbar.Controls.Add(_depthHint);
        toolbar.Controls.Add(Theme.CreateSeparator());

        _buildButton.Margin = new Padding(0, 2, 8, 0);
        _buildButton.Click += (_, _) => BuildFractal();
        toolbar.Controls.Add(_buildButton);

        _clearButton.Margin = new Padding(0, 2, 8, 0);
        _clearButton.Click += (_, _) => ClearCanvas();
        toolbar.Controls.Add(_clearButton);

        _saveButton.Margin = new Padding(0, 2, 8, 0);
        _saveButton.Click += (_, _) => SaveImage();
        toolbar.Controls.Add(_saveButton);

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
            RowCount = 3,
            BackColor = Theme.Surface,
            Padding = new Padding(12, 8, 12, 10),
            Margin = new Padding(0),
        };

        _metricsLabel.Margin = new Padding(0, 0, 0, 2);
        _statusLabel.Margin = new Padding(0, 0, 0, 4);

        _descriptionLabel.AutoSize = true;
        _descriptionLabel.MaximumSize = new Size(900, 0);
        _descriptionLabel.Margin = new Padding(0);

        panel.Controls.Add(_metricsLabel, 0, 0);
        panel.Controls.Add(_statusLabel, 0, 1);
        panel.Controls.Add(_descriptionLabel, 0, 2);
        return panel;
    }

    private void ConfigureFractalList()
    {
        _fractalBox.Items.Clear();
        foreach (IFractal fractal in FractalCatalog.All)
        {
            _fractalBox.Items.Add(fractal);
        }

        if (_fractalBox.Items.Count > 0)
        {
            _fractalBox.SelectedIndex = 0;
        }
    }

    private void ConfigureDepthInput()
    {
        _depthBox.Width = 70;
        _depthBox.Minimum = 0;
        _depthBox.Maximum = 10;
        _depthBox.Value = 3;
        _depthBox.Margin = new Padding(0, 2, 8, 0);
        _depthBox.TextAlign = HorizontalAlignment.Center;
    }

    // -----------------------------------------------------------------------
    // Логика страницы
    // -----------------------------------------------------------------------

    private IFractal? SelectedFractal => _fractalBox.SelectedItem as IFractal;

    private void OnFractalChanged(object? sender, EventArgs e)
    {
        if (SelectedFractal is not { } fractal)
        {
            return;
        }

        // Диапазон допустимых значений берётся из самого алгоритма,
        // поэтому интерфейс не может предложить недопустимую глубину.
        _depthBox.Minimum = fractal.MinDepth;
        _depthBox.Maximum = fractal.MaxDepth;
        if (_depthBox.Value > fractal.MaxDepth || _depthBox.Value < fractal.MinDepth)
        {
            _depthBox.Value = Math.Clamp(Math.Min(3, fractal.MaxDepth), fractal.MinDepth, fractal.MaxDepth);
        }

        _depthHint.Text = $"допустимо: {fractal.MinDepth}–{fractal.MaxDepth}";
        _descriptionLabel.Text = $"{fractal.DisplayName}. {fractal.AlgorithmDescription}";

        // Состояние алгоритма и картинка должны соответствовать друг другу,
        // поэтому при смене фрактала холст очищается.
        _canvas.Clear();
        _metricsLabel.Text = string.Empty;
        SetStatus("Выберите глубину и нажмите «Построить».", StatusKind.Hint);
    }

    /// <summary>Строит выбранный фрактал с текущей глубиной рекурсии.</summary>
    /// <param name="depthOverride">Глубина для самопроверки; null — значение из интерфейса.</param>
    /// <returns>true, если построение прошло успешно.</returns>
    internal bool BuildFractal(int? depthOverride = null)
    {
        if (SelectedFractal is not { } fractal)
        {
            SetStatus("Фрактал не выбран.", StatusKind.Error);
            return false;
        }

        int depth = depthOverride ?? (int)_depthBox.Value;

        try
        {
            var stopwatch = Stopwatch.StartNew();
            FractalGeometry geometry = fractal.Build(new FractalParameters(depth));
            stopwatch.Stop();

            _canvas.Geometry = geometry;

            string metrics = geometry.Triangles.Count > 0
                ? $"{geometry.Triangles.Count:N0} треугольников"
                : $"{geometry.Segments.Count:N0} отрезков";

            _metricsLabel.Text =
                $"Построено · {metrics} · примитивов: {geometry.PrimitiveCount:N0} · время: {stopwatch.Elapsed.TotalMilliseconds:0.#} мс";

            SetStatus(
                $"Готово: «{fractal.DisplayName}», глубина рекурсии {depth}. Детализация растёт с каждым уровнем.",
                StatusKind.Success);
            return true;
        }
        catch (FractalParameterException exception)
        {
            // Понятное сообщение о недопустимом вводе без падения приложения.
            _canvas.Clear();
            _metricsLabel.Text = string.Empty;
            SetStatus(exception.Message, StatusKind.Error);
            return false;
        }
        catch (OutOfMemoryException)
        {
            _canvas.Clear();
            _metricsLabel.Text = string.Empty;
            SetStatus(
                $"Недостаточно памяти для глубины {depth}. Уменьшите глубину рекурсии.",
                StatusKind.Error);
            return false;
        }
        catch (Exception exception)
        {
            _canvas.Clear();
            _metricsLabel.Text = string.Empty;
            SetStatus($"Не удалось построить фрактал: {exception.Message}", StatusKind.Error);
            return false;
        }
    }

    /// <summary>Очищает холст, не меняя настройки.</summary>
    internal void ClearCanvas()
    {
        _canvas.Clear();
        _metricsLabel.Text = string.Empty;
        SetStatus("Холст очищен. Нажмите «Построить», чтобы построить фрактал заново.", StatusKind.Hint);
    }

    private void SaveImage()
    {
        if (_canvas.Geometry is null)
        {
            SetStatus("Сначала постройте фрактал, затем сохраните изображение.", StatusKind.Error);
            return;
        }

        Size size = _canvas.ClientSize.Width > 0 && _canvas.ClientSize.Height > 0
            ? _canvas.ClientSize
            : new Size(1200, 800);

        using var dialog = new SaveFileDialog
        {
            Title = "Сохранить изображение фрактала",
            Filter = "PNG изображение (*.png)|*.png",
            FileName = $"{SelectedFractal?.Kind}_{_canvas.Geometry.Depth}.png",
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            using var bitmap = new Bitmap(size.Width, size.Height);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                _canvas.Render(graphics, size);
            }

            bitmap.Save(dialog.FileName, ImageFormat.Png);
            SetStatus($"Изображение сохранено: {dialog.FileName}", StatusKind.Success);
        }
        catch (Exception exception)
        {
            SetStatus($"Не удалось сохранить изображение: {exception.Message}", StatusKind.Error);
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
}

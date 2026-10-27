using System.Drawing;
using System.Windows.Forms;
using RecursiveLab.App.Views;
using RecursiveLab.App.Views.Individual;

namespace RecursiveLab.App;

/// <summary>
/// Главное окно приложения: навигация по страницам и строка состояния.
/// Страницы не знают друг о друге, поэтому добавление новой страницы
/// не требует изменений в существующих.
/// </summary>
internal sealed class MainForm : Form
{
    private readonly TabControl _tabs = new();
    private readonly ToolStripStatusLabel _statusLabel = new();

    public MainForm()
    {
        Text = "Рекурсивные алгоритмы: фракталы и Ханойская башня";
        BackColor = Theme.Background;
        Font = Theme.BaseFont;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(980, 660);
        Size = new Size(1240, 800);
        DoubleBuffered = true;

        FractalPage = new FractalView();
        HanoiPage = new HanoiView();
        IndividualPage = new IndividualAlgorithmsView();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Theme.Background,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildTabs(), 0, 1);
        root.Controls.Add(BuildStatusStrip(), 0, 2);

        Controls.Add(root);
    }

    /// <summary>Страница фракталов (используется самопроверкой).</summary>
    internal FractalView FractalPage { get; }

    /// <summary>Страница Ханойской башни (используется самопроверкой).</summary>
    internal HanoiView HanoiPage { get; }

    /// <summary>Страница самостоятельных алгоритмов (используется самопроверкой).</summary>
    internal IndividualAlgorithmsView IndividualPage { get; }

    /// <summary>Текст строки состояния главного окна.</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal string StatusText
    {
        get => _statusLabel.Text ?? string.Empty;
        set => _statusLabel.Text = value ?? string.Empty;
    }

    /// <summary>Переключает вкладку главного окна (используется самопроверкой).</summary>
    internal void SelectPage(int index)
    {
        if (index >= 0 && index < _tabs.TabPages.Count)
        {
            _tabs.SelectedIndex = index;
        }
    }

    private Control BuildHeader()
    {
        var header = new Panel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Theme.Surface,
            Padding = new Padding(16, 12, 16, 12),
            Margin = new Padding(0),
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.Surface,
        };

        layout.Controls.Add(new Label
        {
            Text = "Лабораторная работа: рекурсивные алгоритмы",
            Font = Theme.TitleFont,
            ForeColor = Theme.Text,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 2),
        }, 0, 0);

        layout.Controls.Add(new Label
        {
            Text = "Фракталы (треугольник Серпинского, снежинка Коха, фрактальное дерево) · "
                   + "Ханойская башня: рекурсия и решение через явный стек",
            Font = Theme.HintFont,
            ForeColor = Theme.Muted,
            AutoSize = true,
            Margin = new Padding(0),
        }, 0, 1);

        header.Controls.Add(layout);
        return header;
    }

    private Control BuildTabs()
    {
        _tabs.Dock = DockStyle.Fill;
        _tabs.Font = Theme.BaseFont;
        _tabs.Padding = new Point(14, 6);
        _tabs.Margin = new Padding(0);

        _tabs.TabPages.Add(CreatePage("Фракталы", FractalPage));
        _tabs.TabPages.Add(CreatePage("Ханойская башня", HanoiPage));
        _tabs.TabPages.Add(CreatePage("Самостоятельные алгоритмы", IndividualPage));
        _tabs.TabPages.Add(CreatePage("Как это работает", new TheoryView()));

        _tabs.SelectedIndexChanged += (_, _) => UpdateStatusForSelectedTab();
        UpdateStatusForSelectedTab();
        return _tabs;
    }

    private static TabPage CreatePage(string title, Control content)
    {
        var page = new TabPage(title)
        {
            BackColor = Theme.Background,
            Padding = new Padding(0),
            UseVisualStyleBackColor = false,
        };

        content.Dock = DockStyle.Fill;
        page.Controls.Add(content);
        return page;
    }

    private Control BuildStatusStrip()
    {
        var strip = new StatusStrip
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Surface,
            SizingGrip = false,
        };

        _statusLabel.ForeColor = Theme.Muted;
        _statusLabel.Spring = true;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        strip.Items.Add(_statusLabel);
        return strip;
    }

    private void UpdateStatusForSelectedTab()
    {
        StatusText = _tabs.SelectedIndex switch
        {
            0 => "Фракталы: выберите вид, задайте глубину рекурсии и нажмите «Построить».",
            1 => "Ханойская башня: выберите способ решения, нажмите «Решить», затем «Следующий ход» или «Авто».",
            2 => "Самостоятельные алгоритмы: Флойд — Уоршелл, кривая Гильберта и Штрассен.",
            _ => "Справка: описание рекурсивных алгоритмов, базовых случаев и замены рекурсии явным стеком.",
        };
    }
}

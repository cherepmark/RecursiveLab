using System.Windows.Forms;

namespace RecursiveLab.App.Views.Individual;

/// <summary>
/// Страница «Самостоятельные алгоритмы»: три индивидуальных алгоритма лабораторной работы,
/// каждый со своей страницей и собственной визуализацией.
///
/// Все три алгоритма рекурсивные и демонстрируют разные формы рекурсии:
/// троичную (Флойд—Уоршелл), фрактальную с параметрами глубины и ориентации
/// (кривая Гильберта) и «разделяй и властвуй» с ветвлением (Штрассен).
/// </summary>
internal sealed class IndividualAlgorithmsView : UserControl
{
    private readonly TabControl _tabs = new();

    public IndividualAlgorithmsView()
    {
        Dock = DockStyle.Fill;
        BackColor = Theme.Background;
        Font = Theme.BaseFont;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.Background,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildTabs(), 0, 1);
        Controls.Add(root);
    }

    internal FloydWarshallView FloydWarshallPage { get; private set; } = null!;

    internal HilbertCurveView HilbertCurvePage { get; private set; } = null!;

    internal StrassenView StrassenPage { get; private set; } = null!;

    /// <summary>Переключает подстраницу (используется самопроверкой).</summary>
    internal void SelectSubPage(int index)
    {
        if (index >= 0 && index < _tabs.TabPages.Count)
        {
            _tabs.SelectedIndex = index;
        }
    }

    private static Control BuildHeader()
    {
        var header = new Panel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Theme.Surface,
            Padding = new Padding(16, 10, 16, 10),
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
            Text = "Самостоятельные алгоритмы: Флойд — Уоршелл, кривая Гильберта, Штрассен",
            Font = Theme.BoldFont,
            ForeColor = Theme.Text,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 2),
        }, 0, 0);

        layout.Controls.Add(new Label
        {
            Text = "Три рекурсивных алгоритма индивидуального задания и их сравнение с нерекурсивными реализациями",
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
        FloydWarshallPage = new FloydWarshallView();
        HilbertCurvePage = new HilbertCurveView();
        StrassenPage = new StrassenView();

        _tabs.Dock = DockStyle.Fill;
        _tabs.Font = Theme.BaseFont;
        _tabs.Padding = new Point(14, 6);
        _tabs.Margin = new Padding(0);

        _tabs.TabPages.Add(CreatePage("Флойд — Уоршелл", FloydWarshallPage));
        _tabs.TabPages.Add(CreatePage("Кривая Гильберта", HilbertCurvePage));
        _tabs.TabPages.Add(CreatePage("Штрассен", StrassenPage));
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
}

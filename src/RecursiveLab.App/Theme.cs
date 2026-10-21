using System.Drawing;

namespace RecursiveLab.App;

/// <summary>
/// Единый визуальный стиль приложения: палитра, шрифты и фабрики элементов управления.
/// Все формы и панели берут цвета и шрифты отсюда, поэтому интерфейс выглядит целостно.
/// </summary>
internal static class Theme
{
    // --- Базовая палитра ---------------------------------------------------
    public static readonly Color Background = Color.FromArgb(243, 245, 249);
    public static readonly Color Surface = Color.White;
    public static readonly Color Border = Color.FromArgb(214, 220, 232);
    public static readonly Color Text = Color.FromArgb(28, 32, 40);
    public static readonly Color Muted = Color.FromArgb(106, 114, 132);
    public static readonly Color Accent = Color.FromArgb(41, 104, 217);
    public static readonly Color AccentDark = Color.FromArgb(30, 78, 168);
    public static readonly Color Success = Color.FromArgb(19, 133, 78);
    public static readonly Color Danger = Color.FromArgb(196, 56, 56);

    // --- Палитра холстов ---------------------------------------------------
    public static readonly Color FractalCanvasBackground = Color.FromArgb(15, 19, 27);
    public static readonly Color FractalStroke = Color.FromArgb(112, 205, 255);
    public static readonly Color FractalFill = Color.FromArgb(72, 150, 240);
    public static readonly Color HanoiCanvasBackground = Color.FromArgb(250, 251, 254);
    public static readonly Color HanoiPeg = Color.FromArgb(150, 122, 84);
    public static readonly Color HanoiBase = Color.FromArgb(120, 96, 66);
    public static readonly Color HanoiHighlight = Color.FromArgb(41, 104, 217);

    /// <summary>Цвета дисков: от самых больших (холодные) к самым маленьким (тёплые).</summary>
    private static readonly Color[] DiskPalette =
    [
        Color.FromArgb(58, 110, 200),
        Color.FromArgb(46, 137, 190),
        Color.FromArgb(41, 158, 163),
        Color.FromArgb(58, 168, 124),
        Color.FromArgb(112, 172, 78),
        Color.FromArgb(176, 168, 62),
        Color.FromArgb(206, 145, 55),
        Color.FromArgb(212, 116, 62),
        Color.FromArgb(204, 88, 88),
        Color.FromArgb(186, 76, 122),
        Color.FromArgb(150, 88, 178),
        Color.FromArgb(114, 96, 190),
    ];

    public static Color DiskColor(int diskSize, int diskCount)
    {
        if (diskCount <= 1)
        {
            return DiskPalette[0];
        }

        double ratio = (diskSize - 1) / (double)(diskCount - 1);
        int index = (int)Math.Round(ratio * (DiskPalette.Length - 1));
        return DiskPalette[Math.Clamp(index, 0, DiskPalette.Length - 1)];
    }

    // --- Шрифты ------------------------------------------------------------
    public static readonly Font BaseFont = new("Segoe UI", 9.75f, FontStyle.Regular, GraphicsUnit.Point);
    public static readonly Font BoldFont = new("Segoe UI", 9.75f, FontStyle.Bold, GraphicsUnit.Point);
    public static readonly Font TitleFont = new("Segoe UI Semibold", 13f, FontStyle.Bold, GraphicsUnit.Point);
    public static readonly Font MonoFont = new("Consolas", 9.5f, FontStyle.Regular, GraphicsUnit.Point);
    public static readonly Font HintFont = new("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);

    /// <summary>Кнопка в едином стиле. primary — акцентная (главное действие).</summary>
    public static Button CreateButton(string text, bool primary = false)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = false,
            Height = 32,
            Font = primary ? BoldFont : BaseFont,
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Accent : Color.FromArgb(236, 240, 247),
            ForeColor = primary ? Color.White : Text,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 8, 0),
            Padding = new Padding(10, 0, 10, 0),
        };

        button.FlatAppearance.BorderColor = primary ? AccentDark : Border;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = primary ? AccentDark : Color.FromArgb(224, 231, 243);

        // Ширина по тексту, но с учётом отступов.
        button.Width = TextRenderer.MeasureText(text, button.Font).Width + 34;
        return button;
    }

    public static Label CreateLabel(string text, bool muted = false, Font? font = null)
        => new()
        {
            Text = text,
            AutoSize = true,
            Font = font ?? BaseFont,
            ForeColor = muted ? Muted : Text,
            Margin = new Padding(0, 8, 8, 0),
            TextAlign = ContentAlignment.MiddleLeft,
        };

    public static Label CreateCaption(string text)
        => new()
        {
            Text = text,
            AutoSize = true,
            Font = BoldFont,
            ForeColor = Text,
            Margin = new Padding(0, 10, 6, 0),
        };

    /// <summary>Разделитель для панелей инструментов.</summary>
    public static Panel CreateSeparator()
        => new()
        {
            Width = 1,
            Height = 26,
            BackColor = Border,
            Margin = new Padding(6, 2, 12, 0),
        };

    /// <summary>Панель с рамкой — используется для блоков настроек и подсказок.</summary>
    public static Panel CreateCard()
        => new()
        {
            BackColor = Surface,
            Padding = new Padding(10),
        };
}

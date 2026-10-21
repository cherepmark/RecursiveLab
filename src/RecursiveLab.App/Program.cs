using System.Windows.Forms;

namespace RecursiveLab.App;

/// <summary>
/// Точка входа приложения.
///
/// Обычный запуск открывает главное окно. Служебный ключ <c>--smoke [папка]</c>
/// запускает встроенную самопроверку: она открывает окно, прогоняет все фракталы
/// и оба решателя Ханойской башни, сохраняет изображения и отчёт, после чего
/// завершает процесс с кодом 0 при успехе.
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        string? smokeDirectory = ParseSmokeDirectory(args);
        if (smokeDirectory is not null)
        {
            var form = new MainForm();
            SmokeTest.Run(form, smokeDirectory);
            Application.Run(form);
            return SmokeTest.Result;
        }

        Application.Run(new MainForm());
        return 0;
    }

    /// <summary>
    /// Разбирает аргументы командной строки: <c>--smoke</c>, <c>--smoke=&lt;папка&gt;</c>
    /// или <c>--smoke &lt;папка&gt;</c>. Возвращает null, если самопроверка не запрошена.
    /// </summary>
    private static string? ParseSmokeDirectory(string[] args)
    {
        const string key = "--smoke";

        for (int i = 0; i < args.Length; i++)
        {
            string argument = args[i];
            if (argument.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    return Path.GetFullPath(args[i + 1]);
                }

                return DefaultSmokeDirectory();
            }

            if (argument.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
            {
                string value = argument[(key.Length + 1)..];
                return string.IsNullOrWhiteSpace(value) ? DefaultSmokeDirectory() : Path.GetFullPath(value);
            }
        }

        return null;
    }

    private static string DefaultSmokeDirectory()
        => Path.Combine(Path.GetTempPath(), "RecursiveLab.Smoke");
}

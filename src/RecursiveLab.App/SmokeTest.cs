using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using RecursiveLab.App.Rendering;
using RecursiveLab.App.Views;
using RecursiveLab.Core.Fractals;
using RecursiveLab.Core.Hanoi;

namespace RecursiveLab.App;

/// <summary>
/// Встроенная самопроверка приложения (<c>--smoke</c>).
///
/// Запускает настоящее окно с настоящим циклом сообщений, прогоняет через интерфейс
/// все фракталы и оба решателя Ханойской башни (включая пошаговый режим, анимацию
/// и автоматическое воспроизведение), сохраняет изображения холстов и отчёт.
/// Так проверяется не только логика, но и связка «интерфейс — алгоритмы».
/// </summary>
internal static class SmokeTest
{
    /// <summary>Код возврата процесса: 0 — все проверки пройдены.</summary>
    public static int Result { get; private set; } = 1;

    /// <summary>Запускает самопроверку после появления окна.</summary>
    public static void Run(MainForm form, string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(form);

        form.Shown += async (_, _) =>
        {
            var report = new List<string>();

            try
            {
                Result = await RunChecksAsync(form, outputDirectory, report);
            }
            catch (Exception exception)
            {
                report.Add($"FAIL | Самопроверка прервана исключением | {exception}");
                Result = 1;
            }
            finally
            {
                TryWriteReport(outputDirectory, report);
                form.Close();
            }
        };
    }

    private static async Task<int> RunChecksAsync(MainForm form, string outputDirectory, List<string> report)
    {
        Directory.CreateDirectory(outputDirectory);

        int failures = 0;
        void Check(string name, bool condition, string details = "")
        {
            if (!condition)
            {
                failures++;
            }

            report.Add($"{(condition ? "OK  " : "FAIL")} | {name}{(details.Length > 0 ? " | " + details : string.Empty)}");
        }

        report.Add($"# Самопроверка приложения RecursiveLab");
        report.Add($"Окно: {form.Width}x{form.Height}");

        SaveFormSnapshot(form, Path.Combine(outputDirectory, "00-main-window.png"), report);

        // -----------------------------------------------------------------
        // Фракталы
        // -----------------------------------------------------------------
        report.Add(string.Empty);
        report.Add("## Фракталы");

        FractalView fractals = form.FractalPage
            ?? throw new InvalidOperationException("Страница фракталов недоступна.");

        foreach (FractalKind kind in Enum.GetValues<FractalKind>())
        {
            IFractal algorithm = FractalCatalog.Get(kind);
            fractals.SelectFractal(kind);

            Check(
                $"[{kind}] выбор в списке и диапазон глубины",
                (fractals.FractalSelector.SelectedItem as IFractal)?.Kind == kind
                && fractals.DepthInput.Minimum == algorithm.MinDepth
                && fractals.DepthInput.Maximum == algorithm.MaxDepth,
                $"глубина: {fractals.DepthInput.Minimum}..{fractals.DepthInput.Maximum}");

            int depth = Math.Min(3, algorithm.MaxDepth);
            int expected = ExpectedPrimitiveCount(kind, depth);
            bool built = fractals.BuildFractal(depth);

            Check(
                $"[{kind}] построение на глубине {depth}",
                built && fractals.Canvas.Geometry is not null && fractals.StatusText.Contains("Готово", StringComparison.Ordinal),
                fractals.StatusText);

            Check(
                $"[{kind}] число примитивов на глубине {depth}",
                fractals.Canvas.Geometry?.PrimitiveCount == expected,
                $"фактически {fractals.Canvas.Geometry?.PrimitiveCount}, ожидалось {expected}");

            RenderProbe.Analysis? small = SaveCanvas(
                fractals.Canvas,
                Path.Combine(outputDirectory, $"fractal-{kind}-d{depth}.png"),
                report);

            Check(
                $"[{kind}] изображение содержит нарисованную фигуру",
                small is { PaintedPixels: > 500 },
                $"{small?.PaintedPixels ?? 0} пикселей фигуры");

            // Фракталы симметричны относительно вертикальной оси: центр тяжести
            // обязан лежать почти точно в середине холста.
            Check(
                $"[{kind}] центр тяжести фигуры по центру холста",
                small is { CenterOfMassX: > 0.492 and < 0.508 },
                $"центр тяжести X = {small?.CenterOfMassX ?? 0:0.####}");

            Check(
                $"[{kind}] баланс левой и правой половин",
                small is { } halves && Math.Abs(halves.HalfBalance) < 0.02,
                $"дисбаланс половин: {small?.HalfBalance * 100 ?? 0:0.##}%");

            // Пиксельное сравнение с зеркалом чувствительно к сглаживанию тонких линий,
            // поэтому допуск здесь мягче — проверка лишь подтверждает отсутствие сдвига.
            Check(
                $"[{kind}] зеркальное сравнение отрисовки",
                small is { MirrorMismatch: < 0.1 },
                $"доля несимметричных пикселей: {small?.MirrorMismatch * 100 ?? 0:0.##}%");

            // Предельная глубина: проверяем и результат, и время построения.
            var stopwatch = Stopwatch.StartNew();
            bool deepBuilt = fractals.BuildFractal(algorithm.MaxDepth);
            stopwatch.Stop();

            Check(
                $"[{kind}] предельная глубина {algorithm.MaxDepth}",
                deepBuilt && fractals.Canvas.Geometry?.PrimitiveCount == ExpectedPrimitiveCount(kind, algorithm.MaxDepth),
                $"{fractals.Canvas.Geometry?.PrimitiveCount} примитивов за {stopwatch.ElapsedMilliseconds} мс");

            Check(
                $"[{kind}] время построения на пределе < 3000 мс",
                stopwatch.ElapsedMilliseconds < 3000,
                $"{stopwatch.ElapsedMilliseconds} мс");

            RenderProbe.Analysis? deepImage = SaveCanvas(
                fractals.Canvas,
                Path.Combine(outputDirectory, $"fractal-{kind}-d{algorithm.MaxDepth}.png"),
                report);

            Check(
                $"[{kind}] фигура не вырождается на предельной глубине",
                deepImage is { PaintedPixels: > 500 },
                $"{deepImage?.PaintedPixels ?? 0} пикселей");

            // Недопустимая глубина: ожидается понятное сообщение, а не падение.
            bool invalidBuild = fractals.BuildFractal(algorithm.MaxDepth + 1);
            Check(
                $"[{kind}] отказ при глубине {algorithm.MaxDepth + 1}",
                !invalidBuild
                && fractals.Canvas.Geometry is null
                && fractals.StatusText.Contains("вне допустимого диапазона", StringComparison.Ordinal),
                fractals.StatusText);

            fractals.ClearCanvas();
            Check(
                $"[{kind}] очистка холста",
                fractals.Canvas.Geometry is null && fractals.StatusText.Contains("очищен", StringComparison.Ordinal));
        }

        // Проверка роста детализации на одном и том же фрактале.
        fractals.SelectFractal(FractalKind.KochSnowflake);
        fractals.BuildFractal(1);
        int shallow = fractals.Canvas.Geometry?.PrimitiveCount ?? 0;
        fractals.BuildFractal(5);
        int deep = fractals.Canvas.Geometry?.PrimitiveCount ?? 0;
        Check("[KochSnowflake] глубина влияет на детализацию", deep > shallow, $"{shallow} → {deep} примитивов");

        // -----------------------------------------------------------------
        // Ханойская башня
        // -----------------------------------------------------------------
        report.Add(string.Empty);
        report.Add("## Ханойская башня");

        HanoiView hanoi = form.HanoiPage
            ?? throw new InvalidOperationException("Страница «Ханойская башня» недоступна.");

        Check(
            "[Hanoi] решения рекурсии и стека совпадают для 10 дисков",
            new RecursiveHanoiSolver().Solve(10).Moves.SequenceEqual(new StackHanoiSolver().Solve(10).Moves));

        foreach (HanoiSolverKind kind in Enum.GetValues<HanoiSolverKind>())
        {
            hanoi.SelectSolver(kind);
            hanoi.DisksInput.Value = 5;

            bool prepared = hanoi.PrepareSolution();
            Check(
                $"[{kind}] подготовка решения для 5 дисков",
                prepared && hanoi.Solution is { MoveCount: 31 } solution && solution.Kind == kind && solution.IsOptimal,
                hanoi.Solution?.ToString() ?? "нет решения");

            Check(
                $"[{kind}] начальное положение",
                hanoi.PuzzleState is { } initial && initial.Height(0) == 5 && initial.MovesApplied == 0,
                hanoi.PuzzleState?.ToString() ?? "нет состояния");

            RenderProbe.Analysis? startImage = SaveCanvas(
                hanoi.Canvas,
                Path.Combine(outputDirectory, $"hanoi-{kind}-initial.png"),
                report);

            // На старте все диски на левом стержне: центр тяжести картинки слева.
            Check(
                $"[{kind}] диски нарисованы на исходном стержне",
                startImage is { PaintedPixels: > 1000, CenterOfMassX: < 0.4 },
                $"пикселей: {startImage?.PaintedPixels ?? 0}, центр тяжести X = {startImage?.CenterOfMassX ?? 0:0.###}");

            Check(
                $"[{kind}] подпись прогресса",
                hanoi.ProgressText.Contains("0 из 31", StringComparison.Ordinal),
                hanoi.ProgressText);

            hanoi.StepOnce(animate: false);
            hanoi.StepOnce(animate: false);
            // Первые два хода для 5 дисков: диск 1 с A на C, затем диск 2 с A на B.
            Check(
                $"[{kind}] пошаговое выполнение (2 хода)",
                hanoi.AppliedMoveCount == 2
                && hanoi.PuzzleState is { } stepped
                && stepped.Height(0) == 3
                && stepped.Height(1) == 1
                && stepped.Height(2) == 1
                && stepped.TopDiskSize(2) == 1
                && stepped.TopDiskSize(1) == 2
                && stepped.IsOrderingValid(),
                $"{hanoi.ProgressText} · {hanoi.PuzzleState}");

            // Один ход с анимацией: ждём, пока её завершит таймер интерфейса.
            hanoi.StepOnce(animate: true);
            bool animated = await WaitUntilAsync(() => hanoi.AppliedMoveCount == 3, 4000);
            Check($"[{kind}] анимация одного хода завершилась", animated, hanoi.ProgressText);

            SaveCanvas(hanoi.Canvas, Path.Combine(outputDirectory, $"hanoi-{kind}-step.png"), report);

            hanoi.RunToEnd();
            Check(
                $"[{kind}] решение доведено до конца",
                hanoi.AppliedMoveCount == 31
                && hanoi.PuzzleState is { } final
                && final.IsSolved(2)
                && final.IsOrderingValid()
                && final.TotalDisks() == 5
                && hanoi.StatusText.Contains("решена", StringComparison.Ordinal),
                hanoi.StatusText);

            Check(
                $"[{kind}] индикация завершения",
                hanoi.ProgressText.Contains("31 из 31", StringComparison.Ordinal),
                hanoi.ProgressText);

            RenderProbe.Analysis? solvedImage = SaveCanvas(
                hanoi.Canvas,
                Path.Combine(outputDirectory, $"hanoi-{kind}-solved.png"),
                report);

            // После решения все диски на правом стержне: центр тяжести справа.
            Check(
                $"[{kind}] диски нарисованы на целевом стержне",
                solvedImage is { PaintedPixels: > 1000, CenterOfMassX: > 0.6 },
                $"пикселей: {solvedImage?.PaintedPixels ?? 0}, центр тяжести X = {solvedImage?.CenterOfMassX ?? 0:0.###}");

            // Отрисовка диска «в полёте» (промежуточный кадр анимации).
            hanoi.PrepareSolution();
            hanoi.Canvas.BeginFlight(hanoi.Solution!.Moves[0]);
            hanoi.Canvas.UpdateFlight(0.5);
            Check(
                $"[{kind}] отрисовка летящего диска",
                hanoi.Canvas.InFlightMove is { Disk: 1, From: 0, To: 2 });
            SaveCanvas(hanoi.Canvas, Path.Combine(outputDirectory, $"hanoi-{kind}-flying.png"), report);
            hanoi.Canvas.EndFlight();
            Check($"[{kind}] анимация остановлена", hanoi.Canvas.InFlightMove is null);

            hanoi.ResetPuzzle();
            Check(
                $"[{kind}] сброс головоломки",
                hanoi.AppliedMoveCount == 0
                && hanoi.PuzzleState is { } reset
                && reset.Height(0) == 5
                && hanoi.StatusText.Contains("Готово к работе", StringComparison.Ordinal),
                hanoi.StatusText);
        }

        // -----------------------------------------------------------------
        // Автоматическое воспроизведение с максимальной скоростью
        // -----------------------------------------------------------------
        hanoi.SelectSolver(HanoiSolverKind.Recursive);
        hanoi.DisksInput.Value = 4;
        hanoi.SpeedBar.Value = hanoi.SpeedBar.Maximum;
        hanoi.PrepareSolution();
        hanoi.ToggleAutoPlay();

        Check("[Авто] воспроизведение запущено", hanoi.AutoButton.Text == "Пауза", hanoi.AutoButton.Text);

        bool autoFinished = await WaitUntilAsync(() => hanoi.StatusText.Contains("решена", StringComparison.Ordinal), 20000);
        Check(
            "[Авто] решение доиграно автоматически",
            autoFinished && hanoi.AppliedMoveCount == 15 && hanoi.PuzzleState!.IsSolved(2),
            $"{hanoi.ProgressText} · {hanoi.StatusText}");

        Check("[Авто] кнопка вернулась в состояние «Авто»", hanoi.AutoButton.Text == "Авто", hanoi.AutoButton.Text);

        hanoi.ResetPuzzle();
        Check("[Авто] сброс после автоматической игры", hanoi.AppliedMoveCount == 0 && hanoi.PuzzleState!.Height(0) == 4);

        report.Add(string.Empty);
        report.Add(failures == 0
            ? "ИТОГ: все проверки пройдены."
            : $"ИТОГ: не пройдено проверок — {failures}.");

        return failures == 0 ? 0 : 1;
    }

    /// <summary>Ожидаемое количество примитивов — независимая формула для контроля результата.</summary>
    private static int ExpectedPrimitiveCount(FractalKind kind, int depth) => kind switch
    {
        FractalKind.SierpinskiTriangle => (int)Math.Pow(3, depth),
        FractalKind.KochSnowflake => 3 * (int)Math.Pow(4, depth),
        FractalKind.FractalTree => (1 << (depth + 1)) - 1,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Неизвестный вид фрактала."),
    };

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, int timeoutMs)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < timeoutMs)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(40);
        }

        return condition();
    }

    private static RenderProbe.Analysis? SaveCanvas(Control canvas, string path, List<string> report)
    {
        Size size = canvas.ClientSize.Width >= 80 && canvas.ClientSize.Height >= 80
            ? canvas.ClientSize
            : new Size(900, 600);

        try
        {
            using var bitmap = new Bitmap(size.Width, size.Height);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                if (canvas is IRenderable renderable)
                {
                    renderable.Render(graphics, size);
                }
                else
                {
                    canvas.DrawToBitmap(bitmap, new Rectangle(Point.Empty, size));
                }
            }

            bitmap.Save(path, ImageFormat.Png);
            report.Add($"     изображение: {Path.GetFileName(path)} ({size.Width}x{size.Height})");

            // 48 верхних строк — служебные подписи холста, они не входят в анализ фигуры.
            return RenderProbe.Analyze(bitmap, canvas.BackColor, skipTopRows: 48);
        }
        catch (Exception exception)
        {
            report.Add($"FAIL | сохранение {Path.GetFileName(path)} | {exception.Message}");
            return null;
        }
    }

    private static void SaveFormSnapshot(MainForm form, string path, List<string> report)
    {
        try
        {
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height));
            bitmap.Save(path, ImageFormat.Png);
            report.Add($"     изображение: {Path.GetFileName(path)} ({form.Width}x{form.Height})");
        }
        catch (Exception exception)
        {
            report.Add($"INFO | снимок главного окна не сохранён | {exception.Message}");
        }
    }

    private static void TryWriteReport(string outputDirectory, List<string> report)
    {
        try
        {
            Directory.CreateDirectory(outputDirectory);
            File.WriteAllLines(Path.Combine(outputDirectory, "smoke-report.txt"), report);
        }
        catch
        {
            // Отчёт — вспомогательный артефакт; его отсутствие не влияет на код возврата.
        }
    }
}

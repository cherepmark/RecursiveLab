using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using RecursiveLab.App.Rendering;
using RecursiveLab.App.Views;
using RecursiveLab.App.Views.Individual;
using RecursiveLab.Core.Algorithms;
using RecursiveLab.Core.Fractals;
using RecursiveLab.Core.Hanoi;
using RecursiveLab.Core.ShortestPaths;

namespace RecursiveLab.App;

/// <summary>
/// Встроенная самопроверка приложения (<c>--smoke</c>).
///
/// Запускает настоящее окно с настоящим циклом сообщений и прогоняет через интерфейс
/// все фракталы, оба решателя Ханойской башни и три самостоятельных алгоритма
/// (включая пошаговый режим, анимацию, автоматическое воспроизведение и переключение
/// режимов Флойда—Уоршелла), сохраняет изображения холстов и отчёт.
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

        report.Add("# Самопроверка приложения RecursiveLab");
        report.Add($"Окно: {form.Width}x{form.Height}");

        SaveFormSnapshot(form, Path.Combine(outputDirectory, "00-main-window.png"), report);

        CheckFractals(form, outputDirectory, report, Check);
        await CheckHanoiAsync(form, outputDirectory, report, Check);
        await CheckIndividualAlgorithmsAsync(form, outputDirectory, report, Check);

        report.Add(string.Empty);
        report.Add(failures == 0
            ? "ИТОГ: все проверки пройдены."
            : $"ИТОГ: не пройдено проверок — {failures}.");

        return failures == 0 ? 0 : 1;
    }

    // ---------------------------------------------------------------------
    // Фракталы
    // ---------------------------------------------------------------------

    private static void CheckFractals(
        MainForm form,
        string outputDirectory,
        List<string> report,
        Action<string, bool, string> check)
    {
        void Check(string name, bool condition, string details = "") => check(name, condition, details);

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
                report,
                SquareAnalysisSize);

            Check(
                $"[{kind}] изображение содержит нарисованную фигуру",
                small is { PaintedPixels: > 500 },
                $"{small?.PaintedPixels ?? 0} пикселей фигуры");

            Check(
                $"[{kind}] фигура занимает холст",
                small is { } filled && Math.Max(filled.PaintedWidthFraction, filled.PaintedHeightFraction) > 0.85,
                $"доли: ширина {small?.PaintedWidthFraction ?? 0:0.###}, высота {small?.PaintedHeightFraction ?? 0:0.###}");

            if (kind == FractalKind.HilbertCurve)
            {
                // Кривая Гильберта заполняет квадрат целиком (каждая клетка посещается ровно
                // один раз), но она не зеркально симметрична — это свойство проверяется ниже.
                Check(
                    $"[{kind}] кривая заполняет квадрат по обеим осям",
                    small is { } square && square.PaintedWidthFraction > 0.82 && square.PaintedHeightFraction > 0.82,
                    $"доли: ширина {small?.PaintedWidthFraction ?? 0:0.###}, высота {small?.PaintedHeightFraction ?? 0:0.###}");
            }
            else
            {
                Check(
                    $"[{kind}] центр тяжести фигуры по центру холста",
                    small is { CenterOfMassX: > 0.492 and < 0.508 },
                    $"центр тяжести X = {small?.CenterOfMassX ?? 0:0.####}");

                Check(
                    $"[{kind}] баланс левой и правой половин",
                    small is { } halves && Math.Abs(halves.HalfBalance) < 0.02,
                    $"дисбаланс половин: {small?.HalfBalance * 100 ?? 0:0.##}%");

                Check(
                    $"[{kind}] зеркальное сравнение отрисовки",
                    small is { MirrorMismatch: < 0.1 },
                    $"доля несимметричных пикселей: {small?.MirrorMismatch * 100 ?? 0:0.##}%");
            }

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

        fractals.SelectFractal(FractalKind.KochSnowflake);
        fractals.BuildFractal(1);
        int shallow = fractals.Canvas.Geometry?.PrimitiveCount ?? 0;
        fractals.BuildFractal(5);
        int deep = fractals.Canvas.Geometry?.PrimitiveCount ?? 0;
        Check("[KochSnowflake] глубина влияет на детализацию", deep > shallow, $"{shallow} → {deep} примитивов");
    }

    // ---------------------------------------------------------------------
    // Ханойская башня
    // ---------------------------------------------------------------------

    private static async Task CheckHanoiAsync(
        MainForm form,
        string outputDirectory,
        List<string> report,
        Action<string, bool, string> check)
    {
        void Check(string name, bool condition, string details = "") => check(name, condition, details);

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

            Check(
                $"[{kind}] диски нарисованы на исходном стержне",
                startImage is { PaintedPixels: > 1000, CenterOfMassX: < 0.4 },
                $"пикселей: {startImage?.PaintedPixels ?? 0}, центр тяжести X = {startImage?.CenterOfMassX ?? 0:0.###}");

            hanoi.StepOnce(animate: false);
            hanoi.StepOnce(animate: false);
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

            Check(
                $"[{kind}] диски нарисованы на целевом стержне",
                solvedImage is { PaintedPixels: > 1000, CenterOfMassX: > 0.6 },
                $"пикселей: {solvedImage?.PaintedPixels ?? 0}, центр тяжести X = {solvedImage?.CenterOfMassX ?? 0:0.###}");

            hanoi.PrepareSolution();
            hanoi.Canvas.BeginFlight(hanoi.Solution!.Moves[0]);
            hanoi.Canvas.UpdateFlight(0.5);
            Check($"[{kind}] отрисовка летящего диска", hanoi.Canvas.InFlightMove is { Disk: 1, From: 0, To: 2 });
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
    }

    // ---------------------------------------------------------------------
    // Самостоятельные алгоритмы
    // ---------------------------------------------------------------------

    private static async Task CheckIndividualAlgorithmsAsync(
        MainForm form,
        string outputDirectory,
        List<string> report,
        Action<string, bool, string> check)
    {
        void Check(string name, bool condition, string details = "") => check(name, condition, details);

        report.Add(string.Empty);
        report.Add("## Самостоятельные алгоритмы");

        IndividualAlgorithmsView individual = form.IndividualPage
            ?? throw new InvalidOperationException("Страница самостоятельных алгоритмов недоступна.");

        // Переключаем вкладки перед проверками: страницы должны получить реальные размеры,
        // иначе холсты останутся нулевыми и отрисовка окажется вырожденной.
        form.SelectPage(2);
        await SettleAsync();

        individual.SelectSubPage(0);
        await SettleAsync();
        Check(
            "[Флойд—Уоршелл] холст получил размеры после переключения вкладки",
            individual.FloydWarshallPage.DistanceCanvas.ClientSize is { Width: > 200, Height: > 150 },
            $"{individual.FloydWarshallPage.DistanceCanvas.ClientSize.Width}×{individual.FloydWarshallPage.DistanceCanvas.ClientSize.Height}");

        CheckFloydWarshall(individual.FloydWarshallPage, outputDirectory, report, Check);

        individual.SelectSubPage(1);
        await SettleAsync();
        Check(
            "[Гильберт] холст получил размеры после переключения вкладки",
            individual.HilbertCurvePage.Canvas.ClientSize is { Width: > 200, Height: > 150 },
            $"{individual.HilbertCurvePage.Canvas.ClientSize.Width}×{individual.HilbertCurvePage.Canvas.ClientSize.Height}");

        CheckHilbertCurve(individual.HilbertCurvePage, outputDirectory, report, Check);

        individual.SelectSubPage(2);
        await SettleAsync();
        Check(
            "[Штрассен] холст получил размеры после переключения вкладки",
            individual.StrassenPage.ClassicCanvas.ClientSize is { Width: > 200, Height: > 150 },
            $"{individual.StrassenPage.ClassicCanvas.ClientSize.Width}×{individual.StrassenPage.ClassicCanvas.ClientSize.Height}");

        CheckStrassen(individual.StrassenPage, outputDirectory, report, Check);

        form.SelectPage(0);
    }

    /// <summary>Даёт раскладке WinForms примениться после переключения вкладок.</summary>
    private static async Task SettleAsync() => await Task.Delay(250);

    private static void CheckFloydWarshall(
        FloydWarshallView view,
        string outputDirectory,
        List<string> report,
        Action<string, bool, string> check)
    {
        void Check(string name, bool condition, string details = "") => check(name, condition, details);

        FloydWarshallResult reference = FloydWarshall.Solve(WeightedGraph.CreateDemo(), FloydWarshallMode.Iterative);
        view.LoadDemoGraph();

        foreach (FloydWarshallMode mode in Enum.GetValues<FloydWarshallMode>())
        {
            view.SelectMode(mode);
            bool solved = view.SolveSelected();

            Check(
                $"[Флойд—Уоршелл] режим «{FloydWarshall.ModeName(mode)}»",
                solved
                && view.DistanceCanvas.Matrix is not null
                && Matrix.AreEqual(view.DistanceCanvas.Matrix!, reference.Distances)
                && view.InfoText.Contains("совпадение с итеративным алгоритмом: да", StringComparison.Ordinal),
                view.InfoText);
        }

        Check(
            "[Флойд—Уоршелл] список путей заполнен",
            view.PathsList.Items.Count == 30,
            $"строк: {view.PathsList.Items.Count}");

        view.CompareAllModes();
        string comparison = view.ComparisonText;
        Check(
            "[Флойд—Уоршелл] сравнение трёх режимов",
            comparison.Split(Environment.NewLine).Length == 3
            && comparison.Split("совпадение: да").Length == 4,
            comparison.Replace(Environment.NewLine, " | ", StringComparison.Ordinal));

        RenderProbe.Analysis? distances = SaveCanvas(
            view.DistanceCanvas,
            Path.Combine(outputDirectory, "floyd-warshall-distances.png"),
            report);

        Check(
            "[Флойд—Уоршелл] матрица расстояний отрисована",
            distances is { PaintedPixels: > 5000 },
            $"{distances?.PaintedPixels ?? 0} пикселей");

        // Рекурсия без мемоизации обязана отказаться от слишком большого графа.
        view.SelectMode(FloydWarshallMode.NaiveRecursive);
        view.VerticesInput.Value = WeightedGraph.MaxVertexCount;
        view.LoadRandomGraph();
        bool rejected = view.SolveSelected();

        Check(
            "[Флойд—Уоршелл] отказ при слишком большом графе для 3^n",
            !rejected && view.StatusText.Contains("3^n", StringComparison.Ordinal),
            view.StatusText);

        view.VerticesInput.Value = 6;
        view.LoadDemoGraph();
        view.SelectMode(FloydWarshallMode.Iterative);
        view.SolveSelected();
    }

    private static void CheckHilbertCurve(
        HilbertCurveView view,
        string outputDirectory,
        List<string> report,
        Action<string, bool, string> check)
    {
        void Check(string name, bool condition, string details = "") => check(name, condition, details);

        foreach (int order in new[] { 1, 3, HilbertCurveFractal.MaximumDepth })
        {
            bool built = view.BuildCurve(order);
            int expectedSegments = (1 << (2 * order)) - 1;

            Check(
                $"[Гильберт] построение порядка {order}",
                built
                && view.Canvas.Geometry?.Segments.Count == expectedSegments
                && view.MetricsText.Contains($"4^{order}", StringComparison.Ordinal)
                && view.CheckText.Contains("совпадает", StringComparison.Ordinal),
                $"{view.MetricsText} | {view.CheckText}");
        }

        RenderProbe.Analysis? curve = SaveCanvas(
            view.Canvas,
            Path.Combine(outputDirectory, "hilbert-curve.png"),
            report,
            SquareAnalysisSize);

        Check(
            "[Гильберт] кривая заполняет квадрат",
            curve is { PaintedPixels: > 2000 } filled
            && filled.PaintedWidthFraction > 0.82
            && filled.PaintedHeightFraction > 0.82,
            $"пикселей {curve?.PaintedPixels ?? 0}, доли {curve?.PaintedWidthFraction ?? 0:0.###}×{curve?.PaintedHeightFraction ?? 0:0.###}");

        for (int orientation = 0; orientation < HilbertCurve.OrientationCount; orientation++)
        {
            view.SelectOrientation(orientation);
            bool built = view.BuildCurve(4);

            Check(
                $"[Гильберт] ориентация {orientation}",
                built
                && view.Canvas.Geometry?.Segments.Count == 255
                && view.StatusText.Contains("Готово", StringComparison.Ordinal),
                view.StatusText);
        }

        SaveCanvas(view.Canvas, Path.Combine(outputDirectory, "hilbert-curve-orientation.png"), report);

        bool invalid = view.BuildCurve(HilbertCurveFractal.MaximumDepth + 1);
        Check(
            "[Гильберт] отказ при недопустимом порядке",
            !invalid && view.StatusText.Contains("вне допустимого диапазона", StringComparison.Ordinal),
            view.StatusText);

        view.ClearCurve();
        Check("[Гильберт] очистка холста", view.Canvas.Geometry is null);

        view.SelectOrientation(0);
        view.BuildCurve(5);
    }

    private static void CheckStrassen(
        StrassenView view,
        string outputDirectory,
        List<string> report,
        Action<string, bool, string> check)
    {
        void Check(string name, bool condition, string details = "") => check(name, condition, details);

        view.ThresholdSelector.SelectedIndex = 1;   // порог 2

        foreach (int size in new[] { 2, 8, 17, 32 })
        {
            view.SizeInput.Value = size;
            view.FillRandomMatrices();
            bool multiplied = view.Multiply();

            // Для нечётных размеров матрицы дополняются нулями до степени двойки,
            // поэтому сравнение идёт с кубом дополненного размера.
            int padded = 1;
            while (padded < size)
            {
                padded <<= 1;
            }

            long theoretical = (long)padded * padded * padded;
            bool allZero = view.DifferenceCanvasAllZero;

            Check(
                $"[Штрассен] размер {size}: результат совпадает с классическим",
                multiplied
                && Matrix.AreEqual(view.ClassicProduct, view.StrassenProduct)
                && allZero
                && view.InfoText.Contains("результаты совпадают", StringComparison.Ordinal),
                view.InfoText);

            Check(
                $"[Штрассен] размер {size}: скалярных умножений не больше (дополненный размер {padded})³",
                view.StrassenScalarMultiplications > 0 && view.StrassenScalarMultiplications <= theoretical,
                $"{view.StrassenScalarMultiplications:N0} против {padded}³ = {theoretical:N0}");

            if (size == 32)
            {
                RenderProbe.Analysis? classic = SaveCanvas(
                    view.ClassicCanvas,
                    Path.Combine(outputDirectory, "strassen-classic.png"),
                    report);

                RenderProbe.Analysis? differenceImage = SaveCanvas(
                    view.DifferenceCanvas,
                    Path.Combine(outputDirectory, "strassen-difference.png"),
                    report);

                Check(
                    "[Штрассен] результаты отрисованы",
                    classic is { PaintedPixels: > 5000 } && differenceImage is { PaintedPixels: > 5000 },
                    $"классический {classic?.PaintedPixels ?? 0}, разность {differenceImage?.PaintedPixels ?? 0}");
            }
        }

        // Размер 8 = 2³: при пороге 1 ожидается 7³ = 343 базовых умножения вместо 8³ = 512.
        view.SizeInput.Value = 8;
        view.ThresholdSelector.SelectedIndex = 0;
        view.Multiply();
        Check(
            "[Штрассен] порог 1: 7^k базовых умножений вместо 8^k",
            view.StrassenScalarMultiplications == 343 && view.StrassenRecursiveCalls == 57,
            $"{view.StrassenScalarMultiplications} базовых умножений, {view.StrassenRecursiveCalls} рекурсивных вызовов");

        Check(
            "[Штрассен] порог 1: матрица разности нулевая",
            view.DifferenceCanvasAllZero,
            "все элементы равны нулю");

        view.ThresholdSelector.SelectedIndex = 1;
        view.FillRandomMatrices();
    }

    // ---------------------------------------------------------------------
    // Вспомогательные методы
    // ---------------------------------------------------------------------

    /// <summary>
    /// Квадратный размер для анализа пикселей фракталов: на квадратном холсте доли
    /// закрашенной области не зависят от пропорций окна, поэтому проверки устойчивы.
    /// </summary>
    private static readonly Size SquareAnalysisSize = new(600, 600);

    /// <summary>Ожидаемое количество примитивов — независимая формула для контроля результата.</summary>
    private static int ExpectedPrimitiveCount(FractalKind kind, int depth) => kind switch
    {
        FractalKind.SierpinskiTriangle => (int)Math.Pow(3, depth),
        FractalKind.KochSnowflake => 3 * (int)Math.Pow(4, depth),
        FractalKind.FractalTree => (1 << (depth + 1)) - 1,
        FractalKind.HilbertCurve => (1 << (2 * depth)) - 1,
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

    private static RenderProbe.Analysis? SaveCanvas(
        Control canvas,
        string path,
        List<string> report,
        Size? sizeOverride = null)
    {
        Size size = sizeOverride
            ?? (canvas.ClientSize.Width >= 80 && canvas.ClientSize.Height >= 80
                ? canvas.ClientSize
                : new Size(900, 600));

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

            // Верхние строки — служебные подписи холста, они не входят в анализ фигуры.
            return RenderProbe.Analyze(bitmap, canvas.BackColor, skipTopRows: 32);
        }
        catch (Exception exception)
        {
            string frames = exception.StackTrace is null
                ? "нет трассировки"
                : string.Join(" <- ", exception.StackTrace
                    .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                    .Take(4)
                    .Select(line => line.Trim()));

            report.Add($"FAIL | сохранение {Path.GetFileName(path)} | {exception.GetType().Name}: {exception.Message} | {frames}");
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

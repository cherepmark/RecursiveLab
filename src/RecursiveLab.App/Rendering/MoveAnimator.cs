using System.Diagnostics;
using System.Windows.Forms;

namespace RecursiveLab.App.Rendering;

/// <summary>
/// Аниматор одного перемещения диска.
///
/// Работает на таймере WinForms, поэтому интерфейс не блокируется: показ
/// воспроизведения идёт по кадрам, и пользователь может продолжать работу с окном.
/// </summary>
internal sealed class MoveAnimator : IDisposable
{
    private const int FrameIntervalMs = 16;

    private readonly System.Windows.Forms.Timer _timer;
    private readonly Stopwatch _stopwatch = new();
    private int _durationMs = 200;

    public MoveAnimator()
    {
        _timer = new System.Windows.Forms.Timer { Interval = FrameIntervalMs };
        _timer.Tick += OnTick;
    }

    /// <summary>Доля выполнения текущего перемещения: 0..1.</summary>
    public double Progress { get; private set; }

    public bool IsRunning => _timer.Enabled;

    /// <summary>Вызывается на каждом кадре с текущим значением <see cref="Progress"/>.</summary>
    public event Action<double>? ProgressChanged;

    /// <summary>Вызывается один раз по завершении перемещения.</summary>
    public event Action? Completed;

    /// <summary>Запускает анимацию заданной длительности.</summary>
    public void Start(int durationMs)
    {
        Stop();

        _durationMs = Math.Max(FrameIntervalMs, durationMs);
        Progress = 0;
        _stopwatch.Restart();
        _timer.Start();
    }

    /// <summary>Немедленно завершает анимацию (без вызова <see cref="Completed"/>).</summary>
    public void Stop()
    {
        if (_timer.Enabled)
        {
            _timer.Stop();
        }

        _stopwatch.Reset();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        double elapsed = _stopwatch.Elapsed.TotalMilliseconds;
        Progress = Math.Clamp(elapsed / _durationMs, 0, 1);
        ProgressChanged?.Invoke(Progress);

        if (Progress >= 1)
        {
            _timer.Stop();
            _stopwatch.Reset();
            Completed?.Invoke();
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
    }
}

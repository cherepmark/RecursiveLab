namespace RecursiveLab.Core.Hanoi;

/// <summary>
/// Одно перемещение диска: со стержня <see cref="From"/> на стержень <see cref="To"/>.
/// <see cref="Disk"/> — номер (размер) диска; 1 — самый маленький.
/// </summary>
public readonly record struct HanoiMove(int From, int To, int Disk)
{
    /// <summary>Попытка перенести диск на тот же стержень бессмысленна.</summary>
    public bool IsSelfMove => From == To;

    /// <summary>Обратное перемещение (используется при сбросе/анимации).</summary>
    public HanoiMove Reversed() => new(To, From, Disk);

    public override string ToString()
        => $"{HanoiPegs.Name(From)} → {HanoiPegs.Name(To)} (диск {Disk})";
}

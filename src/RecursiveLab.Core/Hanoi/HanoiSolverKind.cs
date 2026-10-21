namespace RecursiveLab.Core.Hanoi;

/// <summary>Способ решения головоломки «Ханойская башня».</summary>
public enum HanoiSolverKind
{
    /// <summary>Классический рекурсивный алгоритм (системный стек вызовов).</summary>
    Recursive = 0,

    /// <summary>Нерекурсивный алгоритм с явным стеком кадров.</summary>
    ExplicitStack = 1,
}

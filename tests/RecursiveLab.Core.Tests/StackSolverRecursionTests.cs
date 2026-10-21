using System.Reflection;
using RecursiveLab.Core.Hanoi;

namespace RecursiveLab.Core.Tests;

/// <summary>
/// Доказательство того, что стековый решатель действительно НЕ использует рекурсию.
///
/// Проверка выполняется на уровне байт-кода CIL: анализируется IL метода Solve и
/// проверяется, что среди инструкций call/callvirt нет вызова самого метода,
/// зато есть создание объекта <c>System.Collections.Generic.Stack&lt;T&gt;</c> —
/// то есть системный стек вызовов заменён явной структурой данных.
///
/// Рекурсивный решатель используется как положительный контроль: анализатор
/// обязан найти рекурсивный вызов в нём. Это исключает ситуацию, когда тест
/// «проходит» из-за ошибки в самом анализаторе.
/// </summary>
public class StackSolverRecursionTests
{
    [Fact]
    public void RecursiveSolver_ContainsSelfCall_PositiveControlForTheInspector()
    {
        // Рекурсивный алгоритм обязательно содержит метод, вызывающий сам себя
        // (Solve -> MoveTower -> MoveTower). Это доказывает, что анализатор IL
        // действительно умеет находить рекурсивные вызовы.
        bool foundSelfCall = IlRecursionInspector
            .DeclaredMethods(typeof(RecursiveHanoiSolver))
            .Any(IlRecursionInspector.CallsItself);

        Assert.True(
            foundSelfCall,
            "Анализатор IL не нашёл рекурсивный вызов в RecursiveHanoiSolver — " +
            "значит, проверка стекового решателя недостоверна.");
    }

    [Fact]
    public void StackSolver_Solve_DoesNotCallItself()
    {
        MethodInfo solve = typeof(StackHanoiSolver).GetMethod(nameof(StackHanoiSolver.Solve))!;

        Assert.False(
            IlRecursionInspector.CallsItself(solve),
            "StackHanoiSolver.Solve содержит вызов самого себя — рекурсия запрещена в стековой реализации.");
    }

    [Fact]
    public void StackSolver_HasNoRecursiveMethodsAtAll()
    {
        foreach (MethodBase method in IlRecursionInspector.DeclaredMethods(typeof(StackHanoiSolver)))
        {
            Assert.False(
                IlRecursionInspector.CallsItself(method),
                $"Метод {method.Name} вызывает сам себя — в стековой реализации не должно быть рекурсии.");
        }
    }

    [Fact]
    public void StackSolver_UsesExplicitStackType()
    {
        var foundStacks = new List<string>();

        foreach (MethodBase method in IlRecursionInspector.DeclaredMethods(typeof(StackHanoiSolver)))
        {
            foreach (string member in IlRecursionInspector.ResolveCalledMembers(method))
            {
                if (member.Contains("System.Collections.Generic.Stack`1", StringComparison.Ordinal))
                {
                    foundStacks.Add($"{method.Name} -> {member}");
                }
            }
        }

        Assert.NotEmpty(foundStacks);
    }

    [Fact]
    public void Inspector_SeesOnlyNonRecursiveCallsInStackSolverSolve()
    {
        MethodInfo solve = typeof(StackHanoiSolver).GetMethod(nameof(StackHanoiSolver.Solve))!;
        IReadOnlyList<string> calledMembers = IlRecursionInspector.ResolveCalledMembers(solve);

        // В IL должны присутствовать вызовы конструктора List<HanoiMove> и самого стека,
        // но ни одного вызова «рекурсивных» методов этого же класса.
        Assert.Contains(calledMembers, name => name.Contains("System.Collections.Generic.List`1", StringComparison.Ordinal));
        Assert.Contains(calledMembers, name => name.Contains("System.Collections.Generic.Stack`1", StringComparison.Ordinal));
        Assert.DoesNotContain(calledMembers, name => name.Contains($"{nameof(StackHanoiSolver)}.Solve", StringComparison.Ordinal));
        Assert.DoesNotContain(calledMembers, name => name.Contains(".MoveTower", StringComparison.Ordinal));
    }
}

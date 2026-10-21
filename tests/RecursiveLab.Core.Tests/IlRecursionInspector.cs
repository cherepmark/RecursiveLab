using System.Reflection;

namespace RecursiveLab.Core.Tests;

/// <summary>
/// Разбор байт-кода CIL для доказательства того, что стековый решатель
/// действительно НЕ вызывает сам себя (то есть не является рекурсивным).
///
/// Метод работает без внешних зависимостей: в IL инструкции call/callvirt/newobj
/// имеют однобайтовый код операции и 4-байтовый операнд-токен (метаданные .NET),
/// поэтому достаточно найти их и попытаться разрешить токен через рефлексию.
/// Разрешение токена отбрасывает случайные совпадения байтов, а «положительный
/// контроль» на рекурсивном алгоритме подтверждает, что детектор действительно
/// находит рекурсивные вызовы.
/// </summary>
internal static class IlRecursionInspector
{
    private const byte CallOpCode = 0x28;
    private const byte CallVirtOpCode = 0x6F;
    private const byte NewObjOpCode = 0x73;

    /// <summary>Байты IL метода (пустой массив для методов без тела).</summary>
    public static byte[] GetInstructions(MethodBase method)
    {
        ArgumentNullException.ThrowIfNull(method);
        return method.GetMethodBody()?.GetILAsByteArray() ?? [];
    }

    /// <summary>
    /// Кандидаты-токены вызовов: все позиции, где встречается код call/callvirt/newobj.
    /// </summary>
    public static IReadOnlyList<int> FindCallTokens(byte[] il)
    {
        var tokens = new List<int>();
        for (int i = 0; i + 4 < il.Length; i++)
        {
            byte opCode = il[i];
            if (opCode is CallOpCode or CallVirtOpCode or NewObjOpCode)
            {
                int token = BitConverter.ToInt32(il, i + 1);
                if (token > 0)
                {
                    tokens.Add(token);
                }
            }
        }

        return tokens;
    }

    /// <summary>Все члены, которые удалось разрешить из токенов вызовов данного метода.</summary>
    public static IReadOnlyList<string> ResolveCalledMembers(MethodBase method)
    {
        byte[] il = GetInstructions(method);
        var names = new List<string>();
        Module module = method.Module;

        foreach (int token in FindCallTokens(il))
        {
            try
            {
                MemberInfo? member = module.ResolveMember(token);
                if (member is not null)
                {
                    string fullName = member is MethodBase calledMethod
                        ? $"{calledMethod.DeclaringType?.FullName}.{member.Name}"
                        : $"{member.DeclaringType?.FullName}.{member.Name}";
                    names.Add(fullName);
                }
            }
            catch (Exception ex) when (ex is ArgumentException or BadImageFormatException or NotSupportedException)
            {
                // Токен не является допустимой ссылкой на метаданные — это не вызов, пропускаем.
            }
        }

        return names;
    }

    /// <summary>
    /// true, если в IL метода найден вызов самого себя (то есть метод рекурсивный).
    /// </summary>
    public static bool CallsItself(MethodBase method)
    {
        int selfToken = method.MetadataToken;
        foreach (int token in FindCallTokens(GetInstructions(method)))
        {
            if (token == selfToken)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Публичные и приватные методы типа, объявленные в исходном коде (без служебных).</summary>
    public static IEnumerable<MethodBase> DeclaredMethods(Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic
                                   | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (MethodBase method in type.GetMethods(flags))
        {
            if (!method.Name.Contains('<', StringComparison.Ordinal))
            {
                yield return method;
            }
        }

        foreach (ConstructorInfo ctor in type.GetConstructors(flags))
        {
            if (!ctor.Name.Contains('<', StringComparison.Ordinal))
            {
                yield return ctor;
            }
        }
    }
}

using System;

namespace ManagedCode.Orleans.Graph;

internal static class TypeExtensions
{
    public static string GetTypeName(this Type type)
    {
        return type.FullName ?? type.Name;
    }
}

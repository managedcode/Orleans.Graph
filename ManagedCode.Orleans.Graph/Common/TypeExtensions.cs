namespace ManagedCode.Orleans.Graph;

internal static class TypeExtensions
{
    public static string GetTypeName(this Type type)
    {
        if (!string.IsNullOrWhiteSpace(type.FullName))
        {
            return type.FullName;
        }

        throw new InvalidOperationException($"Unable to resolve full name for grain type {type}.");
    }
}

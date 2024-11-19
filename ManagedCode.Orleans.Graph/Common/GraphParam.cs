namespace ManagedCode.Orleans.Graph.Interfaces;

public static class GraphParam
{
    public static T Any<T>()
    {
        return default(T)!;
    }
    
    // public static dynamic Any()
    // {
    //     return default;
    // }
}
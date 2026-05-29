namespace ManagedCode.Orleans.Graph.Models;

internal readonly record struct ObservedGrainCallKey(string Source, string Target, string SourceMethod, string TargetMethod)
{
    public static ObservedGrainCallKey From(ObservedGrainCallEdge edge) =>
        new(edge.Source, edge.Target, edge.SourceMethod, edge.TargetMethod);
}

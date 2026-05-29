namespace ManagedCode.Orleans.Graph.Models;

internal readonly record struct ObservedGrainCallKey(string Source, string Target, string SourceMethod, string TargetMethod)
{
    public static ObservedGrainCallKey From(ObservedGrainCall call) =>
        new(call.Source, call.Target, call.SourceMethod, call.TargetMethod);
}

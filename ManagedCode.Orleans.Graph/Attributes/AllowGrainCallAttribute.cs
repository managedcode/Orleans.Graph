using System;

namespace ManagedCode.Orleans.Graph.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true, Inherited = false)]
public sealed class AllowGrainCallAttribute(Type targetGrainType) : Attribute
{
    public Type TargetGrainType { get; } = targetGrainType;

    public bool AllowAllMethods { get; init; } = true;

    public bool AllowReentrancy { get; init; }

    public string[] SourceMethods { get; init; } = Array.Empty<string>();

    public string[] TargetMethods { get; init; } = Array.Empty<string>();
}

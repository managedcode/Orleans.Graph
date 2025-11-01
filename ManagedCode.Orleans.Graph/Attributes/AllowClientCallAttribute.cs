using System;

namespace ManagedCode.Orleans.Graph.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true, Inherited = false)]
public sealed class AllowClientCallAttribute : Attribute
{
    public AllowClientCallAttribute()
    {
    }

    public AllowClientCallAttribute(Type grainType)
    {
        GrainType = grainType;
    }

    public Type? GrainType { get; }
}

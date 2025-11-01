using System;

namespace ManagedCode.Orleans.Graph.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class AllowSelfReentrancyAttribute : Attribute
{
}

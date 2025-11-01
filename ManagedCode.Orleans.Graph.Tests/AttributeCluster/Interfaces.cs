using ManagedCode.Orleans.Graph.Attributes;

namespace ManagedCode.Orleans.Graph.Tests.AttributeCluster;

[AllowClientCall]
[AllowGrainCall(typeof(IAttributeClusterGrainB), AllowAllMethods = true, AllowReentrancy = true)]
public interface IAttributeClusterGrainA : IGrainWithStringKey
{
    Task<int> CallB();
    Task<int> CallC();
}

[AllowClientCall]
[AllowSelfReentrancy]
public interface IAttributeClusterGrainB : IGrainWithStringKey
{
    Task<int> MethodB();
    Task<int> ReentrantCall();
}

public interface IAttributeClusterGrainC : IGrainWithStringKey
{
    Task<int> MethodC();
}

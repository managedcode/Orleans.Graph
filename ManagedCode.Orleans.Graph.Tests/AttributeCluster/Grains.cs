using Orleans.Concurrency;

namespace ManagedCode.Orleans.Graph.Tests.AttributeCluster.Grains;

public class AttributeClusterGrainA : Grain, IAttributeClusterGrainA
{
    public async Task<int> CallB()
    {
        return await GrainFactory.GetGrain<IAttributeClusterGrainB>(this.GetPrimaryKeyString()).MethodB();
    }

    public async Task<int> CallC()
    {
        return await GrainFactory.GetGrain<IAttributeClusterGrainC>(this.GetPrimaryKeyString()).MethodC();
    }
}

[Reentrant]
public class AttributeClusterGrainB : Grain, IAttributeClusterGrainB
{
    public Task<int> MethodB()
    {
        return Task.FromResult(1);
    }

    public async Task<int> ReentrantCall()
    {
        // Reentrant self call should be allowed due to AllowSelfReentrancy
        return await GrainFactory.GetGrain<IAttributeClusterGrainB>(this.GetPrimaryKeyString()).MethodB();
    }
}

public class AttributeClusterGrainC : Grain, IAttributeClusterGrainC
{
    public Task<int> MethodC()
    {
        return Task.FromResult(1);
    }
}

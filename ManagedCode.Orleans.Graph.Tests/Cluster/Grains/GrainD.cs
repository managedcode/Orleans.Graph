using ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;

namespace ManagedCode.Orleans.Graph.Tests.Cluster.Grains;

public class GrainD : Grain, IGrainD
{
    public async Task<int> MethodD1(int input)
    {
        return await Task.FromResult(input + 1);
    }

    public async Task<int> MethodE2(int input)
    {
        return await GrainFactory.GetGrain<IGrainE>(this.GetPrimaryKeyString()).MethodE1(input);
    }
}
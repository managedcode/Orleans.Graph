using ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;

namespace ManagedCode.Orleans.Graph.Tests.Cluster.Grains;

public class GrainA : Grain, IGrainA
{
    public async Task<int> MethodA1(int input)
    {
        return await Task.FromResult(input + 1);
    }

    public async Task<int> MethodB1(int input)
    {
        return await GrainFactory.GetGrain<IGrainB>(this.GetPrimaryKeyString()).MethodB1(input);
    }

    public async Task<int> MethodB2(int input)
    {
        return await GrainFactory.GetGrain<IGrainB>(this.GetPrimaryKeyString()).MethodC2(input);
    }

    public async Task<int> MethodC1(int input)
    {
        return await GrainFactory.GetGrain<IGrainC>(this.GetPrimaryKeyString()).MethodC1(input);
    }
}

public class GrainB : Grain, IGrainB
{
    public async Task<int> MethodB1(int input)
    {
        return await Task.FromResult(input + 1);
    }

    public async Task<int> MethodC2(int input)
    {
        return await GrainFactory.GetGrain<IGrainC>(this.GetPrimaryKeyString()).MethodA2(input);
    }
}

public class GrainC : Grain, IGrainC
{
    public async Task<int> MethodC1(int input)
    {
        return await Task.FromResult(input + 1);
    }

    public async Task<int> MethodA2(int input)
    {
        return await GrainFactory.GetGrain<IGrainA>(this.GetPrimaryKeyString()).MethodA1(input);
    }
    
}

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

public class GrainE : Grain, IGrainE
{
    public async Task<int> MethodE1(int input)
    {
        return await Task.FromResult(input + 1);
    }

    public async Task<int> MethodD2(int input)
    {
        return await GrainFactory.GetGrain<IGrainD>(this.GetPrimaryKeyString()).MethodD1(input);
    }
}


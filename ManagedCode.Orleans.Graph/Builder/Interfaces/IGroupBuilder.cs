using Orleans;

namespace ManagedCode.Orleans.Graph;

public interface IGroupBuilder
{
    IGroupBuilder AddGrain<TGrain>() where TGrain : IGrain;
    IGroupBuilder AllowCallsWithin();
    IGrainCallsBuilder And();
}
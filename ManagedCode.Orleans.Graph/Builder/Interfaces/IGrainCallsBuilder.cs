using Orleans;

namespace ManagedCode.Orleans.Graph;

public interface IGrainCallsBuilder
{
    ITransitionBuilder From<TGrain>() where TGrain : IGrain;  
    IGroupBuilder Group(string name);
    IGrainCallsBuilder AllowAll(); 
    IGrainCallsBuilder DisallowAll();
    IGrainCallsBuilder AddGrain<TGrain>() where TGrain : IGrain;
    GrainGraphManager Build();
}
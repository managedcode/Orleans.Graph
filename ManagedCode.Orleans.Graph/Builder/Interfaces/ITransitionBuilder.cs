using Orleans;

namespace ManagedCode.Orleans.Graph;

public interface ITransitionBuilder
{
    IMethodBuilder To<TGrain>() where TGrain : IGrain;
    IGrainCallsBuilder And(); 
}
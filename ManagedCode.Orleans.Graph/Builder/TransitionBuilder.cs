using System;
using Orleans;

namespace ManagedCode.Orleans.Graph;

public class TransitionBuilder : ITransitionBuilder
{
    private readonly GrainCallsBuilder _parent;
    private readonly string _sourceType;

    public TransitionBuilder(GrainCallsBuilder parent, string sourceType)
    {
        _parent = parent;
        _sourceType = sourceType;
    }

    public IMethodBuilder To<TGrain>() where TGrain : IGrain
    {
        return new MethodBuilder(_parent, _sourceType, typeof(TGrain).FullName);
    }

    public IGrainCallsBuilder And() => _parent;
}
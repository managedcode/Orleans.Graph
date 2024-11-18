using System;
using Orleans;

namespace ManagedCode.Orleans.Graph;

public class TransitionBuilder : ITransitionBuilder
{
    private readonly GrainCallsBuilder _parent;
    private readonly Type _sourceType;

    public TransitionBuilder(GrainCallsBuilder parent, Type sourceType)
    {
        _parent = parent;
        _sourceType = sourceType;
    }

    public IMethodBuilder To<TGrain>() where TGrain : IGrain
    {
        return new MethodBuilder(_parent, _sourceType, typeof(TGrain));
    }

    public IGrainCallsBuilder And() => _parent;
}
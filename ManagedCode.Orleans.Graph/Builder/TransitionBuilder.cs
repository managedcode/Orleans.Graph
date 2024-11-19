using System;
using Orleans;

namespace ManagedCode.Orleans.Graph;

public class TransitionBuilder<TFrom> : ITransitionBuilder<TFrom> where TFrom : IGrain
{
    private readonly GrainCallsBuilder _parent;
    private readonly string _sourceType;

    public TransitionBuilder(GrainCallsBuilder parent, string sourceType)
    {
        _parent = parent;
        _sourceType = sourceType;
    }

    public IMethodBuilder<TFrom, TGrain> To<TGrain>() where TGrain : IGrain
    {
        return new MethodBuilder<TFrom, TGrain>(_parent, _sourceType, typeof(TGrain).FullName);
    }

    public IGrainCallsBuilder And() => _parent;
}
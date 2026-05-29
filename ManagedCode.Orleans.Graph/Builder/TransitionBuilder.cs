namespace ManagedCode.Orleans.Graph;

public class TransitionBuilder<TFrom>(GrainCallsBuilder parent, string sourceType) : ITransitionBuilder<TFrom> where TFrom : IGrain
{
    private readonly GrainCallsBuilder _parent = parent;
    private readonly string _sourceType = sourceType;

    public IMethodBuilder<TFrom, TGrain> To<TGrain>() where TGrain : IGrain
    {
        return new MethodBuilder<TFrom, TGrain>(_parent, _sourceType, typeof(TGrain).GetTypeName());
    }

    public IGrainCallsBuilder And() => _parent;
}

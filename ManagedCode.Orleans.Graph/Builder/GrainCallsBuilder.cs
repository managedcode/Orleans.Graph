using System;
using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Models;
using Orleans;

namespace ManagedCode.Orleans.Graph;

[GrainGraphConfiguration]
public class GrainCallsBuilder(bool allowSelfLoops = true) : IGrainCallsBuilder
{
    private readonly DirectedGraph _graph = new(allowSelfLoops);
    private bool _allowAllByDefault;

    public IGrainCallsBuilder AllowClientCallGrain<TGrain>() where TGrain : IGrain
    {
        AddTransition(Constants.ClientCallerId, typeof(TGrain).FullName!);
        return this;
    }

    internal void AllowClientCall(Type grainType)
    {
        ArgumentNullException.ThrowIfNull(grainType);

        AddTransition(Constants.ClientCallerId, grainType.GetTypeName());
    }

    public ITransitionBuilder<TGrain> From<TGrain>() where TGrain : IGrain
    {
        return new TransitionBuilder<TGrain>(this, typeof(TGrain).FullName!);
    }

    public IMethodBuilder<TGrain, TGrain> AddGrain<TGrain>() where TGrain : IGrain
    {
        return From<TGrain>().To<TGrain>();
    }

    public IMethodBuilder<TFrom, TTo> AddGrainTransition<TFrom, TTo>() where TFrom : IGrain where TTo : IGrain
    {
        return From<TFrom>().To<TTo>();
    }

    public IGrainCallsBuilder And()
    {
        return this;
    }

    public IGrainCallsBuilder AllowAll()
    {
        _allowAllByDefault = true;
        return this;
    }

    public IGrainCallsBuilder DisallowAll()
    {
        _allowAllByDefault = false;
        return this;
    }

    internal void AddTransition(string source, string target)
    {
        _graph.AddTransition(source, target, new GrainTransition("*", "*"));
    }

    internal void AddMethodRule(string source, string target, string sourceMethod, string targetMethod)
    {
        _graph.AddTransition(source, target, new GrainTransition(sourceMethod, targetMethod));
    }

    internal void AddReentrancy(string source, string target)
    {
        _graph.AddTransition(source, target, new GrainTransition("*", "*", IsReentrant: true));
    }

    public GrainTransitionManager Build()
    {
        ValidateCycles();
        return new GrainTransitionManager(_graph, _allowAllByDefault);
    }

    private void ValidateCycles()
    {
        foreach (var edge in _graph.GetAllEdges())
        {
            foreach (var transition in edge.Transitions)
            {
                if (transition.IsReentrant)
                {
                    continue;
                }

                if (_graph.HasCycle())
                {
                    throw new InvalidOperationException($"Cycle detected involving grain {edge.Source}");
                }
            }
        }
    }

    public static IGrainCallsBuilder Create(bool allowSelfLoops = true)
    {
        return new GrainCallsBuilder(allowSelfLoops);
    }
}

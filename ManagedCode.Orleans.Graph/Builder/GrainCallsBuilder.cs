using System;
using System.Collections.Generic;
using Orleans;

namespace ManagedCode.Orleans.Graph;

public class GrainCallsBuilder : IGrainCallsBuilder
{
    private readonly DirectedGraph<Type> _graph = new(allowSelfLoops: true);
    private readonly Dictionary<string, HashSet<Type>> _groups = new();
    private readonly HashSet<(Type Source, Type Target, string SourceMethod, string TargetMethod)> _methodRules = new();
    private readonly HashSet<(Type Source, Type Target)> _reentrancyRules = new();
    private bool _allowAllByDefault;

    public ITransitionBuilder From<TGrain>() where TGrain : IGrain 
    {
        return new TransitionBuilder(this, typeof(TGrain));
    }

    public IGroupBuilder Group(string name)
    {
        _groups.TryAdd(name, new HashSet<Type>());
        return new GroupBuilder(this, name);
    }

    public IGrainCallsBuilder AddGrain<TGrain>() where TGrain : IGrain
    {
        _graph.AddVertex(typeof(TGrain));
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

    internal void AddTransition(Type source, Type target)
    {
        _graph.AddEdge(source, target);
    }

    internal void AddMethodRule(Type source, Type target, string sourceMethod, string targetMethod)
    {
        _methodRules.Add((source, target, sourceMethod, targetMethod));
    }

    internal void AddReentrancy(Type source, Type target)
    {
        _reentrancyRules.Add((source, target));
    }

    public GrainGraphManager Build()
    {
        var manager = new GrainGraphManager();
        foreach (var edge in _graph.Edges)
        {
            manager.AddAllowedTransition(edge.Source, edge.Target);
        }
        return manager;
    }
}
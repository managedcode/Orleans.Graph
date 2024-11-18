using System;
using System.Collections.Generic;
using ManagedCode.Orleans.Graph.Models;
using Orleans;

namespace ManagedCode.Orleans.Graph;

public class GrainCallsBuilder : IGrainCallsBuilder
{
    private readonly Dictionary<string, HashSet<string>> _groups = new();
    private readonly DirectedGraph _graph;
    private bool _allowAllByDefault;

    public GrainCallsBuilder(bool allowSelfLoops = true)
    {
        _graph = new DirectedGraph(allowSelfLoops);
    }

    public ITransitionBuilder From<TGrain>() where TGrain : IGrain
    {
        return new TransitionBuilder(this, typeof(TGrain).FullName);
    }

    public IGroupBuilder Group(string name)
    {
        _groups.TryAdd(name, new HashSet<string>());
        return new GroupBuilder(this, name);
    }

    public IGrainCallsBuilder AddGrain<TGrain>() where TGrain : IGrain
    {
        _graph.AddVertex(typeof(TGrain).FullName);
        return this;
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
        _graph.AddTransition(source, target, new GrainTransition(string.Intern("*"), string.Intern("*")));
    }

    internal void AddMethodRule(string source, string target, string sourceMethod, string targetMethod)
    {
        _graph.AddTransition(source, target, new GrainTransition(sourceMethod, targetMethod));
    }

    internal void AddReentrancy(string source, string target)
    {
        _graph.AddTransition(source, target, new GrainTransition(string.Intern("*"), string.Intern("*"), IsReentrant: true));
    }

    public GrainGraphManager Build()
    {
        ValidateCycles();
        return new GrainGraphManager(_graph);
    }

    private void ValidateCycles()
    {
        foreach (var edge in _graph.GetAllEdges())
        {
            if (_graph.HasCycle())
            {
                throw new InvalidOperationException($"Cycle detected involving grain {edge.Source}");
            }
        }
    }

    public static IGrainCallsBuilder Create(bool allowSelfLoops = true)
    {
        return new GrainCallsBuilder(allowSelfLoops);
    }
}
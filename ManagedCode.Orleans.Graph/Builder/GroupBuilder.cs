using System;
using System.Collections.Generic;
using Orleans;

namespace ManagedCode.Orleans.Graph;

public class GroupBuilder : IGroupBuilder
{
    private readonly GrainCallsBuilder _parent;
    private readonly string _groupName;
    private readonly HashSet<Type> _groupGrains = new();

    public GroupBuilder(GrainCallsBuilder parent, string groupName)
    {
        _parent = parent;
        _groupName = groupName;
    }

    public IGroupBuilder AddGrain<TGrain>() where TGrain : IGrain
    {
        _groupGrains.Add(typeof(TGrain));
        return this;
    }

    public IGroupBuilder AllowCallsWithin()
    {
        foreach (var source in _groupGrains)
        {
            foreach (var target in _groupGrains)
            {
                _parent.AddTransition(source, target);
            }
        }
        return this;
    }

    public IGrainCallsBuilder And() => _parent;
}
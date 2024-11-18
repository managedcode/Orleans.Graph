using System;
using System.Linq;
using ManagedCode.Orleans.Graph.Models;

namespace ManagedCode.Orleans.Graph;

[GrainGraphConfiguration]
public class GrainGraphManager
{
    private readonly DirectedGraph<string> _grainGraph;

    public GrainGraphManager()
    {
        _grainGraph = new DirectedGraph<string>(true);
    }

    public GrainGraphManager AddAllowedTransition(Type sourceGrain, Type targetGrain)
    {
        _grainGraph.AddEdge(sourceGrain.FullName!, targetGrain.FullName!);
        return this;
    }

    public GrainGraphManager AddAllowedTransition<T1, T2>()
    {
        return AddAllowedTransition(typeof(T1), typeof(T2));
    }

    public GrainGraphManager AddAllowedTransition<T1>()
    {
        return AddAllowedTransition(typeof(T1), typeof(T1));
    }

    public bool IsTransitionAllowed(CallHistory callHistory)
    {
        if (callHistory.IsEmpty())
            return false;

        var calls = callHistory.History
            .Reverse()
            .ToArray();
        
        for (var i = 0; i < calls.Length - 1; i++)
        {
            var source = calls[i].Interface;
            var target = calls[i + 1].Interface;

            if (!_grainGraph.IsTransitionAllowed(source, target))
                return false;
        }

        return true;
    }
}
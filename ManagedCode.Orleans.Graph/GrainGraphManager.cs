using System;
using System.Collections.Generic;
using System.Linq;
using ManagedCode.Orleans.Graph.Models;

namespace ManagedCode.Orleans.Graph;

public class GrainGraphManager
{
    private readonly DirectedGraph _grainGraph;

    public GrainGraphManager(DirectedGraph grainGraph)
    {
        _grainGraph = grainGraph ?? throw new ArgumentNullException(nameof(grainGraph));
    }

    public bool IsTransitionAllowed(CallHistory callHistory)
    {
        if (callHistory.IsEmpty())
            return false;
        
        //TODO: check this code
        var calls = callHistory.History.Reverse().ToArray();

        for (var i = 0; i < calls.Length - 1; i++)
        {
            var currentCall = calls[i];
            var nextCall = calls[i + 1];

            if (currentCall.Direction == Direction.Out && nextCall.Direction == Direction.In)
            {
                if (!_grainGraph.IsTransitionAllowed(currentCall.Interface, nextCall.Interface, currentCall.Method, nextCall.Method))
                    return false;
            }
        }

        return true;
    }
}
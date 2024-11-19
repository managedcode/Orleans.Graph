using System;
using System.Collections.Generic;
using System.Linq;
using ManagedCode.Orleans.Graph.Models;

namespace ManagedCode.Orleans.Graph;

public class GrainTransitionManager
{
    private readonly DirectedGraph _grainGraph;

    public GrainTransitionManager(DirectedGraph grainGraph)
    {
        _grainGraph = grainGraph ?? throw new ArgumentNullException(nameof(grainGraph));
    }

    public bool IsTransitionAllowed(CallHistory callHistory)
    {
        if (callHistory.IsEmpty())
            return false;

        var calls = callHistory.History.Reverse().ToArray();
        var visited = new HashSet<string>();

        for (var i = 0; i < calls.Length - 1; i++)
        {
            var currentCall = calls[i];
            var nextCall = calls[i + 1];

            // Check for cycles
            var transitionKey = $"{currentCall.Interface}->{nextCall.Interface}";
            if (visited.Contains(transitionKey))
            {
                return false;
            }
            visited.Add(transitionKey);

            // Check if the transition is allowed
            if (!_grainGraph.IsTransitionAllowed(currentCall.Interface, nextCall.Interface, currentCall.Method, nextCall.Method))
            {
                return false;
            }
        }

        return true;
    }
}
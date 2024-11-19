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

        var calls = callHistory.History.ToArray();
        var visited = new HashSet<string>();

        for (var i = 0; i < calls.Length - 1; i++)
        {
            var currentCall = calls[i + 1];
            var nextCall = calls[i];

            // Only consider transitions from Out to In
            if (currentCall.Direction == Direction.Out && nextCall.Direction == Direction.In)
            {
                string transitionKey;
                if (currentCall is OutCall outCall)
                {
                    transitionKey = $"{outCall.Caller}->{nextCall.Interface}";
                }
                else
                {
                    transitionKey = $"{currentCall.Interface}->{nextCall.Interface}";
                }

                // Check for cycles
                if (visited.Contains(transitionKey))
                {
                    return false;
                }
                visited.Add(transitionKey);

                // Check if the transition is allowed
                if (currentCall is OutCall outCallTransition)
                {
                    if (!_grainGraph.IsTransitionAllowed(outCallTransition.Caller, nextCall.Interface, currentCall.Method, nextCall.Method))
                    {
                        return false;
                    }
                }
                else
                {
                    if (!_grainGraph.IsTransitionAllowed(currentCall.Interface, nextCall.Interface, currentCall.Method, nextCall.Method))
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }
}
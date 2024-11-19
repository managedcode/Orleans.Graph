using System;
using System.Collections.Generic;
using ManagedCode.Orleans.Graph.Models;
using Orleans.Runtime;

namespace ManagedCode.Orleans.Graph;

public class GrainTransitionManager
{
    private readonly DirectedGraph _grainGraph;

    public GrainTransitionManager(DirectedGraph grainGraph)
    {
        _grainGraph = grainGraph ?? throw new ArgumentNullException(nameof(grainGraph));
    }

    public bool IsTransitionAllowed(CallHistory callHistory, bool throwOnViolation = false)
    {
        if (callHistory.IsEmpty())
        {
            return false;
        }

        var calls = callHistory.History.ToArray();


        for (var i = 0; i < calls.Length - 1; i++)
        {
            var currentCall = calls[i + 1];
            var nextCall = calls[i];

            // Only consider transitions from Out to In
            if (currentCall.Direction == Direction.Out && nextCall.Direction == Direction.In)
            {
                // Check if the transition is allowed
                if (currentCall is OutCall outCallTransition)
                {
                    if (!_grainGraph.IsTransitionAllowed(outCallTransition.Caller, nextCall.Interface, currentCall.Method, nextCall.Method))
                    {
                        if(throwOnViolation)
                        {
                            throw new InvalidOperationException($"Transition from {outCallTransition.Caller} to {nextCall.Interface} is not allowed.");
                        }
                        return false;
                    }
                }
                else
                {
                    if (!_grainGraph.IsTransitionAllowed(currentCall.Interface, nextCall.Interface, currentCall.Method, nextCall.Method))
                    {
                        if(throwOnViolation)
                        {
                            throw new InvalidOperationException($"Transition from {currentCall.Interface} to {nextCall.Interface} is not allowed.");
                        }
                        return false;
                    }
                }
            }
        }
        
        return true;
    }

    public bool DetectDeadlocks(CallHistory callHistory, bool throwOnViolation = false)
    {
        var graph = new Dictionary<GrainId, List<GrainId>>();

        foreach (var call in callHistory.History)
        {
            if (call.SourceId.HasValue && call.TargetId.HasValue)
            {
                if (!graph.ContainsKey(call.SourceId.Value))
                {
                    graph[call.SourceId.Value] = new List<GrainId>();
                }

                graph[call.SourceId.Value].Add(call.TargetId.Value);
            }
        }

        var visited = new HashSet<GrainId>();
        var stack = new HashSet<GrainId>();

        foreach (var node in graph.Keys)
        {
            if (IsCyclic(node, graph, visited, stack))
            {
                if(throwOnViolation)
                {
                    throw new InvalidOperationException($"Deadlock detected. GrainId: {node}");
                }
                
                return true;
            }
        }

        return false;
    }

    private bool IsCyclic(GrainId node, Dictionary<GrainId, List<GrainId>> graph, HashSet<GrainId> visited, HashSet<GrainId> stack)
    {
        if (stack.Contains(node))
        {
            return true;
        }

        if (visited.Contains(node))
        {
            return false;
        }

        visited.Add(node);
        stack.Add(node);

        if (graph.ContainsKey(node))
        {
            foreach (var neighbor in graph[node])
            {
                if (IsCyclic(neighbor, graph, visited, stack))
                {
                    return true;
                }
            }
        }

        stack.Remove(node);
        return false;
    }
}
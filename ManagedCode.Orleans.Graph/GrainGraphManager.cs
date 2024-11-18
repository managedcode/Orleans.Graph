using System;
using System.Collections.Generic;
using System.Linq;
using ManagedCode.Orleans.Graph.Models;

namespace ManagedCode.Orleans.Graph;

[GrainGraphConfiguration]
public class GrainGraphManager
{
    private readonly DirectedGraph<string> _grainGraph;
    private readonly HashSet<(string Source, string Target, string SourceMethod, string TargetMethod)> _methodRules;
    private readonly HashSet<(string Source, string Target)> _reentrancyRules;
    private readonly Dictionary<string, HashSet<string>> _groups;

    public GrainGraphManager(DirectedGraph<string> grainGraph, 
                             HashSet<(string Source, string Target, string SourceMethod, string TargetMethod)> methodRules,
                             HashSet<(string Source, string Target)> reentrancyRules,
                             Dictionary<string, HashSet<string>> groups)
    {
        _grainGraph = grainGraph;
        _methodRules = methodRules;
        _reentrancyRules = reentrancyRules;
        _groups = groups;
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
            var currentCall = calls[i];
            var nextCall = calls[i + 1];

            if (currentCall.Direction == Direction.Out && nextCall.Direction == Direction.In)
            {
                var source = currentCall.Interface;
                var target = nextCall.Interface;
                var sourceMethod = currentCall.Method;
                var targetMethod = nextCall.Method;

                if (!_grainGraph.IsTransitionAllowed(source, target) || 
                    !IsMethodRuleAllowed(source, target, sourceMethod, targetMethod) ||
                    !IsReentrancyAllowed(source, target))
                    return false;
            }
        }

        return true;
    }

    private bool IsMethodRuleAllowed(string source, string target, string sourceMethod, string targetMethod)
    {
        return _methodRules.Contains((source, target, sourceMethod, targetMethod)) ||
               _methodRules.Contains((source, target, "*", "*"));
    }

    private bool IsReentrancyAllowed(string source, string target)
    {
        return _reentrancyRules.Contains((source, target));
    }
}
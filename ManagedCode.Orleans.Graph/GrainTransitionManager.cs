using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ManagedCode.Orleans.Graph.Models;
using Orleans.Runtime;

namespace ManagedCode.Orleans.Graph;

public class GrainTransitionManager(DirectedGraph grainGraph, bool allowAllByDefault = false)
{
    private readonly DirectedGraph _grainGraph = grainGraph ?? throw new ArgumentNullException(nameof(grainGraph));
    private readonly bool _allowAllByDefault = allowAllByDefault;

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
                    var isAllowed = _grainGraph.IsTransitionAllowed(outCallTransition.Caller, nextCall.Interface, currentCall.Method, nextCall.Method);
                    if (!isAllowed && !_allowAllByDefault)
                    {
                        if (throwOnViolation)
                        {
                            throw new InvalidOperationException($"Transition from {outCallTransition.Caller} to {nextCall.Interface} is not allowed.");
                        }
                        return false;
                    }
                }
                else
                {
                    var isAllowed = _grainGraph.IsTransitionAllowed(currentCall.Interface, nextCall.Interface, currentCall.Method, nextCall.Method);
                    if (!isAllowed && !_allowAllByDefault)
                    {
                        if (throwOnViolation)
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

        foreach (var call in callHistory.History.OfType<OutCall>())
        {
            if (!call.SourceId.HasValue || !call.TargetId.HasValue)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(call.Caller) &&
                string.Equals(call.Caller, call.Interface, StringComparison.Ordinal) &&
                _grainGraph.HasReentrantTransition(call.Caller, call.Interface))
            {
                continue;
            }

            if (!graph.TryGetValue(call.SourceId.Value, out var neighbors))
            {
                neighbors = new List<GrainId>();
                graph[call.SourceId.Value] = neighbors;
            }

            neighbors.Add(call.TargetId.Value);
        }

        var visited = new HashSet<GrainId>();
        var stack = new HashSet<GrainId>();

        foreach (var node in graph.Keys)
        {
            if (IsCyclic(node, graph, visited, stack))
            {
                if (throwOnViolation)
                {
                    throw new InvalidOperationException($"Deadlock detected. GrainId: {node}");
                }

                return true;
            }
        }

        return false;
    }

    public string GeneratePolicyMermaidDiagram()
    {
        return GenerateMermaidDiagramInternal(new HashSet<(string Source, string Target)>(), null);
    }

    public string GenerateLiveMermaidDiagram(CallHistory callHistory)
    {
        ArgumentNullException.ThrowIfNull(callHistory);

        var highlightedEdges = ExtractHighlightedEdges(callHistory);
        var usageCounts = BuildUsageCounts(callHistory);

        return GenerateMermaidDiagramInternal(highlightedEdges, usageCounts);
    }

    [Obsolete("Use GeneratePolicyMermaidDiagram or GenerateLiveMermaidDiagram instead.")]
    public string GenerateMermaidDiagram(CallHistory? callHistory = null)
    {
        return callHistory is null
            ? GeneratePolicyMermaidDiagram()
            : GenerateLiveMermaidDiagram(callHistory);
    }

    private string GenerateMermaidDiagramInternal(HashSet<(string Source, string Target)> highlightedEdges, Dictionary<(string Source, string Target), int>? usageCounts)
    {
        var sb = new StringBuilder();
        sb.AppendLine("graph LR");

        foreach (var edge in _grainGraph.GetAllEdges().OrderBy(static e => e.Source).ThenBy(static e => e.Target))
        {
            var arrow = GetArrowForEdge(edge, highlightedEdges);
            var label = BuildEdgeLabel(edge.Transitions, usageCounts, edge.Source, edge.Target);

            var sourceId = ToMermaidId(edge.Source);
            var targetId = ToMermaidId(edge.Target);

            sb
                .Append("    ")
                .Append(sourceId)
                .Append("[\"")
                .Append(ToDisplayName(edge.Source))
                .Append("\"] ")
                .Append(arrow);

            if (!string.IsNullOrEmpty(label))
            {
                sb.Append('|').Append(label).Append('|');
            }

            sb
                .Append(' ')
                .Append(targetId)
                .Append("[\"")
                .Append(ToDisplayName(edge.Target))
                .AppendLine("\"]");
        }

        return sb.ToString();
    }

    private static string GetArrowForEdge((string Source, string Target, HashSet<GrainTransition> Transitions) edge, HashSet<(string Source, string Target)> highlighted)
    {
        var hasReentrant = edge.Transitions.Any(static t => t.IsReentrant);

        if (highlighted.Contains((edge.Source, edge.Target)))
        {
            return "==>";
        }

        return hasReentrant ? "-.->" : "-->";
    }

    private static string BuildEdgeLabel(IEnumerable<GrainTransition> transitions, Dictionary<(string Source, string Target), int>? usageCounts, string source, string target)
    {
        var methods = transitions
            .Select(static t => (Source: t.SourceMethod, Target: t.TargetMethod, t.IsReentrant))
            .Distinct()
            .Select(static entry =>
            {
                if (entry.Source == "*" && entry.Target == "*")
                {
                    return entry.IsReentrant ? "all (reentrant)" : "all";
                }

                return entry.Source == entry.Target
                    ? entry.Source
                    : $"{entry.Source}->{entry.Target}";
            })
            .OrderBy(static method => method, StringComparer.Ordinal)
            .ToArray();

        var label = methods.Length switch
        {
            0 => string.Empty,
            1 => methods[0],
            _ => string.Join("<br/>", methods)
        };

        if (usageCounts is null)
        {
            return label;
        }

        if (!usageCounts.TryGetValue((source, target), out var count))
        {
            return label;
        }

        var usageSuffix = $"hits: {count}";
        return string.IsNullOrEmpty(label) ? usageSuffix : $"{label}<br/>{usageSuffix}";
    }

    private static HashSet<(string Source, string Target)> ExtractHighlightedEdges(CallHistory? callHistory)
    {
        var result = new HashSet<(string Source, string Target)>();

        if (callHistory is null || callHistory.IsEmpty())
        {
            return result;
        }

        var calls = callHistory.History.ToArray();
        for (var i = 0; i < calls.Length - 1; i++)
        {
            var currentCall = calls[i + 1];
            var nextCall = calls[i];

            if (currentCall.Direction != Direction.Out || nextCall.Direction != Direction.In)
            {
                continue;
            }

            var source = currentCall is OutCall outCall
                ? outCall.Caller
                : currentCall.Interface;

            var target = nextCall.Interface;

            if (!string.IsNullOrWhiteSpace(source) && !string.IsNullOrWhiteSpace(target))
            {
                result.Add((source, target));
            }
        }

        return result;
    }

    private static Dictionary<(string Source, string Target), int> BuildUsageCounts(CallHistory callHistory)
    {
        var counts = new Dictionary<(string Source, string Target), int>();

        if (callHistory.IsEmpty())
        {
            return counts;
        }

        var calls = callHistory.History.ToArray();
        for (var i = 0; i < calls.Length - 1; i++)
        {
            var currentCall = calls[i + 1];
            var nextCall = calls[i];

            if (currentCall.Direction != Direction.Out || nextCall.Direction != Direction.In)
            {
                continue;
            }

            var source = currentCall is OutCall outCall
                ? outCall.Caller
                : currentCall.Interface;

            var target = nextCall.Interface;

            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
            {
                continue;
            }

            var key = (source, target);
            counts.TryGetValue(key, out var existing);
            counts[key] = existing + 1;
        }

        return counts;
    }

    private static string ToMermaidId(string fullName)
    {
        var sanitized = new StringBuilder(fullName.Length);
        foreach (var ch in fullName)
        {
            sanitized.Append(char.IsLetterOrDigit(ch) ? ch : '_');
        }

        return sanitized.ToString();
    }

    private static string ToDisplayName(string fullName)
    {
        var lastDot = fullName.LastIndexOf('.');
        return lastDot >= 0 && lastDot < fullName.Length - 1
            ? fullName[(lastDot + 1)..]
            : fullName;
    }

    private static bool IsCyclic(GrainId node, Dictionary<GrainId, List<GrainId>> graph, HashSet<GrainId> visited, HashSet<GrainId> stack)
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

        if (graph.TryGetValue(node, out var value))
        {
            foreach (var neighbor in value)
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

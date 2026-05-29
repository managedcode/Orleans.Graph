using System.Text;
using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Models;

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
        if (TryGetSingleIncomingClientCall(calls, out var incomingClientCall))
        {
            return CheckTransitionAllowed(
                Constants.ClientCallerId,
                incomingClientCall.Interface,
                Constants.AnyMethod,
                incomingClientCall.Method,
                throwOnViolation);
        }

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
                    var sourceMethod = ResolveSourceMethod(calls, i + 1, outCallTransition);
                    if (!CheckTransitionAllowed(
                        outCallTransition.Caller,
                        nextCall.Interface,
                        sourceMethod,
                        nextCall.Method,
                        throwOnViolation))
                    {
                        return false;
                    }
                }
                else
                {
                    if (!CheckTransitionAllowed(
                        currentCall.Interface,
                        nextCall.Interface,
                        currentCall.Method,
                        nextCall.Method,
                        throwOnViolation))
                    {
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

    public IReadOnlyCollection<GrainTransitionEdge> GetPolicyEdges()
    {
        return _grainGraph.GetAllEdges()
            .OrderBy(static edge => edge.Source, StringComparer.Ordinal)
            .ThenBy(static edge => edge.Target, StringComparer.Ordinal)
            .Select(static edge => new GrainTransitionEdge(
                edge.Source,
                edge.Target,
                edge.Transitions
                    .OrderBy(static transition => transition.SourceMethod, StringComparer.Ordinal)
                    .ThenBy(static transition => transition.TargetMethod, StringComparer.Ordinal)
                    .ThenBy(static transition => transition.IsReentrant)
                    .ToArray()))
            .ToArray();
    }

    public string GenerateLiveMermaidDiagram(CallHistory callHistory)
    {
        ArgumentNullException.ThrowIfNull(callHistory);

        var observedEdges = GetObservedEdges(callHistory);

        return GenerateLiveMermaidDiagram(observedEdges);
    }

    public string GenerateLiveMermaidDiagram(IEnumerable<ObservedGrainCallEdge> observedEdges)
    {
        ArgumentNullException.ThrowIfNull(observedEdges);

        var edgeArray = observedEdges.ToArray();
        var highlightedEdges = edgeArray
            .Select(static edge => (edge.Source, edge.Target))
            .ToHashSet();
        var usageCounts = BuildUsageCounts(edgeArray);

        return GenerateMermaidDiagramInternal(highlightedEdges, usageCounts, edgeArray);
    }

    public static string GenerateObservedMermaidDiagram(IEnumerable<ObservedGrainCallEdge> observedEdges)
    {
        ArgumentNullException.ThrowIfNull(observedEdges);

        var manager = new GrainTransitionManager(new DirectedGraph());
        return manager.GenerateLiveMermaidDiagram(observedEdges);
    }

    [Obsolete("Use GeneratePolicyMermaidDiagram or GenerateLiveMermaidDiagram instead.")]
    public string GenerateMermaidDiagram(CallHistory? callHistory = null)
    {
        return callHistory is null
            ? GeneratePolicyMermaidDiagram()
            : GenerateLiveMermaidDiagram(callHistory);
    }

    public static IReadOnlyCollection<ObservedGrainCallEdge> GetObservedEdges(CallHistory callHistory)
    {
        ArgumentNullException.ThrowIfNull(callHistory);

        if (callHistory.IsEmpty())
        {
            return Array.Empty<ObservedGrainCallEdge>();
        }

        var calls = callHistory.History.ToArray();
        var edges = new Dictionary<ObservedGrainCallKey, ObservedGrainCallEdge>();

        if (TryGetSingleIncomingClientCall(calls, out var incomingClientCall))
        {
            AddObservedEdge(edges, ObservedGrainCallEdge.Create(
                Constants.ClientCallerId,
                incomingClientCall.Interface,
                Constants.AnyMethod,
                incomingClientCall.Method));
        }

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
            var sourceMethod = currentCall is OutCall sourceOutCall
                ? ResolveSourceMethod(calls, i + 1, sourceOutCall)
                : currentCall.Method;

            AddObservedEdge(edges, ObservedGrainCallEdge.Create(
                source,
                nextCall.Interface,
                sourceMethod,
                nextCall.Method));
        }

        return edges.Values.ToArray();
    }

    public static ObservedGrainCallEdge? GetLatestObservedEdge(CallHistory callHistory)
    {
        ArgumentNullException.ThrowIfNull(callHistory);

        if (callHistory.IsEmpty())
        {
            return null;
        }

        var calls = callHistory.History.ToArray();
        if (TryGetSingleIncomingClientCall(calls, out var incomingClientCall))
        {
            return ObservedGrainCallEdge.Create(
                Constants.ClientCallerId,
                incomingClientCall.Interface,
                Constants.AnyMethod,
                incomingClientCall.Method);
        }

        if (calls.Length < 2)
        {
            return null;
        }

        var incomingCall = calls[0];
        var outgoingCall = calls[1];

        if (incomingCall.Direction != Direction.In || outgoingCall.Direction != Direction.Out)
        {
            return null;
        }

        var source = outgoingCall is OutCall outCall
            ? outCall.Caller
            : outgoingCall.Interface;
        var sourceMethod = outgoingCall is OutCall sourceOutCall
            ? ResolveSourceMethod(calls, 1, sourceOutCall)
            : outgoingCall.Method;

        return ObservedGrainCallEdge.Create(
            source,
            incomingCall.Interface,
            sourceMethod,
            incomingCall.Method);
    }

    private string GenerateMermaidDiagramInternal(
        HashSet<(string Source, string Target)> highlightedEdges,
        Dictionary<(string Source, string Target), long>? usageCounts,
        IReadOnlyCollection<ObservedGrainCallEdge>? observedEdges = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("graph LR");

        foreach (var edge in BuildDiagramEdges(observedEdges).OrderBy(static e => e.Source, StringComparer.Ordinal).ThenBy(static e => e.Target, StringComparer.Ordinal))
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

    private static string BuildEdgeLabel(IEnumerable<GrainTransition> transitions, Dictionary<(string Source, string Target), long>? usageCounts, string source, string target)
    {
        var methods = transitions
            .Select(static t => (Source: t.SourceMethod, Target: t.TargetMethod, t.IsReentrant))
            .Distinct()
            .Select(static entry =>
            {
                if (entry.Source == Constants.AnyMethod && entry.Target == Constants.AnyMethod)
                {
                    return entry.IsReentrant ? "all (reentrant)" : "all";
                }

                if (entry.Source == Constants.AnyMethod)
                {
                    return entry.Target;
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

    private IEnumerable<(string Source, string Target, HashSet<GrainTransition> Transitions)> BuildDiagramEdges(IReadOnlyCollection<ObservedGrainCallEdge>? observedEdges)
    {
        var edges = _grainGraph.GetAllEdges()
            .ToDictionary(
                static edge => (edge.Source, edge.Target),
                static edge => new HashSet<GrainTransition>(edge.Transitions));

        if (observedEdges is not null)
        {
            foreach (var group in observedEdges.GroupBy(static edge => (edge.Source, edge.Target)))
            {
                edges[group.Key] = group
                    .Select(static edge => new GrainTransition(edge.SourceMethod, edge.TargetMethod))
                    .ToHashSet();
            }
        }

        foreach (var edge in edges)
        {
            yield return (edge.Key.Source, edge.Key.Target, edge.Value);
        }
    }

    private static Dictionary<(string Source, string Target), long> BuildUsageCounts(IEnumerable<ObservedGrainCallEdge> observedEdges)
    {
        var counts = new Dictionary<(string Source, string Target), long>();

        foreach (var edge in observedEdges)
        {
            if (string.IsNullOrWhiteSpace(edge.Source) || string.IsNullOrWhiteSpace(edge.Target))
            {
                continue;
            }

            var key = (edge.Source, edge.Target);
            counts.TryGetValue(key, out var existing);
            counts[key] = existing + edge.Count;
        }

        return counts;
    }

    private bool CheckTransitionAllowed(string source, string target, string sourceMethod, string targetMethod, bool throwOnViolation)
    {
        var isAllowed = _grainGraph.IsTransitionAllowed(source, target, sourceMethod, targetMethod);
        if (isAllowed || _allowAllByDefault)
        {
            return true;
        }

        if (throwOnViolation)
        {
            throw new InvalidOperationException($"Transition from {source} to {target} is not allowed.");
        }

        return false;
    }

    private static bool TryGetSingleIncomingClientCall(Call[] calls, out Call incomingCall)
    {
        incomingCall = null!;

        if (calls.Length != 1 || calls[0].Direction != Direction.In || calls[0].SourceId.HasValue)
        {
            return false;
        }

        incomingCall = calls[0];
        return true;
    }

    private static string ResolveSourceMethod(Call[] calls, int outCallIndex, OutCall outCall)
    {
        if (!outCall.SourceId.HasValue)
        {
            return Constants.AnyMethod;
        }

        for (var i = outCallIndex + 1; i < calls.Length; i++)
        {
            var candidate = calls[i];
            if (candidate.Direction != Direction.In)
            {
                continue;
            }

            if (!candidate.TargetId.HasValue || candidate.TargetId.Value.Equals(outCall.SourceId.Value))
            {
                return candidate.Method;
            }
        }

        return outCall.Method;
    }

    private static void AddObservedEdge(Dictionary<ObservedGrainCallKey, ObservedGrainCallEdge> edges, ObservedGrainCallEdge edge)
    {
        var key = ObservedGrainCallKey.From(edge);
        edges[key] = edges.TryGetValue(key, out var existing)
            ? existing.Merge(edge)
            : edge;
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

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
                    var sourceMethod = ResolveSourceMethod(outCallTransition);
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

        var observedGraph = GetObservedGraph(callHistory);

        return GenerateLiveMermaidDiagram(observedGraph);
    }

    public string GenerateLiveMermaidDiagram(ObservedGrainCallGraph observedGraph)
    {
        ArgumentNullException.ThrowIfNull(observedGraph);

        return GenerateLiveMermaidDiagram(observedGraph.Edges);
    }

    public string GenerateLiveMermaidDiagram(IEnumerable<ObservedGrainCall> observedCalls)
    {
        ArgumentNullException.ThrowIfNull(observedCalls);

        var edgeArray = observedCalls.ToArray();
        var highlightedEdges = edgeArray
            .Select(static edge => (edge.Source, edge.Target))
            .ToHashSet();
        var usageCounts = BuildUsageCounts(edgeArray);

        return GenerateMermaidDiagramInternal(highlightedEdges, usageCounts, edgeArray);
    }

    public static string GenerateObservedGraphMermaidDiagram(ObservedGrainCallGraph observedGraph)
    {
        ArgumentNullException.ThrowIfNull(observedGraph);

        var manager = new GrainTransitionManager(new DirectedGraph());
        return manager.GenerateLiveMermaidDiagram(observedGraph);
    }

    public static ObservedGrainCallGraph BuildObservedGraph(IEnumerable<ObservedGrainCall> observedCalls)
    {
        ArgumentNullException.ThrowIfNull(observedCalls);

        var edges = observedCalls
            .OrderBy(static call => call.Source, StringComparer.Ordinal)
            .ThenBy(static call => call.Target, StringComparer.Ordinal)
            .ThenBy(static call => call.SourceMethod, StringComparer.Ordinal)
            .ThenBy(static call => call.TargetMethod, StringComparer.Ordinal)
            .ToArray();

        var vertices = edges
            .SelectMany(static call => new[] { call.Source, call.Target })
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static vertex => vertex, StringComparer.Ordinal)
            .Select(static vertex => new ObservedGrainCallVertex(vertex, ToDisplayName(vertex)))
            .ToArray();

        return new ObservedGrainCallGraph(vertices, edges);
    }

    [Obsolete("Use GeneratePolicyMermaidDiagram or GenerateLiveMermaidDiagram instead.")]
    public string GenerateMermaidDiagram(CallHistory? callHistory = null)
    {
        return callHistory is null
            ? GeneratePolicyMermaidDiagram()
            : GenerateLiveMermaidDiagram(callHistory);
    }

    public static ObservedGrainCallGraph GetObservedGraph(CallHistory callHistory)
    {
        ArgumentNullException.ThrowIfNull(callHistory);

        if (callHistory.IsEmpty())
        {
            return new ObservedGrainCallGraph(Array.Empty<ObservedGrainCallVertex>(), Array.Empty<ObservedGrainCall>());
        }

        var calls = callHistory.History.ToArray();
        var edges = new Dictionary<ObservedGrainCallKey, ObservedGrainCall>();

        if (TryGetSingleIncomingClientCall(calls, out var incomingClientCall))
        {
            AddObservedEdge(edges, ObservedGrainCall.Create(
                Constants.ClientCallerId,
                GetObservedTarget(incomingClientCall),
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

            var sourceMethod = currentCall is OutCall sourceOutCall
                ? ResolveSourceMethod(sourceOutCall)
                : currentCall.Method;

            AddObservedEdge(edges, ObservedGrainCall.Create(
                GetObservedSource(currentCall),
                GetObservedTarget(nextCall),
                sourceMethod,
                nextCall.Method));
        }

        return BuildObservedGraph(edges.Values);
    }

    public static ObservedGrainCall? GetLatestObservedCall(CallHistory callHistory)
    {
        ArgumentNullException.ThrowIfNull(callHistory);

        if (callHistory.IsEmpty())
        {
            return null;
        }

        var calls = callHistory.History.ToArray();
        if (TryGetSingleIncomingClientCall(calls, out var incomingClientCall))
        {
            return ObservedGrainCall.Create(
                Constants.ClientCallerId,
                GetObservedTarget(incomingClientCall),
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

        var sourceMethod = outgoingCall is OutCall sourceOutCall
            ? ResolveSourceMethod(sourceOutCall)
            : outgoingCall.Method;

        return ObservedGrainCall.Create(
            GetObservedSource(outgoingCall),
            GetObservedTarget(incomingCall),
            sourceMethod,
            incomingCall.Method);
    }

    private string GenerateMermaidDiagramInternal(
        HashSet<(string Source, string Target)> highlightedEdges,
        Dictionary<(string Source, string Target), long>? usageCounts,
        IReadOnlyCollection<ObservedGrainCall>? observedCalls = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("graph LR");

        foreach (var edge in BuildDiagramEdges(observedCalls).OrderBy(static e => e.Source, StringComparer.Ordinal).ThenBy(static e => e.Target, StringComparer.Ordinal))
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

    private IEnumerable<(string Source, string Target, HashSet<GrainTransition> Transitions)> BuildDiagramEdges(IReadOnlyCollection<ObservedGrainCall>? observedCalls)
    {
        var edges = _grainGraph.GetAllEdges()
            .ToDictionary(
                static edge => (edge.Source, edge.Target),
                static edge => new HashSet<GrainTransition>(edge.Transitions));

        if (observedCalls is not null)
        {
            foreach (var group in observedCalls.GroupBy(static edge => (edge.Source, edge.Target)))
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

    private static Dictionary<(string Source, string Target), long> BuildUsageCounts(IEnumerable<ObservedGrainCall> observedCalls)
    {
        var counts = new Dictionary<(string Source, string Target), long>();

        foreach (var edge in observedCalls)
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

    private static string ResolveSourceMethod(OutCall outCall)
    {
        if (!outCall.SourceId.HasValue)
        {
            return Constants.AnyMethod;
        }

        if (!string.IsNullOrWhiteSpace(outCall.CallerMethod))
        {
            return outCall.CallerMethod;
        }

        throw new InvalidOperationException(
            $"Unable to resolve source method for outgoing call from {outCall.Caller} to {outCall.Interface}.{outCall.Method}.");
    }

    private static void AddObservedEdge(Dictionary<ObservedGrainCallKey, ObservedGrainCall> edges, ObservedGrainCall edge)
    {
        var key = ObservedGrainCallKey.From(edge);
        edges[key] = edges.TryGetValue(key, out var existing)
            ? existing.Merge(edge)
            : edge;
    }

    private static string GetObservedSource(Call call)
    {
        return call is OutCall outCall
            ? GetObservedIdentity(outCall.Caller)
            : GetObservedIdentity(call.Interface);
    }

    private static string GetObservedTarget(Call call)
    {
        return GetObservedIdentity(call.Interface);
    }

    private static string GetObservedIdentity(string grainIdentity)
    {
        if (IsValidObservedIdentity(grainIdentity))
        {
            return grainIdentity;
        }

        throw new InvalidOperationException("Observed grain call identity resolved to the Orleans base Grain type.");
    }

    private static bool IsBaseGrainIdentity(string value)
    {
        return string.Equals(value, nameof(Grain), StringComparison.Ordinal) ||
               string.Equals(value, typeof(Grain).FullName, StringComparison.Ordinal);
    }

    private static bool IsValidObservedIdentity(string value)
    {
        return !string.IsNullOrWhiteSpace(value) && !IsBaseGrainIdentity(value);
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

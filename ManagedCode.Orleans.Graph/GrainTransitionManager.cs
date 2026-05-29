using System.Runtime.InteropServices;
using System.Text;
using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Models;

namespace ManagedCode.Orleans.Graph;

public class GrainTransitionManager(DirectedGraph grainGraph, bool allowAllByDefault = false)
{
    private readonly DirectedGraph _grainGraph = grainGraph ?? throw new ArgumentNullException(nameof(grainGraph));
    private readonly bool _allowAllByDefault = allowAllByDefault;
    private static readonly Comparison<ObservedGrainCall> _observedCallComparison = CompareObservedCalls;

    public bool IsTransitionAllowed(CallHistory callHistory, bool throwOnViolation = false)
    {
        if (callHistory.IsEmpty())
        {
            return false;
        }

        var calls = callHistory.History;
        if (TryGetSingleIncomingClientCall(calls, out var incomingClientCall))
        {
            return CheckTransitionAllowed(
                Constants.ClientCallerId,
                incomingClientCall.Interface,
                Constants.AnyMethod,
                incomingClientCall.Method,
                throwOnViolation);
        }

        Call? nextCall = null;
        foreach (var currentCall in calls)
        {
            if (nextCall is null)
            {
                nextCall = currentCall;
                continue;
            }

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

            nextCall = currentCall;
        }

        return true;
    }

    internal bool IsLatestTransitionAllowed(CallHistory callHistory, bool throwOnViolation = false)
    {
        if (callHistory.IsEmpty())
        {
            return false;
        }

        var calls = callHistory.History;
        if (TryGetSingleIncomingClientCall(calls, out var incomingClientCall))
        {
            return CheckTransitionAllowed(
                Constants.ClientCallerId,
                incomingClientCall.Interface,
                Constants.AnyMethod,
                incomingClientCall.Method,
                throwOnViolation);
        }

        var enumerator = calls.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            return false;
        }

        var newestCall = enumerator.Current;
        if (newestCall is OutCall newestOutgoingCall)
        {
            if (!newestOutgoingCall.SourceId.HasValue)
            {
                return true;
            }

            return CheckTransitionAllowed(
                newestOutgoingCall.Caller,
                newestOutgoingCall.Interface,
                ResolveSourceMethod(newestOutgoingCall),
                newestOutgoingCall.Method,
                throwOnViolation);
        }

        if (!enumerator.MoveNext())
        {
            return true;
        }

        var previousCall = enumerator.Current;
        if (newestCall.Direction != Direction.In || previousCall.Direction != Direction.Out)
        {
            return true;
        }

        if (previousCall is OutCall previousOutgoingCall)
        {
            return CheckTransitionAllowed(
                previousOutgoingCall.Caller,
                newestCall.Interface,
                ResolveSourceMethod(previousOutgoingCall),
                newestCall.Method,
                throwOnViolation);
        }

        return CheckTransitionAllowed(
            previousCall.Interface,
            newestCall.Interface,
            previousCall.Method,
            newestCall.Method,
            throwOnViolation);
    }

    public bool DetectDeadlocks(CallHistory callHistory, bool throwOnViolation = false)
    {
        if (callHistory.History.Count < 2)
        {
            return false;
        }

        var graph = new Dictionary<GrainId, List<GrainId>>(callHistory.History.Count);

        foreach (var historyEntry in callHistory.History)
        {
            if (historyEntry is not OutCall call)
            {
                continue;
            }

            if (!call.SourceId.HasValue || !call.TargetId.HasValue)
            {
                continue;
            }

            if (IsReentrantSelfCall(call))
            {
                continue;
            }

            ref var neighbors = ref CollectionsMarshal.GetValueRefOrAddDefault(graph, call.SourceId.Value, out var exists);
            if (!exists || neighbors is null)
            {
                neighbors = new List<GrainId>();
            }

            neighbors.Add(call.TargetId.Value);
        }

        if (graph.Count == 0)
        {
            return false;
        }

        var visited = new HashSet<GrainId>(graph.Count);
        var stack = new HashSet<GrainId>(graph.Count);

        foreach (var node in graph.Keys)
        {
            if (IsCyclic(node, graph, visited, stack))
            {
                if (throwOnViolation)
                {
                    ThrowDeadlock(node);
                }

                return true;
            }
        }

        return false;
    }

    internal bool DetectLatestDeadlock(CallHistory callHistory, bool throwOnViolation = false)
    {
        if (callHistory.History.Count < 2)
        {
            return false;
        }

        if (callHistory.History.Peek() is not OutCall latestCall ||
            !latestCall.SourceId.HasValue ||
            !latestCall.TargetId.HasValue)
        {
            return false;
        }

        if (IsReentrantSelfCall(latestCall))
        {
            return false;
        }

        var sourceId = latestCall.SourceId.Value;
        var targetId = latestCall.TargetId.Value;
        if (sourceId.Equals(targetId))
        {
            return ReportDeadlock(sourceId, throwOnViolation);
        }

        var graph = new Dictionary<GrainId, List<GrainId>>(callHistory.History.Count);
        var skipLatest = true;
        foreach (var historyEntry in callHistory.History)
        {
            if (skipLatest)
            {
                skipLatest = false;
                continue;
            }

            if (historyEntry is not OutCall call || !call.SourceId.HasValue || !call.TargetId.HasValue)
            {
                continue;
            }

            if (IsReentrantSelfCall(call))
            {
                continue;
            }

            ref var neighbors = ref CollectionsMarshal.GetValueRefOrAddDefault(graph, call.SourceId.Value, out var exists);
            if (!exists || neighbors is null)
            {
                neighbors = new List<GrainId>();
            }

            neighbors.Add(call.TargetId.Value);
        }

        if (!CanReach(targetId, sourceId, graph, new HashSet<GrainId>(graph.Count)))
        {
            return false;
        }

        return ReportDeadlock(sourceId, throwOnViolation);
    }

    private bool IsReentrantSelfCall(OutCall call)
    {
        return !string.IsNullOrWhiteSpace(call.Caller) &&
               string.Equals(call.Caller, call.Interface, StringComparison.Ordinal) &&
               _grainGraph.HasReentrantTransition(call.Caller, call.Interface);
    }

    private static bool ReportDeadlock(GrainId grainId, bool throwOnViolation)
    {
        if (throwOnViolation)
        {
            ThrowDeadlock(grainId);
        }

        return true;
    }

    private static void ThrowDeadlock(GrainId grainId)
    {
        throw new InvalidOperationException($"Deadlock detected. GrainId: {grainId}");
    }

    public string GeneratePolicyMermaidDiagram()
    {
        return GenerateMermaidDiagramInternal(null, null);
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

        return GenerateLiveMermaidDiagramFromObservedCalls(observedGraph.Edges);
    }

    private string GenerateLiveMermaidDiagramFromObservedCalls(IEnumerable<ObservedGrainCall> observedCalls)
    {
        ArgumentNullException.ThrowIfNull(observedCalls);

        var edgeArray = observedCalls.ToArray();
        var highlightedEdges = new HashSet<(string Source, string Target)>(edgeArray.Length);
        foreach (var edge in edgeArray)
        {
            highlightedEdges.Add((edge.Source, edge.Target));
        }

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

        var edges = observedCalls.ToArray();
        return BuildObservedGraphFromSnapshot(edges);
    }

    internal static ObservedGrainCallGraph BuildObservedGraphFromSnapshot(ObservedGrainCall[] edges)
    {
        ArgumentNullException.ThrowIfNull(edges);

        Array.Sort(edges, _observedCallComparison);

        var vertexSet = new HashSet<string>(edges.Length * 2, StringComparer.Ordinal);
        foreach (var edge in edges)
        {
            vertexSet.Add(edge.Source);
            vertexSet.Add(edge.Target);
        }

        var vertexIds = new string[vertexSet.Count];
        vertexSet.CopyTo(vertexIds);
        Array.Sort(vertexIds, StringComparer.Ordinal);

        var vertices = new ObservedGrainCallVertex[vertexIds.Length];
        for (var i = 0; i < vertexIds.Length; i++)
        {
            var vertexId = vertexIds[i];
            vertices[i] = new ObservedGrainCallVertex(vertexId, ToDisplayName(vertexId));
        }

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

        var calls = callHistory.History;
        var edges = new Dictionary<ObservedGrainCallKey, ObservedGrainCall>(Math.Max(1, calls.Count / 2));

        if (TryGetSingleIncomingClientCall(calls, out var incomingClientCall))
        {
            AddObservedEdge(edges, ObservedGrainCall.Create(
                Constants.ClientCallerId,
                GetObservedTarget(incomingClientCall),
                Constants.AnyMethod,
                incomingClientCall.Method));
        }

        Call? nextCall = null;
        foreach (var currentCall in calls)
        {
            if (nextCall is null)
            {
                nextCall = currentCall;
                continue;
            }

            if (currentCall.Direction != Direction.Out || nextCall.Direction != Direction.In)
            {
                nextCall = currentCall;
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

            nextCall = currentCall;
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

        var calls = callHistory.History;
        if (TryGetSingleIncomingClientCall(calls, out var incomingClientCall))
        {
            return ObservedGrainCall.Create(
                Constants.ClientCallerId,
                GetObservedTarget(incomingClientCall),
                Constants.AnyMethod,
                incomingClientCall.Method);
        }

        if (calls.Count < 2)
        {
            return null;
        }

        var enumerator = calls.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            return null;
        }

        var incomingCall = enumerator.Current;
        if (!enumerator.MoveNext())
        {
            return null;
        }

        var outgoingCall = enumerator.Current;

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
        HashSet<(string Source, string Target)>? highlightedEdges,
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

    private static string GetArrowForEdge((string Source, string Target, HashSet<GrainTransition> Transitions) edge, HashSet<(string Source, string Target)>? highlighted)
    {
        var hasReentrant = edge.Transitions.Any(static t => t.IsReentrant);

        if (highlighted is not null && highlighted.Contains((edge.Source, edge.Target)))
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
        var edges = new Dictionary<(string Source, string Target), HashSet<GrainTransition>>();
        foreach (var edge in _grainGraph.GetAllEdges())
        {
            edges[(edge.Source, edge.Target)] = new HashSet<GrainTransition>(edge.Transitions);
        }

        if (observedCalls is not null)
        {
            var observedEdgeKeys = new HashSet<(string Source, string Target)>(observedCalls.Count);
            foreach (var observedCall in observedCalls)
            {
                var key = (observedCall.Source, observedCall.Target);
                if (observedEdgeKeys.Add(key))
                {
                    edges[key] = new HashSet<GrainTransition>();
                }

                ref var transitions = ref CollectionsMarshal.GetValueRefOrAddDefault(edges, key, out var exists);
                if (!exists || transitions is null)
                {
                    transitions = new HashSet<GrainTransition>();
                }

                transitions.Add(new GrainTransition(observedCall.SourceMethod, observedCall.TargetMethod));
            }
        }

        foreach (var edge in edges)
        {
            yield return (edge.Key.Source, edge.Key.Target, edge.Value);
        }
    }

    private static Dictionary<(string Source, string Target), long> BuildUsageCounts(IEnumerable<ObservedGrainCall> observedCalls)
    {
        var counts = observedCalls is IReadOnlyCollection<ObservedGrainCall> collection
            ? new Dictionary<(string Source, string Target), long>(collection.Count)
            : new Dictionary<(string Source, string Target), long>();

        foreach (var edge in observedCalls)
        {
            if (string.IsNullOrWhiteSpace(edge.Source) || string.IsNullOrWhiteSpace(edge.Target))
            {
                continue;
            }

            var key = (edge.Source, edge.Target);
            ref var count = ref CollectionsMarshal.GetValueRefOrAddDefault(counts, key, out _);
            count += edge.Count;
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

    private static bool TryGetSingleIncomingClientCall(Stack<Call> calls, out Call incomingCall)
    {
        incomingCall = null!;

        if (calls.Count != 1)
        {
            return false;
        }

        var onlyCall = calls.Peek();
        if (onlyCall.Direction != Direction.In || onlyCall.SourceId.HasValue)
        {
            return false;
        }

        incomingCall = onlyCall;
        return true;
    }

    private static string ResolveSourceMethod(OutCall outCall)
    {
        if (!outCall.SourceId.HasValue && string.Equals(outCall.Caller, Constants.ClientCallerId, StringComparison.Ordinal))
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
        ref var existing = ref CollectionsMarshal.GetValueRefOrAddDefault(edges, key, out var exists);
        existing = exists
            ? existing!.Merge(edge)
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

    private static int CompareObservedCalls(ObservedGrainCall left, ObservedGrainCall right)
    {
        var result = string.Compare(left.Source, right.Source, StringComparison.Ordinal);
        if (result != 0)
        {
            return result;
        }

        result = string.Compare(left.Target, right.Target, StringComparison.Ordinal);
        if (result != 0)
        {
            return result;
        }

        result = string.Compare(left.SourceMethod, right.SourceMethod, StringComparison.Ordinal);
        return result != 0
            ? result
            : string.Compare(left.TargetMethod, right.TargetMethod, StringComparison.Ordinal);
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

    private static bool CanReach(GrainId current, GrainId target, Dictionary<GrainId, List<GrainId>> graph, HashSet<GrainId> visited)
    {
        if (current.Equals(target))
        {
            return true;
        }

        if (!visited.Add(current))
        {
            return false;
        }

        if (!graph.TryGetValue(current, out var neighbors))
        {
            return false;
        }

        foreach (var neighbor in neighbors)
        {
            if (CanReach(neighbor, target, graph, visited))
            {
                return true;
            }
        }

        return false;
    }
}

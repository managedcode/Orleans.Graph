using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Models;
using Orleans.CodeGeneration;

namespace ManagedCode.Orleans.Graph;

public class GrainTransitionManager(DirectedGraph grainGraph, bool allowAllByDefault = false)
{
    private readonly DirectedGraph _grainGraph = grainGraph ?? throw new ArgumentNullException(nameof(grainGraph));
    private readonly bool _allowAllByDefault = allowAllByDefault;
    private static readonly Comparison<ObservedGrainCall> _observedCallComparison = CompareObservedCalls;
    private static readonly ObservedGrainCallGraph _emptyObservedGraph = new(
        Array.Empty<ObservedGrainCallVertex>(), Array.Empty<ObservedGrainCall>());

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

        var calls = callHistory.History.ToArray();
        Dictionary<GrainId, List<GrainId>>? graph = null;
        HashSet<GrainId>? visited = null;

        for (var index = calls.Length - 1; index >= 0; index--)
        {
            if (calls[index] is not OutCall { SourceId: { } source, TargetId: { } target } call ||
                !IsWaitEdge(call))
            {
                continue;
            }

            if (source.Equals(target))
            {
                if (!CanInterleaveCall(call, callHistory))
                {
                    return ReportDeadlock(source, throwOnViolation);
                }
            }
            else if (graph is not null && graph.ContainsKey(target) && !CanInterleaveCall(call, callHistory))
            {
                visited ??= new HashSet<GrainId>();
                if (CanReach(target, source, graph, visited))
                {
                    return ReportDeadlock(source, throwOnViolation);
                }

                visited.Clear();
            }

            graph ??= new Dictionary<GrainId, List<GrainId>>(calls.Length);
            ref var neighbors = ref CollectionsMarshal.GetValueRefOrAddDefault(graph, source, out var exists);
            if (!exists || neighbors is null)
            {
                neighbors = new List<GrainId>();
            }

            neighbors.Add(target);
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

        if (!IsWaitEdge(latestCall) || CanInterleaveCall(latestCall, callHistory))
        {
            return false;
        }

        var sourceId = latestCall.SourceId.Value;
        var targetId = latestCall.TargetId.Value;
        if (sourceId.Equals(targetId))
        {
            return ReportDeadlock(sourceId, throwOnViolation);
        }

        var earlier = callHistory.History.GetEnumerator();
        earlier.MoveNext();
        var hasPathStart = false;
        while (earlier.MoveNext())
        {
            if (earlier.Current is OutCall { SourceId: { } source, TargetId: not null } call &&
                source.Equals(targetId) && IsWaitEdge(call))
            {
                hasPathStart = true;
                break;
            }
        }

        if (!hasPathStart)
        {
            return false;
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

            if (!IsWaitEdge(call))
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

    private bool IsWaitEdge(OutCall call)
    {
        if ((call.InvocationOptions & InvokeMethodOptions.OneWay) != 0)
        {
            return false;
        }

        var isReentrantSelfCall = call.SourceId == call.TargetId &&
            !string.IsNullOrWhiteSpace(call.Caller) &&
            string.Equals(call.Caller, call.Interface, StringComparison.Ordinal) &&
            _grainGraph.HasReentrantTransition(call.Caller, call.Interface);
        return !isReentrantSelfCall;
    }

    private static bool CanInterleaveCall(OutCall call, CallHistory history)
    {
        if ((call.InvocationOptions & InvokeMethodOptions.AlwaysInterleave) != 0)
        {
            return true;
        }

        var reentrancyId = RequestContext.ReentrancyId;
        if (reentrancyId != Guid.Empty)
        {
            foreach (var scope in history.NativeCallChainScopes)
            {
                if (scope.ActorId == call.TargetId && scope.ReentrancyId == reentrancyId)
                {
                    return true;
                }
            }
        }

        var isReadOnly = (call.InvocationOptions & InvokeMethodOptions.ReadOnly) != 0;
        var foundCall = false;
        var hasOutstandingTarget = false;
        var foundBlockingRequest = false;
        var hasIncomingTarget = false;
        var canInterleaveReadOnly = true;
        foreach (var earlierCall in history.History)
        {
            if (!foundCall)
            {
                foundCall = ReferenceEquals(earlierCall, call);
                continue;
            }

            if (earlierCall.TargetId != call.TargetId)
            {
                continue;
            }

            if (earlierCall is InCall incomingCall)
            {
                hasIncomingTarget = true;
                // Orleans never makes an AlwaysInterleave request the blocking
                // request. A normal callback can therefore enter while only
                // interleaving requests remain outstanding on this target.
                if ((incomingCall.InvocationOptions & InvokeMethodOptions.AlwaysInterleave) != 0)
                {
                    continue;
                }

                if (!foundBlockingRequest)
                {
                    foundBlockingRequest = true;
                    if (incomingCall.MayInterleave)
                    {
                        return true;
                    }
                }
            }

            if (!isReadOnly)
            {
                continue;
            }

            hasOutstandingTarget = true;
            if ((earlierCall.InvocationOptions & (InvokeMethodOptions.ReadOnly |
                                                 InvokeMethodOptions.AlwaysInterleave)) == 0)
            {
                canInterleaveReadOnly = false;
            }
        }

        return hasIncomingTarget && !foundBlockingRequest ||
               isReadOnly && hasOutstandingTarget && canInterleaveReadOnly;
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
        return GenerateMermaidDiagramInternal(null);
    }

    public IReadOnlyCollection<GrainTransitionEdge> GetPolicyEdges()
    {
        return _grainGraph.GetAllEdges()
            .OrderBy(static edge => edge.Source, StringComparer.Ordinal)
            .ThenBy(static edge => edge.Target, StringComparer.Ordinal)
            .Select(static edge => new GrainTransitionEdge(
                edge.Source,
                edge.Target,
                edge.Transitions.Count <= 1
                    ? edge.Transitions.ToArray()
                    : edge.Transitions
                        .OrderBy(static transition => transition.SourceMethod, StringComparer.Ordinal)
                        .ThenBy(static transition => transition.TargetMethod, StringComparer.Ordinal)
                        .ThenBy(static transition => transition.IsReentrant)
                        .ToArray()))
            .ToArray();
    }

    public string GenerateLiveMermaidDiagram(CallHistory callHistory)
    {
        ArgumentNullException.ThrowIfNull(callHistory);

        // Mermaid uses identities and counts only. No timestamps need to be generated.
        return GenerateMermaidDiagramInternal(GetObservedCalls(callHistory, timeProvider: null));
    }

    public string GenerateLiveMermaidDiagram(ObservedGrainCallGraph observedGraph)
    {
        ArgumentNullException.ThrowIfNull(observedGraph);

        var observedCalls = observedGraph.Edges;
        ArgumentNullException.ThrowIfNull(observedCalls);
        return GenerateMermaidDiagramInternal(observedCalls);
    }

    public static string GenerateObservedGraphMermaidDiagram(ObservedGrainCallGraph observedGraph)
    {
        ArgumentNullException.ThrowIfNull(observedGraph);

        return GenerateObservedCallsMermaidDiagram(observedGraph.Edges);
    }

    internal static string GenerateObservedCallsMermaidDiagram(IReadOnlyCollection<ObservedGrainCall> observedCalls)
    {
        ArgumentNullException.ThrowIfNull(observedCalls);
        var manager = new GrainTransitionManager(new DirectedGraph());
        return manager.GenerateMermaidDiagramInternal(observedCalls);
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
        if (edges.Length == 0)
        {
            return _emptyObservedGraph;
        }

        Array.Sort(edges, _observedCallComparison);

        // Count at most one source per source group and one target per endpoint pair.
        var vertexCapacity = 2;
        for (var edgeIndex = 1; edgeIndex < edges.Length; edgeIndex++)
        {
            var previous = edges[edgeIndex - 1];
            var current = edges[edgeIndex];
            if (!string.Equals(previous.Source, current.Source, StringComparison.Ordinal))
            {
                vertexCapacity += 2;
            }
            else if (!string.Equals(previous.Target, current.Target, StringComparison.Ordinal))
            {
                vertexCapacity++;
            }
        }

        var vertexSet = new HashSet<string>(vertexCapacity, StringComparer.Ordinal);
        foreach (var edge in edges)
        {
            vertexSet.Add(edge.Source);
            vertexSet.Add(edge.Target);
        }

        var vertices = new ObservedGrainCallVertex[vertexSet.Count];
        var index = 0;
        foreach (var vertexId in vertexSet)
        {
            vertices[index++] = new ObservedGrainCallVertex(vertexId, ToDisplayName(vertexId));
        }

        Array.Sort(vertices, static (left, right) => string.Compare(left.Id, right.Id, StringComparison.Ordinal));
        return new ObservedGrainCallGraph(vertices, edges);
    }

    [Obsolete("Use GeneratePolicyMermaidDiagram or GenerateLiveMermaidDiagram instead.")]
    public string GenerateMermaidDiagram(CallHistory? callHistory = null)
    {
        return callHistory is null
            ? GeneratePolicyMermaidDiagram()
            : GenerateLiveMermaidDiagram(callHistory);
    }

    public static ObservedGrainCallGraph GetObservedGraph(CallHistory callHistory, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(callHistory);
        ArgumentNullException.ThrowIfNull(timeProvider);

        var observedCalls = GetObservedCalls(callHistory, timeProvider);
        return BuildObservedGraphFromSnapshot(observedCalls as ObservedGrainCall[] ?? observedCalls.ToArray());
    }

    private static IReadOnlyCollection<ObservedGrainCall> GetObservedCalls(CallHistory callHistory, TimeProvider? timeProvider)
    {
        var calls = callHistory.History;
        if (calls.Count <= 2)
        {
            var observedCall = GetLatestObservedCallCore(callHistory, timeProvider);
            return observedCall is null ? Array.Empty<ObservedGrainCall>() : new[] { observedCall };
        }

        Dictionary<ObservedGrainCallKey, ObservedGrainCall>? edges = null;
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

            edges ??= new Dictionary<ObservedGrainCallKey, ObservedGrainCall>(calls.Count / 2);
            AddObservedEdge(edges, new ObservedGrainCallKey(
                GetObservedSource(currentCall),
                GetObservedTarget(nextCall),
                sourceMethod,
                nextCall.Method), timeProvider?.GetUtcNow().UtcDateTime ?? DateTime.UnixEpoch);

            nextCall = currentCall;
        }

        return edges is null ? Array.Empty<ObservedGrainCall>() : edges.Values;
    }

    public static ObservedGrainCall? GetLatestObservedCall(CallHistory callHistory, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(callHistory);
        ArgumentNullException.ThrowIfNull(timeProvider);

        return GetLatestObservedCallCore(callHistory, timeProvider);
    }

    private static ObservedGrainCall? GetLatestObservedCallCore(CallHistory callHistory, TimeProvider? timeProvider)
    {
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
                incomingClientCall.Method,
                timeProvider?.GetUtcNow().UtcDateTime ?? DateTime.UnixEpoch);
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
            incomingCall.Method,
            timeProvider?.GetUtcNow().UtcDateTime ?? DateTime.UnixEpoch);
    }

    private string GenerateMermaidDiagramInternal(IReadOnlyCollection<ObservedGrainCall>? observedCalls)
    {
        var sb = new StringBuilder();
        sb.AppendLine("graph LR");

        var nodes = new Dictionary<string, (string Id, string Label)>(StringComparer.Ordinal);
        var assignedIds = new HashSet<string>(StringComparer.Ordinal);
        var suffix = 1;
        var edges = BuildDiagramEdges(observedCalls);
        foreach (var edge in edges.OrderBy(static e => e.Source, StringComparer.Ordinal).ThenBy(static e => e.Target, StringComparer.Ordinal))
        {
            var arrow = GetArrowForEdge(edge);
            var label = BuildMethodLabel(edge.Transitions);
            var hasMethodLabel = !string.IsNullOrEmpty(label);

            var source = GetNode(edge.Source);
            var target = GetNode(edge.Target);

            sb
                .Append("    ")
                .Append(source.Id)
                .Append("[\"")
                .Append(source.Label)
                .Append("\"] ")
                .Append(arrow);

            if (hasMethodLabel || edge.Hits.HasValue)
            {
                sb.Append('|').Append(label);
                if (edge.Hits is { } count)
                {
                    if (hasMethodLabel)
                    {
                        sb.Append("<br/>");
                    }

                    sb.Append(CultureInfo.CurrentCulture, $"hits: {count}");
                }

                sb.Append('|');
            }

            sb
                .Append(' ')
                .Append(target.Id)
                .Append("[\"")
                .Append(target.Label)
                .AppendLine("\"]");
        }

        return sb.ToString();

        (string Id, string Label) GetNode(string vertex)
        {
            if (nodes.TryGetValue(vertex, out var node))
            {
                return node;
            }

            var baseId = ToMermaidId(vertex);
            var id = baseId;
            while (!assignedIds.Add(id))
            {
                id = string.Create(CultureInfo.InvariantCulture, $"{baseId}_{++suffix}");
            }

            node = (id, ToDisplayName(vertex));
            nodes.Add(vertex, node);
            return node;
        }
    }

    private static string GetArrowForEdge(DiagramEdge edge)
    {
        if (edge.IsObserved)
        {
            return "==>";
        }

        foreach (var transition in edge.Transitions)
        {
            if (transition.IsReentrant)
            {
                return "-.->";
            }
        }

        return "-->";
    }

    private static string BuildMethodLabel(HashSet<GrainTransition> transitions)
    {
        if (transitions.Count <= 1)
        {
            var items = transitions.GetEnumerator();
            if (!items.MoveNext())
            {
                return string.Empty;
            }

            var item = items.Current;
            return FormatMethodLabel((item.SourceMethod, item.TargetMethod, item.IsReentrant));
        }

        var methods = transitions
            .Select(static item => (item.SourceMethod, item.TargetMethod, item.IsReentrant))
            .Distinct()
            .Select(FormatMethodLabel)
            .OrderBy(static method => method, StringComparer.Ordinal)
            .ToArray();
        return string.Join("<br/>", methods);
    }

    private static string FormatMethodLabel((string Source, string Target, bool IsReentrant) method)
    {
        if (method.Source == Constants.AnyMethod && method.Target == Constants.AnyMethod)
        {
            return method.IsReentrant ? "all (reentrant)" : "all";
        }

        if (method.Source == Constants.AnyMethod)
        {
            return method.Target;
        }

        return method.Source == method.Target ? method.Source : $"{method.Source}->{method.Target}";
    }

    private IEnumerable<DiagramEdge> BuildDiagramEdges(IReadOnlyCollection<ObservedGrainCall>? observedCalls)
    {
        Dictionary<(string Source, string Target), (HashSet<GrainTransition> Transitions, long? Hits)>? observedEdges = null;
        if (observedCalls is not null)
        {
            observedEdges = new();
            foreach (var observedCall in observedCalls)
            {
                var key = (observedCall.Source, observedCall.Target);
                ref var edge = ref CollectionsMarshal.GetValueRefOrAddDefault(observedEdges, key, out _);
                edge.Transitions ??= new HashSet<GrainTransition>();
                edge.Transitions.Add(new GrainTransition(observedCall.SourceMethod, observedCall.TargetMethod));
                if (!string.IsNullOrWhiteSpace(key.Source) && !string.IsNullOrWhiteSpace(key.Target))
                {
                    edge.Hits = edge.Hits.GetValueOrDefault() + observedCall.Count;
                }
            }
        }

        foreach (var (source, target, transitions) in _grainGraph.GetAllEdges())
        {
            if (observedEdges is null || !observedEdges.ContainsKey((source, target)))
            {
                yield return new DiagramEdge(source, target, transitions);
            }
        }

        if (observedEdges is not null)
        {
            foreach (var (key, edge) in observedEdges)
            {
                yield return new DiagramEdge(key.Source, key.Target, edge.Transitions, IsObserved: true, Hits: edge.Hits);
            }
        }
    }

    private readonly record struct DiagramEdge(
        string Source, string Target, HashSet<GrainTransition> Transitions, bool IsObserved = false, long? Hits = null);

    private bool CheckTransitionAllowed(string source, string target, string sourceMethod, string targetMethod, bool throwOnViolation)
    {
        if (_allowAllByDefault || _grainGraph.IsTransitionAllowed(source, target, sourceMethod, targetMethod))
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

    private static void AddObservedEdge(Dictionary<ObservedGrainCallKey, ObservedGrainCall> edges, ObservedGrainCallKey key, DateTime observedAtUtc)
    {
        ref var existing = ref CollectionsMarshal.GetValueRefOrAddDefault(edges, key, out var exists);
        existing = exists
            ? existing!.Merge(1, observedAtUtc, observedAtUtc)
            : ObservedGrainCall.Create(key.Source, key.Target, key.SourceMethod, key.TargetMethod, observedAtUtc);
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
        return string.Create(fullName.Length, fullName, static (destination, name) =>
        {
            for (var index = 0; index < destination.Length; index++)
            {
                var character = name[index];
                destination[index] = char.IsLetterOrDigit(character) ? character : '_';
            }
        });
    }

    private static string ToDisplayName(string fullName)
    {
        var lastDot = fullName.LastIndexOf('.');
        return lastDot >= 0 && lastDot < fullName.Length - 1
            ? fullName[(lastDot + 1)..]
            : fullName;
    }

    private static bool CanReach(GrainId current, GrainId target, Dictionary<GrainId, List<GrainId>> graph, HashSet<GrainId> visited)
    {
        Stack<GrainId>? pending = null;
        while (true)
        {
            if (current.Equals(target))
            {
                return true;
            }

            if (visited.Add(current) && graph.TryGetValue(current, out var neighbors) && neighbors.Count > 0)
            {
                // Follow the first edge directly. Only branches need a worklist.
                for (var index = neighbors.Count - 1; index > 0; index--)
                {
                    (pending ??= new Stack<GrainId>()).Push(neighbors[index]);
                }

                current = neighbors[0];
                continue;
            }

            if (pending is null || !pending.TryPop(out current))
            {
                return false;
            }
        }
    }
}

using System.Runtime.InteropServices;
using ManagedCode.Orleans.Graph.Models;

namespace ManagedCode.Orleans.Graph;

public class DirectedGraph(bool allowSelfLoops = false)
{
    private readonly Dictionary<string, Dictionary<string, HashSet<GrainTransition>>> _adjacencyList = new(StringComparer.Ordinal);
    private readonly bool _allowSelfLoops = allowSelfLoops;

    public void AddVertex(string vertex)
    {
        ref var targets = ref CollectionsMarshal.GetValueRefOrAddDefault(_adjacencyList, vertex, out _);
        targets ??= new Dictionary<string, HashSet<GrainTransition>>(StringComparer.Ordinal);
    }

    public void AddTransition(string source, string target, GrainTransition transition) =>
        AddTransition(source, target, transition, validateEntireGraph: true);

    internal void AddTransitionToAcyclicGraph(string source, string target, GrainTransition transition) =>
        AddTransition(source, target, transition, validateEntireGraph: false);

    private void AddTransition(string source, string target, GrainTransition transition, bool validateEntireGraph)
    {
        if (!_allowSelfLoops && string.Equals(source, target, StringComparison.Ordinal))
        {
            return;
        }

        AddVertex(source);
        AddVertex(target);

        var sourceTargets = _adjacencyList[source];
        ref var transitions = ref CollectionsMarshal.GetValueRefOrAddDefault(sourceTargets, target, out var exists);
        if (!exists || transitions is null)
        {
            transitions = new HashSet<GrainTransition>();
        }

        // For an acyclic graph, only a new blocking edge back into the graph can close a cycle.
        var checkCycle = !transition.IsReentrant && (validateEntireGraph ||
            (!string.Equals(source, target, StringComparison.Ordinal) &&
             _adjacencyList[target].Count > 0 && ContainsOnlyReentrantTransitions(transitions)));

        if (!transitions.Add(transition))
        {
            return;
        }

        if (checkCycle && HasCycle())
        {
            transitions.Remove(transition);
            if (transitions.Count == 0)
            {
                sourceTargets.Remove(target);
            }
            throw new InvalidOperationException($"Adding transition from {source} to {target} creates a cycle.");
        }
    }

    public bool IsTransitionAllowed(string source, string target, string sourceMethod, string targetMethod)
    {
        if (!_adjacencyList.TryGetValue(source, out var targets) || !targets.TryGetValue(target, out var transitions))
        {
            return false;
        }

        foreach (var transition in transitions)
        {
            if (transition.MatchesMethods(sourceMethod, targetMethod))
            {
                return true;
            }
        }

        return false;
    }

    public bool HasCycle()
    {
        Dictionary<string, int>? incoming = null;
        foreach (var (source, targets) in _adjacencyList)
        {
            foreach (var (target, transitions) in targets)
            {
                if (IsBlockingEdge(source, target, transitions))
                {
                    incoming ??= new Dictionary<string, int>(_adjacencyList.Count, StringComparer.Ordinal);
                    CollectionsMarshal.GetValueRefOrAddDefault(incoming, target, out _)++;
                }
            }
        }

        if (incoming is null)
        {
            return false;
        }

        var ready = new Queue<string>();
        foreach (var vertex in _adjacencyList.Keys)
        {
            if (!incoming.ContainsKey(vertex))
            {
                ready.Enqueue(vertex);
            }
        }

        var remaining = _adjacencyList.Count;
        while (ready.TryDequeue(out var source))
        {
            remaining--;
            foreach (var (target, transitions) in _adjacencyList[source])
            {
                if (IsBlockingEdge(source, target, transitions) && --incoming[target] == 0)
                {
                    ready.Enqueue(target);
                }
            }
        }

        return remaining > 0;
    }

    private bool IsBlockingEdge(string source, string target, HashSet<GrainTransition> transitions) =>
        (!_allowSelfLoops || !string.Equals(source, target, StringComparison.Ordinal)) &&
        !ContainsOnlyReentrantTransitions(transitions);

    public IEnumerable<(string Source, string Target, HashSet<GrainTransition> Transitions)> GetAllEdges()
    {
        foreach (var (source, targets) in _adjacencyList)
        {
            foreach (var (target, transitions) in targets)
            {
                yield return (source, target, transitions);
            }
        }
    }

    public bool HasVertex(string vertex) => _adjacencyList.ContainsKey(vertex);

    public bool HasEdge(string source, string target) =>
        _adjacencyList.TryGetValue(source, out var targets) && targets.ContainsKey(target);

    public bool HasReentrantTransition(string source, string target)
    {
        if (!_adjacencyList.TryGetValue(source, out var targets))
        {
            return false;
        }

        if (!targets.TryGetValue(target, out var transitions))
        {
            return false;
        }

        foreach (var transition in transitions)
        {
            if (transition.IsReentrant)
            {
                return true;
            }
        }

        return false;
    }

    public IEnumerable<string> GetVertices() => _adjacencyList.Keys;

    public IEnumerable<string> GetNeighbors(string vertex) =>
        _adjacencyList.TryGetValue(vertex, out var value) ? value.Keys : Enumerable.Empty<string>();

    private static bool ContainsOnlyReentrantTransitions(HashSet<GrainTransition> transitions)
    {
        foreach (var transition in transitions)
        {
            if (!transition.IsReentrant)
            {
                return false;
            }
        }

        return true;
    }
}

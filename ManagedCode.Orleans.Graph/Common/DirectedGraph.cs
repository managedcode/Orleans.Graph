using System.Runtime.InteropServices;
using ManagedCode.Orleans.Graph.Models;

namespace ManagedCode.Orleans.Graph;

public class DirectedGraph(bool allowSelfLoops = false)
{
    private readonly Dictionary<string, Dictionary<string, HashSet<GrainTransition>>> _adjacencyList = new(StringComparer.Ordinal);
    private readonly HashSet<string> _vertices = new(StringComparer.Ordinal);
    private readonly bool _allowSelfLoops = allowSelfLoops;

    public void AddVertex(string vertex)
    {
        if (_vertices.Add(vertex))
        {
            _adjacencyList[vertex] = new Dictionary<string, HashSet<GrainTransition>>(StringComparer.Ordinal);
        }
    }

    public void AddTransition(string source, string target, GrainTransition transition)
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

        transitions.Add(transition);

        if (!transition.IsReentrant && HasCycle())
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
        var visited = new HashSet<string>(_vertices.Count, StringComparer.Ordinal);
        var recursionStack = new HashSet<string>(_vertices.Count, StringComparer.Ordinal);

        foreach (var vertex in _vertices)
        {
            if (IsCyclicUtil(vertex, visited, recursionStack))
            {
                return true;
            }
        }
        return false;
    }

    private bool IsCyclicUtil(string vertex, HashSet<string> visited, HashSet<string> recursionStack)
    {
        if (!visited.Contains(vertex))
        {
            _ = visited.Add(vertex);
            recursionStack.Add(vertex);

            if (_adjacencyList.TryGetValue(vertex, out var value))
            {
                foreach (var (neighbor, transitions) in value)
                {
                    if (ContainsOnlyReentrantTransitions(transitions))
                    {
                        continue;
                    }

                    if (_allowSelfLoops && string.Equals(vertex, neighbor, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!visited.Contains(neighbor) && IsCyclicUtil(neighbor, visited, recursionStack))
                    {
                        return true;
                    }
                    else if (recursionStack.Contains(neighbor))
                    {
                        return true;
                    }
                }
            }
        }
        recursionStack.Remove(vertex);
        return false;
    }

    public IEnumerable<(string Source, string Target, HashSet<GrainTransition> Transitions)> GetAllEdges()
    {
        foreach (var source in _vertices)
        {
            foreach (var targetEntry in _adjacencyList[source])
            {
                yield return (source, targetEntry.Key, targetEntry.Value);
            }
        }
    }

    public bool HasVertex(string vertex) => _vertices.Contains(vertex);

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

    public IEnumerable<string> GetVertices() => _vertices;

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

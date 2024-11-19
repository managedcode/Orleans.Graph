using System;
using System.Collections.Generic;
using System.Linq;
using ManagedCode.Orleans.Graph.Models;

namespace ManagedCode.Orleans.Graph;

public class DirectedGraph
{
    private readonly Dictionary<string, Dictionary<string, HashSet<GrainTransition>>> _adjacencyList;
    private readonly HashSet<string> _vertices;
    private readonly bool _allowSelfLoops;

    public DirectedGraph(bool allowSelfLoops = false)
    {
        _allowSelfLoops = allowSelfLoops;
        _adjacencyList = new Dictionary<string, Dictionary<string, HashSet<GrainTransition>>>();
        _vertices = new HashSet<string>();
    }

    public void AddVertex(string vertex)
    {
        if (!_vertices.Contains(vertex))
        {
            _vertices.Add(vertex);
            _adjacencyList[vertex] = new Dictionary<string, HashSet<GrainTransition>>();
        }
    }

    public void AddTransition(string source, string target, GrainTransition transition)
    {
        if (!_allowSelfLoops && source.Equals(target))
            return;

        AddVertex(source);
        AddVertex(target);

        if (!_adjacencyList[source].ContainsKey(target))
        {
            _adjacencyList[source][target] = new HashSet<GrainTransition>();
        }
        
        _adjacencyList[source][target].Add(transition);

        if (!transition.IsReentrant && HasCycle())
        {
            _adjacencyList[source][target].Remove(transition);
            if (!_adjacencyList[source][target].Any())
            {
                _adjacencyList[source].Remove(target);
            }
            throw new InvalidOperationException($"Adding transition from {source} to {target} creates a cycle.");
        }
    }

    public bool IsTransitionAllowed(string source, string target, string sourceMethod, string targetMethod)
    {
        if (!_adjacencyList.ContainsKey(source) || !_adjacencyList[source].ContainsKey(target))
            return false;

        return _adjacencyList[source][target]
            .Any(t => t.MatchesMethods(sourceMethod, targetMethod));
    }

    public bool HasCycle()
    {
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();

        foreach (var vertex in _vertices)
        {
            if (IsCyclicUtil(vertex, visited, recursionStack))
                return true;
        }
        return false;
    }

    private bool IsCyclicUtil(string vertex, HashSet<string> visited, HashSet<string> recursionStack)
    {
        if (!visited.Contains(vertex))
        {
            visited.Add(vertex);
            recursionStack.Add(vertex);

            if (_adjacencyList.ContainsKey(vertex))
            {
                foreach (var neighbor in _adjacencyList[vertex].Keys)
                {
                    if (!visited.Contains(neighbor) && IsCyclicUtil(neighbor, visited, recursionStack))
                        return true;
                    else if (recursionStack.Contains(neighbor))
                        return true;
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
        _adjacencyList.ContainsKey(source) && _adjacencyList[source].ContainsKey(target);

    public IEnumerable<string> GetVertices() => _vertices;

    public IEnumerable<string> GetNeighbors(string vertex) =>
        _adjacencyList.ContainsKey(vertex) ? _adjacencyList[vertex].Keys : Enumerable.Empty<string>();
}
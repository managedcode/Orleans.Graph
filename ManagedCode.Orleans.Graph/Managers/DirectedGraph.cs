using System;
using System.Collections.Generic;

namespace ManagedCode.Orleans.Graph;

public class DirectedGraph<T> where T : class
{
    private readonly Dictionary<T, HashSet<T>> _adjacencyList;
    private readonly bool _allowSelfLoops;
    private readonly HashSet<T> _vertices;

    public DirectedGraph(bool allowSelfLoops = false)
    {
        _allowSelfLoops = allowSelfLoops;
        _adjacencyList = new Dictionary<T, HashSet<T>>();
        _vertices = new HashSet<T>();
    }

    public void AddVertex(T vertex)
    {
        if (!_vertices.Contains(vertex))
        {
            _vertices.Add(vertex);
            _adjacencyList[vertex] = new HashSet<T>();
        }
    }

    public void AddEdge(T source, T destination)
    {
        if (_allowSelfLoops && source.Equals(destination))
            return;

        // Add vertices if they don't exist
        AddVertex(source);
        AddVertex(destination);

        _adjacencyList[source]
            .Add(destination);

        // Check for cycles immediately when adding edge
        if (HasCycle())
            throw new InvalidOperationException($"Adding edge from {source} to {destination} creates a cycle in the graph.");
    }

    public bool IsTransitionAllowed(T source, T destination)
    {
        if (_allowSelfLoops && source.Equals(destination))
            return true;

        return _adjacencyList.ContainsKey(source) && _adjacencyList[source]
            .Contains(destination);
    }

    private bool HasCycle()
    {
        var visited = new HashSet<T>();
        var recursionStack = new HashSet<T>();

        foreach (var vertex in _vertices)
            if (IsCyclicUtil(vertex, visited, recursionStack))
                return true;

        return false;
    }

    private bool IsCyclicUtil(T vertex, HashSet<T> visited, HashSet<T> recursionStack)
    {
        if (!visited.Contains(vertex))
        {
            visited.Add(vertex);
            recursionStack.Add(vertex);

            foreach (var neighbor in _adjacencyList[vertex])
            {
                if (!visited.Contains(neighbor) && IsCyclicUtil(neighbor, visited, recursionStack))
                    return true;

                if (recursionStack.Contains(neighbor))
                    return true;
            }
        }

        recursionStack.Remove(vertex);
        return false;
    }

    public IEnumerable<T> GetAllVertices()
    {
        return _vertices;
    }

    public IEnumerable<T> GetAdjacentVertices(T vertex)
    {
        return _adjacencyList.TryGetValue(vertex, out var value) ? value : [];
    }
}
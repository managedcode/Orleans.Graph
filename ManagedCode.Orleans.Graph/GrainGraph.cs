using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;

namespace ManagedCode.Orleans.Graph;

using System;
using System.Collections.Generic;
using System.Linq;

public class DirectedGraph<T> where T : class
{
    private readonly Dictionary<T, HashSet<T>> _adjacencyList;
    private readonly HashSet<T> _vertices;

    public DirectedGraph()
    {
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
        // Add vertices if they don't exist
        AddVertex(source);
        AddVertex(destination);

        _adjacencyList[source].Add(destination);
        
        // Check for cycles immediately when adding edge
        if (HasCycle())
        {
            throw new InvalidOperationException($"Adding edge from {source} to {destination} creates a cycle in the graph");
        }
    }

    public bool IsTransitionAllowed(T source, T destination)
    {
        return _adjacencyList.ContainsKey(source) && _adjacencyList[source].Contains(destination);
    }

    private bool HasCycle()
    {
        var visited = new HashSet<T>();
        var recursionStack = new HashSet<T>();

        foreach (var vertex in _vertices)
        {
            if (IsCyclicUtil(vertex, visited, recursionStack))
            {
                return true;
            }
        }

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
                {
                    return true;
                }
                else if (recursionStack.Contains(neighbor))
                {
                    return true;
                }
            }
        }

        recursionStack.Remove(vertex);
        return false;
    }

    public IEnumerable<T> GetAllVertices() => _vertices;
    
    public IEnumerable<T> GetAdjacentVertices(T vertex)
    {
        return _adjacencyList.TryGetValue(vertex, out var value) ? value : [];
    }
}

[GrainGraphConfiguration]
public class GrainGraphManager
{
    private readonly DirectedGraph<Type> _grainGraph;

    public GrainGraphManager()
    {
        _grainGraph = new DirectedGraph<Type>();
    }

    public GrainGraphManager AddAllowedTransition(Type sourceGrain, Type targetGrain)
    {
        _grainGraph.AddEdge(sourceGrain, targetGrain);
        return this;
    }
    
    public GrainGraphManager AddAllowedTransition<T1, T2>()
    {
        return AddAllowedTransition(typeof(T1), typeof(T2));
    }

    public bool IsTransitionAllowed(Type sourceGrain, Type targetGrain)
    {
        return _grainGraph.IsTransitionAllowed(sourceGrain, targetGrain);
    }
}


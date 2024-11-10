using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;

namespace ManagedCode.Orleans.Graph;

// Direction type for transitions
public enum TransitionDirection
{
    OneWay,
    BiDirectional
}

// Interface for defining transitions
public interface IGraphBuilder
{
    // Add single transition
    IGraphBuilder AddTransition<TFrom, TTo>(TransitionDirection direction = TransitionDirection.OneWay) 
        where TFrom : IGrain 
        where TTo : IGrain;
    
    // Add multiple transitions at once
    IGraphBuilder AddTransitions<TGrain>(params Type[] targetGrainTypes) 
        where TGrain : IGrain;
    
    // Check if transition is allowed
    bool IsTransitionAllowed<TFrom, TTo>() 
        where TFrom : IGrain 
        where TTo : IGrain;
}

// Internal storage implementation 
public class GrainGraphManager : IGraphBuilder
{
    private readonly HashSet<(Type From, Type To)> _allowedTransitions = new();
    
    public IGraphBuilder AddTransition<TFrom, TTo>(TransitionDirection direction = TransitionDirection.OneWay)
        where TFrom : IGrain
        where TTo : IGrain
    {
        _allowedTransitions.Add((typeof(TFrom), typeof(TTo)));
        
        if (direction == TransitionDirection.BiDirectional)
        {
            _allowedTransitions.Add((typeof(TTo), typeof(TFrom)));
        }
        
        return this;
    }
    
    public IGraphBuilder AddTransitions<TGrain>(params Type[] targetGrainTypes) 
        where TGrain : IGrain
    {
        foreach (var targetType in targetGrainTypes)
        {
            _allowedTransitions.Add((typeof(TGrain), targetType));
        }
        return this;
    }
    
    public bool IsTransitionAllowed<TFrom, TTo>()
        where TFrom : IGrain
        where TTo : IGrain
    {
        return _allowedTransitions.Contains((typeof(TFrom), typeof(TTo)));
    }
    
    public bool IsTransitionAllowed(Type? fromType, Type? toType)
    {
        if (fromType == null || toType == null)
        {
            return false;
        }
        return _allowedTransitions.Contains((fromType, toType));
    }
}


public record GrainTransitionKey(string FromInterface, string MethodName, string ToInterface);

public interface IGrainGraph
{
    IGrainGraph AddTransition<TFrom, TTo>(string methodName, TransitionDirection direction = TransitionDirection.OneWay)
        where TFrom : IGrain
        where TTo : IGrain;
        
    bool IsTransitionAllowed(string fromInterface, string methodName, string toInterface);
    
    IReadOnlyCollection<GrainTransitionKey> GetAllowedTransitions();
}

public class GrainGraph : IGrainGraph
{
    private readonly HashSet<GrainTransitionKey> _allowedTransitions = new();
    private readonly object _lock = new();

    public IGrainGraph AddTransition<TFrom, TTo>(string methodName, TransitionDirection direction = TransitionDirection.OneWay) 
        where TFrom : IGrain 
        where TTo : IGrain
    {
        var fromType = typeof(TFrom).Name;
        var toType = typeof(TTo).Name;

        lock (_lock)
        {
            _allowedTransitions.Add(new GrainTransitionKey(fromType, methodName, toType));

            if (direction == TransitionDirection.BiDirectional)
            {
                _allowedTransitions.Add(new GrainTransitionKey(toType, methodName, fromType));
            }
        }

        return this;
    }

    public bool IsTransitionAllowed(string fromInterface, string methodName, string toInterface)
    {
        var key = new GrainTransitionKey(fromInterface, methodName, toInterface);
        lock (_lock)
        {
            return _allowedTransitions.Contains(key);
        }
    }

    public IReadOnlyCollection<GrainTransitionKey> GetAllowedTransitions()
    {
        lock (_lock)
        {
            return _allowedTransitions.ToList().AsReadOnly();
        }
    }
}
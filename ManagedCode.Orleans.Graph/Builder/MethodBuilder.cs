using System;
using System.Linq.Expressions;
using Orleans;

namespace ManagedCode.Orleans.Graph;

public class MethodBuilder<TSource, TTarget> : IMethodBuilder<TSource, TTarget> where TSource : IGrain where TTarget : IGrain
{
    private readonly GrainCallsBuilder _parent;
    private readonly string _sourceType;
    private readonly string _targetType;

    public MethodBuilder(GrainCallsBuilder parent, string sourceType, string targetType)
    {
        _parent = parent;
        _sourceType = sourceType;
        _targetType = targetType;
    }

    public IMethodBuilder<TSource, TTarget> Method(Expression<Action<TSource>> source, Expression<Action<TTarget>> target)
    {
        var sourceMethod = ExtractMethodName(source);
        var targetMethod = ExtractMethodName(target);
        _parent.AddMethodRule(_sourceType, _targetType, sourceMethod, targetMethod);
        return this;
    }

    public IMethodBuilder<TSource, TTarget> Methods(params (Expression<Action<TSource>> source, Expression<Action<TTarget>> target)[] methods)
    {
        foreach (var (source, target) in methods)
        {
            var sourceMethod = ExtractMethodName(source);
            var targetMethod = ExtractMethodName(target);
            _parent.AddMethodRule(_sourceType, _targetType, sourceMethod, targetMethod);
        }
        return this;
    }

    public IMethodBuilder<TSource, TTarget> WithReentrancy()
    {
        _parent.AddReentrancy(_sourceType, _targetType);
        return this;
    }

    public IMethodBuilder<TSource, TTarget> AllMethods()
    {
        _parent.AddMethodRule(_sourceType, _targetType, "*", "*");
        return this;
    }

    public IGrainCallsBuilder And() => _parent;

    public IMethodBuilder<TSource, TTarget> MethodByName(string sourceMethodName, string targetMethodName)
    {
        _parent.AddMethodRule(_sourceType, _targetType, sourceMethodName, targetMethodName);
        return this;
    }

    public IMethodBuilder<TSource, TTarget> MethodsByName(params (string sourceMethodName, string targetMethodName)[] methods)
    {
        foreach (var (sourceMethodName, targetMethodName) in methods)
        {
            _parent.AddMethodRule(_sourceType, _targetType, sourceMethodName, targetMethodName);
        }
        return this;
    }
    
    public IMethodBuilder<TSource, TTarget> AllowClientCallGrain()
    {
        _parent.AllowClientCallGrain<TSource>();
        _parent.AllowClientCallGrain<TTarget>();
        return this;
    }

    public IMethodBuilder<TSource, TTarget> AllSourceMethodsToSpecificTargetMethods(params string[] targetMethods)
    {
        foreach (var targetMethod in targetMethods)
        {
            _parent.AddMethodRule(_sourceType, _targetType, "*", targetMethod);
        }
        return this;
    }

    public IMethodBuilder<TSource, TTarget> SpecificSourceMethodsToAllTargetMethods(params string[] sourceMethods)
    {
        foreach (var sourceMethod in sourceMethods)
        {
            _parent.AddMethodRule(_sourceType, _targetType, sourceMethod, "*");
        }
        return this;
    }

    private string ExtractMethodName<T>(Expression<Action<T>> expression) where T : IGrain
    {
        return (expression.Body as MethodCallExpression)?.Method.Name
               ?? throw new ArgumentException("Expression must be a method call");
    }
}
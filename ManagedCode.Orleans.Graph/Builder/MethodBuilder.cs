using System;
using System.Linq.Expressions;
using Orleans;

namespace ManagedCode.Orleans.Graph;

public class MethodBuilder : IMethodBuilder
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

    public IMethodBuilder Method<TSource, TTarget>(Expression<Action<TSource>> source, Expression<Action<TTarget>> target) where TSource : IGrain where TTarget : IGrain
    {
        var sourceMethod = ExtractMethodName(source);
        var targetMethod = ExtractMethodName(target);
        _parent.AddMethodRule(_sourceType, _targetType, sourceMethod, targetMethod);
        return this;
    }

    public IMethodBuilder Methods<TSource, TTarget>(params (Expression<Action<TSource>> source, Expression<Action<TTarget>> target)[] methods) where TSource : IGrain where TTarget : IGrain
    {
        foreach (var (source, target) in methods)
        {
            var sourceMethod = ExtractMethodName(source);
            var targetMethod = ExtractMethodName(target);
            _parent.AddMethodRule(_sourceType, _targetType, sourceMethod, targetMethod);
        }
        return this;
    }

    public IMethodBuilder WithReentrancy()
    {
        _parent.AddReentrancy(_sourceType, _targetType);
        return this;
    }

    public IGrainCallsBuilder AllMethods()
    {
        _parent.AddMethodRule(_sourceType, _targetType, "*", "*");
        return _parent;
    }

    public IGrainCallsBuilder And() => _parent;

    private string ExtractMethodName<T>(Expression<Action<T>> expression) where T : IGrain
    {
        return (expression.Body as MethodCallExpression)?.Method.Name
               ?? throw new ArgumentException("Expression must be a method call");
    }
}
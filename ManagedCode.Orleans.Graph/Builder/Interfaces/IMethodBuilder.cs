using System;
using System.Linq.Expressions;
using Orleans;

namespace ManagedCode.Orleans.Graph;

public interface IMethodBuilder
{
    IMethodBuilder Method<TSource, TTarget>(Expression<Action<TSource>> source, Expression<Action<TTarget>> target) where TSource : IGrain where TTarget : IGrain;
    IMethodBuilder Methods<TSource, TTarget>(params (Expression<Action<TSource>> source, Expression<Action<TTarget>> target)[] methods) where TSource : IGrain where TTarget : IGrain;
    IMethodBuilder WithReentrancy();
    IGrainCallsBuilder AllMethods();
    IGrainCallsBuilder And();
}
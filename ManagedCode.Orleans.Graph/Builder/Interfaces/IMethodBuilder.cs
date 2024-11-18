using System;
using System.Linq.Expressions;
using Orleans;

namespace ManagedCode.Orleans.Graph;

/// <summary>
/// Interface for building methods in an Orleans graph.
/// </summary>
public interface IMethodBuilder
{
    /// <summary>
    /// Specifies a method transition between two grains.
    /// </summary>
    /// <typeparam name="TSource">The type of the source grain.</typeparam>
    /// <typeparam name="TTarget">The type of the target grain.</typeparam>
    /// <param name="source">The source method expression.</param>
    /// <param name="target">The target method expression.</param>
    /// <returns>The current instance of <see cref="IMethodBuilder"/>.</returns>
    IMethodBuilder Method<TSource, TTarget>(Expression<Action<TSource>> source, Expression<Action<TTarget>> target) where TSource : IGrain where TTarget : IGrain;

    /// <summary>
    /// Specifies multiple method transitions between two grains.
    /// </summary>
    /// <typeparam name="TSource">The type of the source grain.</typeparam>
    /// <typeparam name="TTarget">The type of the target grain.</typeparam>
    /// <param name="methods">An array of method transition expressions.</param>
    /// <returns>The current instance of <see cref="IMethodBuilder"/>.</returns>
    IMethodBuilder Methods<TSource, TTarget>(params (Expression<Action<TSource>> source, Expression<Action<TTarget>> target)[] methods) where TSource : IGrain where TTarget : IGrain;

    /// <summary>
    /// Enables reentrancy for the methods.
    /// </summary>
    /// <returns>The current instance of <see cref="IMethodBuilder"/>.</returns>
    IMethodBuilder WithReentrancy();

    /// <summary>
    /// Specifies that all methods should be included.
    /// </summary>
    /// <returns>An instance of <see cref="IGrainCallsBuilder"/>.</returns>
    IGrainCallsBuilder AllMethods();

    /// <summary>
    /// Adds a logical AND to the builder.
    /// </summary>
    /// <returns>An instance of <see cref="IGrainCallsBuilder"/>.</returns>
    IGrainCallsBuilder And();
}
using System;
using System.Linq.Expressions;
using Orleans;

namespace ManagedCode.Orleans.Graph;

/// <summary>
/// Interface for building methods in an Orleans graph.
/// </summary>
public interface IMethodBuilder<TSource, TTarget> where TSource : IGrain where TTarget : IGrain
{
    /// <summary>
    /// Specifies a method transition between two grains.
    /// </summary>
    /// <param name="source">The source method expression.</param>
    /// <param name="target">The target method expression.</param>
    /// <returns>The current instance of <see cref="IMethodBuilder{TSource, TTarget}"/>.</returns>
    IMethodBuilder<TSource, TTarget> Method(Expression<Action<TSource>> source, Expression<Action<TTarget>> target);

    /// <summary>
    /// Specifies multiple method transitions between two grains.
    /// </summary>
    /// <param name="methods">An array of method transition expressions.</param>
    /// <returns>The current instance of <see cref="IMethodBuilder{TSource, TTarget}"/>.</returns>
    IMethodBuilder<TSource, TTarget> Methods(params (Expression<Action<TSource>> source, Expression<Action<TTarget>> target)[] methods);

    /// <summary>
    /// Enables reentrancy for the methods.
    /// </summary>
    /// <returns>The current instance of <see cref="IMethodBuilder{TSource, TTarget}"/>.</returns>
    IMethodBuilder<TSource, TTarget> WithReentrancy();
    
    /// <summary>
    /// Allows client calls to the grain.
    /// </summary>
    /// <returns>The current instance of <see cref="IMethodBuilder{TSource, TTarget}"/>.</returns>
    IMethodBuilder<TSource, TTarget> AllowClientCallGrain();
    
    /// <summary>
    /// Specifies a method transition between two grains using method names.
    /// </summary>
    /// <param name="sourceMethodName">The source method name.</param>
    /// <param name="targetMethodName">The target method name.</param>
    /// <returns>The current instance of <see cref="IMethodBuilder{TSource, TTarget}"/>.</returns>
    IMethodBuilder<TSource, TTarget> MethodByName(string sourceMethodName, string targetMethodName);

    /// <summary>
    /// Specifies multiple method transitions between two grains using method names.
    /// </summary>
    /// <param name="methods">An array of method name pairs.</param>
    /// <returns>The current instance of <see cref="IMethodBuilder{TSource, TTarget}"/>.</returns>
    IMethodBuilder<TSource, TTarget> MethodsByName(params (string sourceMethodName, string targetMethodName)[] methods);

    /// <summary>
    /// Specifies that all methods of the source grain can call specific methods of the target grain.
    /// </summary>
    /// <param name="targetMethods">The target method names.</param>
    /// <returns>The current instance of <see cref="IMethodBuilder{TSource, TTarget}"/>.</returns>
    IMethodBuilder<TSource, TTarget> AllSourceMethodsToSpecificTargetMethods(params string[] targetMethods);

    /// <summary>
    /// Specifies that specific methods of the source grain can call all methods of the target grain.
    /// </summary>
    /// <param name="sourceMethods">The source method names.</param>
    /// <returns>The current instance of <see cref="IMethodBuilder{TSource, TTarget}"/>.</returns>
    IMethodBuilder<TSource, TTarget> SpecificSourceMethodsToAllTargetMethods(params string[] sourceMethods);

    /// <summary>
    /// Specifies that all methods should be included.
    /// </summary>
    /// <returns>The current instance of <see cref="IMethodBuilder{TSource, TTarget}"/>.</returns>
    IMethodBuilder<TSource, TTarget> AllMethods();
    

    /// <summary>
    /// Adds a logical AND to the builder.
    /// </summary>
    /// <returns>An instance of <see cref="IGrainCallsBuilder"/>.</returns>
    IGrainCallsBuilder And();
}
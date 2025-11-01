using Orleans;

namespace ManagedCode.Orleans.Graph;

/// <summary>
/// Interface for building grain calls in an Orleans graph.
/// </summary>
public interface IGrainCallsBuilder
{
    /// <summary>
    /// Specifies the starting grain type for the transition.
    /// </summary>
    /// <typeparam name="TGrain">The type of the grain.</typeparam>
    /// <returns>An instance of <see cref="ITransitionBuilder{TGrain}"/>.</returns>
    ITransitionBuilder<TGrain> From<TGrain>() where TGrain : IGrain;

    /// <summary>
    /// Allows the client to call the specified grain.
    /// </summary>
    /// <typeparam name="TGrain">The type of the grain.</typeparam>
    /// <returns>The current instance of <see cref="IGrainCallsBuilder"/>.</returns>
    IGrainCallsBuilder AllowClientCallGrain<TGrain>() where TGrain : IGrain;

    /// <summary>
    /// Allows all grain calls.
    /// </summary>
    /// <returns>The current instance of <see cref="IGrainCallsBuilder"/>.</returns>
    IGrainCallsBuilder AllowAll();

    /// <summary>
    /// Disallows all grain calls.
    /// </summary>
    /// <returns>The current instance of <see cref="IGrainCallsBuilder"/>.</returns>
    IGrainCallsBuilder DisallowAll();

    /// <summary>
    /// Adds a grain to the graph and allows defining method transitions for it.
    /// </summary>
    /// <typeparam name="TGrain">The type of the grain.</typeparam>
    /// <returns>The current instance of <see cref="IMethodBuilder{TGrain, TGrain}"/> to define method transitions.</returns>
    IMethodBuilder<TGrain, TGrain> AddGrain<TGrain>() where TGrain : IGrain;

    /// <summary>
    /// Defines a transition between two grains in the graph.
    /// </summary>
    /// <typeparam name="TFrom">The type of the source grain.</typeparam>
    /// <typeparam name="TTo">The type of the target grain.</typeparam>
    /// <returns>The current instance of <see cref="IGrainCallsBuilder"/>.</returns>
    IMethodBuilder<TFrom, TTo> AddGrainTransition<TFrom, TTo>() where TFrom : IGrain where TTo : IGrain;

    /// <summary>
    /// Adds a logical AND to the builder.
    /// </summary>
    /// <returns>The current instance of <see cref="IGrainCallsBuilder"/>.</returns>
    IGrainCallsBuilder And();

    /// <summary>
    /// Builds the grain graph manager.
    /// </summary>
    /// <returns>An instance of <see cref="GrainTransitionManager"/>.</returns>
    GrainTransitionManager Build();
}

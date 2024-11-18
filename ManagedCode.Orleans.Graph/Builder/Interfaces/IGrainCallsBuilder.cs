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
    /// <returns>An instance of <see cref="ITransitionBuilder"/>.</returns>
    ITransitionBuilder From<TGrain>() where TGrain : IGrain;

    /// <summary>
    /// Groups grains under a specified name.
    /// </summary>
    /// <param name="name">The name of the group.</param>
    /// <returns>An instance of <see cref="IGroupBuilder"/>.</returns>
    IGroupBuilder Group(string name);

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
    /// Adds a grain to the builder.
    /// </summary>
    /// <typeparam name="TGrain">The type of the grain.</typeparam>
    /// <returns>The current instance of <see cref="IGrainCallsBuilder"/>.</returns>
    IGrainCallsBuilder AddGrain<TGrain>() where TGrain : IGrain;

    /// <summary>
    /// Adds a logical AND to the builder.
    /// </summary>
    /// <returns>The current instance of <see cref="IGrainCallsBuilder"/>.</returns>
    IGrainCallsBuilder And();

    /// <summary>
    /// Builds the grain graph manager.
    /// </summary>
    /// <returns>An instance of <see cref="GrainGraphManager"/>.</returns>
    GrainGraphManager Build();
}

using Orleans;

namespace ManagedCode.Orleans.Graph;

/// <summary>
/// Interface for building transitions between grains in an Orleans graph.
/// </summary>
public interface ITransitionBuilder
{
    /// <summary>
    /// Specifies the target grain type for the transition.
    /// </summary>
    /// <typeparam name="TGrain">The type of the target grain.</typeparam>
    /// <returns>An instance of <see cref="IMethodBuilder"/>.</returns>
    IMethodBuilder To<TGrain>() where TGrain : IGrain;

    /// <summary>
    /// Adds a logical AND to the builder.
    /// </summary>
    /// <returns>An instance of <see cref="IGrainCallsBuilder"/>.</returns>
    IGrainCallsBuilder And();
}
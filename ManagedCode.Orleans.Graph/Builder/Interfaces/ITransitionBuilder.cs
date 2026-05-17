namespace ManagedCode.Orleans.Graph;

#pragma warning disable CA1716 // Fluent DSL intentionally uses To/And for readable chaining.
/// <summary>
/// Interface for building transitions between grains in an Orleans graph.
/// </summary>
public interface ITransitionBuilder<TFrom> where TFrom : IGrain
{
    /// <summary>
    /// Specifies the target grain type for the transition.
    /// </summary>
    /// <typeparam name="TToGrain">The type of the target grain.</typeparam>
    /// <returns>An instance of <see cref="IMethodBuilder{TFrom, TToGrain}"/>.</returns>
    IMethodBuilder<TFrom, TToGrain> To<TToGrain>() where TToGrain : IGrain;

    /// <summary>
    /// Adds a logical AND to the builder.
    /// </summary>
    /// <returns>An instance of <see cref="IGrainCallsBuilder"/>.</returns>
    IGrainCallsBuilder And();
}
#pragma warning restore CA1716

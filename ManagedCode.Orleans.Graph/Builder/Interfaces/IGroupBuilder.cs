using Orleans;

namespace ManagedCode.Orleans.Graph;

// /// <summary>
// /// Interface for building groups of grains in an Orleans graph.
// /// </summary>
// public interface IGroupBuilder
// {
//     /// <summary>
//     /// Adds a grain to the group.
//     /// </summary>
//     /// <typeparam name="TGrain">The type of the grain.</typeparam>
//     /// <returns>The current instance of <see cref="IGroupBuilder"/>.</returns>
//     IGroupBuilder AddGrain<TGrain>() where TGrain : IGrain;
//
//     /// <summary>
//     /// Allows calls within the group.
//     /// </summary>
//     /// <returns>The current instance of <see cref="IGroupBuilder"/>.</returns>
//     IGroupBuilder AllowCallsWithin();
//
//     /// <summary>
//     /// Adds a logical AND to the builder.
//     /// </summary>
//     /// <returns>An instance of <see cref="IGrainCallsBuilder"/>.</returns>
//     IGrainCallsBuilder And();
// }
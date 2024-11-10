using System;
using ManagedCode.Orleans.Graph.Filters;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Hosting;

namespace ManagedCode.Orleans.Graph.Extensions;

public static class OrleansGraphExtensions
{
    public static ISiloBuilder AddOrleansGraph(this ISiloBuilder builder)
    {
        builder.AddIncomingGrainCallFilter<GraphIncomingGrainCallFilter>();
        builder.AddOutgoingGrainCallFilter<GraphOutgoingGrainCallFilter>();
        return builder;
    }

    
    public static ISiloBuilder CreateGraph(this ISiloBuilder builder, Action<IGraphBuilder> graph)
    {
        var grainGraph = new GrainGraphManager();
        graph(grainGraph);
        builder.ConfigureServices(services => services.AddSingleton(grainGraph));
        return builder;
    }


    public static IClientBuilder AddOrleansGraph(this IClientBuilder builder)
    {
        builder.AddOutgoingGrainCallFilter<GraphOutgoingGrainCallFilter>();
        return builder;
    }
}
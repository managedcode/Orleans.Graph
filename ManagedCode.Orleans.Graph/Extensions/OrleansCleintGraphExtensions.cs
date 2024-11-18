using System;
using ManagedCode.Orleans.Graph.Filters;
using ManagedCode.Orleans.Graph.Models;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Hosting;

namespace ManagedCode.Orleans.Graph.Extensions;

public static class OrleansCleintGraphExtensions
{
    public static IClientBuilder AddOrleansGraph(this IClientBuilder builder)
    {
        return builder.AddOrleansGraph(_ => { });
    }


    public static IClientBuilder AddOrleansGraph(this IClientBuilder builder, Action<GraphCallFilterConfig> config)
    {
        var graphCallFilterConfig = new GraphCallFilterConfig();
        config.Invoke(graphCallFilterConfig);
        builder.Services.AddSingleton(graphCallFilterConfig);

        builder.AddOutgoingGrainCallFilter<GraphOutgoingGrainCallFilter>();
        return builder;
    }

    public static IClientBuilder CreateGraph(this IClientBuilder builder, Action<IGrainCallsBuilder> graphBuilder)
    {
        var grainGraph = new GrainCallsBuilder();
        graphBuilder(grainGraph);
        var manager = grainGraph.Build();
        
        builder.ConfigureServices(services => services.AddSingleton(manager));
        return builder;
    }
}
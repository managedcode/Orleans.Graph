using System;
using System.Reflection;
using ManagedCode.Orleans.Graph.Filters;
using ManagedCode.Orleans.Graph.Models;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Hosting;

namespace ManagedCode.Orleans.Graph.Extensions;

public static class OrleansCleintGraphExtensions
{
    public static IClientBuilder AddOrleansGraph(this IClientBuilder builder) =>
        builder.AddOrleansGraph(null, null);

    public static IClientBuilder AddOrleansGraph(this IClientBuilder builder, Action<GraphCallFilterConfig> configureFilters) =>
        builder.AddOrleansGraph(configureFilters, null);

    public static IClientBuilder AddOrleansGraph(this IClientBuilder builder, Action<IGrainCallsBuilder> configureGraph) =>
        builder.AddOrleansGraph(null, configureGraph);

    public static IClientBuilder AddOrleansGraph(this IClientBuilder builder, Action<GraphCallFilterConfig>? configureFilters, Action<IGrainCallsBuilder>? configureGraph, params Assembly[] assemblies)
    {
        var graphCallFilterConfig = new GraphCallFilterConfig();
        configureFilters?.Invoke(graphCallFilterConfig);
        builder.Services.AddSingleton(graphCallFilterConfig);

        builder.AddOutgoingGrainCallFilter<GraphOutgoingGrainCallFilter>();

        var manager = BuildManager(configureGraph, assemblies);
        builder.ConfigureServices(services => services.AddSingleton(manager));
        return builder;
    }

    private static GrainTransitionManager BuildManager(Action<IGrainCallsBuilder>? configureGraph, Assembly[]? assemblies)
    {
        var grainGraph = new GrainCallsBuilder();
        var assemblySet = assemblies is { Length: > 0 } ? assemblies : null;
        AttributeGraphConfigurator.ApplyFromAssemblies(grainGraph, assemblySet);
        configureGraph?.Invoke(grainGraph);
        return grainGraph.Build();
    }

    [Obsolete("AddOrleansGraph(configureGraph: ...) automatically registers the graph.")]
    public static IClientBuilder CreateGraph(this IClientBuilder builder, Action<IGrainCallsBuilder> graphBuilder)
    {
        var grainGraph = new GrainCallsBuilder();
        graphBuilder(grainGraph);
        var manager = grainGraph.Build();

        builder.ConfigureServices(services => services.AddSingleton(manager));
        return builder;
    }

    [Obsolete("AddOrleansGraph automatically scans assemblies for attributes.")]
    public static IClientBuilder CreateGraphFromAttributes(this IClientBuilder builder, params Assembly[] assemblies)
    {
        var manager = BuildManager(null, assemblies);
        builder.ConfigureServices(services => services.AddSingleton(manager));
        return builder;
    }
}

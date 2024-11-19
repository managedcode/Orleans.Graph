using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManagedCode.Orleans.Graph;

[Generator]
public class GrainCallsBuilderSourceGenerator : ISourceGenerator
{
    private const string DiagnosticId = "GCB001";
    private static readonly DiagnosticDescriptor CycleRule = new DiagnosticDescriptor(
        DiagnosticId,
        "Cycle detected in grain graph",
        "The grain configuration contains cycles in builder pattern",
        "GrainGraph",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForSyntaxNotifications(() => new GrainCallsBuilderSyntaxReceiver());
    }

    public void Execute(GeneratorExecutionContext context)
    {
        if (context.SyntaxReceiver is not GrainCallsBuilderSyntaxReceiver receiver)
            return;

        foreach (var graphConfig in receiver.GraphConfigurations)
        {
            var model = context.Compilation.GetSemanticModel(graphConfig.SyntaxTree);
            var transitions = FindTransitions(graphConfig, model);

            if (HasCycles(transitions))
            {
                context.ReportDiagnostic(Diagnostic.Create(CycleRule, graphConfig.GetLocation()));
            }
        }
    }

    private static IEnumerable<(ITypeSymbol Source, ITypeSymbol Target)> FindTransitions(InvocationExpressionSyntax graphConfig, SemanticModel model)
    {
        var transitions = new List<(ITypeSymbol, ITypeSymbol)>();

        var addTransitionCalls = graphConfig.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(i => i.Expression.ToString().Contains("AddGrainTransition"));

        foreach (var call in addTransitionCalls)
        {
            if (call.ArgumentList.Arguments.Count == 2)
            {
                var sourceArg = call.ArgumentList.Arguments[0].Expression;
                var targetArg = call.ArgumentList.Arguments[1].Expression;

                var sourceType = model.GetTypeInfo(sourceArg).Type;
                var targetType = model.GetTypeInfo(targetArg).Type;

                if (sourceType != null && targetType != null)
                {
                    transitions.Add((sourceType, targetType));
                }
            }
        }

        return transitions;
    }

    private static bool HasCycles(IEnumerable<(ITypeSymbol Source, ITypeSymbol Target)> transitions)
    {
        var graph = transitions.GroupBy(t => t.Source, SymbolEqualityComparer.Default)
            .ToDictionary(g => g.Key, g => g.Select(t => t.Target).ToHashSet(SymbolEqualityComparer.Default), SymbolEqualityComparer.Default);

        var visited = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var recursionStack = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        foreach (var vertex in graph.Keys)
        {
            if (IsCyclicUtil(vertex, graph, visited, recursionStack))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCyclicUtil(ISymbol vertex, Dictionary<ISymbol, HashSet<ISymbol>> graph, HashSet<ISymbol> visited, HashSet<ISymbol> recursionStack)
    {
        if (!visited.Contains(vertex))
        {
            visited.Add(vertex);
            recursionStack.Add(vertex);

            if (graph.TryGetValue(vertex, out var neighbors))
            {
                foreach (var neighbor in neighbors)
                {
                    if (!visited.Contains(neighbor) && IsCyclicUtil(neighbor, graph, visited, recursionStack))
                    {
                        return true;
                    }
                    if (recursionStack.Contains(neighbor))
                    {
                        return true;
                    }
                }
            }
        }

        recursionStack.Remove(vertex);
        return false;
    }

    private class GrainCallsBuilderSyntaxReceiver : ISyntaxReceiver
    {
        public List<InvocationExpressionSyntax> GraphConfigurations { get; } = new List<InvocationExpressionSyntax>();

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            if (syntaxNode is InvocationExpressionSyntax invocation && invocation.Expression.ToString().Contains("Create"))
            {
                GraphConfigurations.Add(invocation);
            }
        }
    }
}
// Suppress analyzer packaging warnings for in-assembly generator.
#pragma warning disable RS1036, RS1038, RS1041, RS1042, RS2008
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
    private static readonly DiagnosticDescriptor CycleRule = new(
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
        {
            return;
        }

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
            .Where(i =>
            {
                if (i.Expression is MemberAccessExpressionSyntax { Name: GenericNameSyntax genericName })
                {
                    return genericName.Identifier.Text is "AddGrainTransition" or "AddGrain";
                }

                return false;
            });

        foreach (var call in addTransitionCalls)
        {
            if (call.Expression is not MemberAccessExpressionSyntax { Name: GenericNameSyntax genericName })
            {
                continue;
            }

            if (genericName.Identifier.Text == "AddGrainTransition" && genericName.TypeArgumentList.Arguments.Count == 2)
            {
                var sourceArg = genericName.TypeArgumentList.Arguments[0];
                var targetArg = genericName.TypeArgumentList.Arguments[1];

                var sourceType = model.GetTypeInfo(sourceArg).Type;
                var targetType = model.GetTypeInfo(targetArg).Type;

                if (sourceType != null && targetType != null)
                {
                    transitions.Add((sourceType, targetType));
                }
            }
            else if (genericName.Identifier.Text == "AddGrain" && genericName.TypeArgumentList.Arguments.Count == 1)
            {
                var typeArg = genericName.TypeArgumentList.Arguments[0];
                var type = model.GetTypeInfo(typeArg).Type;
                if (type != null)
                {
                    transitions.Add((type, type));
                }
            }
        }

        return transitions;
    }

    private static bool HasCycles(IEnumerable<(ITypeSymbol Source, ITypeSymbol Target)> transitions)
    {
        var graph = new Dictionary<ITypeSymbol, HashSet<ITypeSymbol>>(SymbolEqualityComparer.Default);

        foreach (var (source, target) in transitions)
        {
            if (!graph.TryGetValue(source, out var neighbors))
            {
                neighbors = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
                graph[source] = neighbors;
            }

            neighbors.Add(target);
        }

        var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        var recursionStack = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var vertex in graph.Keys)
        {
            if (IsCyclicUtil(vertex, graph, visited, recursionStack))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCyclicUtil(ITypeSymbol vertex, Dictionary<ITypeSymbol, HashSet<ITypeSymbol>> graph, HashSet<ITypeSymbol> visited, HashSet<ITypeSymbol> recursionStack)
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

#pragma warning restore RS1036, RS1038, RS1041, RS1042, RS2008

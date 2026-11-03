// Suppress analyzer packaging warnings for in-assembly generator.
#pragma warning disable RS1036, RS1038, RS1041, RS1042, RS2008
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManagedCode.Orleans.Graph;

[Generator]
public class GrainCallsBuilderSourceGenerator : ISourceGenerator
{
    private const string DiagnosticId = "GCB001";
    private static readonly DiagnosticDescriptor _cycleRule = new(
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
                context.ReportDiagnostic(Diagnostic.Create(_cycleRule, graphConfig.GetLocation()));
            }
        }
    }

    private static IEnumerable<(ITypeSymbol Source, ITypeSymbol Target)> FindTransitions(InvocationExpressionSyntax graphConfig, SemanticModel model)
    {
        var transitions = new List<(ITypeSymbol, ITypeSymbol)>();

        foreach (var call in graphConfig.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (call.Expression is not MemberAccessExpressionSyntax { Name: GenericNameSyntax genericName } ||
                genericName.Identifier.ValueText != nameof(IGrainCallsBuilder.AddGrainTransition) ||
                genericName.TypeArgumentList.Arguments.Count != 2)
            {
                continue;
            }

            var sourceType = model.GetTypeInfo(genericName.TypeArgumentList.Arguments[0]).Type;
            var targetType = model.GetTypeInfo(genericName.TypeArgumentList.Arguments[1]).Type;
            // Same-interface rules are excluded from the runtime policy cycle check.
            if (sourceType is not null && targetType is not null &&
                !SymbolEqualityComparer.Default.Equals(sourceType, targetType))
            {
                transitions.Add((sourceType, targetType));
            }
        }

        return transitions;
    }

    private static bool HasCycles(IEnumerable<(ITypeSymbol Source, ITypeSymbol Target)> transitions)
    {
        var graph = new Dictionary<ITypeSymbol, (List<ITypeSymbol>? Targets, int Incoming)>(SymbolEqualityComparer.Default);

        foreach (var (source, target) in transitions)
        {
            ref var sourceNode = ref CollectionsMarshal.GetValueRefOrAddDefault(graph, source, out _);
            (sourceNode.Targets ??= []).Add(target);
            CollectionsMarshal.GetValueRefOrAddDefault(graph, target, out _).Incoming++;
        }

        var ready = new Queue<ITypeSymbol>();
        foreach (var (vertex, node) in graph)
        {
            if (node.Incoming == 0)
            {
                ready.Enqueue(vertex);
            }
        }

        var remaining = graph.Count;
        while (ready.TryDequeue(out var source))
        {
            remaining--;
            if (graph[source].Targets is not { } targets)
            {
                continue;
            }

            foreach (var target in targets)
            {
                ref var targetNode = ref CollectionsMarshal.GetValueRefOrNullRef(graph, target);
                if (--targetNode.Incoming == 0)
                {
                    ready.Enqueue(target);
                }
            }
        }

        return remaining > 0;
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

using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ManagedCode.Orleans.Graph.Tests;

public class SourceGeneratorTests
{
    [Test]
    public void SourceGenerator_ReportsCycleDiagnostic()
    {
        const string source = @"using ManagedCode.Orleans.Graph;
using Orleans;

[GrainGraphConfiguration]
public class GraphSetup
{
    public GraphSetup()
    {
        GrainCallsBuilder.Create()
            .AddGrainTransition<GrainA, GrainB>()
            .AllMethods()
            .And()
            .AddGrainTransition<GrainB, GrainA>()
            .AllMethods()
            .And()
            .Build();
    }
}

public interface GrainA : IGrain { }
public interface GrainB : IGrain { }
";

        RunGenerator(source).ShouldContain(d => d.Id == "GCB001");
    }

    [Test]
    [Arguments("AddGrain<GrainA>()")]
    [Arguments("AddGrainTransition<GrainA, GrainA>()")]
    public void SourceGenerator_AllowsSameGrainRules(string rule)
    {
        var source = $$"""
            using ManagedCode.Orleans.Graph;
            using Orleans;

            public static class GraphSetup
            {
                public static void Configure() => GrainCallsBuilder.Create()
                    .{{rule}}.AllMethods().And().Build();
            }

            public interface GrainA : IGrain { }
            """;

        RunGenerator(source).ShouldBeEmpty();
    }

    [Test]
    public void SourceGenerator_AllowsDeepAcyclicGraphWithRepeatedEdges()
    {
        const int depth = 4096;
        var source = new StringBuilder("""
            using ManagedCode.Orleans.Graph;
            using Orleans;
            using System;

            public static class GraphSetup
            {
                public static void Configure() => CreateGraph(builder =>
                {
            """);
        source.AppendLine();

        for (var index = 1; index < depth; index++)
        {
            source.AppendLine(CultureInfo.InvariantCulture, $"builder.AddGrainTransition<Grain{index - 1}, Grain{index}>().AllMethods();");
        }

        source.AppendLine("""
                    builder.AddGrainTransition<Grain0, Grain1>().AllMethods();
                    builder.AddGrainTransition<Grain0, Grain2>().AllMethods();
                });

                private static void CreateGraph(Action<IGrainCallsBuilder> configure) =>
                    configure(GrainCallsBuilder.Create());
            }
            """);

        for (var index = 0; index < depth; index++)
        {
            source.AppendLine(CultureInfo.InvariantCulture, $"public interface Grain{index} : IGrain {{ }}");
        }

        RunGenerator(source.ToString()).ShouldBeEmpty();
    }

    private static IReadOnlyCollection<Diagnostic> RunGenerator(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview));
        var references = CreateReferences();

        var compilation = CSharpCompilation.Create(
            "GeneratorTests",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new GrainCallsBuilderSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        return diagnostics;
    }

    private static IEnumerable<MetadataReference> CreateReferences()
    {
        var trustedAssemblies = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => MetadataReference.CreateFromFile(path));

        var additional = new[]
        {
            MetadataReference.CreateFromFile(typeof(GrainCallsBuilder).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(GrainGraphConfigurationAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IGrain).Assembly.Location)
        };

        return trustedAssemblies.Concat(additional);
    }
}

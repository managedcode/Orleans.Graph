using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace ManagedCode.Orleans.Graph.Tests;

public class SourceGeneratorTests
{
    [Fact]
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

        var syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview));
        var references = CreateReferences();

        var compilation = CSharpCompilation.Create(
            "GeneratorTests",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new GrainCallsBuilderSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "GCB001");
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

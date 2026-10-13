using ClankerWorld.Simulation.Society;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ClankerWorld.Tests;

public sealed class TypedMemoryStorageTests
{
    [Fact]
    public void RawInterpolatedStringsCannotEnterMemoryOrOrderEffectStorage()
    {
        const string source = """
            using ClankerWorld.Simulation;
            using ClankerWorld.Simulation.Society;
            using ClankerWorld.Simulation.World;
            class StorageControl
            {
                object Memory(string owner) => new SocietySocialMemory($"memory:{owner}", owner, owner, "Text", "private", 0);
                object Order(string owner) => new OrderTask("move_to", "doing", 1, 0, "moves", false, LastEffectId: $"effect:{owner}");
            }
            """;
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(SocietySocialMemory).Assembly.Location).Distinct(StringComparer.Ordinal)
            .Select(path => MetadataReference.CreateFromFile(path));
        var invalid = CSharpCompilation.Create("RawMemoryStorage", [CSharpSyntaxTree.ParseText(source)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var errors = invalid.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.Equal(2, errors.Length);
        Assert.All(errors, error => Assert.Equal("CS1503", error.Id));
        var validSource = source.Replace("$\"memory:{owner}\"", "Identity.Create($\"memory:{owner}\")", StringComparison.Ordinal)
            .Replace("$\"effect:{owner}\"", "Identity.Create($\"effect:{owner}\")", StringComparison.Ordinal);
        var valid = invalid.RemoveAllSyntaxTrees().AddSyntaxTrees(CSharpSyntaxTree.ParseText(validSource));
        Assert.Empty(valid.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error));
    }
}

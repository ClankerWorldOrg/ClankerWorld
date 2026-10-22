using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ClankerWorld.Tests;

public sealed class GoodsQueryBoundaryTests
{
    [Fact]
    public void DirectInventoryReadsStayWithinTheirRecordedFileBudgets()
    {
        var root = RuntimeWriterScan.RepositoryRoot;
        var compilation = RuntimeWriterScan.Compilation;
        var actual = CountReads(compilation, root);
        var path = Path.Combine(root, "tests/ClankerWorld.Tests/Architecture/goods-query-reads.json");
        var recorded = JsonSerializer.Deserialize<SortedDictionary<string, int>>(File.ReadAllText(path))!;
        AssertBudgets(recorded, actual);
        foreach (var (file, budget) in recorded)
            Assert.True(budget == actual.GetValueOrDefault(file),
                $"Lower {file}'s direct inventory read budget from {budget} to {actual.GetValueOrDefault(file)}.");
    }

    [Fact]
    public void AnAddedQueryCannotSpendAnotherFilesBudgetOrHideBehindAnAlias()
    {
        var compilation = ControlCompilation("""
            class Selection
            {
                object Pick(ClankerWorld.Simulation.Kernel.InventoryCheckpoint inventory)
                {
                    var alias = inventory;
                    // inventory.Lots in a comment is not a read.
                    var label = "inventory.Lots";
                    return alias.Lots;
                }
                object PickIndex(ClankerWorld.Simulation.Kernel.InventoryIndex index) => index?.Lots;
                object Validate(ClankerWorld.Simulation.Kernel.InventoryCheckpoint inventory) => inventory.Lots;
                object Other(Receipt receipt) => receipt.Lots;
            }
            class Receipt { public object Lots { get; } }
            """, "/source/src/ClankerWorld.Simulation/Selection.cs");
        var actual = CountReads(compilation, "/source");
        Assert.Equal(2, actual["src/ClankerWorld.Simulation/Selection.cs"]);
        var recorded = new SortedDictionary<string, int>(StringComparer.Ordinal)
        {
            ["src/ClankerWorld.Simulation/Selection.cs"] = 1,
            ["src/ClankerWorld.Simulation/Another.cs"] = 10,
        };
        Assert.ThrowsAny<Exception>(() => AssertBudgets(recorded, actual));
        recorded.Remove("src/ClankerWorld.Simulation/Selection.cs");
        Assert.ThrowsAny<Exception>(() => AssertBudgets(recorded, actual));
        recorded["src/ClankerWorld.Simulation/Selection.cs"] = 2;
        AssertBudgets(recorded, actual);
    }

    [Theory]
    [InlineData("/source/src/ClankerWorld.Simulation/World/Goods/Selection.cs")]
    [InlineData("/source/src/ClankerWorld.Simulation/World/WorldRuntime.Validation.cs")]
    public void SharedQueriesAndValidationDoNotSpendSelectionBudgets(string path)
    {
        var compilation = ControlCompilation("""
            class Selection
            {
                object Pick(ClankerWorld.Simulation.Kernel.InventoryCheckpoint inventory) => inventory.Lots;
            }
            """, path);
        Assert.Empty(CountReads(compilation, "/source"));
    }

    private static CSharpCompilation ControlCompilation(string source, string path) => CSharpCompilation.Create(
        "GoodsQueryControl", [CSharpSyntaxTree.ParseText("""
            namespace ClankerWorld.Simulation.Kernel
            {
                public class InventoryCheckpoint { public object Lots { get; } }
                public class InventoryIndex { public object Lots { get; } }
            }
            """), CSharpSyntaxTree.ParseText(source, path: path)],
        [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

    private static SortedDictionary<string, int> CountReads(CSharpCompilation compilation, string root)
    {
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var tree in compilation.SyntaxTrees.Where(tree => tree.FilePath.Length > 0))
        {
            var path = Path.GetRelativePath(root, tree.FilePath).Replace(Path.DirectorySeparatorChar, '/');
            if (path.Split('/').Contains("Goods", StringComparer.Ordinal) ||
                Path.GetFileNameWithoutExtension(path).EndsWith("Validation", StringComparison.Ordinal)) continue;
            var model = compilation.GetSemanticModel(tree);
            var count = tree.GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>().Count(name =>
                name.Identifier.ValueText == "Lots" &&
                !name.Ancestors().OfType<MethodDeclarationSyntax>().Any(method =>
                    method.Identifier.ValueText.StartsWith("Validate", StringComparison.Ordinal) ||
                    method.Identifier.ValueText.StartsWith("Ensure", StringComparison.Ordinal)) &&
                model.GetSymbolInfo(name).Symbol is IPropertySymbol property &&
                property.ContainingType.ToDisplayString() is "ClankerWorld.Simulation.Kernel.InventoryCheckpoint" or
                    "ClankerWorld.Simulation.Kernel.InventoryIndex");
            if (count > 0) counts[path] = count;
        }
        return counts;
    }

    private static void AssertBudgets(SortedDictionary<string, int> recorded, SortedDictionary<string, int> actual)
    {
        foreach (var (path, count) in actual)
            Assert.True(count <= recorded.GetValueOrDefault(path),
                $"{path}: {count} direct inventory reads exceed the recorded {recorded.GetValueOrDefault(path)}. " +
                "Use the shared goods query; review and lower budgets when reads move or disappear.");
    }
}

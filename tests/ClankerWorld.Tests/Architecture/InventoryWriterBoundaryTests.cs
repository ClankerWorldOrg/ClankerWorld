using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ClankerWorld.Tests;

public sealed class InventoryWriterBoundaryTests
{
    private static readonly HashSet<string> InventoryRecords = new(StringComparer.Ordinal)
    {
        "InventoryLot", "InventoryReservation", "DirectBarterOffer", "InventoryStorageChange",
        "InventoryEvent", "InventoryCheckpoint", "InventoryHold", "DirectBarterProposal",
    };

    [Fact]
    public void SharedInventoryRecordsHaveOnlyTheirNamedStoreWriter()
    {
        var compilation = RuntimeWriterScan.CreateCompilation(RuntimeWriterScan.RepositoryRoot);
        var violations = new List<string>();
        foreach (var tree in compilation.SyntaxTrees.Where(tree => tree.FilePath.Length > 0))
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var assignment in tree.GetRoot().DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                if (model.GetSymbolInfo(assignment.Left).Symbol is not IPropertySymbol property) continue;
                var writer = assignment.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText;
                if (InventoryRecords.Contains(property.ContainingType.Name) && writer != "InventoryStore" ||
                    property.ContainingType.Name == "InventoryStore" && writer != "InventoryStore")
                    violations.Add(tree.FilePath + ": " + assignment);
            }
            foreach (var creation in tree.GetRoot().DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
                if (model.GetTypeInfo(creation).Type is { } type && InventoryRecords.Contains(type.Name) &&
                    creation.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText != "InventoryStore")
                    violations.Add(tree.FilePath + ": " + creation);
        }
        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }
}

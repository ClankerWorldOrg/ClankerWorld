using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ClankerWorld.Tests;

public sealed class OrderWriterBoundaryTests
{
    private static readonly HashSet<string> OrderRecords = new(StringComparer.Ordinal)
    {
        "OrderTask", "OwnerQueuedInstruction", "OwnerInstructionReceipt", "RecordedInstructionReceipt", "OwnerOrderCancellation", "OwnerOrderControlReceipt",
        "OwnerBoatTravelBinding", "OwnerKnowledgeReadCompletion", "OwnerShelterBinding", "OwnerShelterCompletion", "OwnerBuildingExpansionBinding",
    };

    private static bool IsOrderRecord(ITypeSymbol type)
    {
        for (var current = type as INamedTypeSymbol; current is not null; current = current.BaseType)
            if (OrderRecords.Contains(current.Name) || current.Name == "OrderTaskDefinition") return true;
        return false;
    }

    [Fact]
    public void OrderRecordsHaveOnlyTheirNamedStoreWriter()
    {
        var compilation = RuntimeWriterScan.Compilation;
        var violations = new List<string>();
        foreach (var tree in compilation.SyntaxTrees.Where(tree => tree.FilePath.Length > 0))
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var assignment in tree.GetRoot().DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                if (model.GetSymbolInfo(assignment.Left).Symbol is not IPropertySymbol property) continue;
                var writer = assignment.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText;
                if ((IsOrderRecord(property.ContainingType) && writer != "OrderStore" &&
!(property.ContainingType.Name == "OrderTask" && writer == "OrderTask")) ||
                    property.ContainingType.Name == "OrderStore" && writer != "OrderStore")
                    violations.Add(tree.FilePath + ": " + assignment);
            }
            foreach (var creation in tree.GetRoot().DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
                if (model.GetTypeInfo(creation).Type is { } type && IsOrderRecord(type) &&
                    creation.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText != "OrderStore")
                    violations.Add(tree.FilePath + ": " + creation);
        }
        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }
}

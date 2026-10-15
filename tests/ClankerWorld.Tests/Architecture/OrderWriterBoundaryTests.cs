using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ClankerWorld.Tests;

public sealed class OrderWriterBoundaryTests
{
    private static readonly HashSet<string> OrderRecords = new(StringComparer.Ordinal)
    {
        "OrderTask", "OwnerQueuedInstruction", "OwnerInstructionReceipt", "OwnerOrderCancellation", "OwnerOrderControlReceipt",
        "OwnerBoatTravelBinding", "OwnerKnowledgeReadCompletion", "OwnerShelterBinding", "OwnerShelterCompletion", "OwnerBuildingExpansionBinding",
    };

    [Fact]
    public void OrderRecordsHaveOnlyTheirNamedStoreWriter()
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
                if (OrderRecords.Contains(property.ContainingType.Name) && writer != "OrderStore" ||
                    property.ContainingType.Name == "OrderStore" && writer != "OrderStore")
                    violations.Add(tree.FilePath + ": " + assignment);
            }
            foreach (var creation in tree.GetRoot().DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
                if (model.GetTypeInfo(creation).Type is { } type && OrderRecords.Contains(type.Name) &&
                    creation.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText != "OrderStore")
                    violations.Add(tree.FilePath + ": " + creation);
        }
        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }
}

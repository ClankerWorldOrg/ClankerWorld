using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ClankerWorld.Tests;

public sealed class MarketWriterBoundaryTests
{
    private static readonly HashSet<string> MarketRecords = new(StringComparer.Ordinal)
    {
        "ShopTradeState", "TownMarketState", "MarketStallState", "MarketStallOccupancy", "MarketTradeState", "MarketStockReceipt",
    };

    [Fact]
    public void MarketRecordsHaveOneWriterAndTownProjectionUsesItsNamedStore()
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
                if (MarketRecords.Contains(property.ContainingType.Name) && writer != "MarketSystem" ||
                    property is { ContainingType.Name: "TownRuntimeState", Name: "Markets" } && writer != "TownSystem")
                    violations.Add(tree.FilePath + ": " + assignment);
            }
            foreach (var creation in tree.GetRoot().DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
                if (model.GetTypeInfo(creation).Type is { } type && MarketRecords.Contains(type.Name) &&
                    creation.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText != "MarketSystem")
                    violations.Add(tree.FilePath + ": " + creation);
        }
        Assert.Empty(violations);
    }
}

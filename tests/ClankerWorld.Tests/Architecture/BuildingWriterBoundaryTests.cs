using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ClankerWorld.Tests;

public sealed class BuildingWriterBoundaryTests
{
    [Fact]
    public void BuildingRecordsHaveOneWriter()
    {
        var compilation = RuntimeWriterScan.CreateCompilation(RuntimeWriterScan.RepositoryRoot);
        var violations = new List<string>();
        foreach (var tree in compilation.SyntaxTrees.Where(tree => tree.FilePath.Length > 0))
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var assignment in tree.GetRoot().DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                if (model.GetSymbolInfo(assignment.Left).Symbol is not IPropertySymbol property) continue;
                var owned = property.ContainingType.Name == "WorldConstructionState" ||
                    property.ContainingType.Name == "BuildingStore" && property.Name == "State" ||
                    property.ContainingType.Name == "WorldContentSimulationState" &&
                    property.Name is "Buildings" or "BuildingExpansions" or "GuestInvitations" or "ConstructionReceipts";
                if (owned && assignment.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText != "BuildingStore")
                    violations.Add(tree.FilePath + ": " + assignment);
            }
        }
        Assert.Empty(violations);
    }
}

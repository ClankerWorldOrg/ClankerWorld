using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ClankerWorld.Tests;

public sealed class ProductionWriterBoundaryTests
{
    [Fact]
    public void OnlyTheProductionSystemWritesItsState()
    {
        var root = RuntimeWriterScan.RepositoryRoot;
        var compilation = RuntimeWriterScan.CreateCompilation(root);
        var violations = new List<string>();
        foreach (var tree in compilation.SyntaxTrees.Where(tree => tree.FilePath.Length > 0))
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var write in tree.GetRoot().DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                if (model.GetSymbolInfo(write.Left).Symbol is not IPropertySymbol property) continue;
                if (property.ContainingType.Name == "WorldContentSimulationState" &&
                    property.Name is "ProductionJobs" or "NextProductionJobSequence" or "CropBuilds" or "ArchivedProductionJobCount" &&
                    write.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText != "ProductionSystem" &&
                    !(property.Name == "CropBuilds" && Path.GetFileName(tree.FilePath) == "WorldRuntime.cs"))
                    violations.Add(tree.FilePath + ": production snapshot write " + write);
                if (
                    property.ContainingType.ToDisplayString() != "ClankerWorld.Simulation.World.ProductionState" &&
                    !(property.Name == "State" && property.ContainingType.Name == "ProductionSystem")) continue;
                if (write.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText != "ProductionSystem")
                    violations.Add(tree.FilePath + ": " + write);
            }
            foreach (var field in tree.GetRoot().DescendantNodes().OfType<FieldDeclarationSyntax>())
            {
                if (field.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText != "WorldRuntime") continue;
                var type = model.GetTypeInfo(field.Declaration.Type).Type?.ToDisplayString() ?? "";
                if (type.Contains("WorldProductionJob", StringComparison.Ordinal) ||
                    type.Contains("ToolMakingRequestState", StringComparison.Ordinal) ||
                    type.Contains("RecipeProjectActionPlan", StringComparison.Ordinal) ||
                    type == "ClankerWorld.Simulation.World.WorldContentSimulationState")
                    violations.Add(tree.FilePath + ": " + field);
            }
        }
        Assert.Empty(violations);
    }
}

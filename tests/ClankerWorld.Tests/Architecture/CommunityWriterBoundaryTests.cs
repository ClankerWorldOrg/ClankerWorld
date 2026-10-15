using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ClankerWorld.Tests;

public sealed class CommunityWriterBoundaryTests
{
    [Fact]
    public void CommunityKnowledgeHasOneWriter()
    {
        var compilation = RuntimeWriterScan.CreateCompilation(RuntimeWriterScan.RepositoryRoot);
        var violations = new List<string>();
        foreach (var tree in compilation.SyntaxTrees.Where(tree => tree.FilePath.Length > 0))
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var assignment in tree.GetRoot().DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                if (model.GetSymbolInfo(assignment.Left).Symbol is not IPropertySymbol property) continue;
                var owned = property.ContainingType.Name == "WorldKnowledgeState" ||
                    property.ContainingType.Name == "CommunitySystem" && property.Name == "State";
                if (owned && assignment.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText != "CommunitySystem")
                    violations.Add(tree.FilePath + ": " + assignment);
            }
        }
        Assert.Empty(violations);
    }
}

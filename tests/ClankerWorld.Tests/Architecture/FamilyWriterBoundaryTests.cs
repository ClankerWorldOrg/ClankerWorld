using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ClankerWorld.Tests;

public sealed class FamilyWriterBoundaryTests
{
    [Fact]
    public void SavedFamilyStateHasOneWriter()
    {
        var compilation = RuntimeWriterScan.Compilation;
        var violations = new List<string>();
        foreach (var tree in compilation.SyntaxTrees.Where(tree => tree.FilePath.Length > 0))
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var assignment in tree.GetRoot().DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                if (model.GetSymbolInfo(assignment.Left).Symbol is IPropertySymbol
                    { ContainingType.Name: "FamilySystem", Name: "Continuity" } &&
                    assignment.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText != "FamilySystem")
                    violations.Add(tree.FilePath + ": " + assignment);
            }
        }
        Assert.Empty(violations);
    }
}

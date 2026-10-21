using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ClankerWorld.Tests;

public sealed class InventoryCommandBoundaryTests
{
    [Fact]
    public void InventoryCommandsHaveClosedConstructionAndNoGenericWorldWriterPort()
    {
        var compilation = RuntimeWriterScan.CreateCompilation(RuntimeWriterScan.RepositoryRoot);
        var violations = new List<string>();
        foreach (var tree in compilation.SyntaxTrees.Where(tree => tree.FilePath.Length > 0))
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var declaration in tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if ((model.GetDeclaredSymbol(declaration) as INamedTypeSymbol)?.BaseType is { Name: "Command", ContainingType.Name: "InventorySystem" } &&
                    !declaration.Ancestors().OfType<ClassDeclarationSyntax>().Any(type => type.Identifier.ValueText == "InventorySystem"))
                    violations.Add(tree.FilePath + ": " + declaration.Identifier);
            }
            foreach (var method in tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                var symbol = (IMethodSymbol)model.GetDeclaredSymbol(method)!;
                if (symbol.ContainingNamespace.ToDisplayString() != "ClankerWorld.Simulation.World" ||
                    method.Ancestors().OfType<ClassDeclarationSyntax>().Any(type => type.Identifier.ValueText == "InventorySystem")) continue;
                if (symbol.Parameters.Any(parameter => parameter.Type is INamedTypeSymbol { Name: "Func", TypeArguments.Length: 2 } function &&
                    function.TypeArguments.All(type => type.Name == "InventoryCheckpoint")))
                    violations.Add(tree.FilePath + ": " + symbol.Name);
            }
        }
        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }
}

using ClankerWorld.Simulation.World;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ClankerWorld.Tests;

public sealed class OrderStatusBoundaryTests
{
    [Fact]
    public void OnlyTheTransitionFunctionWritesOrderStatus()
    {
        var compilation = RuntimeWriterScan.CreateCompilation(RuntimeWriterScan.RepositoryRoot);
        var violations = new List<string>();
        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var write in tree.GetRoot().DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                var symbol = model.GetSymbolInfo(write.Left).Symbol;
                if (symbol?.ContainingType?.Name != nameof(OrderTask) || symbol.Name is not ("Status" or "status")) continue;
                var method = write.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
                var statusSetter = write.Ancestors().OfType<AccessorDeclarationSyntax>().Any(accessor =>
                    accessor.Parent?.Parent is PropertyDeclarationSyntax { Identifier.ValueText: "Status" });
                if (method?.Identifier.ValueText != nameof(OrderTask.Transition) && !statusSetter)
                    violations.Add($"{tree.FilePath}:{write.GetLocation().GetLineSpan().StartLinePosition.Line + 1}");
            }
        }
        Assert.Empty(violations);
        Assert.True(typeof(OrderTask).GetProperty(nameof(OrderTask.Status))!.SetMethod!.IsPrivate);
    }
}

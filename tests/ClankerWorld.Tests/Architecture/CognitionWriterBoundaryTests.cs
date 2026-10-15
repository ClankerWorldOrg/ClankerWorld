using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ClankerWorld.Tests;

public sealed class CognitionWriterBoundaryTests
{
    private static readonly HashSet<string> CognitionRecords = new(StringComparer.Ordinal)
    {
        "ModelDecisionOutcome", "PendingModelDecision", "ConversationTurnOutcome", "PendingConversationTurn",
        "IdentityMomentOutcome", "PendingIdentityMoment", "PreparedIdentityMoment", "AgentIdentityMoment", "ModelAttempt", "RecentThought",
    };

    [Fact]
    public void CognitionRecordsAndLiveRequestsHaveOneWriter()
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
                if (CognitionRecords.Contains(property.ContainingType.Name) && writer != "CognitionSystem" ||
                    property.ContainingType.Name == "CognitionSystem" && writer != "CognitionSystem")
                    violations.Add(tree.FilePath + ": " + assignment);
            }
            foreach (var creation in tree.GetRoot().DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
                if (model.GetTypeInfo(creation).Type is { } type && CognitionRecords.Contains(type.Name) &&
                    creation.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText != "CognitionSystem")
                    violations.Add(tree.FilePath + ": " + creation);
        }
        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }
}

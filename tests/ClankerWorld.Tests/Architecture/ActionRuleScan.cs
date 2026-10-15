using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ClankerWorld.Tests;

internal static class ActionRuleScan
{
    internal static string[] Expressions(string source)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        return root.DescendantNodes().Where(IsRule).Select(node => node.NormalizeWhitespace().ToFullString())
            .Order(StringComparer.Ordinal).ToArray();
    }

    private static bool IsRule(SyntaxNode node) => node switch
    {
        InvocationExpressionSyntax call => call.Expression is IdentifierNameSyntax { Identifier.ValueText: "FindUnoccupiedRoute" } or
            MemberAccessExpressionSyntax { Name.Identifier.ValueText: "FindUnoccupiedRoute" } or
            MemberBindingExpressionSyntax { Name.Identifier.ValueText: "FindUnoccupiedRoute" },
        IsPatternExpressionSyntax pattern => IsAge(pattern.Expression),
        BinaryExpressionSyntax comparison when comparison.IsKind(SyntaxKind.EqualsExpression) || comparison.IsKind(SyntaxKind.NotEqualsExpression) =>
            IsAge(comparison.Left) || IsAge(comparison.Right),
        BinaryExpressionSyntax comparison when comparison.IsKind(SyntaxKind.LessThanExpression) || comparison.IsKind(SyntaxKind.LessThanOrEqualExpression) ||
            comparison.IsKind(SyntaxKind.GreaterThanExpression) || comparison.IsKind(SyntaxKind.GreaterThanOrEqualExpression) =>
            IsNeed(comparison.Left) && IsNumber(comparison.Right) || IsNumber(comparison.Left) && IsNeed(comparison.Right),
        SubpatternSyntax pattern => pattern.NameColon is { Name.Identifier.ValueText: "FullnessBasisPoints" or "WarmthBasisPoints" } &&
            pattern.Pattern.DescendantNodesAndSelf().OfType<LiteralExpressionSyntax>().Any(IsNumber),
        _ => false,
    };

    private static bool IsAge(ExpressionSyntax expression) => expression is
        MemberAccessExpressionSyntax { Name.Identifier.ValueText: "AgeBand" } or
        IdentifierNameSyntax { Identifier.ValueText: "age" or "ageBand" };

    private static bool IsNeed(ExpressionSyntax expression) => expression is
        MemberAccessExpressionSyntax { Name.Identifier.ValueText: "FullnessBasisPoints" or "WarmthBasisPoints" } or
        IdentifierNameSyntax { Identifier.ValueText: "fullness" or "warmth" };

    private static bool IsNumber(ExpressionSyntax expression) => expression is LiteralExpressionSyntax literal &&
        literal.IsKind(SyntaxKind.NumericLiteralExpression);

    internal static SortedDictionary<string, string[]> Scan(string root)
    {
        var result = new SortedDictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "src/ClankerWorld.Simulation/World"), "*.cs", SearchOption.AllDirectories))
        {
            var path = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
            if (path.EndsWith("/Actions/LifeStageRules.cs", StringComparison.Ordinal) ||
                path.EndsWith("/Actions/WorldRuntime.Reach.cs", StringComparison.Ordinal)) continue;
            var expressions = Expressions(File.ReadAllText(file));
            if (expressions.Length > 0) result[path] = expressions;
        }
        return result;
    }
}

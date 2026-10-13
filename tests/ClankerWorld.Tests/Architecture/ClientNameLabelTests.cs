using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ClankerWorld.Tests;

public sealed class ClientNameLabelTests
{
    [Fact]
    public void NameCaptionsUseTheSharedFittedLabel()
    {
        var directory = Path.Combine(RuntimeWriterScan.RepositoryRoot, "src", "ClankerWorld.GodotClient");
        var violations = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "ClientChecks" + Path.DirectorySeparatorChar,
                StringComparison.Ordinal) && !path.Contains(Path.DirectorySeparatorChar + ".godot" + Path.DirectorySeparatorChar,
                StringComparison.Ordinal))
            .SelectMany(path => PlainNames(File.ReadAllText(path)).Select(line => $"{Path.GetRelativePath(directory, path)}:{line}"));
        Assert.Empty(violations);
    }

    [Theory]
    [InlineData("class X { Label actorNameLabel = new(); }", true)]
    [InlineData("class X { Label NameLabel { get; } = new(); }", true)]
    [InlineData("class X { void F() { var title = new Label { Text = town.Name }; } }", true)]
    [InlineData("class X { void F() { var row = new Label { Text = $\"Steward: {person.DisplayName}\" }; } }", true)]
    [InlineData("class X { void F() { var row = new FittedLabel { Text = person.DisplayName }; } }", false)]
    [InlineData("class X { void F() { var title = new Label { Text = \"Family Tree\" }; } }", false)]
    public void GuardDistinguishesNamesFromStaticCopy(string source, bool refused) =>
        Assert.Equal(refused, PlainNames(source).Count > 0);

    private static List<int> PlainNames(string source)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        List<int> lines = [];
        foreach (var node in root.DescendantNodes())
        {
            var plainName = node switch
            {
                FieldDeclarationSyntax field => field.Declaration.Type.ToString() == "Label" &&
                    field.Declaration.Variables.Any(variable => Name(variable.Identifier.ValueText)),
                PropertyDeclarationSyntax property => property.Type.ToString() == "Label" && Name(property.Identifier.ValueText),
                ObjectCreationExpressionSyntax creation => creation.Type.ToString() == "Label" &&
                    creation.Initializer?.Expressions.OfType<AssignmentExpressionSyntax>().Any(assignment =>
                        assignment.Left.ToString() == "Text" && assignment.Right.DescendantNodesAndSelf()
                            .OfType<IdentifierNameSyntax>().Any(identifier => Name(identifier.Identifier.ValueText))) == true,
                _ => false,
            };
            if (plainName) lines.Add(node.GetLocation().GetLineSpan().StartLinePosition.Line + 1);
        }
        return lines;
    }

    private static bool Name(string value) => value.Equals("name", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("title", StringComparison.OrdinalIgnoreCase) || value.EndsWith("Name", StringComparison.Ordinal) ||
        value.EndsWith("NameLabel", StringComparison.Ordinal) || value.EndsWith("Title", StringComparison.Ordinal);
}

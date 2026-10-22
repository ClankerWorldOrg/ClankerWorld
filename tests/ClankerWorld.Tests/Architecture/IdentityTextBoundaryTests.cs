using System.Diagnostics;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ClankerWorld.Tests;

public sealed class IdentityTextBoundaryTests
{
    private const string BudgetPath = "tests/ClankerWorld.Tests/Architecture/identity-text-uses.json";
    private static readonly string[] SharedFiles = ["Identity.cs", "IdentityRules.cs", "DerivedId.cs", "BoundedText.cs"];

    [Fact]
    public void HandBuiltIdentitiesAndLiteralLengthChecksCanOnlyDisappear()
    {
        var root = RuntimeWriterScan.RepositoryRoot;
        var compilation = RuntimeWriterScan.Compilation;
        var actual = Scan(compilation.SyntaxTrees, compilation, root);
        var scannedPaths = compilation.SyntaxTrees.Where(tree => tree.FilePath.Length > 0)
            .Select(tree => tree.FilePath.Replace('\\', '/')).ToHashSet(StringComparer.Ordinal);
        var otherTrees = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => IsAdditionalSource(path, scannedPaths))
            .Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path));
        foreach (var (key, count) in ScanAdditionalSource(otherTrees, compilation, root)) actual[key] = count;
        var recorded = JsonSerializer.Deserialize<SortedDictionary<string, int>>(File.ReadAllText(Path.Combine(root, BudgetPath)))!;
        AssertWithinBudget(recorded, actual);
        foreach (var (key, count) in recorded)
            Assert.True(count == actual.GetValueOrDefault(key), $"Remove or lower obsolete identity/text allowance: {key}");
        AssertAllowanceDidNotGrow(root, recorded);
    }

    [Theory]
    [InlineData("/source/src/ClankerWorld.Simulation/World/State.cs", false)]
    [InlineData("/source/src\\ClankerWorld.Simulation\\World\\State.cs", false)]
    [InlineData("/source/src/ClankerWorld.Shared/AgentPlacementRules.cs", false)]
    [InlineData("/source/src\\ClankerWorld.Shared\\AgentPlacementRules.cs", false)]
    [InlineData("/source/src/ClankerWorld.Server/Actions.cs", true)]
    [InlineData("/source/src\\ClankerWorld.GodotClient\\Actions.cs", true)]
    [InlineData("/source/src\\ClankerWorld.Server\\obj\\Generated.cs", false)]
    public void AdditionalSourceNeverRescansKnownTreesOrBuildOutput(string path, bool expected)
    {
        var scanned = new HashSet<string>(StringComparer.Ordinal)
        {
            "/source/src/ClankerWorld.Simulation/World/State.cs",
            "/source/src/ClankerWorld.Shared/AgentPlacementRules.cs",
        };
        Assert.Equal(expected, IsAdditionalSource(path, scanned));
    }

    [Theory]
    [InlineData("ClankerWorld.Server")]
    [InlineData("ClankerWorld.GodotClient")]
    public void AdditionalSourceChecksExplicitAndTargetTypedConstructorsAndMethodArguments(string project)
    {
        var shared = CSharpSyntaxTree.ParseText("record Request(string IssuerId, string Label);",
            path: "/source/src/ClankerWorld.Simulation/Request.cs");
        var tree = CSharpSyntaxTree.ParseText("""
            class Actions
            {
                Request Explicit(string actor) => new Request($"owner-device:{actor}", $"Hello {actor}");
                Request Inferred(string actor) => new($"owner-device:{actor}", $"Hello {actor}");
                Request Safe(string actor) => new(DerivedId.Create($"owner-device:{actor}"), $"Hello {actor}");
                void Send(string issuerId, string label) { }
                void Invoke(string actor) => Send($"owner-device:{actor}", $"Hello {actor}");
            }
            static class DerivedId { public static string Create(string value) => value; }
            """, path: "/source/src/" + project + "/Actions.cs");
        var compilation = CSharpCompilation.Create("AdditionalSourceControl", [shared],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        var actual = ScanAdditionalSource([tree], compilation, "/source");
        Assert.Equal(3, actual.Values.Sum());
        Assert.ThrowsAny<Exception>(() => AssertWithinBudget(new(StringComparer.Ordinal), actual));
    }

    private static bool IsAdditionalSource(string path, HashSet<string> scannedPaths)
    {
        var normalized = path.Replace('\\', '/');
        return !scannedPaths.Contains(normalized) &&
            !normalized.Split('/').Any(part => part is "bin" or "obj" or ".godot");
    }

    [Fact]
    public void AdditionalSourceChecksIdentityArgumentsWhenFrameworkInheritanceIsUnavailable()
    {
        // Godot's Marker -> Button -> Control inheritance is absent from the source-only scan.
        var tree = CSharpSyntaxTree.ParseText("""
            class Marker : Button { }
            class Actions
            {
                void PresentMovingMarker(string id, Control marker, Vector2 offset) { }
                void Invoke(string actor, Marker marker) => PresentMovingMarker("agent:" + actor, marker, new(4, 4));
                void Label(string text, Control marker) { }
                void Show(string actor, Marker marker) => Label($"Hello {actor}", marker);
            }
            """, path: "/source/src/ClankerWorld.GodotClient/Actions.cs");
        var compilation = CSharpCompilation.Create("MissingFrameworkControl", references:
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        var actual = ScanAdditionalSource([tree], compilation, "/source");
        Assert.Single(actual);
        Assert.Equal(1, actual.Values.Sum());
        Assert.ThrowsAny<Exception>(() => AssertWithinBudget(new(StringComparer.Ordinal), actual));
    }

    private static SortedDictionary<string, int> ScanAdditionalSource(IEnumerable<SyntaxTree> trees,
        CSharpCompilation compilation, string root)
    {
        var additional = trees.ToArray();
        return Scan(additional, compilation.AddSyntaxTrees(additional), root);
    }

    [Fact]
    public void NewIdsCannotUseAnExistingFilesAllowanceOrHideInAConstructor()
    {
        var source = """
            record Receipt(string Id);
            class Generator
            {
                string ReceiptId(string actor) => $"receipt:{actor}";
                Receipt Make(string actor) => new($"other:{actor}");
                void Set(string actor) { var id = "hand:" + actor; }
                string Safe(string actor) => DerivedId.Create($"receipt:{actor}");
                string Parsed(string actor) => Identity.Parse($"parsed:{actor}");
                string Label(string actor) => $"Hello {actor}";
            }
            static class DerivedId { public static string Create(string value) => value; }
            static class Identity { public static string Parse(string value) => value; }
            """;
        var tree = CSharpSyntaxTree.ParseText(source, path: "/source/src/Generator.cs");
        var compilation = CSharpCompilation.Create("IdentityControl", [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        var actual = Scan([tree], compilation, "/source");
        Assert.Equal(4, actual.Values.Sum());
        Assert.ThrowsAny<Exception>(() => AssertWithinBudget(new(StringComparer.Ordinal), actual));
        var recorded = new SortedDictionary<string, int>(actual, StringComparer.Ordinal);
        recorded.Remove(actual.Keys.First());
        Assert.ThrowsAny<Exception>(() => AssertWithinBudget(recorded, actual));
    }

    [Fact]
    public void LiteralLengthChecksAreSyntaxCheckedIncludingReversedOperandsAndPatterns()
    {
        var tree = CSharpSyntaxTree.ParseText("""
            class Limits
            {
                bool Check(string value) => value.Length > 128 || 64 < value.Length || value.Length is > 512;
                bool Pattern(string value) => value is { Length: > 256 };
                bool Named(string value) => value.Length > Maximum;
                const int Maximum = 128;
            }
            """, path: "/source/src/Limits.cs");
        Assert.Equal(4, Scan([tree], null, "/source").Values.Sum());
    }

    internal static SortedDictionary<string, int> Scan(IEnumerable<SyntaxTree> trees, CSharpCompilation? compilation, string root)
    {
        var result = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var tree in trees.Where(tree => tree.FilePath.Length > 0))
        {
            var path = Path.GetRelativePath(root, tree.FilePath).Replace(Path.DirectorySeparatorChar, '/');
            if (path.StartsWith("src/ClankerWorld.Simulation/", StringComparison.Ordinal) &&
                SharedFiles.Contains(Path.GetFileName(path), StringComparer.Ordinal)) continue;
            var model = compilation?.GetSemanticModel(tree);
            foreach (var node in tree.GetRoot().DescendantNodes())
            {
                var kind = node switch
                {
                    BinaryExpressionSyntax binary when IsLiteralLengthCheck(binary) => "length",
                    RelationalPatternSyntax pattern when pattern.Expression is LiteralExpressionSyntax &&
                        (pattern.Ancestors().OfType<IsPatternExpressionSyntax>().FirstOrDefault()?.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Length" } ||
                         pattern.Ancestors().OfType<SubpatternSyntax>().Any(part => part.NameColon?.Name.Identifier.ValueText == "Length")) => "length",
                    InterpolatedStringExpressionSyntax when IsIdentityContext(node, model) => "id",
                    BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AddExpression) &&
                        (model?.GetTypeInfo(binary).Type?.SpecialType == SpecialType.System_String ||
                         binary.DescendantNodes().Any(part => part is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))) &&
                        !binary.Parent!.IsKind(SyntaxKind.AddExpression) && IsIdentityContext(node, model) => "id",
                    _ => null,
                };
                if (kind is null) continue;
                var key = path + "|" + kind + "|" + node.NormalizeWhitespace().ToFullString();
                result[key] = result.GetValueOrDefault(key) + 1;
            }
        }
        return result;
    }

    private static bool IsLiteralLengthCheck(BinaryExpressionSyntax binary) =>
        binary.Kind() is SyntaxKind.GreaterThanExpression or SyntaxKind.GreaterThanOrEqualExpression or
            SyntaxKind.LessThanExpression or SyntaxKind.LessThanOrEqualExpression &&
        (binary.Left is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Length" } && binary.Right is LiteralExpressionSyntax ||
         binary.Right is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Length" } && binary.Left is LiteralExpressionSyntax);

    private static bool IdentityName(string name) => name.Equals("id", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith("Id", StringComparison.Ordinal) || name.EndsWith("Identity", StringComparison.Ordinal) ||
        name.EndsWith("Receipt", StringComparison.Ordinal) || name.EndsWith("Key", StringComparison.Ordinal);

    private static bool IsIdentityContext(SyntaxNode node, SemanticModel? model)
    {
        foreach (var parent in node.Ancestors())
        {
            if (parent is ArgumentSyntax argument)
            {
                if (argument.Parent is not ArgumentListSyntax arguments) return false;
                if (argument.Parent?.Parent is InvocationExpressionSyntax call)
                {
                    var owner = (model?.GetSymbolInfo(call).Symbol as IMethodSymbol)?.ContainingType.Name;
                    var syntaxOwner = (call.Expression as MemberAccessExpressionSyntax)?.Expression.ToString();
                    if (owner == "DerivedId" || syntaxOwner == "DerivedId") return false;
                    if (owner == "Identity" || syntaxOwner == "Identity")
                        return (call.Expression as MemberAccessExpressionSyntax)?.Name.Identifier.ValueText != "Create";
                }
                if (argument.NameColon is { } named) return IdentityName(named.Name.Identifier.ValueText);
                if (model is not null)
                {
                    var info = model.GetSymbolInfo(arguments.Parent!);
                    var index = arguments.Arguments.IndexOf(argument);
                    // Missing framework assemblies can leave a declared method as a candidate.
                    // Check every candidate rather than silently allowing a new identity there.
                    var methods = info.Symbol is IMethodSymbol resolved
                        ? [resolved] : info.CandidateSymbols.OfType<IMethodSymbol>();
                    return methods.Any(method => index < method.Parameters.Length && IdentityName(method.Parameters[index].Name));
                }
                return false;
            }
            if (parent is VariableDeclaratorSyntax variable) return IdentityName(variable.Identifier.ValueText);
            if (parent is AssignmentExpressionSyntax assignment)
                return IdentityName((assignment.Left as MemberAccessExpressionSyntax)?.Name.Identifier.ValueText ?? assignment.Left.ToString());
            if (parent is MethodDeclarationSyntax declaration) return IdentityName(declaration.Identifier.ValueText);
            if (parent is PropertyDeclarationSyntax property) return IdentityName(property.Identifier.ValueText);
        }
        return false;
    }

    private static void AssertWithinBudget(SortedDictionary<string, int> recorded, SortedDictionary<string, int> actual)
    {
        foreach (var (key, count) in actual)
            Assert.True(count <= recorded.GetValueOrDefault(key), $"New hand-built identity or literal length check: {key}. Use Identity/DerivedId, IdentityRules or BoundedText.");
    }

    private static void AssertAllowanceDidNotGrow(string root, SortedDictionary<string, int> recorded)
    {
        // CI checks out a merge with its base as first parent; local commits compare with their parent.
        // The first introduction has no prior file. Later changes may only remove allowances.
        using var process = Process.Start(new ProcessStartInfo("git")
        {
            WorkingDirectory = root,
            ArgumentList = { "show", "HEAD^:" + BudgetPath },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        })!;
        var previous = process.StandardOutput.ReadToEnd();
        _ = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0) return;
        AssertWithinBudget(JsonSerializer.Deserialize<SortedDictionary<string, int>>(previous)!, recorded);
    }
}

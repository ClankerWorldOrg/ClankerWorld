using System.Diagnostics;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ClankerWorld.Tests;

public sealed class AsyncWaitBoundaryTests
{
    private static readonly string[] ExpectedRules = ["async-fake", "delay", "reflection", "sleep", "stopwatch", "timeout-cancellation", "timeout-wait"];
    private const string AllowListPath = "tests/ClankerWorld.Tests/Architecture/async-waits.json";
    public sealed record Allowance(string Path, string Rule, string Expression, int Count);
    private static string Root => RuntimeWriterScan.RepositoryRoot;

    [Fact]
    public void AsyncWaitAllowancesMatchExistingSourceAndCannotGrow()
    {
        var actual = Scan(Root);
        var allowed = JsonSerializer.Deserialize<Allowance[]>(File.ReadAllText(System.IO.Path.Combine(Root, AllowListPath)))!;
        Assert.Equal(allowed, actual);
        var parent = Git("rev-parse", "HEAD^");
        Assert.Equal(0, parent.ExitCode);
        var previous = Git("show", "HEAD^:" + AllowListPath);
        if (previous.ExitCode != 0) return; // The first guard commit establishes the legacy baseline.
        var old = JsonSerializer.Deserialize<Allowance[]>(previous.Output)!;
        foreach (var entry in allowed)
        {
            var previousEntry = Assert.Single(old, prior => prior.Path == entry.Path && prior.Rule == entry.Rule && prior.Expression == entry.Expression);
            Assert.True(entry.Count <= previousEntry.Count, $"Async wait allowance grew: {entry.Path} {entry.Rule}");
        }
    }

    [Fact]
    public void ScanRecognizesEveryBannedWaitAndIgnoresInfiniteDelaysAndTokenWaits()
    {
        const string source = """
            class Example {
                void Bad() {
                    TimeSpan deadline = TimeSpan.FromSeconds(1);
                    Task.Delay(1);
                    task.WaitAsync(TimeSpan.FromSeconds(1));
                    task.WaitAsync(deadline);
                    new CancellationTokenSource(100);
                    cancellation.CancelAfter(TimeSpan.FromSeconds(1));
                    var watch = Stopwatch.StartNew();
                    while (watch.Elapsed < TimeSpan.FromSeconds(1)) { }
                    var forWatch = Stopwatch.StartNew();
                    for (; forWatch.Elapsed < TimeSpan.FromSeconds(1);) { }
                    var doWatch = Stopwatch.StartNew();
                    do { } while (doWatch.Elapsed < TimeSpan.FromSeconds(1));
                    Thread.Sleep(1);
                    typeof(WorldRuntime).GetField("hidden");
                    new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                }
                void Good() {
                    Task.Delay(Timeout.Infinite, cancellationToken);
                    Task.Delay(Timeout.InfiniteTimeSpan);
                    task.WaitAsync(cancellation.Token);
                    task.WaitAsync(CancellationToken.None);
                    new CancellationTokenSource();
                }
            }
            """;
        var entries = ScanSource("example.cs", source);
        Assert.Equal(11, entries.Sum(item => item.Count));
        Assert.Equal(ExpectedRules,
            entries.Select(item => item.Rule).Distinct().Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("timeout-cancellation", "class Example { CancellationTokenSource deadline = new(100); }")]
    [InlineData("timeout-cancellation", "class Example { CancellationTokenSource Deadline { get; } = new(100); }")]
    [InlineData("timeout-cancellation", "class Example { CancellationTokenSource Deadline() => new(100); }")]
    [InlineData("timeout-cancellation", "class Example { CancellationTokenSource Deadline() { return new(100); } }")]
    [InlineData("timeout-wait", "class Limits { public static TimeSpan Deadline => TimeSpan.FromSeconds(1); void Wait() { task.WaitAsync(Limits.Deadline); } }")]
    [InlineData("timeout-wait", "class Example { TimeSpan deadline = TimeSpan.FromSeconds(1); void Wait() { task.WaitAsync(this.deadline); } }")]
    public void AlternativeTimeoutSyntaxCannotBypassTheAllowanceLedger(string rule, string source)
    {
        var entry = Assert.Single(ScanSource("example.cs", source));
        Assert.Equal(rule, entry.Rule);
        Assert.Equal(1, entry.Count);
    }

    public static Allowance[] Scan(string root) => Directory.EnumerateFiles(System.IO.Path.Combine(root, "tests"), "*.cs", SearchOption.AllDirectories)
        .Select(path => (Path: System.IO.Path.GetRelativePath(root, path).Replace('\\', '/'), Source: path))
        .Where(item => !item.Path.Contains("/obj/", StringComparison.Ordinal) && !item.Path.Contains("/bin/", StringComparison.Ordinal) &&
            !item.Path.Contains("/TestSupport/ModelWork/", StringComparison.Ordinal))
        .SelectMany(item => ScanSource(item.Path, File.ReadAllText(item.Source)))
        .OrderBy(item => item.Path, StringComparer.Ordinal).ThenBy(item => item.Rule, StringComparer.Ordinal)
        .ThenBy(item => item.Expression, StringComparer.Ordinal).ToArray();

    private static Allowance[] ScanSource(string path, string source)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        var found = new List<(string Rule, string Expression)>();
        var durations = root.DescendantNodes().OfType<VariableDeclarationSyntax>()
            .Where(declaration => declaration.Type.ToString().TrimEnd('?').EndsWith("TimeSpan", StringComparison.Ordinal) ||
                declaration.Type.ToString() == "var" && declaration.Variables.Any(variable => variable.Initializer?.Value.ToString().StartsWith("TimeSpan.", StringComparison.Ordinal) == true))
            .SelectMany(declaration => declaration.Variables.Select(variable => variable.Identifier.ValueText))
            .Concat(root.DescendantNodes().OfType<ParameterSyntax>().Where(parameter => parameter.Type?.ToString().TrimEnd('?').EndsWith("TimeSpan", StringComparison.Ordinal) == true)
                .Select(parameter => parameter.Identifier.ValueText))
            .Concat(root.DescendantNodes().OfType<PropertyDeclarationSyntax>().Where(property => property.Type.ToString().TrimEnd('?').EndsWith("TimeSpan", StringComparison.Ordinal))
                .Select(property => property.Identifier.ValueText)).ToHashSet(StringComparer.Ordinal);
        void Add(string rule, SyntaxNode node) => found.Add((rule, string.Concat(node.DescendantTokens().Select(token => token.Text))));
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax member) continue;
            var name = member.Name.Identifier.ValueText;
            var receiver = member.Expression.ToString();
            var arguments = invocation.ArgumentList.Arguments;
            if (name == "Delay" && receiver.EndsWith("Task", StringComparison.Ordinal) &&
                arguments.Count > 0 && !arguments[0].Expression.ToString().Contains("Timeout.Infinite", StringComparison.Ordinal) &&
                arguments[0].Expression.ToString() != "-1") Add("delay", invocation);
            if (name == "WaitAsync" && arguments.Count > 0)
            {
                var expression = arguments[0].Expression;
                var first = expression.ToString();
                var durationName = expression is MemberAccessExpressionSyntax duration ? duration.Name.Identifier.ValueText : first;
                if (first.Contains("TimeSpan", StringComparison.Ordinal) || durations.Contains(durationName)) Add("timeout-wait", invocation);
            }
            if (name == "CancelAfter") Add("timeout-cancellation", invocation);
            if (name == "Sleep" && receiver.EndsWith("Thread", StringComparison.Ordinal)) Add("sleep", invocation);
            if (receiver.EndsWith("Stopwatch", StringComparison.Ordinal) && name is "StartNew" or "GetTimestamp")
            {
                var variable = invocation.Ancestors().OfType<VariableDeclaratorSyntax>().FirstOrDefault()?.Identifier.ValueText;
                var polled = variable is not null && root.DescendantNodes().Select(node => node switch
                    {
                        WhileStatementSyntax loop => loop.Condition,
                        DoStatementSyntax loop => loop.Condition,
                        ForStatementSyntax loop => loop.Condition,
                        _ => null,
                    }).OfType<ExpressionSyntax>()
                    .Any(condition => condition.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()
                        .Any(identifier => identifier.Identifier.ValueText == variable));
                if (polled) Add("stopwatch", invocation);
            }
            if (name.StartsWith("Get", StringComparison.Ordinal) && member.Expression is TypeOfExpressionSyntax type &&
                type.Type.ToString().EndsWith("WorldRuntime", StringComparison.Ordinal)) Add("reflection", invocation);
        }
        foreach (var creation in root.DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
        {
            var type = creation is ObjectCreationExpressionSyntax explicitCreation ? explicitCreation.Type : creation.Ancestors().Select(node => node switch
            {
                VariableDeclarationSyntax declaration => declaration.Type,
                PropertyDeclarationSyntax property => property.Type,
                MethodDeclarationSyntax method => method.ReturnType,
                _ => null,
            }).OfType<TypeSyntax>().FirstOrDefault();
            if (type?.ToString().EndsWith("CancellationTokenSource", StringComparison.Ordinal) == true && creation.ArgumentList is { Arguments.Count: > 0 } arguments &&
                !arguments.Arguments[0].Expression.ToString().Contains("Timeout.Infinite", StringComparison.Ordinal)) Add("timeout-cancellation", creation);
        }
        foreach (var member in root.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
            if (member.Name.Identifier.ValueText == "RunContinuationsAsynchronously") Add("async-fake", member);
        return found.GroupBy(item => item).Select(group => new Allowance(path, group.Key.Rule, group.Key.Expression, group.Count()))
            .OrderBy(item => item.Rule, StringComparer.Ordinal).ThenBy(item => item.Expression, StringComparer.Ordinal).ToArray();
    }

    private static (int ExitCode, string Output) Git(params string[] arguments)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = Root, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        _ = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output);
    }
}

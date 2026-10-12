using System.Reflection;
using System.Text.Json;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ClankerWorld.Tests;

public sealed class RuntimeStateBoundaryTests
{
    private static readonly string[] LifetimeNames = ["live", "saved", "scratch"];
    private static string Root => RuntimeWriterScan.RepositoryRoot;
    private static Dictionary<string, string[]> Classification => JsonSerializer.Deserialize<Dictionary<string, string[]>>(
        File.ReadAllText(Path.Combine(Root, "tests/ClankerWorld.Tests/Architecture/runtime-fields.json")))!;

    [Fact]
    public void EveryInstanceFieldHasExactlyOneStateLifetime()
    {
        var categories = Classification;
        Assert.Equal(LifetimeNames, categories.Keys.Order(StringComparer.Ordinal));
        var classified = categories.Values.SelectMany(names => names).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(classified.Distinct(StringComparer.Ordinal).Count(), classified.Length);
        var fields = typeof(WorldRuntime).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Select(field => field.Name).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(fields, classified);
    }

    [Fact]
    public void CommitTransfersEverySavedFieldAndNoLiveOrScratchField()
    {
        var source = File.ReadAllText(Path.Combine(Root,
            "src/ClankerWorld.Simulation/World/Runtime/WorldRuntime.Tick.cs"));
        var method = CSharpSyntaxTree.ParseText(source).GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Single(item => item.Identifier.ValueText == "CommitPreparedTick");
        var transfers = new List<string>();
        foreach (var assignment in method.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (assignment.Left is IdentifierNameSyntax left && assignment.Right is MemberAccessExpressionSyntax right &&
                right.Expression is IdentifierNameSyntax { Identifier.ValueText: "proposed" } &&
                left.Identifier.ValueText == right.Name.Identifier.ValueText)
                transfers.Add(left.Identifier.ValueText);
            else if (assignment.Left is TupleExpressionSyntax swapLeft && assignment.Right is TupleExpressionSyntax swapRight &&
                swapLeft.ToString() == "(society, proposed.society)" && swapRight.ToString() == "(proposed.society, society)")
                transfers.Add("society");
            else
                Assert.Fail("Unrecognized commit transfer: " + assignment);
        }
        Assert.Equal(Classification["saved"].Order(StringComparer.Ordinal), transfers.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task CommittedRuntimeEncodesExactlyThePreparedRuntime()
    {
        using var committed = NormalPathWorld.CreateGenerated("runtime-commit-old", _ => new DeterministicDecisionProvider());
        using var prepared = NormalPathWorld.CreateGenerated("runtime-commit-prepared", _ => new DeterministicDecisionProvider());
        Assert.True((await prepared.AdvanceOneTickAsync()).Advanced);
        Assert.True((await prepared.AdvanceOneTickAsync()).Advanced);
        var expected = WorldRuntimeCodec.Encode(prepared.ExportState());
        var expectedFields = Classification["saved"].ToDictionary(name => name,
            name => typeof(WorldRuntime).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(prepared));
        var commit = typeof(WorldRuntime).GetMethod("CommitPreparedTick", BindingFlags.Instance | BindingFlags.NonPublic)!;
        commit.Invoke(committed, [prepared]);
        Assert.Equal(expected, WorldRuntimeCodec.Encode(committed.ExportState()));
        foreach (var (name, value) in expectedFields)
            Assert.Equal(value, typeof(WorldRuntime).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(committed));
        committed.Validate();
    }

    [Fact]
    public void TrustedPreparationSharesCivicRecordsUntilAProposedWrite()
    {
        using var committed = NormalPathWorld.CreateGenerated("civic-snapshot-sharing", _ => new DeterministicDecisionProvider());
        var civicField = typeof(WorldRuntime).GetField("townSystem", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var original = ((TownSystem)civicField.GetValue(committed)!).State;
        var before = WorldRuntimeCodec.Encode(committed.ExportState());
        var restore = typeof(WorldRuntime).GetMethod("RestoreCore", BindingFlags.Static | BindingFlags.NonPublic)!;
        using var prepared = (WorldRuntime)restore.Invoke(null, [committed.ExportState(), null, 4, true, false, null, original, null])!;
        var proposed = (TownSystem)civicField.GetValue(prepared)!;
        Assert.Same(original, proposed.State);
        var snapshot = prepared.ExportState();
        var town = Assert.Single(snapshot.Towns!);
        var occupied = snapshot.Agents.Select(person => person.Position)
            .Concat(snapshot.Map.Resources.Select(resource => resource.Position))
            .Concat(snapshot.Map.MapObjects.Select(item => item.Position))
            .Concat(snapshot.WorldSimulation!.Buildings.SelectMany(building => WorldContentSimulationRules.Footprint(
                snapshot.WorldContent!.BuildingDefinitions[building.DefinitionId], building))).ToHashSet();
        var position = town.BorderTiles.First(point => snapshot.Map.IsBuildable(point) && !occupied.Contains(point));
        const string newcomer = "agent:fffffffffffffffffffffffffffffffe";

        prepared.AddAgent(newcomer, position);

        Assert.NotSame(original, proposed.State);
        Assert.Same(original.TownLandTitles, proposed.State.TownLandTitles);
        Assert.Contains(newcomer, Assert.Single(prepared.Towns).ResidentIds);
        Assert.DoesNotContain(newcomer, Assert.Single(committed.Towns).ResidentIds);
        Assert.Same(original, ((TownSystem)civicField.GetValue(committed)!).State);
        Assert.Equal(before, WorldRuntimeCodec.Encode(committed.ExportState()));
        prepared.Validate();
    }
}

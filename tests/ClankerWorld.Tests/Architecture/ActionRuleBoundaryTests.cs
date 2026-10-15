using System.Text.Json;

namespace ClankerWorld.Tests;

public sealed class ActionRuleBoundaryTests
{
    [Fact]
    public void ExistingRulesCanOnlyDisappearAsAreasAdoptSharedRules()
    {
        var root = RuntimeWriterScan.RepositoryRoot;
        var path = Path.Combine(root, "tests/ClankerWorld.Tests/Architecture/action-rule-allowances.json");
        var recorded = JsonSerializer.Deserialize<SortedDictionary<string, string[]>>(File.ReadAllText(path))!;
        var actual = ActionRuleScan.Scan(root);
        Assert.Equal(recorded.Keys.Order(StringComparer.Ordinal), actual.Keys);
        foreach (var (file, expressions) in actual)
            Assert.Equal(recorded[file], expressions);
        Assert.DoesNotContain(actual.Keys, file => file.Contains("/Family/WorldRuntime.Child", StringComparison.Ordinal));
        Assert.DoesNotContain(actual.Values.SelectMany(expressions => expressions),
            expression => expression.Contains("AgeBand", StringComparison.Ordinal));
    }

    [Fact]
    public void ScanSeesRawAgeThresholdsAndRoutesButIgnoresCommentsAndSharedRules()
    {
        const string source = """
            class Rules
            {
                bool Check(dynamic actor, dynamic age)
                {
                    // actor.AgeBand == Adult; FindUnoccupiedRoute();
                    var text = "actor.FullnessBasisPoints < 6_000";
                    var first = actor.AgeBand is Child or Adolescent;
                    var second = age != Infant;
                    var third = actor.FullnessBasisPoints < 6_000;
                    var fourth = actor is { WarmthBasisPoints: < 3_500 };
                    FindUnoccupiedRoute(actor, origin, target, 0);
                    world.FindUnoccupiedRoute(actor, origin, target, 0);
                    world?.FindUnoccupiedRoute(actor, origin, target, 0);
                    return LifeStageRules.IsChild(actor.AgeBand) && actor.FullnessBasisPoints < NeedThresholds.ChildHelpingFullness;
                }
            }
            """;
        Assert.Equal(7, ActionRuleScan.Expressions(source).Length);
    }
}

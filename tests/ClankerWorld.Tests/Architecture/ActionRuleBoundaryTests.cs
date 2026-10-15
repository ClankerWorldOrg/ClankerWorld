namespace ClankerWorld.Tests;

public sealed class ActionRuleBoundaryTests
{
    [Fact]
    public void RuntimeUsesSharedLifeStageNeedAndReachRules()
    {
        Assert.Empty(ActionRuleScan.Scan(RuntimeWriterScan.RepositoryRoot));
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
                    var fifth = warmth < 2_500;
                    var sixth = fullness > 3_500;
                    FindUnoccupiedRoute(actor, origin, target, 0);
                    world.FindUnoccupiedRoute(actor, origin, target, 0);
                    world?.FindUnoccupiedRoute(actor, origin, target, 0);
                    return LifeStageRules.IsChild(actor.AgeBand) && actor.FullnessBasisPoints < NeedThresholds.ChildHelpingFullness;
                }
            }
            """;
        Assert.Equal(9, ActionRuleScan.Expressions(source).Length);
    }
}

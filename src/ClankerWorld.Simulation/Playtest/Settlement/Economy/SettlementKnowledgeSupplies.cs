namespace ClankerWorld.Simulation.Playtest;

public sealed partial class PrivateWorldRuntime
{
    private bool NeedsKnowledgePaper(string? householdId)
    {
        if (householdId is null || knowledge.Artifacts.Count >= AgentKnowledgeRules.MaximumArtifactsInWorld)
            return false;

        var writers = society.Checkpoint.Inhabitants.Where(person => person.HouseholdId == householdId &&
                AdultResident(person.Id) && knowledge.Facts.Any(fact => fact.OwnerId == person.Id) &&
                knowledge.Artifacts.Count(artifact => artifact.CreatorId == person.Id) <
                    AgentKnowledgeRules.MaximumArtifactsPerCreator)
            .Select(person => person.Id).ToHashSet(StringComparer.Ordinal);
        if (writers.Count == 0)
            return false;

        // Two sheets per eligible writer permit a book. The shared same-job
        // rule counts writing commitments without making them available to pickups.
        var available = FindGoods(new(GoodsUse.Holdings, null, new(writers.Append(householdId).ToArray()),
            GoodsKinds.One(KnowledgeContent.Paper), DemandCoverage: GoodsDemandCoverage.KnowledgeWriting)).CoveredTotal;
        return available < writers.Count * 2;
    }
}

using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed partial class GrownAgentHelperMemoryTests
{
    [Theory]
    [InlineData("birth-person")]
    [InlineData("birth-request")]
    [InlineData("birth-relationships")]
    [InlineData("birth-food")]
    [InlineData("domestic-person")]
    [InlineData("birth-domestic")]
    [InlineData("household-creation")]
    [InlineData("household-join")]
    [InlineData("household-rejoin")]
    [InlineData("consumption")]
    [InlineData("memory-summary")]
    public async Task NativeThirdGenerationSocietyGeneratorInventoryKeepsExactLinksAndRecallAfterStrictReload(string generator)
    {
        var state = WorldRuntimeCodec.Decode(await NativeFiftyMilkTrace.Value);
        var society = state.Society.Society;
        var (actor, relative, births) = NativeFamily(society);
        var priorEdges = society.Relationships.Select(edge => edge.Id).ToHashSet(StringComparer.Ordinal);
        var priorHolds = society.Inventory.Reservations.Select(hold => hold.Id.Value).ToHashSet(StringComparer.Ordinal);
        var newHome = "household:" + new string('h', 118);
        var name = "Household " + new string('n', 118);
        if (generator.StartsWith("household-", StringComparison.Ordinal))
        {
            society = SocietyRules.LeaveHousehold(society, actor).Checkpoint;
            Assert.Null(society.GetAgent(actor).HouseholdId);
            if (generator == "household-creation")
                society = SocietyRules.CreateHousehold(society, newHome, name, [actor]).Checkpoint;
            else
            {
                society = SocietyRules.LeaveHousehold(society, relative).Checkpoint;
                Assert.Null(society.GetAgent(relative).HouseholdId);
                society = SocietyRules.CreateHousehold(society, newHome, name, [relative]).Checkpoint;
                society = SocietyRules.JoinHousehold(society, actor, newHome).Checkpoint;
                if (generator == "household-rejoin")
                {
                    society = SocietyRules.LeaveHousehold(society, actor).Checkpoint;
                    society = SocietyRules.JoinHousehold(society, actor, newHome).Checkpoint;
                }
            }
            Assert.Equal(newHome, society.GetAgent(actor).HouseholdId);
            Assert.Equal(name, society.GetHousehold(newHome).Name);
        }
        else if (generator == "consumption")
        {
            var lot = "bread:" + new string('b', 122);
            society = society with { Inventory = InventoryAuthority.AddLot(society.Inventory, lot, "bread", actor, 3) };
            society = SocietyRules.ConsumeInventory(society, actor, lot, 1).Checkpoint;
            Assert.Equal(2, society.Inventory.GetLot(lot).Quantity);
        }
        var ids = generator switch
        {
            "birth-person" => births.Select(birth => birth.ChildId).ToArray(),
            "birth-request" => births.Select(birth => birth.RequestId).ToArray(),
            "birth-relationships" => society.Relationships.Where(edge => births.Any(birth => birth.ChildId == edge.TargetId) &&
                edge.Type is SocietyRelationshipType.BiologicalParentage or SocietyRelationshipType.Caregiver or
                SocietyRelationshipType.HouseholdMembership).Select(edge => edge.Id).ToArray(),
            "birth-food" => society.Inventory.Reservations.Where(hold => births.Any(birth => hold.Purpose == "birth:" + birth.RequestId))
                .Select(hold => hold.Id.Value).ToArray(),
            "domestic-person" => births.Select(birth => society.GetAgent(birth.ChildId).DomesticFamilyUnitId!).ToArray(),
            "birth-domestic" => births.Select(birth => birth.DomesticFamilyUnitId).ToArray(),
            "household-creation" or "household-join" or "household-rejoin" => society.Relationships
                .Where(edge => !priorEdges.Contains(edge.Id)).Select(edge => edge.Id).ToArray(),
            "consumption" => society.Inventory.Reservations.Where(hold => !priorHolds.Contains(hold.Id)).Select(hold => hold.Id.Value).ToArray(),
            "memory-summary" => new[] { SocietyMemorySummaryRules.Id(actor, society.Memories.Where(memory => memory.OwnerId == actor)
                .TakeLast(SocietyMemorySummaryRules.MaximumSources)
                .Select(memory => new SocietyMemorySummarySource(memory.Id, SocietyMemorySourceKind.Experience, memory.SourceTick)).ToArray()) },
            _ => throw new ArgumentOutOfRangeException(nameof(generator)),
        };
        Assert.NotEmpty(ids);
        foreach (var id in ids.Distinct(StringComparer.Ordinal))
        {
            Assert.InRange(id.Length, 1, DerivedId.MaximumLength);
            var summary = "I remember society" + generator.Replace("-", "", StringComparison.Ordinal) + " work.";
            var checkpoint = SocietyRules.RecordSocialMemory(society, new(Identity.Parse(id), actor, relative, summary,
                "private", society.WorldTick)).Checkpoint;
            var bytes = WorldRuntimeCodec.Encode(state with { Society = state.Society with { Society = checkpoint } });
            using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes));
            Assert.Equal(bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
            AssertNativeRecall(restored, id, actor, relative, summary, "sha256:society-inventory");
        }
    }

    private static (string Actor, string Relative, SocietyBirthRecord[] Births) NativeFamily(SocietyCheckpoint society)
    {
        var births = society.Births.OrderBy(birth => birth.CommittedTick).ToArray();
        Assert.Equal(3, births.Length);
        var actor = births[^1].ChildId;
        var relative = births[^2].ChildId;
        Assert.Contains(society.Relationships, edge => edge.Type == SocietyRelationshipType.BiologicalParentage &&
            edge.ProposerId == relative && edge.TargetId == actor);
        return (actor, relative, births);
    }

    private static void AssertNativeRecall(WorldRuntime restored, string id, string actor, string subject,
        string summary, string digest, IReadOnlyList<CognitionCandidate>? candidates = null)
    {
        var recalled = WorldMemoryRetrieval.Retrieve(restored.Society.Memories, restored.Society.Beliefs ?? [],
            null, actor, restored.WorldTick, candidates ?? [new("safe_idle", summary, 0)], restored.Society);
        var memory = Assert.Single(recalled, memory => memory.Id == id);
        Assert.Equal((actor, subject, summary), (memory.OwnerId, memory.SubjectId, memory.Summary));
        new AgentObservation(actor, restored.WorldTick, 0, 1, digest, 0,
            candidates ?? [new("safe_idle", summary, 0)], RetrievedMemories: recalled).Validate();
    }
}

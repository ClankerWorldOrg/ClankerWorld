using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Playtest;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.Tests;

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
        var state = PrivateWorldRuntimeCodec.Decode(await NativeFiftyMilkTrace.Value);
        var society = state.Society.Society;
        var births = society.Births.OrderBy(birth => birth.CommittedTick).ToArray();
        Assert.Equal(3, births.Length);
        var actor = births[^1].ChildId;
        var relative = births[^2].ChildId;
        Assert.Contains(society.Relationships, edge => edge.Type == SocietyRelationshipType.BiologicalParentage &&
            edge.ProposerId == relative && edge.TargetId == actor);
        var priorEdges = society.Relationships.Select(edge => edge.Id).ToHashSet(StringComparer.Ordinal);
        var priorHolds = society.Inventory.Reservations.Select(hold => hold.Id).ToHashSet(StringComparer.Ordinal);
        var newHome = "household:" + new string('h', 118);
        var name = "Household " + new string('n', 118);
        if (generator.StartsWith("household-", StringComparison.Ordinal))
        {
            society = SocietyFixture.LeaveHousehold(society, actor).Checkpoint;
            Assert.Null(society.GetInhabitant(actor).HouseholdId);
            if (generator == "household-creation")
                society = SocietyFixture.CreateHousehold(society, newHome, name, [actor]).Checkpoint;
            else
            {
                society = SocietyFixture.LeaveHousehold(society, relative).Checkpoint;
                Assert.Null(society.GetInhabitant(relative).HouseholdId);
                society = SocietyFixture.CreateHousehold(society, newHome, name, [relative]).Checkpoint;
                society = SocietyFixture.JoinHousehold(society, actor, newHome).Checkpoint;
                if (generator == "household-rejoin")
                {
                    society = SocietyFixture.LeaveHousehold(society, actor).Checkpoint;
                    society = SocietyFixture.JoinHousehold(society, actor, newHome).Checkpoint;
                }
            }
            Assert.Equal(newHome, society.GetInhabitant(actor).HouseholdId);
            Assert.Equal(name, society.GetHousehold(newHome).Name);
        }
        else if (generator == "consumption")
        {
            var lot = "bread:" + new string('b', 122);
            society = society with { Inventory = InventoryFixture.AddLot(society.Inventory, lot, "bread", actor, 3) };
            society = SocietyFixture.ConsumeInventory(society, actor, lot, 1).Checkpoint;
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
                .Select(hold => hold.Id).ToArray(),
            "domestic-person" => births.Select(birth => society.GetInhabitant(birth.ChildId).DomesticFamilyUnitId!).ToArray(),
            "birth-domestic" => births.Select(birth => birth.DomesticFamilyUnitId).ToArray(),
            "household-creation" or "household-join" or "household-rejoin" => society.Relationships
                .Where(edge => !priorEdges.Contains(edge.Id)).Select(edge => edge.Id).ToArray(),
            "consumption" => society.Inventory.Reservations.Where(hold => !priorHolds.Contains(hold.Id)).Select(hold => hold.Id).ToArray(),
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
            var checkpoint = SocietyFixture.RecordSocialMemory(society, new(id, actor, relative, summary,
                "private", society.WorldTick)).Checkpoint;
            var bytes = PrivateWorldRuntimeCodec.Encode(state with { Society = state.Society with { Society = checkpoint } });
            using var restored = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(bytes));
            Assert.Equal(bytes, PrivateWorldRuntimeCodec.Encode(restored.ExportState()));
            var recalled = PrivateWorldMemoryRetrieval.Retrieve(restored.Society.Memories, restored.Society.Beliefs ?? [],
                null, actor, restored.WorldTick, [new("safe_idle", summary, 0)], restored.Society);
            var memory = Assert.Single(recalled, memory => memory.Id == id);
            Assert.Equal((actor, relative, summary), (memory.OwnerId, memory.SubjectId, memory.Summary));
            new InhabitantObservation(actor, restored.WorldTick, 0, 1, "sha256:society-inventory", 0,
                [new("safe_idle", summary, 0)], RetrievedMemories: recalled).Validate();
        }
    }
}

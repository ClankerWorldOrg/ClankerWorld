using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed class PartnershipFamilyUnitTests
{
    private static readonly string[] FounderIds = ["alice", "bob", "cara"];

    [Theory]
    [InlineData("withdraw")]
    [InlineData("target-withdraw")]
    [InlineData("refuse")]
    [InlineData("end-accepted")]
    public void ClosingAProposalPreservesFamilyUnlessPartnershipWasAccepted(string action)
    {
        var (checkpoint, child) = BirthFamily();
        checkpoint = SocietyRules.AdvanceTo(checkpoint, checkpoint.Config.AdultYears * checkpoint.Config.TicksPerWorldYear).Checkpoint;
        Assert.Equal(SocietyAgeBand.Adult, checkpoint.GetAgent(child).AgeBand);
        var originalUnits = Units(checkpoint);
        Assert.Equal(checkpoint.GetAgent("alice").DomesticFamilyUnitId, checkpoint.GetAgent(child).DomesticFamilyUnitId);
        Assert.Equal(4, Capacity(checkpoint).Limit);
        checkpoint = SocietyRules.ProposeRelationship(checkpoint,
            new("adult-child-proposal", 1, SocietyRelationshipType.Partnership, child, "cara", checkpoint.WorldTick)).Checkpoint;
        Assert.Equal(originalUnits, Units(checkpoint));
        checkpoint = Roundtrip(checkpoint);
        if (action == "end-accepted")
        {
            checkpoint = SocietyRules.AcceptRelationship(checkpoint, "adult-child-proposal", 1, "cara").Checkpoint;
            Assert.Equal(checkpoint.GetAgent(child).DomesticFamilyUnitId, checkpoint.GetAgent("cara").DomesticFamilyUnitId);
            Assert.NotEqual(checkpoint.GetAgent("alice").DomesticFamilyUnitId, checkpoint.GetAgent(child).DomesticFamilyUnitId);
        }
        var closure = action == "refuse"
            ? SocietyRules.RefuseRelationship(checkpoint, "adult-child-proposal", 1, "cara")
            : SocietyRules.RevokeRelationship(checkpoint, "adult-child-proposal", action == "target-withdraw" ? "cara" : child);
        Assert.Equal(action == "refuse" ? "relationship_rejected" : "relationship_revoked", Assert.Single(closure.NewEvents!).Kind);
        checkpoint = closure.Checkpoint;
        checkpoint = Roundtrip(checkpoint);
        Assert.Equal(action == "refuse" ? SocietyRelationshipState.Rejected : SocietyRelationshipState.Revoked,
            checkpoint.GetRelationship("adult-child-proposal").State);
        Assert.All(checkpoint.Agents, person => Assert.Equal("home", person.HouseholdId));
        AssertBirthHistory(checkpoint, child);
        if (action == "end-accepted")
        {
            Assert.NotEqual(checkpoint.GetAgent(child).DomesticFamilyUnitId, checkpoint.GetAgent("cara").DomesticFamilyUnitId);
            Assert.StartsWith("domestic:separate:", checkpoint.GetAgent(child).DomesticFamilyUnitId);
            Assert.StartsWith("domestic:separate:", checkpoint.GetAgent("cara").DomesticFamilyUnitId);
            Assert.Equal(3, Capacity(checkpoint).Limit);
        }
        else
        {
            Assert.Equal(4, Capacity(checkpoint).Limit);
            Assert.Equal(originalUnits, Units(checkpoint));
        }
    }

    [Fact]
    public void RevokedPrimaryCarePreservesChildFamilyWhenFormerCaregiverFindsPartner()
    {
        var (checkpoint, child) = BirthFamily();
        checkpoint = SocietyRules.RevokeRelationship(checkpoint, "parents", "alice").Checkpoint;
        var originalChildUnit = checkpoint.GetAgent(child).DomesticFamilyUnitId;
        Assert.Equal(checkpoint.GetAgent("alice").DomesticFamilyUnitId, originalChildUnit);
        Assert.NotEqual(checkpoint.GetAgent("bob").DomesticFamilyUnitId, originalChildUnit);
        var care = Assert.Single(checkpoint.Relationships, edge => edge.Type == SocietyRelationshipType.Caregiver &&
            edge.ProposerId == "alice" && edge.TargetId == child && edge.State == SocietyRelationshipState.Accepted);
        checkpoint = SocietyRules.RevokeRelationship(checkpoint, care.Id, "alice").Checkpoint;
        checkpoint = Roundtrip(checkpoint);
        Assert.Null(checkpoint.GetAgent(child).PrimaryCaregiverId);
        var originalLimit = Capacity(checkpoint).Limit;
        checkpoint = SocietyRules.ProposeRelationship(checkpoint,
            new("new-partnership", 1, SocietyRelationshipType.Partnership, "alice", "cara", checkpoint.WorldTick)).Checkpoint;
        checkpoint = SocietyRules.AcceptRelationship(checkpoint, "new-partnership", 1, "cara").Checkpoint;
        checkpoint = Roundtrip(checkpoint);
        Assert.Equal(originalChildUnit, checkpoint.GetAgent(child).DomesticFamilyUnitId);
        Assert.NotEqual(checkpoint.GetAgent("alice").DomesticFamilyUnitId, checkpoint.GetAgent(child).DomesticFamilyUnitId);
        Assert.Null(checkpoint.GetAgent(child).PrimaryCaregiverId);
        Assert.Equal(originalLimit, Capacity(checkpoint).Limit);
        Assert.All(checkpoint.Agents, person => Assert.Equal("home", person.HouseholdId));
        AssertBirthHistory(checkpoint, child);
    }

    [Fact]
    public async Task NormalTickExpiryOfUnacceptedProposalPreservesFamilyUnits()
    {
        using var seed = NormalPathWorld.CreateGenerated("audit-town-invariants", _ => new Idle());
        var state = seed.ExportState();
        var people = state.Society.Society.Agents.Select(person => person.Id).Order(StringComparer.Ordinal).ToArray();
        var proposed = SocietyRules.ProposeRelationship(state.Society.Society,
            new("expiring-proposal", 1, SocietyRelationshipType.Partnership, people[0], people[1], 0)).Checkpoint;
        state = state with { Society = state.Society with { Society = proposed } };
        var originalUnits = Units(proposed);
        var originalHouseholds = proposed.Agents.Select(person => (person.Id, person.HouseholdId)).ToArray();
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)), _ => new Idle());
        while (world.WorldTick < 120) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(SocietyRelationshipState.Proposed, world.Society.GetRelationship("expiring-proposal").State);
        Assert.Equal(originalUnits, Units(world.Society));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())), _ => new Idle());
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        world.Validate();
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        Assert.Equal(SocietyRelationshipState.Revoked, world.Society.GetRelationship("expiring-proposal").State);
        Assert.Equal(people[0], Assert.Single(world.ExportState().Events, item => item.Kind == "partnership_expired").Detail);
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        Assert.Equal(Units(world.Society), Units(restored.Society));
        Assert.Equal(originalUnits, Units(world.Society));
        Assert.Equal(originalHouseholds, world.Society.Agents.Select(person => (person.Id, person.HouseholdId)));
    }

    private static (SocietyCheckpoint Checkpoint, string Child) BirthFamily()
    {
        var config = new SocietyConfig(TicksPerWorldDay: 1, DaysPerWorldYear: 10);
        var people = FounderIds.Select(id => SocietyRules.CreateFounder(id, id, config: config)).ToArray();
        var checkpoint = SocietyRules.CreateGenesis("partnership-family", people,
            [new InventoryLot("food", "food", "alice", 3, 10_000, 10_000, 0)], config);
        checkpoint = SocietyRules.CreateHousehold(checkpoint, "home", "Home", ["alice", "bob", "cara"]).Checkpoint;
        checkpoint = SocietyRules.ProposeRelationship(checkpoint,
            new("parents", 1, SocietyRelationshipType.Partnership, "alice", "bob", 0)).Checkpoint;
        checkpoint = SocietyRules.AcceptRelationship(checkpoint, "parents", 1, "bob").Checkpoint;
        var birth = SocietyRules.CommitBirth(checkpoint, new SocietyBirthRequest("child", 1, "alice", "bob", "home",
            ["alice", "bob"], ["alice", "bob"], "food", 2, checkpoint.WorldTick, PrimaryCaregiverId: "alice"));
        return (Roundtrip(birth.Checkpoint), Assert.IsType<string>(birth.CreatedId));
    }

    private static HouseResidentCapacityRules.Capacity Capacity(SocietyCheckpoint checkpoint) =>
        HouseResidentCapacityRules.Calculate(checkpoint.Agents.Where(person => person.HouseholdId == "home"), 1, 1);

    private static (string Id, string? Family)[] Units(SocietyCheckpoint checkpoint) =>
        checkpoint.Agents.Select(person => (person.Id, person.DomesticFamilyUnitId)).ToArray();

    private static void AssertBirthHistory(SocietyCheckpoint checkpoint, string child)
    {
        var birth = Assert.Single(checkpoint.Births);
        Assert.Equal(child, birth.ChildId);
        Assert.Equal("alice", birth.PrimaryCaregiverId);
        Assert.Equal(FounderIds.Take(2), checkpoint.Relationships
            .Where(edge => edge.Type == SocietyRelationshipType.BiologicalParentage && edge.TargetId == child)
            .Select(edge => edge.ProposerId).Order(StringComparer.Ordinal));
    }

    private static SocietyCheckpoint Roundtrip(SocietyCheckpoint checkpoint)
    {
        SocietyRules.Validate(checkpoint);
        var bytes = SocietyCheckpointCodec.Encode(checkpoint);
        var restored = SocietyCheckpointCodec.Decode(bytes);
        Assert.Equal(bytes, SocietyCheckpointCodec.Encode(restored));
        return restored;
    }

    private sealed class Idle : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default) =>
            new DeterministicDecisionProvider().DecideAsync(request with
            {
                Observation = request.Observation with { Candidates = [request.Observation.Candidates.Single(item => item.Id == "safe_idle")] },
            }, cancellationToken);
    }
}

using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed class SocietyTests
{
    [Theory]
    [InlineData("b", true)]
    public void DeathReleasesBothOpenBarterReservationsButPreservesOtherWork(string deceased, bool accepted)
    {
        var state = SocietyRules.CreateGenesis("death-barter", [SocietyRules.CreateFounder("a", "A"), SocietyRules.CreateFounder("b", "B")]);
        state = state with { Inventory = InventoryAuthority.AddLot(state.Inventory, "a-food", "food", "a", 2) };
        state = state with { Inventory = InventoryAuthority.AddLot(state.Inventory, "b-food", "food", "b", 2) };
        state = SocietyRules.CreateBarterOffer(state, new DirectBarterProposal("offer", 1, "a", "b", "a-food", 1, "b-food", 1, 120)).Checkpoint;
        if (accepted) state = SocietyRules.AcceptBarterOffer(state, "offer", 1, "a").Checkpoint;
        var survivor = deceased == "a" ? "b" : "a";
        state = state with { Inventory = InventoryAuthority.Reserve(state.Inventory, "unrelated", survivor, survivor + "-food", 1, "work", 120) };
        state = SocietyRules.Kill(state, deceased, SocietyDeathCause.Accident).Checkpoint;
        Assert.Equal(DirectBarterState.Cancelled, state.Inventory.GetOffer("offer").State);
        Assert.Equal(InventoryReservationState.Released, state.Inventory.GetReservation("offer:first").State);
        Assert.Equal(InventoryReservationState.Released, state.Inventory.GetReservation("offer:second").State);
        Assert.Equal(InventoryReservationState.Reserved, state.Inventory.GetReservation("unrelated").State);
        state = SocietyCheckpointCodec.Decode(SocietyCheckpointCodec.Encode(state));
        state = SocietyRules.ConsumeInventory(state, survivor, survivor + "-food", 1).Checkpoint;
        Assert.Equal(1, state.Inventory.Lots.Single(lot => lot.Id == survivor + "-food").Quantity);
    }

    [Fact]
    public void DeathDoesNotCancelAnAlreadySettledBarter()
    {
        var state = SocietyRules.CreateGenesis("settled-barter-death", [SocietyRules.CreateFounder("a", "A"), SocietyRules.CreateFounder("b", "B")]);
        state = state with { Inventory = InventoryAuthority.AddLot(state.Inventory, "a-food", "food", "a", 1) };
        state = state with { Inventory = InventoryAuthority.AddLot(state.Inventory, "b-food", "food", "b", 1) };
        state = SocietyRules.CreateBarterOffer(state, new DirectBarterProposal("done", 1, "a", "b", "a-food", 1, "b-food", 1, 120)).Checkpoint;
        state = SocietyRules.AcceptBarterOffer(state, "done", 1, "a").Checkpoint;
        state = SocietyRules.AcceptBarterOffer(state, "done", 1, "b").Checkpoint;
        state = SocietyRules.Kill(state, "a", SocietyDeathCause.Accident).Checkpoint;
        Assert.Equal(DirectBarterState.Settled, state.Inventory.GetOffer("done").State);
    }

    [Fact]
    public void SocietyClockExpiresInventoryReservationsExactlyOnce()
    {
        var checkpoint = Genesis(TestConfig(), "alice", "bob");
        checkpoint = checkpoint with
        {
            Inventory = InventoryAuthority.Reserve(checkpoint.Inventory, "meal", "alice", "food-lot", 1, "meal", 2),
        };
        var atBoundary = SocietyRules.AdvanceTo(checkpoint, 2).Checkpoint;
        Assert.Equal(InventoryReservationState.Reserved, atBoundary.Inventory.GetReservation("meal").State);
        var after = SocietyRules.AdvanceTo(atBoundary, 3).Checkpoint;
        Assert.Equal(InventoryReservationState.Released, after.Inventory.GetReservation("meal").State);
        Assert.Single(after.Inventory.Events, item => item.Kind == "reservation_released");
        var later = SocietyRules.AdvanceTo(after, 9).Checkpoint;
        Assert.Single(later.Inventory.Events, item => item.Kind == "reservation_released");
    }

    [Fact]
    public void RelationshipConsentAndCardinalityAreAuthoritative()
    {
        var config = TestConfig();
        var checkpoint = Genesis(config, "alice", "bob", "cara");
        checkpoint = SocietyRules.CreateHousehold(checkpoint, "home", "The Home", ["alice", "bob", "cara"]).Checkpoint;

        checkpoint = SocietyRules.ProposeRelationship(
            checkpoint,
            new SocietyRelationshipProposal(
                "partnership-1",
                1,
                SocietyRelationshipType.Partnership,
                "alice",
                "bob",
                checkpoint.WorldTick)).Checkpoint;
        checkpoint = SocietyRules.AcceptRelationship(checkpoint, "partnership-1", 1, "bob").Checkpoint;
        Assert.Equal(SocietyRelationshipState.Accepted, checkpoint.GetRelationship("partnership-1").State);

        checkpoint = SocietyRules.ProposeRelationship(
            checkpoint,
            new SocietyRelationshipProposal(
                "partnership-2",
                1,
                SocietyRelationshipType.Partnership,
                "bob",
                "cara",
                checkpoint.WorldTick)).Checkpoint;
        var rejected = SocietyRules.AcceptRelationship(checkpoint, "partnership-2", 1, "cara");

        Assert.Equal(SocietyRelationshipState.Proposed, rejected.Checkpoint.GetRelationship("partnership-2").State);
        Assert.Contains(
            rejected.NewEvents!,
            societyEvent => societyEvent.Kind == "relationship_rejected" &&
                societyEvent.Detail.EndsWith("cardinality_conflict", StringComparison.Ordinal));
    }

    [Fact]
    public void PartnershipChangesDomesticFamilyGroupAndDependentsFollowRecordedCaregiver()
    {
        var checkpoint = Genesis(TestConfig(), "alice", "bob", "cara", "child");
        checkpoint = checkpoint with
        {
            Agents = checkpoint.Agents.Select(person => person.Id == "child"
                ? person with
                {
                    BirthTick = 0,
                    LastLifecycleYearChecked = 0,
                    AgeBand = SocietyAgeBand.Infant,
                    PrimaryCaregiverId = "alice",
                    DomesticFamilyUnitId = "domestic:initial-child",
                }
                : person).ToArray(),
        };
        checkpoint = SocietyRules.CreateHousehold(checkpoint, "home-a", "Home A", ["alice", "child"]).Checkpoint;
        checkpoint = SocietyRules.CreateHousehold(checkpoint, "home-b", "Home B", ["bob"]).Checkpoint;
        checkpoint = SocietyRules.CreateHousehold(checkpoint, "home-c", "Home C", ["cara"]).Checkpoint;

        checkpoint = SocietyRules.ProposeRelationship(checkpoint,
            new("first-partnership", 1, SocietyRelationshipType.Partnership, "alice", "bob", 0)).Checkpoint;
        checkpoint = SocietyRules.AcceptRelationship(checkpoint, "first-partnership", 1, "bob").Checkpoint;
        var firstUnit = checkpoint.GetAgent("alice").DomesticFamilyUnitId;
        Assert.Equal(firstUnit, checkpoint.GetAgent("bob").DomesticFamilyUnitId);
        Assert.Equal(firstUnit, checkpoint.GetAgent("child").DomesticFamilyUnitId);

        checkpoint = SocietyRules.RevokeRelationship(checkpoint, "first-partnership", "alice").Checkpoint;
        var separatedCaregiverUnit = checkpoint.GetAgent("alice").DomesticFamilyUnitId;
        Assert.NotEqual(separatedCaregiverUnit, checkpoint.GetAgent("bob").DomesticFamilyUnitId);
        Assert.Equal(separatedCaregiverUnit, checkpoint.GetAgent("child").DomesticFamilyUnitId);

        checkpoint = SocietyRules.ProposeRelationship(checkpoint,
            new("second-partnership", 1, SocietyRelationshipType.Partnership, "alice", "cara", checkpoint.WorldTick)).Checkpoint;
        checkpoint = SocietyRules.AcceptRelationship(checkpoint, "second-partnership", 1, "cara").Checkpoint;
        var newUnit = checkpoint.GetAgent("alice").DomesticFamilyUnitId;
        Assert.Equal(newUnit, checkpoint.GetAgent("cara").DomesticFamilyUnitId);
        Assert.Equal(newUnit, checkpoint.GetAgent("child").DomesticFamilyUnitId);
        Assert.NotEqual(newUnit, checkpoint.GetAgent("bob").DomesticFamilyUnitId);
        Assert.Equal("home-a", checkpoint.GetAgent("alice").HouseholdId);
        Assert.Equal("home-b", checkpoint.GetAgent("bob").HouseholdId);
        Assert.Equal("home-a", checkpoint.GetAgent("child").HouseholdId);

        var restored = SocietyCheckpointCodec.Decode(SocietyCheckpointCodec.Encode(checkpoint));
        Assert.Equal(newUnit, restored.GetAgent("child").DomesticFamilyUnitId);
        Assert.Equal("alice", restored.GetAgent("child").PrimaryCaregiverId);
        Assert.Equal("home-a", restored.GetAgent("child").HouseholdId);
    }

    [Fact]
    public void RevokedPrimaryCareDoesNotCarryChildIntoFormerCaregiversNewPartnership()
    {
        var (checkpoint, childId) = BirthWithAcceptedCare();
        checkpoint = SocietyRules.RevokeRelationship(checkpoint, "partnership-1", "alice").Checkpoint;
        var separatedFamily = checkpoint.GetAgent(childId).DomesticFamilyUnitId;
        Assert.Equal(separatedFamily, checkpoint.GetAgent("alice").DomesticFamilyUnitId);
        Assert.NotEqual(separatedFamily, checkpoint.GetAgent("bob").DomesticFamilyUnitId);

        var primaryCare = Assert.Single(checkpoint.Relationships, edge => edge.Type == SocietyRelationshipType.Caregiver &&
            edge.ProposerId == "alice" && edge.TargetId == childId && edge.State == SocietyRelationshipState.Accepted);
        checkpoint = SocietyRules.RevokeRelationship(checkpoint, primaryCare.Id, "alice").Checkpoint;
        Assert.Equal(separatedFamily, checkpoint.GetAgent(childId).DomesticFamilyUnitId);

        checkpoint = SocietyRules.ProposeRelationship(checkpoint,
            new("partnership-2", 1, SocietyRelationshipType.Partnership, "alice", "cara", checkpoint.WorldTick)).Checkpoint;
        checkpoint = SocietyRules.AcceptRelationship(checkpoint, "partnership-2", 1, "cara").Checkpoint;

        Assert.NotEqual(separatedFamily, checkpoint.GetAgent("alice").DomesticFamilyUnitId);
        Assert.Equal(separatedFamily, checkpoint.GetAgent(childId).DomesticFamilyUnitId);
        Assert.Null(checkpoint.GetAgent(childId).PrimaryCaregiverId);
        Assert.Equal("alice", Assert.Single(checkpoint.Births).PrimaryCaregiverId);
        Assert.Equal("home-a", checkpoint.GetAgent(childId).HouseholdId);
    }

    [Fact]
    public void RevokingNonprimaryCareLeavesTheRecordedPrimaryCaregiver()
    {
        var (checkpoint, childId) = BirthWithAcceptedCare();
        var birthFamily = checkpoint.GetAgent(childId).DomesticFamilyUnitId;
        var secondaryCare = Assert.Single(checkpoint.Relationships, edge => edge.Type == SocietyRelationshipType.Caregiver &&
            edge.ProposerId == "bob" && edge.TargetId == childId && edge.State == SocietyRelationshipState.Accepted);

        checkpoint = SocietyRules.RevokeRelationship(checkpoint, secondaryCare.Id, "bob").Checkpoint;

        Assert.Equal("alice", checkpoint.GetAgent(childId).PrimaryCaregiverId);
        Assert.True(SocietyRules.HasActivePrimaryCaregiver(checkpoint, childId));
        Assert.Equal(birthFamily, checkpoint.GetAgent(childId).DomesticFamilyUnitId);
        Assert.Equal("alice", Assert.Single(checkpoint.Births).PrimaryCaregiverId);
    }

    [Fact]
    public void RevokingAcceptedCareFromAnAdultWithoutAHouseholdClearsTheCurrentPrimary()
    {
        var (checkpoint, childId) = BirthWithAcceptedCare();
        var caraMembership = Assert.Single(checkpoint.Relationships, edge => edge.Type == SocietyRelationshipType.HouseholdMembership &&
            edge.TargetId == "cara" && edge.State == SocietyRelationshipState.Accepted);
        checkpoint = SocietyRules.RevokeRelationship(checkpoint, caraMembership.Id, "cara").Checkpoint;
        Assert.Null(checkpoint.GetAgent("cara").HouseholdId);

        var formerPrimary = Assert.Single(checkpoint.Relationships, edge => edge.Type == SocietyRelationshipType.Caregiver &&
            edge.ProposerId == "alice" && edge.TargetId == childId && edge.State == SocietyRelationshipState.Accepted);
        checkpoint = SocietyRules.RevokeRelationship(checkpoint, formerPrimary.Id, "alice").Checkpoint;
        var acceptedCare = SocietyRules.AcceptDependentGuardianship(checkpoint, "cara", childId);
        Assert.Contains(acceptedCare.NewEvents ?? [], item => item.Kind == "dependent_guardian_accepted");
        checkpoint = acceptedCare.Checkpoint;

        var acceptedEdge = Assert.Single(checkpoint.Relationships, edge => edge.Type == SocietyRelationshipType.Caregiver &&
            edge.ProposerId == "cara" && edge.TargetId == childId && edge.State == SocietyRelationshipState.Accepted);
        Assert.Null(acceptedEdge.HouseholdId);
        Assert.Equal("cara", checkpoint.GetAgent(childId).PrimaryCaregiverId);
        var familyUnit = checkpoint.GetAgent(childId).DomesticFamilyUnitId;

        checkpoint = SocietyRules.RevokeRelationship(checkpoint, acceptedEdge.Id, "cara").Checkpoint;

        Assert.Null(checkpoint.GetAgent(childId).PrimaryCaregiverId);
        Assert.Equal(familyUnit, checkpoint.GetAgent(childId).DomesticFamilyUnitId);
        Assert.Equal("home-a", checkpoint.GetAgent(childId).HouseholdId);
        Assert.Equal("alice", Assert.Single(checkpoint.Births).PrimaryCaregiverId);
    }

    [Fact]
    public void BirthBindsCaregiverAndHomeWhenParentsLiveInDifferentHouseholds()
    {
        var checkpoint = Genesis(TestConfig(), "alice", "bob");
        checkpoint = SocietyRules.CreateHousehold(checkpoint, "home-a", "Home A", ["alice"]).Checkpoint;
        checkpoint = SocietyRules.CreateHousehold(checkpoint, "home-b", "Home B", ["bob"]).Checkpoint;
        checkpoint = SocietyRules.ProposeRelationship(checkpoint,
            new("cross-home-partnership", 1, SocietyRelationshipType.Partnership, "alice", "bob", 0)).Checkpoint;
        checkpoint = SocietyRules.AcceptRelationship(checkpoint, "cross-home-partnership", 1, "bob").Checkpoint;

        var birth = SocietyRules.CommitBirth(checkpoint, new SocietyBirthRequest(
            "cross-home-birth", 1, "alice", "bob", "home-a", ["alice"], ["alice", "bob"],
            "food-lot", 2, checkpoint.WorldTick, PrimaryCaregiverId: "alice"));
        var child = birth.Checkpoint.GetAgent(Assert.IsType<string>(birth.CreatedId));
        var record = Assert.Single(birth.Checkpoint.Births);
        Assert.Throws<InvalidDataException>(() => SocietyRules.Validate(birth.Checkpoint with
        {
            Births = [record with { CommittedTick = birth.Checkpoint.WorldTick + 1 }],
        }));

        Assert.Equal("home-a", child.HouseholdId);
        Assert.Equal("alice", child.PrimaryCaregiverId);
        Assert.Equal(birth.Checkpoint.GetAgent("alice").DomesticFamilyUnitId, child.DomesticFamilyUnitId);
        Assert.Equal("home-b", birth.Checkpoint.GetAgent("bob").HouseholdId);
        Assert.Equal("alice", record.PrimaryCaregiverId);
        Assert.Equal("home-a", record.HouseholdId);
        var restored = SocietyCheckpointCodec.Decode(SocietyCheckpointCodec.Encode(birth.Checkpoint));
        Assert.Equal("home-a", restored.GetAgent(child.Id).HouseholdId);
        Assert.Equal("alice", Assert.Single(restored.Births).PrimaryCaregiverId);
        Assert.Contains(restored.Relationships, item => item.Type == SocietyRelationshipType.BiologicalParentage &&
            item.ProposerId == "bob" && item.TargetId == child.Id);
    }

    [Fact]
    public void ReplacementPrimaryRequiresExplicitCareAndFollowsTheirCurrentDomesticUnit()
    {
        var checkpoint = Genesis(TestConfig(), "alice", "bob", "cara");
        checkpoint = SocietyRules.CreateHousehold(checkpoint, "home-a", "Home A", ["alice", "bob"]).Checkpoint;
        checkpoint = SocietyRules.CreateHousehold(checkpoint, "home-b", "Home B", ["cara"]).Checkpoint;
        checkpoint = SocietyRules.ProposeRelationship(checkpoint,
            new("first-partnership", 1, SocietyRelationshipType.Partnership, "alice", "bob", checkpoint.WorldTick)).Checkpoint;
        checkpoint = SocietyRules.AcceptRelationship(checkpoint, "first-partnership", 1, "bob").Checkpoint;
        var birthUnit = checkpoint.GetAgent("alice").DomesticFamilyUnitId;

        var birth = SocietyRules.CommitBirth(checkpoint, new SocietyBirthRequest(
            "caregiver-reassignment", 1, "alice", "bob", "home-a", ["alice", "bob"], ["alice", "bob"],
            "food-lot", 2, checkpoint.WorldTick, PrimaryCaregiverId: "alice"));
        checkpoint = birth.Checkpoint;
        var childId = Assert.IsType<string>(birth.CreatedId);
        Assert.Equal("alice", checkpoint.GetAgent(childId).PrimaryCaregiverId);
        Assert.Equal(birthUnit, checkpoint.GetAgent(childId).DomesticFamilyUnitId);
        Assert.Single(checkpoint.Relationships, item => item.Type == SocietyRelationshipType.Caregiver &&
            item.ProposerId == "bob" && item.TargetId == childId && item.State == SocietyRelationshipState.Accepted);

        checkpoint = SocietyRules.Kill(checkpoint, "alice", SocietyDeathCause.NaturalAge).Checkpoint;
        checkpoint = SocietyRules.ProposeRelationship(checkpoint,
            new("survivor-partnership", 1, SocietyRelationshipType.Partnership, "bob", "cara", checkpoint.WorldTick)).Checkpoint;
        checkpoint = SocietyRules.AcceptRelationship(checkpoint, "survivor-partnership", 1, "cara").Checkpoint;
        var survivorUnit = checkpoint.GetAgent("bob").DomesticFamilyUnitId;
        Assert.NotEqual(birthUnit, survivorUnit);
        Assert.Null(checkpoint.GetAgent(childId).PrimaryCaregiverId);
        Assert.Equal(birthUnit, checkpoint.GetAgent(childId).DomesticFamilyUnitId);

        var assumption = SocietyRules.AssumePrimaryCare(checkpoint, "bob", childId);
        Assert.Contains(assumption.NewEvents ?? [], item => item.Kind == "primary_caregiver_assumed");
        checkpoint = assumption.Checkpoint;
        Assert.Equal("bob", checkpoint.GetAgent(childId).PrimaryCaregiverId);
        Assert.Equal(survivorUnit, checkpoint.GetAgent(childId).DomesticFamilyUnitId);
        Assert.Equal("alice", Assert.Single(checkpoint.Births).PrimaryCaregiverId);
        Assert.Single(checkpoint.Relationships, item => item.Type == SocietyRelationshipType.Caregiver &&
            item.ProposerId == "bob" && item.TargetId == childId && item.State == SocietyRelationshipState.Accepted);

        var restored = SocietyCheckpointCodec.Decode(SocietyCheckpointCodec.Encode(checkpoint));
        Assert.Equal("bob", restored.GetAgent(childId).PrimaryCaregiverId);
        Assert.Equal(survivorUnit, restored.GetAgent(childId).DomesticFamilyUnitId);
        Assert.Equal("alice", Assert.Single(restored.Births).PrimaryCaregiverId);
    }

    [Fact]
    public void BirthConsumesReservedFoodCreatesOneIdentityAndIsIdempotent()
    {
        var config = TestConfig();
        var checkpoint = Genesis(config, "alice", "bob");
        checkpoint = SocietyRules.CreateHousehold(checkpoint, "home", "The Home", ["alice", "bob"]).Checkpoint;
        checkpoint = AcceptPartnership(checkpoint, "alice", "bob");
        var request = new SocietyBirthRequest(
            "birth-1",
            1,
            "alice",
            "bob",
            "home",
            ["alice", "bob"],
            ["alice", "bob"],
            "food-lot",
            2,
            checkpoint.WorldTick,
            PrimaryCaregiverId: "alice");

        var committed = SocietyRules.CommitBirth(checkpoint, request);
        var retried = SocietyRules.CommitBirth(committed.Checkpoint, request);

        Assert.Equal("world:agent:birth-1", committed.CreatedId);
        Assert.Single(committed.Checkpoint.Births);
        Assert.Equal(1, committed.Checkpoint.Inventory.GetLot("food-lot").Quantity);
        Assert.Equal(committed.Checkpoint, retried.Checkpoint);
        Assert.Equal(1, retried.Checkpoint.Agents.Count(item => item.Id == committed.CreatedId));
        Assert.Contains(
            committed.Checkpoint.Relationships,
            relationship => relationship.Type == SocietyRelationshipType.BiologicalParentage &&
                relationship.TargetId == committed.CreatedId);
    }

    [Theory]
    [InlineData("propose")]
    [InlineData("revoke")]
    public void BiologicalParentageCannotBeEditedThroughOrdinaryRelationshipCommands(string operation)
    {
        var checkpoint = Genesis(TestConfig(), "alice", "bob");
        checkpoint = SocietyRules.CreateHousehold(checkpoint, "home", "Home", ["alice", "bob"]).Checkpoint;
        checkpoint = AcceptPartnership(checkpoint, "alice", "bob");
        var birth = SocietyRules.CommitBirth(checkpoint, new("history-child", 1, "alice", "bob", "home",
            ["alice", "bob"], ["alice", "bob"], "food-lot", 2, checkpoint.WorldTick,
            PrimaryCaregiverId: "alice"));
        checkpoint = birth.Checkpoint;
        var edge = checkpoint.Relationships.First(item => item.Type == SocietyRelationshipType.BiologicalParentage);
        if (operation == "propose")
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SocietyRules.ProposeRelationship(checkpoint,
                new("fake-parentage", 1, SocietyRelationshipType.BiologicalParentage, "bob", "alice", checkpoint.WorldTick)));
            return;
        }
        if (operation == "accept")
        {
            checkpoint = checkpoint with
            {
                Relationships = checkpoint.Relationships.Select(item => item.Id == edge.Id
                ? item with { State = SocietyRelationshipState.Proposed, Consent = SocietyConsentState.Pending } : item).ToArray()
            };
        }
        var result = operation switch
        {
            "accept" => SocietyRules.AcceptRelationship(checkpoint, edge.Id, edge.Revision, edge.TargetId),
            _ => SocietyRules.RevokeRelationship(checkpoint, edge.Id, edge.ProposerId),
        };
        Assert.Equal(checkpoint.Relationships, result.Checkpoint.Relationships);
        Assert.Equal(checkpoint.Births, result.Checkpoint.Births);
        Assert.Contains(result.NewEvents!, item => item.Kind == "relationship_rejected");
    }

    [Fact]
    public void NaturalMortalityIsGradualPolicyAndEstateSettlementIsDeterministic()
    {
        var config = TestConfig() with
        {
            BaseNaturalMortalityBasisPoints = 10_000,
            NaturalMortalitySlopeBasisPoints = 0,
            EstateEscrowDays = 2,
        };
        var naturalCheckpoint = Genesis(config, "elder");

        var elderTick = checked(65L * config.TicksPerWorldYear);
        var aged = SocietyRules.AdvanceTo(naturalCheckpoint, elderTick).Checkpoint;
        var elder = aged.GetAgent("elder");

        Assert.Equal(SocietyAgentStatus.Dead, elder.Status);
        Assert.Equal(SocietyAgeBand.Elder, elder.AgeBand);
        Assert.Equal(SocietyDeathCause.NaturalAge, elder.DeathCause);
        Assert.Single(aged.Estates);

        var estateCheckpoint = Genesis(config with { BaseNaturalMortalityBasisPoints = 0 }, "alice", "bob");
        estateCheckpoint = SocietyRules.CreateHousehold(
            estateCheckpoint,
            "home",
            "The Home",
            ["alice", "bob"]).Checkpoint;
        var killed = SocietyRules.Kill(estateCheckpoint, "alice", SocietyDeathCause.Hazard).Checkpoint;
        var estateId = "estate:alice:0";

        var settled = SocietyRules.AdvanceTo(killed, 2).Checkpoint;

        Assert.True(settled.GetEstate(estateId).Settled);
        Assert.Contains(
            settled.Inventory.Lots,
            lot => lot.OwnerId == "bob" && lot.ItemKind == "food");
        Assert.Contains(settled.Events, societyEvent => societyEvent.Kind == "estate_settled");
    }

    [Fact]
    public async Task MultipleAgentsHaveIndependentFairCognitionSchedules()
    {
        var config = TestConfig();
        var founders = new[]
        {
            SocietyRules.CreateFounder("alice", "Alice", config: config),
            SocietyRules.CreateFounder("bob", "Bob", config: config),
            SocietyRules.CreateFounder("cara", "Cara", config: config),
        };
        var scheduler = new SocietyCognitionScheduler(founders, maxQueueLength: 2, maxDispatchPerCycle: 2);

        Assert.True(scheduler.Enqueue(Entry("a", "alice", 1, 0)));
        Assert.True(scheduler.Enqueue(Entry("b", "bob", 0, 0)));
        Assert.True(scheduler.Enqueue(Entry("a-newer", "alice", 2, 1)));
        Assert.False(scheduler.Enqueue(Entry("c", "cara", 0, 1)));

        var decisions = await scheduler.DispatchAsync();
        var restored = SocietyCognitionScheduler.Restore(scheduler.ExportState());

        Assert.Equal(2, decisions.Count);
        Assert.All(decisions, decision => Assert.True(decision.Admission.Accepted));
        Assert.Equal("safe_idle", scheduler.CaptureRuntime("alice").CurrentIntention?.CandidateId);
        Assert.Equal("safe_idle", scheduler.CaptureRuntime("bob").CurrentIntention?.CandidateId);
        Assert.Equal(
            scheduler.CaptureRuntime("alice").CurrentIntention,
            restored.CaptureRuntime("alice").CurrentIntention);
    }

    [Fact]
    public void PreviouslyOfferedButNowIllegalDeferredChoiceRemainsQueuedWithoutFallback()
    {
        var scheduler = new SocietyCognitionScheduler(
            [SocietyRules.CreateFounder("alice", "Alice", config: TestConfig())],
            _ => new PreviewOnlyModelProvider());
        var entry = Entry("a", "alice", 1, 0);
        entry = entry with
        {
            Observation = entry.Observation with
            {
                Candidates =
            [new CognitionCandidate("safe_idle", "Wait safely.", 0), new CognitionCandidate("seek_food", "Seek food.", 1)]
            }
        };
        Assert.True(scheduler.Enqueue(entry));
        var request = Assert.Single(scheduler.PreviewModelRequests(new HashSet<string>())).Request;
        var response = new CognitionDecisionResponse(request.RequestId, "alice", DecisionProviderKind.LargeLanguageModel,
            request.ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration,
            request.Observation.ObservationDigest, "seek_food", 1,
            new Dictionary<string, double> { ["seek_food"] = 1 });
        Assert.Null(scheduler.CompleteDeferred(request, response, null, 0, new HashSet<string> { "safe_idle" }));
        Assert.Single(scheduler.ExportState().Queue);
        Assert.Null(scheduler.CaptureRuntime("alice").CurrentIntention);
    }

    private sealed class PreviewOnlyModelProvider : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException("Preview only.");
    }

    [Fact]
    public async Task SocietyRuntimeReconcilesBirthsAndDeathsWithCognition()
    {
        var config = TestConfig();
        var checkpoint = Genesis(config, "alice", "bob");
        checkpoint = SocietyRules.CreateHousehold(checkpoint, "home", "The Home", ["alice", "bob"]).Checkpoint;
        checkpoint = AcceptPartnership(checkpoint, "alice", "bob");

        using var runtime = new SocietyRuntime(checkpoint);
        Assert.True(runtime.EnqueueCognition(Entry("alice-start", "alice", 1, 0)));
        var firstDispatch = await runtime.DispatchCognitionAsync();
        Assert.Single(firstDispatch.Decisions);

        var encodedRuntime = SocietyRuntimeCodec.Encode(runtime.ExportState());
        var decodedRuntime = SocietyRuntimeCodec.Decode(encodedRuntime);
        Assert.Equal(
            SocietyRuntimeCodec.Digest(runtime.ExportState()),
            SocietyRuntimeCodec.Digest(decodedRuntime));

        var birth = new SocietyBirthRequest(
            "birth-runtime",
            1,
            "alice",
            "bob",
            "home",
            ["alice", "bob"],
            ["alice", "bob"],
            "food-lot",
            1,
            runtime.Checkpoint.WorldTick,
            PrimaryCaregiverId: "alice");
        runtime.Apply(current => SocietyRules.CommitBirth(current, birth));
        var childId = "world:agent:birth-runtime";

        Assert.Contains(childId, runtime.Capture().Cognition.Runtimes.Select(item => item.AgentId));
        Assert.False(runtime.EnqueueCognition(Entry("infant-start", childId, 0, 0)));
        Assert.DoesNotContain(runtime.Capture().Cognition.Queue, item => item.AgentId == childId);
        runtime.AdvanceTo(20); // The fixture's two-year infancy ends here.
        Assert.Equal(SocietyAgeBand.Child, runtime.Checkpoint.GetAgent(childId).AgeBand);
        Assert.True(runtime.EnqueueCognition(Entry("child-start", childId, 0, 20)));
        var queued = Assert.Single(runtime.Capture().Cognition.Queue);
        Assert.True(queued.Observation.RequiresPersonalProvider);

        // A restored queue still marks this born child as needing its own
        // selection, including saves written before the observation flag.
        var unmarked = runtime.ExportState();
        unmarked = unmarked with
        {
            Cognition = unmarked.Cognition with
            {
                Queue = unmarked.Cognition.Queue.Select(item => item with
                {
                    Observation = item.Observation with { RequiresPersonalProvider = false },
                }).ToArray(),
            }
        };
        using (var recovered = SocietyRuntime.Restore(unmarked))
        {
            Assert.True(Assert.Single(recovered.Capture().Cognition.Queue).Observation.RequiresPersonalProvider);
            await recovered.DispatchCognitionAsync();
        }

        await runtime.DispatchCognitionAsync();

        runtime.Apply(current => SocietyRules.Kill(current, "bob", SocietyDeathCause.Hazard));
        var restored = SocietyRuntime.Restore(runtime.ExportState());
        using (restored)
        {
            restored.Validate();
            Assert.DoesNotContain("bob", restored.Capture().Cognition.Runtimes.Select(item => item.AgentId));
            Assert.Contains(childId, restored.Capture().Cognition.Runtimes.Select(item => item.AgentId));
            Assert.Equal(SocietyAgentStatus.Dead, restored.Checkpoint.GetAgent("bob").Status);
        }
    }

    [Fact]
    public void DeathEndsRelationshipsCancelsExchangeAndEscrowsInventory()
    {
        var config = TestConfig() with { BaseNaturalMortalityBasisPoints = 0 };
        var founders = new[]
        {
            SocietyRules.CreateFounder("alice", "Alice", config: config),
            SocietyRules.CreateFounder("bob", "Bob", config: config),
        };
        var checkpoint = SocietyRules.CreateGenesis(
            "world",
            founders,
            [
                new InventoryLot("food-lot", "food", "alice", 3, 10_000, 10_000, 0),
                new InventoryLot("tool-lot", "tool", "bob", 1, 10_000, 10_000, 0),
            ],
            config);
        checkpoint = SocietyRules.CreateHousehold(checkpoint, "home", "The Home", ["alice", "bob"]).Checkpoint;
        checkpoint = AcceptPartnership(checkpoint, "alice", "bob");
        checkpoint = checkpoint with
        {
            Inventory = InventoryAuthority.Reserve(
                checkpoint.Inventory,
                "alice-reservation",
                "alice",
                "food-lot",
                1,
                "test",
                10),
        };
        checkpoint = SocietyRules.CreateBarterOffer(
            checkpoint,
            new DirectBarterProposal("barter-death", 1, "alice", "bob", "food-lot", 1, "tool-lot", 1, 10)).Checkpoint;

        var killed = SocietyRules.Kill(checkpoint, "alice", SocietyDeathCause.Hazard).Checkpoint;

        Assert.Equal(SocietyRelationshipState.EndedByDeath, killed.GetRelationship("partnership-1").State);
        Assert.Equal(DirectBarterState.Cancelled, killed.Inventory.GetOffer("barter-death").State);
        Assert.Equal(InventoryReservationState.Released, killed.Inventory.GetReservation("alice-reservation").State);
        Assert.Contains(killed.Inventory.Lots, lot => lot.OwnerId == "estate:alice:0");

        var settled = SocietyRules.AdvanceTo(killed, config.EstateEscrowDays).Checkpoint;
        Assert.True(settled.GetEstate("estate:alice:0").Settled);
        Assert.Contains(settled.Inventory.Lots, lot => lot.OwnerId == "bob");
    }

    [Fact]
    public void NaturalMortalityHasNoHardMaximumAndElderhoodIsNotDeath()
    {
        var config = new SocietyConfig();

        Assert.Equal(0, config.NaturalMortalityRiskBasisPoints(config.AdultYears));
        Assert.Equal(100, config.NaturalMortalityRiskBasisPoints(config.ElderYears));
        Assert.True(config.NaturalMortalityRiskBasisPoints(120) < 10_000);
        Assert.True(config.NaturalMortalityRiskBasisPoints(145) < 10_000);

        var noMortality = config with
        {
            BaseNaturalMortalityBasisPoints = 0,
            NaturalMortalitySlopeBasisPoints = 0,
        };
        var elder = SocietyRules.CreateFounder("survivor", "Survivor", config: noMortality);
        var checkpoint = SocietyRules.CreateGenesis("world", [elder], config: noMortality);
        var atElderhood = SocietyRules.AdvanceTo(
            checkpoint,
            (config.ElderYears - config.AdultYears) * config.TicksPerWorldYear).Checkpoint;

        Assert.Equal(SocietyAgentStatus.Active, atElderhood.GetAgent("survivor").Status);
        Assert.Equal(SocietyAgeBand.Elder, atElderhood.GetAgent("survivor").AgeBand);
    }

    private static SocietyCheckpoint Genesis(SocietyConfig config, params string[] ids)
    {
        var founders = ids.Select(id => SocietyRules.CreateFounder(id, id, config: config)).ToArray();
        var lots = new[]
        {
            new InventoryLot("food-lot", "food", ids[0], 3, 10_000, 10_000, 0),
        };
        return SocietyRules.CreateGenesis("world", founders, lots, config);
    }

    private static (SocietyCheckpoint Checkpoint, string ChildId) BirthWithAcceptedCare()
    {
        var checkpoint = Genesis(TestConfig(), "alice", "bob", "cara");
        checkpoint = SocietyRules.CreateHousehold(checkpoint, "home-a", "Home A", ["alice", "bob"]).Checkpoint;
        checkpoint = SocietyRules.CreateHousehold(checkpoint, "home-c", "Home C", ["cara"]).Checkpoint;
        checkpoint = AcceptPartnership(checkpoint, "alice", "bob");
        var birth = SocietyRules.CommitBirth(checkpoint, new SocietyBirthRequest(
            "caregiver-revocation-child", 1, "alice", "bob", "home-a", ["alice", "bob"], ["alice", "bob"],
            "food-lot", 2, checkpoint.WorldTick, PrimaryCaregiverId: "alice"));
        return (birth.Checkpoint, Assert.IsType<string>(birth.CreatedId));
    }

    private static SocietyCheckpoint AcceptPartnership(
        SocietyCheckpoint checkpoint,
        string firstId,
        string secondId)
    {
        checkpoint = SocietyRules.ProposeRelationship(
            checkpoint,
            new SocietyRelationshipProposal(
                "partnership-1",
                1,
                SocietyRelationshipType.Partnership,
                firstId,
                secondId,
                checkpoint.WorldTick)).Checkpoint;
        return SocietyRules.AcceptRelationship(checkpoint, "partnership-1", 1, secondId).Checkpoint;
    }

    private static SocietyConfig TestConfig() =>
        new(TicksPerWorldDay: 1, DaysPerWorldYear: 10);

    private static SocietyCognitionScheduleEntry Entry(
        string scheduleId,
        string agentId,
        int priority,
        long tick) =>
        new(
            scheduleId,
            agentId,
            priority,
            tick,
            [$"trigger:{scheduleId}"],
            new AgentObservation(
                agentId,
                tick,
                0,
                0,
                $"digest:{scheduleId}",
                5_000,
            [new CognitionCandidate("safe_idle", "Continue safely.", 0)]));
}

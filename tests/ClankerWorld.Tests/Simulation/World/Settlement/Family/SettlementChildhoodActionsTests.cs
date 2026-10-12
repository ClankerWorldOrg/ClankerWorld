using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed class SettlementChildhoodActionsTests
{
    private const string Child = "founder-scout";

    [Fact]
    public async Task ChildHouseholdHelpDoesNotTransferFoodOutOfACarriedPersonalPot()
    {
        var state = await ChildState(withCarriedFood: true);
        var inventory = state.Society.Society.Inventory;
        var food = Assert.Single(inventory.Lots, lot => lot.OwnerId == Child && lot.ItemKind == "food");
        const string potId = "child-personal-pot";
        inventory = InventoryAuthority.AddLot(inventory, potId, InventoryContainerRules.StoragePot, Child, 1);
        inventory = InventoryAuthority.PutIntoContainer(inventory, "child-food-pot-setup", Child, potId, food.Id, 2);
        state = state with { Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } } };
        var choices = new SelectProvider("child_help_food");
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)),
            id => id == Child ? choices : new SelectProvider("safe_idle"));
        for (var tick = 0; tick < 4; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.True(choices.Calls > 0);
        Assert.DoesNotContain("child_help_food", choices.SeenCandidates);
        Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == "child_helped_household" && item.Detail == Child);
        var remaining = world.Society.Inventory.GetLot(food.Id);
        Assert.Equal((Child, 2, potId), (remaining.OwnerId, remaining.Quantity, remaining.ContainerLotId));
        Assert.Equal((Child, 1), (world.Society.Inventory.GetLot(potId).OwnerId, world.Society.Inventory.GetLot(potId).Quantity));
        world.Validate();
    }

    [Fact]
    public async Task IllnessDoesNotSuppressOrdinaryChildConversationOrChangePersonality()
    {
        var state = await ChildState();
        var original = state.Inhabitants.Single(person => person.InhabitantId == Child);
        state = state with
        {
            Inhabitants = state.Inhabitants.Select(person => person.InhabitantId == Child ? person with
            {
                Survival = new(10_000, 9_000),
                LastDecisionContext = null,
            } : person).ToArray(),
        };
        foreach (var kind in new[] { "converse", "play", "learn" })
        {
            var choice = "child_" + kind + ":";
            var eventKind = "child_" + kind;
            var childProvider = new SelectProvider(choice);
            using var world = WorldRuntime.Restore(state,
                id => id == Child ? childProvider : new SelectProvider("safe_idle"));
            for (var tick = 0; tick < 45 && !world.ExportState().Events.Any(item => item.Kind == eventKind); tick++)
                await world.AdvanceOneTickAsync();

            Assert.Contains(childProvider.SeenCandidates, id => id.StartsWith(choice, StringComparison.Ordinal));
            Assert.Contains(world.ExportState().Events, item => item.Kind == eventKind);
            Assert.Equal(original.Personality, world.Inhabitants.Single(person => person.InhabitantId == Child).Personality);
            var memory = Assert.Single(world.Society.Memories, item =>
                item.OwnerId == Child && item.Id.StartsWith("child-social:" + kind + ":", StringComparison.Ordinal));
            Assert.Equal($"child-social:{kind}:{Child}:{memory.SubjectId}:{memory.SourceTick}", memory.Id);
            Assert.True(world.Inhabitants.Single(item => item.InhabitantId == Child).SocialStanding?.Any() == true);
            var saved = WorldRuntimeCodec.Encode(world.ExportState());
            using var reloaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), _ => new SelectProvider("safe_idle"));
            Assert.Equal(saved, WorldRuntimeCodec.Encode(reloaded.ExportState()));
            Assert.Contains(reloaded.Society.Memories, item => item.Id == memory.Id);
        }
    }

    [Theory]
    [InlineData("build:building:forged-house")]
    public async Task MalformedChildModelChoiceCannotPerformAdultAction(string forged)
    {
        // The child carries food, so only the fallback stops this order from completing.
        var state = await ChildState(withCarriedFood: true);
        var provider = new SelectProvider(forged, forge: true);
        using var world = WorldRuntime.Restore(state, id => id == Child ? provider : new SelectProvider("safe_idle"));
        var instruction = world.SubmitInstruction(new("child-order", "owner", Child, OwnerInstructionKind.MustDo,
            "eat food"));
        var admissions = new List<SocietyCognitionDispatchResult>();
        for (var tick = 0; tick < 4; tick++)
            admissions.AddRange((await world.AdvanceOneTickAsync()).Decisions.Where(item => item.InhabitantId == Child));

        Assert.True(provider.Calls > 0);
        Assert.DoesNotContain(forged, provider.SeenCandidates);
        Assert.NotEmpty(admissions);
        Assert.All(admissions, item =>
        {
            Assert.True(item.Admission.Accepted);
            Assert.True(item.Admission.FellBack);
            Assert.Equal("candidate_not_legal", item.Admission.Outcome);
            Assert.Equal("safe_idle", item.Admission.Intention?.CandidateId);
        });
        Assert.Null(world.Inhabitants.Single(item => item.InhabitantId == Child).Project);
        Assert.Empty(world.Society.Births);
        Assert.Empty(world.Society.Inventory.Offers);
        Assert.DoesNotContain(world.Society.Relationships, item => item.Type == SocietyRelationshipType.Partnership);
        Assert.Equal(SocietyWorkRole.Unassigned, world.Society.GetInhabitant(Child).CurrentRole);
        Assert.DoesNotContain(instruction.InstructionId, world.ExportState().CompletedInstructionIds ?? []);

        var helpProvider = new SelectProvider("child_help_food");
        using var helping = WorldRuntime.Restore(state, id => id == Child ? helpProvider : new SelectProvider("safe_idle"));
        var householdBefore = helping.Society.Inventory.Lots.Where(item => item.OwnerId == "household:camp-alpha" && item.ItemKind == "food").Sum(item => item.Quantity);
        for (var tick = 0; tick < 45 && !helping.ExportState().Events.Any(item => item.Kind == "child_helped_household"); tick++)
            await helping.AdvanceOneTickAsync();
        Assert.Contains(helpProvider.SeenCandidates, id => id == "child_help_food");
        Assert.Contains(helping.ExportState().Events, item => item.Kind == "child_helped_household");
        Assert.Equal(householdBefore + 1, helping.Society.Inventory.Lots.Where(item => item.OwnerId == "household:camp-alpha" && item.ItemKind == "food").Sum(item => item.Quantity));
        Assert.Null(helping.Inhabitants.Single(item => item.InhabitantId == Child).Project);
        var saved = WorldRuntimeCodec.Encode(helping.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved));
        Assert.Equal(saved, WorldRuntimeCodec.Encode(restored.ExportState()));
    }

    private static async Task<WorldRuntimeState> ChildState(bool withCarriedFood = false)
    {
        using var world = new WorldRuntime("child-actions-fixture", _ => new SelectProvider("safe_idle"));
        world.StageStarterContent();
        for (var tick = 0; tick < 5; tick++) await world.AdvanceOneTickAsync();
        var state = world.ExportState();
        var society = state.Society.Society;
        var birth = society.LifeTickAt(society.WorldTick) - 4 * society.Config.TicksPerLifecycleAge;
        var inventory = withCarriedFood
            ? InventoryAuthority.Transfer(society.Inventory, "child-food-setup", "household:camp-alpha", Child,
                "food:camp-alpha", 2, "test_setup")
            : society.Inventory;
        return state with
        {
            Society = state.Society with
            {
                Society = society with
                {
                    Inhabitants = society.Inhabitants.Select(person => person.Id == Child ? person with
                    {
                        BirthTick = birth,
                        BirthLifeTick = society.LifeClock is null ? null : birth,
                        AgeBand = SocietyAgeBand.Child,
                        LastLifecycleYearChecked = 4,
                        CurrentRole = SocietyWorkRole.Unassigned,
                    } : person).ToArray(),
                    Inventory = inventory,
                }
            },
            Inhabitants = state.Inhabitants.Select(person => person.InhabitantId == Child
                ? person with { LastDecisionContext = null, HungerBasisPoints = 9_000 } : person).ToArray(),
        };
    }

    private sealed class SelectProvider(string wanted, bool forge = false) : IDecisionProvider
    {
        public int Calls { get; private set; }
        public HashSet<string> SeenCandidates { get; } = new(StringComparer.Ordinal);
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;

        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            foreach (var candidate in request.Observation.Candidates) SeenCandidates.Add(candidate.Id);
            var chosen = forge ? wanted : request.Observation.Candidates.FirstOrDefault(candidate =>
                candidate.Id.StartsWith(wanted, StringComparison.Ordinal))?.Id ?? "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId,
                request.Observation.InhabitantId, Kind, ProviderEpoch, request.Observation.RunEpoch,
                request.Observation.DecisionGeneration, request.Observation.ObservationDigest, chosen, 1,
                request.Observation.Candidates.ToDictionary(candidate => candidate.Id,
                    candidate => candidate.Id == chosen ? 1d : 0d, StringComparer.Ordinal)));
        }
    }
}

using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed class NativeForagedDietTests
{
    [Fact]
    public async Task NativeDescendantForageKeepsItsDietCategoryAfterHarvestAndReload()
    {
        using var seed = new WorldRuntime(new string('s', 50), _ => new IdleProvider());
        var state = seed.ExportState();
        var checkpoint = state.Society.Society;
        var actor = "founder-scout";
        // Actual accepted births retain their complete ancestry references.
        // Accelerate ages only, so the check starts with a working adult.
        for (var generation = 0; generation < 2; generation++)
        {
            var partner = generation == 0 ? "founder-mira" : "founder-rowan";
            checkpoint = ChosenBirthNameTestFixture.NameParent(checkpoint, actor);
            var partnership = "forage-parenthood-" + generation;
            checkpoint = SocietyRules.ProposeRelationship(checkpoint, new(partnership, 1,
                SocietyRelationshipType.Partnership, actor, partner, checkpoint.WorldTick)).Checkpoint;
            checkpoint = SocietyRules.AcceptRelationship(checkpoint, partnership, 1, partner).Checkpoint;
            var household = checkpoint.GetInhabitant(actor).HouseholdId!;
            var birth = SocietyRules.CommitBirth(checkpoint, new($"family:{actor}:{checkpoint.WorldTick}", 1,
                actor, partner, household, [actor, partner], [actor, partner], "food:camp-alpha", 2,
                checkpoint.WorldTick, ChildName: ChosenBirthNameTestFixture.ChildName(checkpoint, actor, "Forager" + generation),
                PrimaryCaregiverId: actor));
            actor = Assert.IsType<string>(birth.CreatedId);
            checkpoint = birth.Checkpoint;
            var adultBirth = checkpoint.LifeTickAt(checkpoint.WorldTick) - 20 * checkpoint.Config.TicksPerLifecycleAge;
            checkpoint = checkpoint with
            {
                Inhabitants = checkpoint.Inhabitants.Select(person => person.Id == actor ? person with
                {
                    BirthTick = adultBirth,
                    BirthLifeTick = checkpoint.LifeClock is null ? null : adultBirth,
                    AgeBand = SocietyAgeBand.Adult,
                    LastLifecycleYearChecked = 20,
                } : person).ToArray(),
            };
            var position = state.Map.Tiles.Select(tile => tile.Position).First(point =>
                state.Map.IsBuildable(point) && state.Inhabitants.All(person => person.Position != point) &&
                state.Map.Resources.All(resource => resource.Position != point) &&
                state.Map.MapObjects.All(item => item.Position != point));
            state = state with
            {
                Inhabitants = state.Inhabitants.Append(new InhabitantState(actor, position,
                    3_000, 0, "curious", "find food")).ToArray(),
            };
        }
        Assert.Equal(2, checkpoint.Births.Count);
        Assert.InRange(actor.Length, 105, DerivedId.MaximumLength);
        var source = state.Map.Resources.First(resource => resource.Kind == "food" &&
            resource.NaturalObjectKind != "wild_greens");
        var stand = state.Map.FootNeighbors(source.Position).First(point => state.Map.IsBuildable(point) &&
            state.Inhabitants.All(person => person.Position != point) &&
            state.Map.Resources.All(resource => resource.Position != point) &&
            state.Map.MapObjects.All(item => item.Position != point));
        state = state with
        {
            RoutineHelper = RoutineHelperSettings.Off,
            JevEnabled = false,
            Survival = new SettlementSurvivalState(checkpoint.WorldTick, []),
            Society = state.Society with { Society = checkpoint },
            Inhabitants = state.Inhabitants.Select(person => person.InhabitantId == actor ? person with
            {
                Position = stand,
                Survival = new SurvivalCondition(NutritionBasisPoints: 5_000, LastMealKind: "camp_rations"),
            } : person).ToArray(),
            Knowledge = state.Knowledge! with
            {
                Facts = state.Knowledge.Facts.Append(new AgentKnowledgeFact("forage-known-site", actor, actor,
                    source.Position, state.Map.Tiles.Single(tile => tile.Position == source.Position).Terrain.ToString(),
                    ["berries"], checkpoint.WorldTick, "firsthand")).ToArray(),
            },
        };
        using var world = WorldRuntime.Restore(state, _ => new IdleProvider(actor));
        var harvest = world.SubmitInstruction(new("native-forage", "owner:test", actor,
            OwnerInstructionKind.MustDo, "gather berries from " + source.Id));
        for (var tick = 0; tick < 20 && !world.Society.Inventory.Lots.Any(lot => lot.OwnerId == actor && lot.ItemKind == "berries"); tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var food = Assert.Single(world.Society.Inventory.Lots, lot => lot.OwnerId == actor && lot.ItemKind == "berries");
        Assert.Contains(harvest.InstructionId, world.ExportState().CompletedInstructionIds!);
        Assert.InRange(food.Id.Length, 1, DerivedId.MaximumLength);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new IdleProvider(actor));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
        var eat = restored.SubmitInstruction(new("eat-native-forage", "owner:test", actor,
            OwnerInstructionKind.MustDo, "eat 1 berries"));
        for (var tick = 0; tick < 20 && !restored.ExportState().CompletedInstructionIds!.Contains(eat.InstructionId); tick++)
            Assert.True((await restored.AdvanceOneTickAsync()).Advanced);
        Assert.Contains(eat.InstructionId, restored.ExportState().CompletedInstructionIds!);
        var condition = restored.Inhabitants.Single(person => person.InhabitantId == actor).Survival!;
        Assert.Equal("foraged", condition.LastMealKind);
        Assert.Equal(5_500, condition.NutritionBasisPoints);
        Assert.Equal(food.Quantity - 1, restored.Society.Inventory.GetLot(food.Id).Quantity);
        var eaten = WorldRuntimeCodec.Encode(restored.ExportState());
        using var final = WorldRuntime.Restore(WorldRuntimeCodec.Decode(eaten), _ => new IdleProvider());
        Assert.Equal(eaten, WorldRuntimeCodec.Encode(final.ExportState()));
        Assert.Equal("foraged", final.Inhabitants.Single(person => person.InhabitantId == actor).Survival!.LastMealKind);
    }

    private sealed class IdleProvider(string? actor = null) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = request.Observation.InhabitantId == actor
                ? request.Observation.Candidates.FirstOrDefault(candidate => candidate.Id is "consume_food" or "harvest_food")
                : null;
            candidate ??= request.Observation.Candidates.Single(candidate => candidate.Id == "safe_idle");
            return new DeterministicDecisionProvider().DecideAsync(request with
            {
                Observation = request.Observation with { Candidates = [candidate] },
            }, cancellationToken);
        }
    }
}

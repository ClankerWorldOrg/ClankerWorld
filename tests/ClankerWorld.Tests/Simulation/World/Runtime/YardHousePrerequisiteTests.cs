using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class YardHousePrerequisiteTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnOrderedAnimalYardRequiresARealCompletedHouse(bool houseFirst)
    {
        var choices = new YardChoices();
        using var forming = NormalPathWorld.CreateGenerated("yard-house-prerequisite-audit", _ => choices);
        var actor = forming.Society.GetHousehold("household:camp-alpha").MemberIds[0];
        choices.Actor = actor;
        choices.Leave = true;
        await Until(forming, () => forming.Society.GetAgent(actor).HouseholdId is null);
        choices.Leave = false;
        choices.Form = true;
        await Until(forming, () => forming.Society.GetAgent(actor).HouseholdId is not null);
        choices.Form = false;
        var home = forming.Society.GetAgent(actor).HouseholdId!;
        Assert.NotEqual("household:camp-alpha", home);
        Assert.DoesNotContain(forming.WorldSimulation.Buildings, building => building.HouseholdId == home);
        Assert.Contains(forming.ExportState().Events, item => item.Kind == "household_founded" && item.Detail.StartsWith(actor + "|", StringComparison.Ordinal));
        var state = forming.ExportState();
        var actorPosition = state.Agents.Single(person => person.AgentId == actor).Position;
        var titled = state.TownLandTitles!.SelectMany(title => title.Tiles).ToHashSet();
        var occupied = state.Agents.Where(person => person.AgentId != actor).Select(person => person.Position).ToHashSet();
        using var routes = new UnoccupiedRouteSearch(state.Map, actorPosition, occupied, state.Map.FootStepCost);
        var startingSitePosition = state.Map.Tiles.Select(tile => tile.Position).Where(point => state.Map.IsBuildable(point) && !titled.Contains(point) &&
            state.Map.FootNeighbors(point).Count(neighbor => state.Map.IsBuildable(neighbor) && !titled.Contains(neighbor)) >= 4)
            .OrderBy(point => state.Map.FootDistance(actorPosition, point)).ThenBy(point => point.Y).ThenBy(point => point.X)
            .First(point => routes.RouteTo(point, 0).Count > 0);
        var inventory = state.Society.Society.Inventory;
        inventory = inventory with { Lots = inventory.Lots.Where(lot => lot.OwnerId != actor).ToArray() };
        inventory = InventoryAuthority.AddLot(inventory, "yard-prerequisite-wood", "wood", home, 16, groundPosition: new(startingSitePosition.X, startingSitePosition.Y));
        inventory = InventoryAuthority.AddLot(inventory, "yard-prerequisite-rope", "rope", home, 2, groundPosition: new(startingSitePosition.X, startingSitePosition.Y));
        inventory = InventoryAuthority.AddLot(inventory, "yard-prerequisite-axe", "wooden_axe", actor, 1);
        state = state with
        {
            Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } },
            Agents = state.Agents.Select(person => person with
            {
                Position = person.AgentId == actor ? startingSitePosition : person.Position,
                FullnessBasisPoints = 9500,
                Survival = new SurvivalCondition(),
                Project = null,
                LastDecisionContext = null,
                Equipment = person.AgentId == actor ? null : person.Equipment,
            }).ToArray(),
        };
        var bytes = WorldRuntimeCodec.Encode(state);
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => choices);
        if (houseFirst)
        {
            var house = world.SubmitInstruction(new("yard-prerequisite-house", "owner", actor, OwnerInstructionKind.Order, "build a House"));
            await Until(world, () => world.ExportState().Instructions!.Single(item => item.InstructionId == house.InstructionId).Task!.Status == "finished");
            Assert.Single(world.WorldSimulation.Buildings, building => building.HouseholdId == home &&
                world.WorldContent.Buildings.Single(definition => definition.CanonicalId == building.DefinitionId).Tags.Contains("house"));
        }
        var yard = world.SubmitInstruction(new("yard-prerequisite-yard", "owner", actor, OwnerInstructionKind.Order, "build an animal yard"));
        bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => choices);
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        for (var tick = 0; tick < (houseFirst ? 100 : 4); tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
            if (Order().Status == "finished") break;
        }
        if (houseFirst)
        {
            Assert.Equal(("finished", 1), (Order().Status, Order().CompletedUnits));
            Assert.Single(world.WorldSimulation.Buildings, building => building.HouseholdId == home &&
                world.WorldContent.Buildings.Single(definition => definition.CanonicalId == building.DefinitionId).Tags.Contains("animal-yard"));
            Assert.DoesNotContain(world.Society.Inventory.Lots, lot => lot.Id is "yard-prerequisite-wood" or "yard-prerequisite-rope");
        }
        else
        {
            Assert.Equal(("blocked", 0), (Order().Status, Order().CompletedUnits));
            Assert.Contains("House", Order().BlockedReason!, StringComparison.Ordinal);
            Assert.DoesNotContain(world.WorldSimulation.Buildings, building => building.HouseholdId == home);
            Assert.Equal(16, world.Society.Inventory.GetLot("yard-prerequisite-wood").Quantity);
            Assert.Equal(2, world.Society.Inventory.GetLot("yard-prerequisite-rope").Quantity);
            Assert.DoesNotContain(world.Society.Inventory.Reservations, reservation => reservation.LotId.StartsWith("yard-prerequisite-", StringComparison.Ordinal));
        }
        bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var reload = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => choices);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(reload.ExportState()));
        world.Validate();
        reload.Validate();
        OrderTask Order() => world.ExportState().Instructions!.Single(item => item.InstructionId == yard.InstructionId).Task!;
    }

    private static async Task Until(WorldRuntime world, Func<bool> ready)
    {
        for (var tick = 0; tick < 100 && !ready(); tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.True(ready());
    }

    private sealed class YardChoices : IDecisionProvider
    {
        public string? Actor { get; set; }
        public bool Leave { get; set; }
        public bool Form { get; set; }
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidates = request.Observation.Candidates;
            CognitionCandidate? selected = request.Observation.AgentId == Actor
                ? Leave ? candidates.FirstOrDefault(candidate => candidate.Id == "household_leave")
                    : Form ? candidates.FirstOrDefault(candidate => candidate.Id == "household_found") ?? candidates.FirstOrDefault(candidate => candidate.Id.StartsWith("household_ask:", StringComparison.Ordinal))
                        : candidates.FirstOrDefault(candidate => candidate.Id == "construct_building")
                : candidates.FirstOrDefault(candidate => candidate.Id.StartsWith("household_refuse:", StringComparison.Ordinal));
            selected ??= candidates.Single(candidate => candidate.Id == "safe_idle");
            return new DeterministicDecisionProvider().DecideAsync(request with { Observation = request.Observation with { Candidates = [selected] } }, cancellationToken);
        }
    }
}

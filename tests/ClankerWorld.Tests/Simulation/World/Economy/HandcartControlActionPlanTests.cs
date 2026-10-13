using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class HandcartRuntimeTests
{
    [Theory]
    [InlineData(HandcartControlKind.Attach)]
    [InlineData(HandcartControlKind.Park)]
    [InlineData(HandcartControlKind.Give)]
    public async Task CartControlKeepsItsPlanAcrossRollbackReplayAndStrictReload(HandcartControlKind kind)
    {
        var (state, actor, recipient) = await ControlFixture(kind);
        var chooser = new ControlChooser(actor, kind, true);
        using var world = WorldRuntime.Restore(state, _ => chooser);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new ControlChooser(actor, kind, true));
        var step = await world.AdvanceOneTickAsync();
        Assert.True(step.Advanced);
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        var plan = Assert.IsType<HandcartControlActionPlan>(chooser.Plan);
        Assert.Same(plan, step.Decisions.Single(decision => decision.AgentId == actor).ActionPlan);
        Assert.Equal("control-cart", plan.CartId);
        Assert.Equal(2, plan.Family.Count);
        Assert.Equal(kind == HandcartControlKind.Attach ? 1 : 0, world.ExportState().HandcartHitches!.Count);
        var owner = kind == HandcartControlKind.Give ? recipient : actor;
        Assert.Equal(owner, world.Society.Inventory.GetLot(plan.CartId).OwnerId);
        var cargo = world.Society.Inventory.GetLot("control-cargo");
        Assert.Equal((owner, plan.CartId, 4), (cargo.OwnerId, cargo.ContainerLotId, cargo.Quantity));
        Assert.Single(world.ExportState().Events, item => item.Kind == (kind switch
        { HandcartControlKind.Attach => "handcart_attached", HandcartControlKind.Park => "handcart_parked", _ => "handcart_transferred" }));
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        reopened.Validate();
    }

    [Theory]
    [InlineData(HandcartControlKind.Attach)]
    [InlineData(HandcartControlKind.Give)]
    public async Task NewCartDoesNotReplaceThePlanAndChangedCargoRefusesIt(HandcartControlKind kind)
    {
        var (state, actor, _) = await ControlFixture(kind);
        var chooser = new ControlChooser(actor, kind, false);
        using var world = WorldRuntime.Restore(state, _ => chooser);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<HandcartControlActionPlan>(chooser.Plan);
        var checkpoint = world.ExportState();
        var inventory = InventoryAuthority.AddLot(checkpoint.Society.Society.Inventory, "0-earlier-cart", "handcart", actor, 1,
            groundPosition: new(plan.Position.X, plan.Position.Y));
        WorldRuntimeState WithInventory(InventoryCheckpoint value) => checkpoint with
        { Society = checkpoint.Society with { Society = checkpoint.Society.Society with { Inventory = value } } };
        using var added = WorldRuntime.Restore(WithInventory(inventory));
        Assert.True(added.CheckActionPlan(plan).Allowed);
        inventory = InventoryAuthority.AddLot(inventory, "control-new-cargo", "wood", actor, 1, containerLotId: plan.CartId);
        using var changed = WorldRuntime.Restore(WithInventory(inventory));
        var bytes = WorldRuntimeCodec.Encode(changed.ExportState());
        Assert.Equal(ActionRefusalReason.StockUnavailable, changed.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(changed.ExportState()));
    }

    [Fact]
    public async Task GivingRefusesAChangedRecipientAndAClaimOnTheCapturedCargo()
    {
        var (state, actor, recipient) = await ControlFixture(HandcartControlKind.Give);
        var chooser = new ControlChooser(actor, HandcartControlKind.Give, false);
        using var world = WorldRuntime.Restore(state, _ => chooser);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<HandcartControlActionPlan>(chooser.Plan);
        var checkpoint = world.ExportState();
        var recipientBefore = checkpoint.Agents.Single(person => person.AgentId == recipient);
        var otherPosition = state.Agents.First(person => person.Position != recipientBefore.Position).Position;
        using var moved = WorldRuntime.Restore(checkpoint with
        { Agents = checkpoint.Agents.Select(person => person.AgentId == recipient ? person with { Position = otherPosition } : person).ToArray() });
        Assert.Equal(ActionRefusalReason.TargetUnavailable, moved.CheckActionPlan(plan).Refusal!.Reason);
        var inventory = InventoryAuthority.Reserve(checkpoint.Society.Society.Inventory, "give-cargo-other-work", actor,
            "control-cargo", 4, "other_work", long.MaxValue);
        using var claimed = WorldRuntime.Restore(checkpoint with
        { Society = checkpoint.Society with { Society = checkpoint.Society.Society with { Inventory = inventory } } });
        Assert.Equal(ActionRefusalReason.StockUnavailable, claimed.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(actor, claimed.Society.Inventory.GetLot(plan.CartId).OwnerId);
    }

    [Fact]
    public async Task ParkingPromisedCargoOnlyDetachesAndBrokenCartCanStillBeGiven()
    {
        var (state, actor, _) = await ControlFixture(HandcartControlKind.Park);
        var inventory = InventoryAuthority.Reserve(state.Society.Society.Inventory, "control-cargo-other-work", actor,
            "control-cargo", 4, "other_work", long.MaxValue);
        inventory = InventoryAuthority.Reserve(inventory, "control-cart-other-work", actor,
            "control-cart", 1, "other_work", long.MaxValue);
        state = state with { Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } } };
        using var parked = WorldRuntime.Restore(state, _ => new ControlChooser(actor, HandcartControlKind.Park, true));
        Assert.True((await parked.AdvanceOneTickAsync()).Advanced);
        Assert.Empty(parked.ExportState().HandcartHitches!);
        Assert.Equal(4, parked.Society.Inventory.GetReservation("control-cargo-other-work").Quantity);
        Assert.Equal("control-cart", parked.Society.Inventory.GetLot("control-cargo").ContainerLotId);
        parked.Validate();
        var (brokenState, giver, recipient) = await ControlFixture(HandcartControlKind.Give);
        inventory = brokenState.Society.Society.Inventory with
        { Lots = brokenState.Society.Society.Inventory.Lots.Select(lot => lot.Id == "control-cart" ? lot with { ConditionBasisPoints = 0 } : lot).ToArray() };
        brokenState = brokenState with { Society = brokenState.Society with { Society = brokenState.Society.Society with { Inventory = inventory } } };
        using var given = WorldRuntime.Restore(brokenState, _ => new ControlChooser(giver, HandcartControlKind.Give, true));
        Assert.True((await given.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(recipient, given.Society.Inventory.GetLot("control-cart").OwnerId);
        Assert.Equal(0, given.Society.Inventory.GetLot("control-cart").ConditionBasisPoints);
        given.Validate();
    }

    private static async Task<(WorldRuntimeState State, string Actor, string Recipient)> ControlFixture(HandcartControlKind kind)
    {
        using var setup = NormalPathWorld.CreateGenerated("handcart-loop-audit", _ => new CartChooser("safe_idle"));
        Assert.True((await setup.AdvanceOneTickAsync()).Advanced);
        var state = setup.ExportState();
        var actor = state.Agents[0].AgentId;
        var recipient = state.Agents[1].AgentId;
        var position = state.Agents[0].Position;
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "control-cart", "handcart", actor, 1,
            groundPosition: new(position.X, position.Y));
        inventory = InventoryAuthority.AddLot(inventory, "control-cargo", "stone", actor, 4, containerLotId: "control-cart");
        state = AtPosition(state, actor, position, inventory) with
        {
            JevEnabled = false,
            RoutineHelper = RoutineHelperSettings.Off,
            HandcartHitches = kind == HandcartControlKind.Park ? [new("control-cart", actor)] : [],
            Agents = state.Agents.Select(person => person with
            {
                Position = person.AgentId == recipient ? position : person.Position,
                FullnessBasisPoints = 9_500,
                Survival = new(),
                Project = null,
                LastDecisionContext = null,
                TravelCooldownTicks = 0,
            }).ToArray(),
        };
        return (WeatherTestFixture.WithWeather(state, WeatherKind.Clear), actor, recipient);
    }

    private sealed class ControlChooser(string actor, HandcartControlKind kind, bool choose) : IDecisionProvider
    {
        public HandcartControlActionPlan? Plan { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = request.Observation.AgentId == actor ? request.Observation.Candidates.FirstOrDefault(item =>
                item.ActionPlan is HandcartControlActionPlan plan && plan.Choice.Kind == kind) : null;
            if (candidate is not null) Plan = Assert.IsType<HandcartControlActionPlan>(candidate.ActionPlan);
            var selected = choose ? candidate?.Id ?? "safe_idle" : "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind,
                ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                selected, 1, new Dictionary<string, double> { [selected] = 1 }));
        }
    }
}

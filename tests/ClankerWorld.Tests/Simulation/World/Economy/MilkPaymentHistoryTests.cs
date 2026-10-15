using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Runtime;
using ClankerWorld.Server.Saves;

namespace ClankerWorld.Tests;

public sealed partial class ShopTradeTests
{
    [Theory]
    [InlineData(2, false)]
    [InlineData(2, true)]
    [InlineData(4, false)]
    [InlineData(4, true)]
    public async Task OrdinaryMilkTradesRetainMovedPaymentThroughHostReloadAndResolution(int partialMoves, bool accept)
    {
        var (state, buyer, seller, shop) = CreateShopState("store");
        var home = state.Society.Society.GetAgent(seller).HouseholdId!;
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory,
            "history-milk", "milk", home, 2, storageBuildingId: shop);
        var buyerHome = state.Society.Society.GetAgent(buyer).HouseholdId!;
        var house = state.WorldSimulation!.Buildings.First(building => building.HouseholdId == buyerHome &&
            state.WorldContent!.Buildings.Single(definition => definition.CanonicalId == building.DefinitionId).Tags.Contains("house"));
        var paymentId = "buyer-payment";
        inventory = InventoryAuthority.Relocate(inventory, "history-start", paymentId, buyer, 6, storageBuildingId: house.InstanceId);
        for (var index = 1; index <= partialMoves; index++)
        {
            var quantity = inventory.GetLot(paymentId).Quantity - 1;
            var moveId = $"personal:{buyer}:{index}:{paymentId}";
            inventory = InventoryAuthority.Relocate(inventory, moveId, paymentId, buyer, quantity, carrierId: buyer);
            paymentId = DerivedId.Create(paymentId + "#move:" + moveId);
            if (index < partialMoves)
                inventory = InventoryAuthority.Relocate(inventory, "history-return-" + index, paymentId, buyer, quantity,
                    storageBuildingId: house.InstanceId);
        }
        Assert.InRange(paymentId.Length, 1, DerivedId.MaximumLength);
        var paymentBefore = inventory.GetLot(paymentId).Quantity;
        state = state with
        {
            Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } },
            Agents = state.Agents.Select(person => person.AgentId == buyer
                ? person with { FullnessBasisPoints = 5000, LastDecisionContext = null }
                : person).ToArray(),
        };
        var choices = new MilkHistoryChoices(seller, buyer);
        using var world = WorldRuntime.Restore(state, _ => choices);
        using var host = new MilkHistoryHost(world);
        if (world.Society.IsPaused) world.Resume();
        for (var step = 0; step < 20 && !world.Society.Inventory.Offers.Any(offer => offer.State == DirectBarterState.Open); step++)
            await host.Advance();
        state = world.ExportState();
        var trade = Assert.Single(state.ShopTrades!);
        var offer = world.Society.Inventory.GetOffer(trade.OfferId);
        Assert.Equal(("history-milk", paymentId, buyer, shop),
            (offer.FirstLotId, offer.SecondLotId, trade.BuyerId, trade.BuildingInstanceId));
        Assert.Equal(1, offer.FirstQuantity);
        Assert.Equal(InventoryReservationState.Reserved, world.Society.Inventory.GetReservation(offer.Id + ":first").State);
        var pending = host.Saved();
        using var reload = WorldRuntime.Restore(WorldRuntimeCodec.Decode(pending), _ => choices);
        Assert.Equal(pending, WorldRuntimeCodec.Encode(reload.ExportState()));
        using var reloadHost = new MilkHistoryHost(reload);
        Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Encode(state with
        { ShopTrades = state.ShopTrades!.Select(item => item with { BuyerId = seller }).ToArray() }));
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(pending, WorldRuntimeCodec.Encode(world.ExportState()));
        choices.Mode = accept ? "accept" : "decline";
        world.SubmitInstruction(new("ordinary-milk-response", "owner", seller, OwnerInstructionKind.Suggestion, "Consider the shop offer."));
        reload.SubmitInstruction(new("ordinary-milk-response", "owner", seller, OwnerInstructionKind.Suggestion, "Consider the shop offer."));
        for (var step = 0; step < 20 && world.Society.Inventory.GetOffer(offer.Id).State == DirectBarterState.Open; step++)
        {
            await host.Advance();
            await reloadHost.Advance();
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(reload.ExportState()));
        }
        Assert.Equal(accept ? DirectBarterState.Settled : DirectBarterState.Cancelled,
            world.Society.Inventory.GetOffer(offer.Id).State);
        Assert.Equal(accept ? 1 : 2, world.Society.Inventory.GetLot("history-milk").Quantity);
        Assert.Equal(paymentBefore - (accept ? offer.SecondQuantity : 0), world.Society.Inventory.GetLot(paymentId).Quantity);
        Assert.All(world.Society.Inventory.Lots.Where(lot => lot.ItemKind == "milk"), lot => Assert.Null(lot.ContainerLotId));
        world.Validate();
    }

    private sealed class MilkHistoryChoices(string seller, string buyer) : IDecisionProvider
    {
        public string Mode { get; set; } = "offer";
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            request.Validate();
            var observation = request.Observation;
            var prefix = Mode == "offer" && observation.AgentId == buyer ? "shop_buy:"
                : observation.AgentId == seller && Mode is "accept" or "decline"
                    ? Mode == "accept" ? "shop_continue:" : "shop_cancel:" : "safe_idle";
            var choice = observation.Candidates.FirstOrDefault(candidate => candidate.Id.StartsWith(prefix, StringComparison.Ordinal))
                ?? observation.Candidates.Single(candidate => candidate.Id == "safe_idle");
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, observation.AgentId, Kind, 0,
                observation.RunEpoch, observation.DecisionGeneration, observation.ObservationDigest, choice.Id, 1,
                observation.Candidates.ToDictionary(candidate => candidate.Id, candidate => candidate.Id == choice.Id ? 1d : 0d)));
        }
    }

    private sealed class MilkHistoryHost : IDisposable
    {
        private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("clanker-milk-history-");
        private readonly WorldRuntime world;
        private readonly WorldStateFile file;
        private readonly WorldRuntimeService service;
        private readonly RecordingLogger<WorldRuntimeService> logger = new();

        public MilkHistoryHost(WorldRuntime world, string? checkpointPath = null)
        {
            this.world = world;
            file = new WorldStateFile(checkpointPath ?? Path.Combine(directory.FullName, "world.json"));
            file.Save(world);
            var presence = new OwnerClientPresenceLease(TimeSpan.FromMinutes(5));
            presence.RecordAuthenticatedReconnect("test-owner");
            service = new WorldRuntimeService(world, file, presence, logger);
        }

        public byte[] Saved() => File.ReadAllBytes(file.Path);
        public async Task Advance()
        {
            // Compare the same admitted replies, rather than thread-pool timing.
            // The playable host intentionally runs personal calls between ticks.
            await ModelWorkHarness.WaitForExistingAsync(world);
            Assert.True(await service.AdvanceWithModelWorkAsync(world), string.Join("\n", logger.Messages));
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), Saved());
        }
        public void Dispose()
        {
            service.Dispose();
            directory.Delete(recursive: true);
        }
    }
}

using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Playtest;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.Tests;

public sealed partial class GrownAgentHelperMemoryTests
{
    [Theory]
    [InlineData("transfer")]
    [InlineData("contain")]
    [InlineData("take")]
    [InlineData("move")]
    [InlineData("cart-load")]
    [InlineData("cart-unload")]
    [InlineData("barter")]
    [InlineData("milk")]
    public async Task NativeThirdGenerationInventoryGeneratorsKeepIdentityProvenanceAndRecallAfterStrictReload(string generator)
    {
        var state = PrivateWorldRuntimeCodec.Decode(await LaterAdult.Value);
        var births = state.Society.Society.Births.OrderBy(birth => birth.CommittedTick).ToArray();
        Assert.Equal(3, births.Length);
        var owner = births[^1].ChildId;
        var recipient = births[^2].ChildId;
        Assert.Contains(state.Society.Society.Relationships, edge =>
            edge.Type == SocietyRelationshipType.BiologicalParentage && edge.ProposerId == recipient && edge.TargetId == owner);
        var physical = state.Inhabitants.Single(person => person.InhabitantId == owner).Position;
        var ground = new InventoryGroundPosition(physical.X, physical.Y);
        var society = state.Society.Society;
        // Display-name controls preserve the actual IDs, relationships and native birth history.
        society = society with
        {
            Households = society.Households.Select(home =>
            home with { Name = "Household " + new string('h', 118) }).ToArray()
        };
        var sourceId = "bounded-stock:" + owner;
        var paymentId = "bounded-payment:" + recipient;
        var vesselId = "bounded-vessel:" + owner;
        var receivingId = "bounded-vessel:" + recipient;
        var operation = DerivedId.Create("bounded-operation:" + owner + ":" + recipient + ":" + generator);
        var inventory = society.Inventory;
        inventory = InventoryFixture.AddLot(inventory, paymentId, "wood", recipient, 3);
        var cart = generator.StartsWith("cart-", StringComparison.Ordinal);
        var container = generator is "contain" or "take" or "milk" || cart;
        if (container)
            inventory = InventoryFixture.AddLot(inventory, vesselId,
                cart ? "handcart" : generator == "milk" ? "water_jug" : InventoryContainerRules.StoragePot, owner, 1,
                groundPosition: cart ? ground : null);
        if (generator == "milk")
            inventory = InventoryFixture.AddLot(inventory, receivingId, "water_jug", recipient, 1);
        inventory = InventoryFixture.AddLot(inventory, sourceId,
            generator == "milk" ? "milk" : generator is "contain" or "take" ? "bread" : "wood", owner, 3,
            containerLotId: generator is "take" or "milk" ? vesselId : null,
            groundPosition: cart ? ground : null);
        var initialLotIds = inventory.Lots.Select(lot => lot.Id).ToHashSet(StringComparer.Ordinal);
        var initialReservationIds = inventory.Reservations.Select(held => held.Id).ToHashSet(StringComparer.Ordinal);
        // Controlled stock, actual inventory transitions. Every branch forces a partial split.
        switch (generator)
        {
            case "transfer":
                inventory = InventoryFixture.Transfer(inventory, operation, owner, recipient, sourceId, 1, "bounded-generator");
                break;
            case "contain":
                inventory = InventoryFixture.PutIntoContainer(inventory, operation, owner, vesselId, sourceId, 1);
                break;
            case "take":
                inventory = InventoryFixture.TakeFromContainer(inventory, operation, owner, owner, vesselId, sourceId, 1);
                break;
            case "move":
                inventory = InventoryFixture.Relocate(inventory, operation, sourceId, owner, 1, carrierId: owner);
                break;
            case "cart-load":
                inventory = InventoryFixture.LoadHandcart(inventory, operation, owner, vesselId, sourceId, 1);
                break;
            case "cart-unload":
                inventory = InventoryFixture.LoadHandcart(inventory, operation, owner, vesselId, sourceId, 2);
                var loaded = Assert.Single(inventory.Lots, lot => lot.ContainerLotId == vesselId);
                inventory = InventoryFixture.UnloadHandcart(inventory, operation, owner, vesselId, loaded.Id, 1, ontoGround: true);
                break;
            case "barter":
                inventory = InventoryFixture.CreateDirectBarterOffer(inventory,
                    new(operation, 1, owner, recipient, sourceId, 1, paymentId, 1, inventory.WorldTick + 30));
                inventory = InventoryFixture.AcceptDirectBarterOffer(inventory, operation, 1, owner);
                inventory = InventoryFixture.AcceptDirectBarterOffer(inventory, operation, 1, recipient);
                Assert.Equal(DirectBarterState.Settled, inventory.GetOffer(operation).State);
                break;
            case "milk":
                var held = DerivedId.Create(operation + ":milk");
                inventory = InventoryFixture.Reserve(inventory, held, owner, sourceId, 1,
                    "milk-sale:" + operation, inventory.WorldTick + 30);
                inventory = InventoryFixture.ExchangeMilk(inventory, operation, held, receivingId, recipient, paymentId, ground);
                Assert.Equal(InventoryReservationState.Released, inventory.GetReservation(held).State);
                Assert.Equal(owner, inventory.GetLot(vesselId).OwnerId);
                Assert.Equal(recipient, inventory.GetLot(receivingId).OwnerId);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(generator));
        }
        var created = inventory.Lots.Where(lot => !initialLotIds.Contains(lot.Id)).ToArray();
        Assert.NotEmpty(created);
        Assert.All(created, lot =>
        {
            Assert.InRange(lot.Id.Length, 1, DerivedId.MaximumLength);
            Assert.NotNull(lot.ProvenanceLotId);
            Assert.Contains(lot.ProvenanceLotId, inventory.Lots.Select(source => source.Id).Concat(initialLotIds));
        });
        var generatedIds = created.Select(lot => lot.Id).Concat(inventory.Reservations
            .Where(held => !initialReservationIds.Contains(held.Id)).Select(held => held.Id)).ToArray();
        Assert.All(generatedIds, id => Assert.InRange(id.Length, 1, DerivedId.MaximumLength));
        foreach (var id in generatedIds)
        {
            var summary = "I remember bounded" + generator.Replace("-", "", StringComparison.Ordinal) + " work.";
            var checkpoint = SocietyFixture.RecordSocialMemory(society with { Inventory = inventory },
                new(id, owner, recipient, summary, "private", society.WorldTick)).Checkpoint;
            var bytes = PrivateWorldRuntimeCodec.Encode(state with
            { Society = state.Society with { Society = checkpoint } });
            using var restored = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(bytes));
            Assert.Equal(bytes, PrivateWorldRuntimeCodec.Encode(restored.ExportState()));
            var candidates = new CognitionCandidate[] { new("safe_idle", summary, 0) };
            var memories = PrivateWorldMemoryRetrieval.Retrieve(restored.Society.Memories,
                restored.Society.Beliefs ?? [], null, owner, restored.WorldTick, candidates, restored.Society);
            var recalled = Assert.Single(memories, memory => memory.Id == id);
            Assert.Equal((owner, recipient, summary), (recalled.OwnerId, recalled.SubjectId, recalled.Summary));
            new InhabitantObservation(owner, restored.WorldTick, 0, 1, "sha256:bounded-generator", 0,
                candidates, RetrievedMemories: memories).Validate();
            Assert.Equal("Household " + new string('h', 118), restored.Society.GetHousehold(
                restored.Society.GetInhabitant(owner).HouseholdId!).Name);
        }
    }
}

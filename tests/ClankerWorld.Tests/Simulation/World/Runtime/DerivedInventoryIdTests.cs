using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

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
        var state = WorldRuntimeCodec.Decode(await LaterAdult.Value);
        var births = state.Society.Society.Births.OrderBy(birth => birth.CommittedTick).ToArray();
        Assert.Equal(3, births.Length);
        var owner = births[^1].ChildId;
        var recipient = births[^2].ChildId;
        Assert.Contains(state.Society.Society.Relationships, edge =>
            edge.Type == SocietyRelationshipType.BiologicalParentage && edge.ProposerId == recipient && edge.TargetId == owner);
        var physical = state.Agents.Single(person => person.AgentId == owner).Position;
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
        var operation = DerivedId.Create("bounded-operation:" + owner + ":" + recipient + ":" + generator);
        var inventory = society.Inventory;
        inventory = InventoryAuthority.AddLot(inventory, paymentId, "wood", recipient, 3);
        var cart = generator.StartsWith("cart-", StringComparison.Ordinal);
        var container = generator is "contain" or "take" || cart;
        if (container)
            inventory = InventoryAuthority.AddLot(inventory, vesselId,
                cart ? "handcart" : InventoryContainerRules.StoragePot, owner, 1,
                groundPosition: cart ? ground : null);
        inventory = InventoryAuthority.AddLot(inventory, sourceId,
            generator == "milk" ? "milk" : generator is "contain" or "take" ? "bread" : "wood", owner, 3,
            containerLotId: generator == "take" ? vesselId : null,
            groundPosition: cart ? ground : null);
        var initialLotIds = inventory.Lots.Select(lot => lot.Id).ToHashSet(StringComparer.Ordinal);
        var initialReservationIds = inventory.Reservations.Select(held => held.Id.Value).ToHashSet(StringComparer.Ordinal);
        // Controlled stock, actual inventory transitions. Every branch forces a partial split.
        switch (generator)
        {
            case "transfer":
                inventory = InventoryAuthority.Transfer(inventory, operation, owner, recipient, sourceId, 1, "bounded-generator");
                break;
            case "contain":
                inventory = InventoryAuthority.PutIntoContainer(inventory, operation, owner, vesselId, sourceId, 1);
                break;
            case "take":
                inventory = InventoryAuthority.TakeFromContainer(inventory, operation, owner, owner, vesselId, sourceId, 1);
                break;
            case "move":
                inventory = InventoryAuthority.Relocate(inventory, operation, sourceId, owner, 1, carrierId: owner);
                break;
            case "cart-load":
                inventory = InventoryAuthority.LoadHandcart(inventory, operation, owner, vesselId, sourceId, 1);
                break;
            case "cart-unload":
                inventory = InventoryAuthority.LoadHandcart(inventory, operation, owner, vesselId, sourceId, 2);
                var loaded = Assert.Single(inventory.Lots, lot => lot.ContainerLotId == vesselId);
                inventory = InventoryAuthority.UnloadHandcart(inventory, operation, owner, vesselId, loaded.Id, 1, ontoGround: true);
                break;
            case "barter":
            case "milk":
                inventory = InventoryAuthority.CreateDirectBarterOffer(inventory,
                    new(Identity.Parse(operation), 1, owner, recipient, sourceId, 1, paymentId, 1, inventory.WorldTick + 30));
                inventory = InventoryAuthority.AcceptDirectBarterOffer(inventory, operation, 1, owner);
                inventory = InventoryAuthority.AcceptDirectBarterOffer(inventory, operation, 1, recipient);
                Assert.Equal(DirectBarterState.Settled, inventory.GetOffer(operation).State);
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
            .Where(held => !initialReservationIds.Contains(held.Id)).Select(held => held.Id.Value)).ToArray();
        Assert.All(generatedIds, id => Assert.InRange(id.Length, 1, DerivedId.MaximumLength));
        foreach (var id in generatedIds)
        {
            var summary = "I remember bounded" + generator.Replace("-", "", StringComparison.Ordinal) + " work.";
            var checkpoint = SocietyRules.RecordSocialMemory(society with { Inventory = inventory },
                new(Identity.Parse(id), owner, recipient, summary, "private", society.WorldTick)).Checkpoint;
            var bytes = WorldRuntimeCodec.Encode(state with
            { Society = state.Society with { Society = checkpoint } });
            using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes));
            Assert.Equal(bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
            var candidates = new CognitionCandidate[] { new("safe_idle", summary, 0) };
            var memories = WorldMemoryRetrieval.Retrieve(restored.Society.Memories,
                restored.Society.Beliefs ?? [], null, owner, restored.WorldTick, candidates, restored.Society);
            var recalled = Assert.Single(memories, memory => memory.Id == id);
            Assert.Equal((owner, recipient, summary), (recalled.OwnerId, recalled.SubjectId, recalled.Summary));
            new AgentObservation(owner, restored.WorldTick, 0, 1, "sha256:bounded-generator", 0,
                candidates, RetrievedMemories: memories).Validate();
            Assert.Equal("Household " + new string('h', 118), restored.Society.GetHousehold(
                restored.Society.GetAgent(owner).HouseholdId!).Name);
        }
    }
}

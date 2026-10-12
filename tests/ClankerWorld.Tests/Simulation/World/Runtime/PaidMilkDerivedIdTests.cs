using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed partial class GrownAgentHelperMemoryTests
{
    private static readonly Lazy<Task<byte[]>> NativeFiftyMilkTrace = new(CreateNativeFiftyMilkTraceAsync);

    [Fact]
    public async Task ThreeNativeGenerationsAndFiftyOrdinaryMilkBartersKeepEveryNewPaymentLotSaveableAndRecallable()
    {
        var bytes = await NativeFiftyMilkTrace.Value;
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
        var trades = restored.Society.Inventory.Reservations.Where(held => held.Purpose.StartsWith("barter:", StringComparison.Ordinal) &&
            restored.Society.Inventory.Offers.Any(offer => offer.Id == held.Purpose["barter:".Length..] && offer.FirstLotId == "native-resale-milk")).ToArray();
        Assert.Equal(100, trades.Length);
        Assert.Equal(50, restored.Society.Inventory.Offers.Count(offer => offer.FirstLotId == "native-resale-milk" &&
            offer.State == DirectBarterState.Settled));
        Assert.All(trades, held =>
        {
            Assert.InRange(held.Id.Length, 1, DerivedId.MaximumLength);
            Assert.Equal((1, InventoryReservationState.Completed), (held.Quantity, held.State));
        });
        Assert.Equal(3, restored.Society.Births.Count);
        Assert.InRange(Assert.Single(restored.Society.Inventory.Lots, lot => lot.ItemKind == "milk").Id.Length,
            1, DerivedId.MaximumLength);
    }

    private static async Task<byte[]> CreateNativeFiftyMilkTraceAsync()
    {
        var state = WorldRuntimeCodec.Decode(await LaterAdult.Value);
        var births = state.Society.Society.Births.OrderBy(birth => birth.CommittedTick).ToArray();
        Assert.Equal(3, births.Length);
        var people = new[] { births[^1].ChildId, births[^2].ChildId };
        Assert.Contains(state.Society.Society.Relationships, edge => edge.Type == SocietyRelationshipType.BiologicalParentage &&
            edge.ProposerId == people[1] && edge.TargetId == people[0]);
        // A 57-unit semantic prefix makes actual payment splits exercise the full 128-unit boundary.
        var paymentPrefix = "native-resale-payment-" + new string('p', 34) + ":";
        var payments = people.Select(person => DerivedId.Create(paymentPrefix + person)).ToArray();
        var society = state.Society.Society;
        var inventory = society.Inventory;
        for (var index = 0; index < 2; index++)
        {
            inventory = InventoryAuthority.AddLot(inventory, payments[index], "wood", people[index], 50);
        }
        inventory = InventoryAuthority.AddLot(inventory, "native-resale-milk", "milk", people[0], 1);
        // Controlled starting stock; all fifty barters require both actual consents and complete both holds.
        for (var round = 0; round < 50; round++)
        {
            var seller = round % 2;
            var buyer = 1 - seller;
            var milk = Assert.Single(inventory.Lots, lot => lot.ItemKind == "milk");
            Assert.Equal((people[seller], "native-resale-milk", 1), (milk.OwnerId, milk.Id, milk.Quantity));
            Assert.Null(milk.ContainerLotId);
            var operation = DerivedId.Create("native-resale:" + people[seller] + ":" + people[buyer] + ":" + round);
            var beforePayment = inventory.GetLot(payments[buyer]).Quantity;
            inventory = InventoryAuthority.CreateDirectBarterOffer(inventory,
                new(operation, 1, people[seller], people[buyer], milk.Id, 1, payments[buyer], 1, inventory.WorldTick + 30));
            inventory = InventoryAuthority.AcceptDirectBarterOffer(inventory, operation, 1, people[seller]);
            Assert.Equal(DirectBarterState.Open, inventory.GetOffer(operation).State);
            Assert.Equal(people[seller], inventory.GetLot(milk.Id).OwnerId);
            Assert.Equal(beforePayment, inventory.GetLot(payments[buyer]).Quantity);
            inventory = InventoryAuthority.AcceptDirectBarterOffer(inventory, operation, 1, people[buyer]);
            Assert.Equal(DirectBarterState.Settled, inventory.GetOffer(operation).State);
            Assert.Equal((people[buyer], "native-resale-milk", 1),
                (inventory.GetLot(milk.Id).OwnerId, inventory.GetLot(milk.Id).Id, inventory.GetLot(milk.Id).Quantity));
            Assert.Null(inventory.GetLot(milk.Id).ProvenanceLotId);
            var transferred = inventory.GetLot(DerivedId.Create(payments[buyer] + "#barter:" + operation));
            Assert.Equal((people[seller], payments[buyer], 1),
                (transferred.OwnerId, transferred.ProvenanceLotId, transferred.Quantity));
            Assert.InRange(transferred.Id.Length, 1, DerivedId.MaximumLength);
            Assert.Equal(beforePayment - 1, inventory.GetLot(payments[buyer]).Quantity);
            Assert.Equal(InventoryReservationState.Completed, inventory.GetReservation(DerivedId.Create(operation + ":first")).State);
            Assert.Equal(InventoryReservationState.Completed, inventory.GetReservation(DerivedId.Create(operation + ":second")).State);
            var marker = "milkresale" + (char)('a' + round / 26) + (char)('a' + round % 26);
            var summary = "I remember " + marker + " paid work.";
            society = SocietyRules.RecordSocialMemory(society with { Inventory = inventory },
                new(transferred.Id, people[0], people[1], summary, "private", society.WorldTick)).Checkpoint;
            var bytes = WorldRuntimeCodec.Encode(state with { Society = state.Society with { Society = society } });
            using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes));
            Assert.Equal(bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
            var candidates = new CognitionCandidate[] { new("safe_idle", summary, 0) };
            var memories = WorldMemoryRetrieval.Retrieve(restored.Society.Memories, restored.Society.Beliefs ?? [],
                null, people[0], restored.WorldTick, candidates, restored.Society);
            var recalled = Assert.Single(memories, memory => memory.Id == transferred.Id);
            Assert.Equal((people[0], people[1], summary), (recalled.OwnerId, recalled.SubjectId, recalled.Summary));
            new AgentObservation(people[0], restored.WorldTick, 0, 1, "sha256:native-resale", 0,
                candidates, RetrievedMemories: memories).Validate();
        }
        return WorldRuntimeCodec.Encode(state with { Society = state.Society with { Society = society } });
    }
}

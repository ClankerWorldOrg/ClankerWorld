using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Playtest;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.Tests;

public sealed partial class GrownAgentHelperMemoryTests
{
    private static readonly Lazy<Task<byte[]>> NativeFiftyMilkTrace = new(CreateNativeFiftyMilkTraceAsync);

    [Fact]
    public async Task ThreeNativeGenerationsAndFiftyPaidMilkResalesKeepEveryNewLotSaveableAndRecallable()
    {
        var bytes = await NativeFiftyMilkTrace.Value;
        using var restored = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(bytes));
        Assert.Equal(bytes, PrivateWorldRuntimeCodec.Encode(restored.ExportState()));
        var trades = restored.Society.Inventory.Reservations.Where(held => held.Purpose.StartsWith("milk-sale:native-resale-", StringComparison.Ordinal)).ToArray();
        Assert.Equal(50, trades.Length);
        Assert.All(trades, held =>
        {
            Assert.InRange(held.Id.Length, 1, DerivedId.MaximumLength);
            Assert.Equal((1, InventoryReservationState.Released), (held.Quantity, held.State));
        });
        Assert.Equal(3, restored.Society.Births.Count);
        Assert.InRange(Assert.Single(restored.Society.Inventory.Lots, lot => lot.ItemKind == "milk").Id.Length,
            1, DerivedId.MaximumLength);
    }

    private static async Task<byte[]> CreateNativeFiftyMilkTraceAsync()
    {
        var state = PrivateWorldRuntimeCodec.Decode(await LaterAdult.Value);
        var births = state.Society.Society.Births.OrderBy(birth => birth.CommittedTick).ToArray();
        Assert.Equal(3, births.Length);
        var people = new[] { births[^1].ChildId, births[^2].ChildId };
        Assert.Contains(state.Society.Society.Relationships, edge => edge.Type == SocietyRelationshipType.BiologicalParentage &&
            edge.ProposerId == people[1] && edge.TargetId == people[0]);
        var jugs = people.Select(person => DerivedId.Create("native-resale-jug:" + person)).ToArray();
        var payments = people.Select(person => DerivedId.Create("native-resale-payment:" + person)).ToArray();
        var society = state.Society.Society;
        var inventory = society.Inventory;
        for (var index = 0; index < 2; index++)
        {
            inventory = InventoryFixture.AddLot(inventory, jugs[index], InventoryContainerRules.WaterJug, people[index], 1);
            inventory = InventoryFixture.AddLot(inventory, payments[index], "wood", people[index], 50);
        }
        inventory = InventoryFixture.AddLot(inventory, "native-resale-milk", "milk", people[0], 1, containerLotId: jugs[0]);
        var position = state.Inhabitants.Single(person => person.InhabitantId == people[0]).Position;
        // Controlled starting stock; all fifty transfers, exclusive holds and payments are actual kernel transitions.
        for (var round = 0; round < 50; round++)
        {
            var seller = round % 2;
            var buyer = 1 - seller;
            var milk = Assert.Single(inventory.Lots, lot => lot.ItemKind == "milk");
            Assert.Equal((people[seller], jugs[seller], 1), (milk.OwnerId, milk.ContainerLotId, milk.Quantity));
            var operation = DerivedId.Create("native-resale:" + people[seller] + ":" + people[buyer] + ":" + round);
            var held = DerivedId.Create(operation + ":milk");
            inventory = InventoryFixture.Reserve(inventory, held, people[seller], milk.Id, 1,
                "milk-sale:native-resale-" + round, long.MaxValue);
            var beforePayment = inventory.GetLot(payments[buyer]).Quantity;
            inventory = InventoryFixture.ExchangeMilk(inventory, operation, held, jugs[buyer], people[buyer],
                payments[buyer], new(position.X, position.Y));
            var transferred = Assert.Single(inventory.Lots, lot => lot.ItemKind == "milk");
            Assert.Equal((people[buyer], jugs[buyer], milk.Id, 1),
                (transferred.OwnerId, transferred.ContainerLotId, transferred.ProvenanceLotId, transferred.Quantity));
            Assert.InRange(transferred.Id.Length, 1, DerivedId.MaximumLength);
            Assert.Equal(beforePayment - 1, inventory.GetLot(payments[buyer]).Quantity);
            Assert.Equal(InventoryReservationState.Released, inventory.GetReservation(held).State);
            Assert.Equal(people[0], inventory.GetLot(jugs[0]).OwnerId);
            Assert.Equal(people[1], inventory.GetLot(jugs[1]).OwnerId);
            var marker = "milkresale" + (char)('a' + round / 26) + (char)('a' + round % 26);
            var summary = "I remember " + marker + " paid work.";
            society = SocietyFixture.RecordSocialMemory(society with { Inventory = inventory },
                new(transferred.Id, people[0], people[1], summary, "private", society.WorldTick)).Checkpoint;
            var bytes = PrivateWorldRuntimeCodec.Encode(state with { Society = state.Society with { Society = society } });
            using var restored = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(bytes));
            Assert.Equal(bytes, PrivateWorldRuntimeCodec.Encode(restored.ExportState()));
            var candidates = new CognitionCandidate[] { new("safe_idle", summary, 0) };
            var memories = PrivateWorldMemoryRetrieval.Retrieve(restored.Society.Memories, restored.Society.Beliefs ?? [],
                null, people[0], restored.WorldTick, candidates, restored.Society);
            var recalled = Assert.Single(memories, memory => memory.Id == transferred.Id);
            Assert.Equal((people[0], people[1], summary), (recalled.OwnerId, recalled.SubjectId, recalled.Summary));
            new InhabitantObservation(people[0], restored.WorldTick, 0, 1, "sha256:native-resale", 0,
                candidates, RetrievedMemories: memories).Validate();
        }
        return PrivateWorldRuntimeCodec.Encode(state with { Society = state.Society with { Society = society } });
    }
}

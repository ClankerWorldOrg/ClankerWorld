using System.Text.Json;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Server.Observation;

namespace ClankerWorld.Tests;

public sealed class DeveloperEditTests
{
    internal static WorldDeveloperEdit Edit(WorldRuntime world, string operation, string value,
        int amount = 0, string? actor = null, string? other = null) => new(world.Society.WorldId,
        world.ExportState().Events[^1].EventId, actor ?? world.Agents[0].AgentId, operation, value, amount, other);

    [Fact]
    public async Task NormalWorldEditsSurviveSaveAndReplayWithMatchingContinuation()
    {
        using var world = NormalPathWorld.CreateGenerated("developer-edits-normal", _ => new DeterministicDecisionProvider());
        world.Pause();
        var baseline = WorldRuntimeCodec.Encode(world.ExportState());
        var actor = world.Agents[0].AgentId;
        var other = world.Agents[1].AgentId;
        void Apply(string operation, string value, int amount = 0, string? target = null)
        {
            var result = world.ApplyDeveloperEdit(Edit(world, operation, value, amount, actor, target));
            Assert.True(result.Applied, result.Failure);
            Assert.Equal(0, world.WorldTick);
        }
        Apply("set_need", "fullness", 62);
        Apply("set_need", "warmth", 80);
        Apply("set_need", "illness", 10);
        Apply("set_need", "nutrition", 70);
        Apply("give_goods", "wood", 2);
        Apply("remove_goods", "wood", 1);
        Apply("add_skill", "farming");
        Apply("add_skill", "smithing");
        Apply("remove_skill", "smithing");
        Apply("start_partnership", "partnership", target: other);
        Assert.Contains(world.Society.Relationships, edge => edge.Type == SocietyRelationshipType.Partnership && edge.State == SocietyRelationshipState.Accepted);
        Apply("end_partnership", "partnership", target: other);
        var person = world.Agents.Single(item => item.AgentId == actor);
        Assert.Equal(6200, person.FullnessBasisPoints);
        Assert.Equal(8000, person.Survival!.WarmthBasisPoints);
        Assert.Equal(1000, person.Survival.IllnessBasisPoints);
        Assert.Equal(7000, person.Survival.NutritionBasisPoints);
        Assert.Contains(person.Skills!, skill => skill.Kind == SkillKind.Farming);
        Assert.DoesNotContain(person.Skills!, skill => skill.Kind == SkillKind.Smithing);
        var wood = Assert.Single(world.Society.Inventory.Lots, lot => lot.Id.StartsWith("developer-edit-", StringComparison.Ordinal) && lot.ItemKind == "wood");
        Assert.Equal(1, wood.Quantity);
        Assert.True(PersonalEquipmentRules.IsCarried(wood, actor));
        Assert.DoesNotContain(world.Society.Relationships, edge => edge.Type == SocietyRelationshipType.Partnership && edge.State == SocietyRelationshipState.Accepted);
        var accepted = world.ExportState();
        var commands = accepted.Events.Where(item => item.Kind == "developer_edit").ToArray();
        Assert.Equal(11, commands.Length);
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(baseline));
        foreach (var item in commands)
        {
            var result = replay.ApplyDeveloperEdit(JsonSerializer.Deserialize<WorldDeveloperEdit>(item.Detail)!);
            Assert.True(result.Applied, result.Failure);
        }
        var bytes = WorldRuntimeCodec.Encode(accepted);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(replay.ExportState()));
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
        world.Resume(); replay.Resume(); restored.Resume();
        for (var tick = 0; tick < 3; tick++)
        {
            await world.AdvanceOneTickAsync(); await replay.AdvanceOneTickAsync(); await restored.AdvanceOneTickAsync();
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(restored.ExportState()));
        }
    }

    [Theory]
    [InlineData("set_need", "fullness", -1)]
    [InlineData("set_need", "fullness", 101)]
    [InlineData("set_need", "unknown", 10)]
    [InlineData("give_goods", "wood", 100)]
    [InlineData("give_goods", "unknown", 1)]
    [InlineData("give_goods", "wood", -1)]
    [InlineData("remove_goods", "wood", 100)]
    [InlineData("add_skill", "unknown", 0)]
    [InlineData("remove_skill", "unknown", 0)]
    [InlineData("start_partnership", "partnership", 0)]
    [InlineData("end_partnership", "partnership", 0)]
    [InlineData("unknown", "wood", 0)]
    public void InvalidEditsLeaveCheckpointAndEventsUntouched(string operation, string value, int amount)
    {
        using var world = new WorldRuntime("developer-invalid");
        world.Pause();
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        var result = world.ApplyDeveloperEdit(Edit(world, operation, value, amount));
        Assert.False(result.Applied);
        Assert.NotNull(result.Failure);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
    }

    [Fact]
    public void RunningStaleAndWrongWorldEditsAreRejectedAndRetriesCannotDuplicateGoods()
    {
        using var world = new WorldRuntime("developer-retry");
        Assert.NotNull(world.ApplyDeveloperEdit(Edit(world, "give_goods", "wood", 1)).Failure);
        world.Pause();
        var command = Edit(world, "give_goods", "wood", 1);
        Assert.NotNull(world.ApplyDeveloperEdit(command with { WorldId = "another-world" }).Failure);
        Assert.True(world.ApplyDeveloperEdit(command).Applied);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.True(world.ApplyDeveloperEdit(command).AlreadyApplied);
        Assert.NotNull(world.ApplyDeveloperEdit(command with { Amount = 2 }).Failure);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes));
        Assert.True(restored.ApplyDeveloperEdit(command).AlreadyApplied);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
    }

    [Fact]
    public void FailedWritePreservesLiveStateAndPriorSaveThenRetryCommitsOnce()
    {
        var directory = Directory.CreateTempSubdirectory("developer-write-");
        try
        {
            using var world = new WorldRuntime("developer-write");
            world.Pause();
            var file = new WorldStateFile(Path.Combine(directory.FullName, "world.json"));
            file.Save(world);
            var before = WorldRuntimeCodec.Encode(world.ExportState());
            var saved = File.ReadAllBytes(file.Path);
            var command = Edit(world, "give_goods", "wood", 2);
            File.Move(file.Path, file.Path + ".prior");
            Directory.CreateDirectory(file.Path);
            var failure = Record.Exception(() => file.ApplyDeveloperEdit(world, command));
            Assert.True(failure is IOException or UnauthorizedAccessException, failure?.ToString());
            Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
            Assert.Equal(saved, File.ReadAllBytes(file.Path + ".prior"));
            Directory.Delete(file.Path);
            File.Move(file.Path + ".prior", file.Path);
            Assert.True(file.ApplyDeveloperEdit(world, command).Applied);
            using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(File.ReadAllBytes(file.Path)));
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(restored.ExportState()));
            Assert.True(file.ApplyDeveloperEdit(world, command).AlreadyApplied);
            Assert.Single(world.ExportState().Events, item => item.Kind == "developer_edit");
        }
        finally { directory.Delete(true); }
    }

    [Fact]
    public void PartnershipsRejectDeadAgentsAndCloseRelatives()
    {
        using var original = new WorldRuntime("developer-family");
        original.Pause();
        var state = original.ExportState();
        var actor = state.Agents[0].AgentId;
        var other = state.Agents[1].AgentId;
        var related = FamilyTreeFixture.WithRelationships(state, SocietyRelationshipType.BiologicalParentage, (actor, other));
        using var relatives = WorldRuntime.Restore(related);
        Assert.NotNull(relatives.ApplyDeveloperEdit(Edit(relatives, "start_partnership", "partnership", actor: actor, other: other)).Failure);
        using var society = SocietyRuntime.Restore(state.Society);
        society.Apply(checkpoint => SocietyRules.Kill(checkpoint, other, SocietyDeathCause.Accident, original.WorldTick));
        using var bereaved = WorldRuntime.Restore(state with
        {
            Society = society.ExportState(),
            Agents = state.Agents.Where(person => person.AgentId != other).ToArray(),
        });
        var before = WorldRuntimeCodec.Encode(bereaved.ExportState());
        Assert.NotNull(bereaved.ApplyDeveloperEdit(Edit(bereaved, "start_partnership", "partnership", actor: actor, other: other)).Failure);
        Assert.NotNull(bereaved.ApplyDeveloperEdit(Edit(bereaved, "set_need", "fullness", 50, actor: other)).Failure);
        Assert.Equal(before, WorldRuntimeCodec.Encode(bereaved.ExportState()));
    }

    [Fact]
    public void EveryOfferedGoodHasAValidCarriedLocationAndCanBeRemoved()
    {
        using var world = new WorldRuntime("developer-goods");
        world.Pause();
        var actor = world.Agents[0].AgentId;
        foreach (var kind in WorldRuntime.DeveloperGoods)
        {
            var added = world.ApplyDeveloperEdit(Edit(world, "give_goods", kind, 1));
            Assert.True(added.Applied, kind + ": " + added.Failure);
            var lot = world.Society.Inventory.Lots.Single(item => item.Id == "developer-edit-" + world.ExportState().Events[^1].EventId);
            Assert.True(PersonalEquipmentRules.IsCarried(lot, actor));
            Assert.Equal(actor, lot.OwnerId);
            var removed = world.ApplyDeveloperEdit(Edit(world, "remove_goods", kind, 1));
            Assert.True(removed.Applied, kind + ": " + removed.Failure);
        }
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
    }

    [Fact]
    public void MultipleVesselsRemainSeparateLotsAndCanBeRemovedTogether()
    {
        using var world = new WorldRuntime("developer-vessels");
        world.Pause();
        var result = world.ApplyDeveloperEdit(Edit(world, "give_goods", "water_jug", 2));
        Assert.True(result.Applied, result.Failure);
        var vessels = world.Society.Inventory.Lots.Where(lot => lot.ItemKind == "water_jug").ToArray();
        Assert.Equal(2, vessels.Length);
        Assert.All(vessels, lot => Assert.Equal(1, lot.Quantity));
        var removed = world.ApplyDeveloperEdit(Edit(world, "remove_goods", "water_jug", 2));
        Assert.True(removed.Applied, removed.Failure);
        Assert.DoesNotContain(world.Society.Inventory.Lots, lot => lot.ItemKind == "water_jug");
    }

    [Fact]
    public void RemovalProtectsEquipmentBorrowedGoodsVesselContentsAndReservations()
    {
        using var original = new WorldRuntime("developer-protected-goods");
        original.Pause();
        var state = original.ExportState();
        var actor = state.Agents[0].AgentId;
        var other = state.Agents[1].AgentId;
        var inventory = state.Society.Society.Inventory;
        inventory = InventoryAuthority.AddLot(inventory, "equipped", "clothing", actor, 1);
        inventory = InventoryAuthority.AddLot(inventory, "reserved", "wood", actor, 2);
        inventory = InventoryAuthority.Reserve(inventory, "keep-reserved", actor, "reserved", 1, "test", 100);
        inventory = InventoryAuthority.AddLot(inventory, "borrowed", "stone", other, 1);
        inventory = InventoryAuthority.Relocate(inventory, "borrow", "borrowed", other, 1, actor);
        inventory = InventoryAuthority.AddLot(inventory, "pot", "storage_pot", actor, 1);
        inventory = InventoryAuthority.AddLot(inventory, "inside", "berries", actor, 1, containerLotId: "pot");
        state = state with
        {
            Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } },
            Agents = state.Agents.Select(person => person.AgentId == actor
                ? person with { Equipment = new PersonalEquipment(ClothingLotId: "equipped") } : person).ToArray(),
        };
        using var world = WorldRuntime.Restore(state);
        var before = WorldRuntimeCodec.Encode(world.ExportState());
        foreach (var kind in new[] { "clothing", "stone", "storage_pot", "berries" })
        {
            Assert.NotNull(world.ApplyDeveloperEdit(Edit(world, "remove_goods", kind, 1, actor)).Failure);
            Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
        }
        Assert.NotNull(world.ApplyDeveloperEdit(Edit(world, "remove_goods", "wood", 2, actor)).Failure);
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
        var result = world.ApplyDeveloperEdit(Edit(world, "remove_goods", "wood", 1, actor));
        Assert.True(result.Applied, result.Failure);
        Assert.Equal(1, world.Society.Inventory.GetLot("reserved").Quantity);
        Assert.Equal(InventoryReservationState.Reserved, world.Society.Inventory.GetReservation("keep-reserved").State);
    }
}

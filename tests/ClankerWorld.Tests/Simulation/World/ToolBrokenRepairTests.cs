using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed class ToolBrokenRepairTests
{
    private const string Tool = "native-break-pick";

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(3, true)]
    public async Task RealMiningWearAllowsWornRepairsButRequiresBrokenToolReplacement(int harvests, bool ownerOrder)
    {
        var idle = new MarketRulesPolicy { Choose = (_, candidates) => candidates.Single(candidate => candidate.Id == "safe_idle") };
        using var generated = NormalPathWorld.CreateGenerated("broken-shared-tool-audit", idle.CreateProvider);
        Assert.True((await generated.AdvanceOneTickAsync()).Advanced);
        var state = generated.ExportState();
        var smith = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == "first-town-blacksmith");
        var actor = state.Society.Society.Agents.First(person => person.HouseholdId == smith.HouseholdId &&
            person.AgeBand is SocietyAgeBand.Adult or SocietyAgeBand.Elder).Id;
        var deposit = state.Map.Resources.Where(resource => resource.Kind == "stone" &&
                state.Map.IsReachableOnFoot(smith.Position, resource.Position) &&
                state.Resources.Single(item => item.ResourceId == resource.Id).State == ResourceState.Available)
            .OrderBy(resource => state.Map.FootDistance(smith.Position, resource.Position)).First();
        var inventory = state.Society.Society.Inventory with { Lots = [], Reservations = [] };
        inventory = InventoryAuthority.AddLot(inventory, Tool, HouseToolsContent.CrudeWoodenPickaxe, actor, 1);
        inventory = InventoryAuthority.AddLot(inventory, "repair-wood", "wood", actor, 1);
        inventory = InventoryAuthority.AddLot(inventory, "mining-sack", "leather_sack", actor, 1);
        state = state with
        {
            Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } },
            Agents = state.Agents.Select(person => person with
            {
                Position = person.AgentId == actor ? deposit.Position : person.Position,
                HungerBasisPoints = 9_500,
                Survival = new(),
                Project = null,
                LastDecisionContext = null,
                Equipment = person.AgentId == actor ? new(CarryAidLotId: "mining-sack") : null,
            }).ToArray(),
        };
        var working = new MarketRulesPolicy
        {
            Choose = (_, candidates) => candidates.FirstOrDefault(candidate => candidate.Id == "gather_material" ||
                candidate.Id == "move") ?? candidates.Single(candidate => candidate.Id == "safe_idle"),
        };
        using (var setup = WorldRuntime.Restore(state, working.CreateProvider))
        {
            setup.Validate();
            for (var count = 1; count <= harvests; count++)
            {
                await FinishOrderAsync(setup, actor, "mine-" + count, "gather stone from " + deposit.Id);
                Assert.Equal(Math.Max(0, 10_000 - 4_000 * count), setup.Society.Inventory.GetLot(Tool).ConditionBasisPoints);
            }
            await FinishOrderAsync(setup, actor, "return-smith", $"move to ({smith.Position.X}, {smith.Position.Y})");
            Assert.Equal(smith.Position, setup.ExportState().Agents.Single(person => person.AgentId == actor).Position);
            setup.Pause();
            state = setup.ExportState();
        }
        var bytes = WorldRuntimeCodec.Encode(state);
        var choices = new MarketRulesPolicy
        {
            Choose = (id, candidates) => id == actor
                ? candidates.FirstOrDefault(candidate => candidate.Id == (ownerOrder ? "repair_tool" : "repair_tool:" + Tool)) ??
                    candidates.Single(candidate => candidate.Id == "safe_idle")
                : candidates.Single(candidate => candidate.Id == "safe_idle"),
        };
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), choices.CreateProvider);
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), choices.CreateProvider);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        world.Validate();
        world.Resume();
        replay.Resume();
        string? repairInstructionId = null;
        if (ownerOrder)
        {
            repairInstructionId = world.SubmitInstruction(new("broken-owner-repair", "owner:test", actor, OwnerInstructionKind.MustDo, "repair crude wooden pickaxe")).InstructionId;
            replay.SubmitInstruction(new("broken-owner-repair", "owner:test", actor, OwnerInstructionKind.MustDo, "repair crude wooden pickaxe"));
        }
        var before = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
        for (var tick = 0; tick < 8; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            world.Validate();
            replay.Validate();
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
        var broken = harvests == 3;
        Assert.Equal(broken ? 0 : 10_000, world.Society.Inventory.GetLot(Tool).ConditionBasisPoints);
        Assert.Equal(broken ? 1 : 0, world.Society.Inventory.Lots.Where(lot => lot.OwnerId == actor && lot.ItemKind == "wood").Sum(lot => lot.Quantity));
        Assert.Equal(broken ? 0 : 1, world.ExportState().Events.Count(item => item.Kind == "tool_repaired"));
        if (broken && !ownerOrder)
            Assert.DoesNotContain(choices.OfferedTo(actor), candidate => candidate.Id == "repair_tool:" + Tool);
        if (ownerOrder)
            Assert.Equal("blocked", world.ExportState().Instructions!.Single(item => item.InstructionId == repairInstructionId).Order!.Status);
        bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes));
        restored.Validate();
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
    }

    private static async Task FinishOrderAsync(WorldRuntime world, string actor, string key, string text)
    {
        var receipt = world.SubmitInstruction(new(key, "owner:test", actor, OwnerInstructionKind.MustDo, text));
        for (var tick = 0; tick < 100 && world.ExportState().Instructions!.Single(item => item.InstructionId == receipt.InstructionId).Order!.Status != "finished"; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            world.Validate();
        }
        Assert.Equal("finished", world.ExportState().Instructions!.Single(item => item.InstructionId == receipt.InstructionId).Order!.Status);
    }
}

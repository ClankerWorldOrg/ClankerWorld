using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Tests;

public sealed partial class TownLandTransferRuntimeTests
{
    [Theory]
    [InlineData(128)]
    [InlineData(129)]
    [InlineData(512)]
    public async Task ImportedResourcePaysForActualLandPermissionAcrossReload(int length)
    {
        var kind = new string('x', length);
        var provider = new TransferProvider { Sell = true, AllowPropose = true, PaySales = false, SaleItemKind = kind, SaleQuantity = 1 };
        WorldRuntimeState state;
        using (var generated = NewWorld(provider))
        {
            Configure(provider, generated);
            var house = generated.WorldSimulation.Buildings.First(building => building.HouseholdId == provider.SourceHouseholdId &&
                generated.WorldContent.Buildings.Single(definition => definition.CanonicalId == building.DefinitionId).Tags.Contains("house"));
            state = await ContentIdFixture.Produce(generated.ExportState(), house.InstanceId, Filer, kind);
        }
        var produced = Assert.Single(state.Society.Society.Inventory.Lots, lot => lot.ItemKind == kind);
        // Transfer the genuinely produced unit; preserve native title/permissions and all adult parties.
        var inventory = InventoryAuthority.Transfer(state.Society.Society.Inventory, "mod-land-payment-gift",
            produced.OwnerId, Beneficiary, produced.Id, 1, "gift");
        state = state with
        {
            Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } },
            Agents = state.Agents.Select(person => person with
            { Position = state.Towns![0].OriginSite!.Value, Project = null, LastDecisionContext = null }).ToArray(),
        };
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)), _ => provider);
        var permissions = Permissions(world.ExportState());
        await UntilAsync(world, () => world.Towns[0].LandHearings.Transfers.Count == 1, 12, provider);
        var offered = Assert.Single(world.Towns[0].LandHearings.Transfers);
        Assert.Equal(kind, offered.Price!.ItemKind);
        await UntilAsync(world, () => world.Towns[0].LandHearings.Transfers[0].Responses.Count == 4, 40, provider);
        Assert.Equal(permissions, Permissions(world.ExportState()));
        var saved = ContentIdFixture.Saved(world);
        using var restored = WorldRuntime.Restore(saved, _ => provider);
        provider.PaySales = true;
        foreach (var actor in offered.Parties.SelectMany(party => party.AdultIds))
            restored.SubmitInstruction(new("mod-land-pay-" + actor, "owner:test", actor, OwnerInstructionKind.Suggestive, "Consider completing the agreed goods payment."));
        await UntilAsync(restored, () => restored.Towns[0].LandHearings.Transfers[0].Status == "transferred", 100, provider);
        var completed = Assert.Single(restored.Towns[0].LandHearings.Transfers);
        var payment = Assert.IsType<TownLandSalePayment>(completed.Receipt!.Payment);
        Assert.Equal(kind, completed.Price!.ItemKind);
        Assert.Equal(1, payment.Lots.Sum(lot => lot.Quantity));
        Assert.Equal(1, restored.Society.Inventory.Lots.Where(lot => lot.ItemKind == kind && lot.OwnerId == provider.SourceHouseholdId).Sum(lot => lot.Quantity));
        Assert.All(provider.Plot, tile => Assert.Contains(restored.HouseholdLandUseRights, right => right.HouseholdId == provider.TargetHouseholdId && right.Tiles.Contains(tile)));
        saved = ContentIdFixture.Saved(restored);
        using var replay = WorldRuntime.Restore(saved, _ => provider.ReplayPolicy());
        for (var tick = 0; tick < 3; tick++)
        {
            Assert.True((await restored.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(restored.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
    }
}

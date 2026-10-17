namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ActionCheck CheckProjectMaterialSource(MapResource source, int? treeSeedQuantity = null)
    {
        if (!map.Resources.Contains(source) || resources.GetValueOrDefault(source.Id) != ResourceState.Available ||
            !EcologyRules.Harvest(worldSystems.Ecology.GetResource(source.Id), 1).IsValid ||
            treeSeedQuantity is { } seeds && seeds != (TreeGrowthRules.IsWoodTree(source.TreeKind) && worldSystems.Ecology.GetResource(source.Id).Quantity == 1
                ? TreeGrowthRules.TreeSeedsPerFelledTree : 0))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected project material source or harvest has changed");
        return new();
    }

    private ActionCheck CheckPlannedProjectHarvest(string actor, MapResource source, ToolGatheringPlan harvest,
        string? toolOwner, string? toolKind)
    {
        var selectedSource = CheckProjectMaterialSource(source, harvest.TreeSeedQuantity);
        if (!selectedSource.Allowed) return selectedSource;
        if (harvest.ToolLotId is { } toolId &&
            (RecheckGoods(new(GoodsUse.CarriedInputs, actor, GoodsOwners.One(toolOwner!), GoodsKinds.One(toolKind!)), toolId) is not { Lot: var tool } ||
                !ToolProgressionRules.IsTopLevelCarriedLot(tool, actor)))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected project material tool is unavailable");
        return new();
    }
}

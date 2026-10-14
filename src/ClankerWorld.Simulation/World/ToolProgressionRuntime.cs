using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private const string RepairToolPrefix = ToolRepairChoice.Prefix;
    private const string RareMiningPrefix = "gather_rare_material:";
    // One trial load before a household needs another use for these goods.
    private const int RareMaterialStockTarget = 8;

    private MapResource? RareMiningSource(string actor, string itemKind)
    {
        if (itemKind is not ("gold_ore" or "diamond") || !IsAdult(actor) ||
            !agents.TryGetValue(actor, out var state) || NeedsUrgentFood(state) ||
            NeedsUrgentWarmth(state) || state.Project is { Stage: not ("completed" or "cancelled"), RequiresFreshChoice: false } ||
            ToolProgressionRules.BestUsableTool(society.Checkpoint.Inventory, actor, ToolFamily.Pickaxe) is not { } pick ||
            ToolProgressionRules.Find(pick.ItemKind)!.Tier < 3)
            return null;
        var household = HouseholdFor(actor);
        var stock = society.Checkpoint.Inventory.Lots.Where(lot => lot.ItemKind == itemKind &&
                (lot.OwnerId == actor || lot.OwnerId == household)).Sum(lot => lot.Quantity);
        if (stock >= RareMaterialStockTarget)
            return null;
        var source = MaterialSource(itemKind, actor);
        var plan = source is null ? null : ProjectMaterialHarvest(actor, itemKind, source);
        return plan is not null && FreeCarryCapacity(actor) >= plan.Quantity + plan.TreeSeedQuantity
            ? source : null;
    }

    private void AddRareMiningCandidates(List<CognitionCandidate> candidates, string actor)
    {
        foreach (var itemKind in new[] { "gold_ore", "diamond" })
            if (RareMiningSource(actor, itemKind) is { } source)
                candidates.Add(new(RareMiningPrefix + itemKind,
                    $"Mine {itemKind.Replace('_', ' ')} with the carried iron pickaxe.", 36, source.Id));
    }

    private void GatherRareMaterial(string actor, AgentState state, string itemKind)
    {
        if (RareMiningSource(actor, itemKind) is { } source)
            GatherProjectMaterial(actor, state, itemKind, source);
    }

    private bool CanGatherFromSource(string actor, string itemKind, MapResource source,
        Dictionary<(ToolFamily Family, int Tier), ToolDefinition?> reachableToolCache)
    {
        var inventory = society.Checkpoint.Inventory;
        var quantity = worldSystems.Ecology.GetResource(source.Id).Quantity;
        if (ToolProgressionRules.PlanGather(itemKind, source, inventory, actor, quantity) is not null)
            return true;
        var required = ToolProgressionRules.RequiredToolForGathering(itemKind, source);
        if (required is null) return true;
        var key = (required.Family, required.Tier);
        if (!reachableToolCache.TryGetValue(key, out var available))
        {
            available = FindReachableSharedGatheringTool(actor, required);
            reachableToolCache.Add(key, available);
        }
        return available is not null;
    }

    private int AvailableGatherQuantity(string actor, string itemKind, MapResource source,
        Dictionary<(ToolFamily Family, int Tier), ToolDefinition?>? reachableToolCache = null)
    {
        var inventory = society.Checkpoint.Inventory;
        var quantity = worldSystems.Ecology.GetResource(source.Id).Quantity;
        var plan = ToolProgressionRules.PlanGather(itemKind, source, inventory, actor, quantity);
        if (plan is not null) return plan.Quantity;
        if (RequiredGatheringTool(itemKind, source) is not { } required) return 0;
        var key = (required.Family, required.Tier);
        if (reachableToolCache is not null && reachableToolCache.TryGetValue(key, out var cached))
            return cached?.GatherQuantity ?? 0;
        var available = FindReachableSharedGatheringTool(actor, required);
        reachableToolCache?.Add(key, available);
        return available?.GatherQuantity ?? 0;
    }

    private static ToolDefinition? RequiredGatheringTool(string itemKind, MapResource source) =>
        ToolProgressionRules.RequiredToolForGathering(itemKind, source);

    private ToolDefinition? FindReachableSharedGatheringTool(string actor, ToolDefinition required)
    {
        return ToolProgressionRules.All
            .Where(definition => definition.Family == required.Family && definition.Tier >= required.Tier)
            .Select(definition => (Definition: definition, Lot: SharedPreparationItem(definition.ItemKind, actor)))
            .Where(item => item.Lot is { ContainerLotId: null, ConditionBasisPoints: > 0, FreshnessBasisPoints: > 0 })
            .OrderByDescending(item => item.Definition.Tier)
            .ThenByDescending(item => item.Lot!.ConditionBasisPoints)
            .ThenBy(item => item.Lot!.Id, StringComparer.Ordinal)
            .Select(item => item.Definition)
            .FirstOrDefault();
    }

    private bool CollectToolForGathering(string actor, AgentState state,
        string itemKind, MapResource source)
    {
        if (ToolProgressionRules.PlanGather(itemKind, source, society.Checkpoint.Inventory, actor,
                worldSystems.Ecology.GetResource(source.Id).Quantity) is not null ||
            RequiredGatheringTool(itemKind, source) is not { } required ||
            FindReachableSharedGatheringTool(actor, required) is not { } available)
            return false;
        CollectEquipment(actor, state, available.ItemKind);
        return true;
    }

    private void ApplyGatheringInventory(string actor, string itemKind, ToolGatheringPlan plan,
        string? deliveryBuildingId)
    {
        ApplyInventoryTransition(inventory =>
        {
            var updated = inventory;
            var wornToolLotId = plan.ToolLotId;
            if (wornToolLotId is not null)
            {
                var tool = updated.GetLot(wornToolLotId);
                if (tool.Quantity > 1)
                {
                    wornToolLotId = UsedToolLotId(tool.Id, WorldTick, actor);
                    updated = InventoryAuthority.SplitLot(updated, tool.Id, 1, wornToolLotId, holdAccess: InventoryHoldRules.BorrowedBy(actor));
                }
            }

            var materialLotId = MaterialLotId(WorldTick, actor);
            updated = MarkBuildingMaterialDelivery(InventoryAuthority.AddLot(updated, materialLotId,
                itemKind, actor, plan.Quantity, WorldTick), materialLotId, deliveryBuildingId);
            if (plan.TreeSeedQuantity > 0)
            {
                var seedLotId = TreeSeedLotId(WorldTick, actor);
                updated = MarkBuildingMaterialDelivery(InventoryAuthority.AddLot(updated, seedLotId,
                    TreeGrowthRules.TreeSeedItem, actor, plan.TreeSeedQuantity, WorldTick), seedLotId,
                    deliveryBuildingId);
            }
            if (wornToolLotId is not null && plan.WearLossBasisPoints > 0)
                updated = InventoryAuthority.WearSingleUnit(updated, wornToolLotId, plan.WearLossBasisPoints, holdAccess: InventoryHoldRules.BorrowedBy(actor));
            return updated;
        });
    }

    private void ApplyToolWork(string actor, params ToolWorkPlan[] plans)
    {
        ApplyInventoryTransition(inventory => ApplyToolWorkToInventory(inventory, actor, WorldTick, plans));
    }

    private static InventoryCheckpoint ApplyToolWorkToInventory(InventoryCheckpoint inventory, string actor,
        long tick, IReadOnlyList<ToolWorkPlan> plans)
    {
        var updated = inventory;
        foreach (var plan in plans)
        {
            var lot = updated.GetLot(plan.ToolLotId);
            var definition = ToolProgressionRules.Find(lot.ItemKind);
            if (definition is null || ToolProgressionRules.PlanWorkForLot(updated, actor,
                    definition.Family, lot.Id) is null)
                throw new InvalidOperationException("The selected tool must remain usable and physically carried by its owner.");
            var usedLotId = lot.Id;
            if (lot.Quantity > 1)
            {
                usedLotId = UsedToolLotId(lot.Id, tick, actor);
                updated = InventoryAuthority.SplitLot(updated, lot.Id, 1, usedLotId, holdAccess: InventoryHoldRules.BorrowedBy(actor));
            }
            updated = InventoryAuthority.WearSingleUnit(updated, usedLotId, plan.WearLossBasisPoints, holdAccess: InventoryHoldRules.BorrowedBy(actor));
        }
        return updated;
    }

    private void AddToolRepairCandidates(List<CognitionCandidate> candidates, string actor)
    {
        if (HouseholdFor(actor) is not { } household) return;
        foreach (var tool in CarriedRepairTools(actor, household))
        {
            if (PlanToolRepair(actor, tool.Id, tool) is not { } plan || !CheckActionPlan(plan).Allowed) continue;
            candidates.Add(new CognitionCandidate(plan.CandidateId,
                $"Repair the worn {tool.ItemKind.Replace('_', ' ')} at the household Blacksmith.", 23,
                plan.SmithId)
            { ActionPlan = plan });
        }
    }

    private InventoryLot[] RepairableTools(string actor, IReadOnlyList<string>? protectedToolIds = null)
    {
        if (!IsAdult(actor) || society.Checkpoint.GetAgent(actor).HouseholdId is not { } householdId ||
            HouseholdBuildingWithTag(householdId, "blacksmith") is not { } blacksmith ||
            !agents.TryGetValue(actor, out var state))
            return [];

        var inventory = society.Checkpoint.Inventory;
        // Ordinary repair includes borrowed household tools; personal orders filter ownership further.
        var tools = inventory.Lots.Where(lot => ToolProgressionRules.IsTopLevelCarriedLot(lot, actor) &&
                (lot.OwnerId == actor || lot.OwnerId == householdId) &&
                ToolProgressionRules.Find(lot.ItemKind) is not null &&
                lot.ConditionBasisPoints is > 0 and < 10_000 && UnreservedQuantity(inventory, lot) > 0)
            .OrderBy(lot => lot.Id, StringComparer.Ordinal)
            .Where(tool => ToolProgressionRules.RepairMaterials(tool.ItemKind) is { Count: > 0 } materials &&
                CanPrepareRepairInputs(actor, tool.Id, materials, blacksmith.Position, protectedToolIds)).ToArray();
        if (tools.Length == 0 || !IsWithinInteractionRange(state.Position, blacksmith.Position, 0) &&
            FindUnoccupiedRoute(actor, state.Position, blacksmith.Position, 0).Count == 0)
            return [];
        return tools;
    }

    private bool CanPrepareRepairInputs(string actor, string repairToolId,
        IReadOnlyList<ContentQuantity> repairMaterials, GridPoint destination, IReadOnlyList<string>? protectedToolIds = null)
    {
        if (!agents.TryGetValue(actor, out var state)) return false;

        var inventory = society.Checkpoint.Inventory;
        var harvestToolIds = new List<string>();
        var requiredCarryUnits = 0;
        foreach (var input in repairMaterials)
        {
            var carried = inventory.Lots.Where(lot => ToolProgressionRules.IsTopLevelCarriedLot(lot, actor) && lot.OwnerId == actor &&
                    lot.ItemKind == input.ResourceId)
                .Sum(AvailableLotQuantity);
            var missing = Math.Max(0, input.Amount - carried);
            if (missing == 0) continue;

            if (SharedPreparationItem(input.ResourceId, actor, destination) is { } shared)
            {
                if (AvailableLotQuantity(shared) < missing)
                    return false;
                requiredCarryUnits = checked(requiredCarryUnits + missing);
                continue;
            }

            if (MaterialSource(input.ResourceId, actor, destination) is not { } source ||
                ProjectMaterialHarvest(actor, input.ResourceId, source) is not { } plan)
                return false;
            if (plan.ToolLotId is { } harvestToolId && protectedToolIds?.Contains(harvestToolId, StringComparer.Ordinal) == true &&
                inventory.GetLot(harvestToolId).ConditionBasisPoints <= plan.WearLossBasisPoints)
                return false;
            requiredCarryUnits = checked(requiredCarryUnits + plan.Quantity + plan.TreeSeedQuantity);
            if (plan.ToolLotId is not null)
                harvestToolIds.Add(plan.ToolLotId);
        }

        var missingCarryUnits = checked(requiredCarryUnits - FreeCarryCapacity(actor));
        if (missingCarryUnits <= 0)
            return true;

        var cargo = SpareCargoForToolRepair(actor, missingCarryUnits, repairToolId, repairMaterials,
            harvestToolIds.Concat(protectedToolIds ?? []).Distinct(StringComparer.Ordinal).ToArray());
        return cargo.Count > 0 && SpareCargoDestination(actor, state,
            cargo.Sum(move => move.PhysicalQuantity)) is not null;
    }

    private Dictionary<string, int> CarriedRepairMaterialQuantities(string actor, IReadOnlyList<ContentQuantity> repairMaterials)
    {
        var retained = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var input in repairMaterials)
        {
            var remaining = input.Amount;
            foreach (var lot in society.Checkpoint.Inventory.Lots.Where(lot =>
                         ToolProgressionRules.IsTopLevelCarriedLot(lot, actor) && lot.OwnerId == actor && lot.ItemKind == input.ResourceId)
                         .OrderBy(lot => lot.Id, StringComparer.Ordinal))
            {
                var quantity = Math.Min(remaining, AvailableLotQuantity(lot));
                if (quantity > 0) retained.Add(lot.Id, quantity);
                remaining -= quantity;
                if (remaining == 0) break;
            }
        }
        return retained;
    }

    private List<SpareCargoMove> SpareCargoForToolRepair(string actor, int missing, string repairToolId,
        IReadOnlyList<ContentQuantity> repairMaterials, IReadOnlyList<string>? protectedToolIds)
    {
        var cargo = OrdinarySpareCargo(actor, missing, repairToolId, protectedToolIds,
            CarriedRepairMaterialQuantities(actor, repairMaterials));
        return cargo.Sum(move => move.PhysicalQuantity) >= missing ? cargo : [];
    }

    private bool MakeRoomForToolRepairInput(string actor, AgentState state,
        string repairToolId, int requiredUnits, string? harvestToolId, IReadOnlyList<ContentQuantity> repairMaterials,
        IReadOnlyList<string>? protectedToolIds = null)
    {
        var missing = checked(requiredUnits - FreeCarryCapacity(actor));
        if (missing <= 0) return false;
        return StoreSpareCargo(actor, state, HouseholdFor(actor)!,
            SpareCargoForToolRepair(actor, missing, repairToolId, repairMaterials,
                (protectedToolIds ?? []).Concat(harvestToolId is null ? [] : new[] { harvestToolId }).ToArray()));
    }

    private List<InputCollectionStep>? ToolRepairCollectionInputs(string actor, IReadOnlyList<ContentQuantity> materials,
        GridPoint returnTo)
    {
        var inputs = new List<InputCollectionStep>();
        foreach (var input in materials)
        {
            if (HasCarriedMaterial(actor, input.ResourceId, input.Amount)) continue;
            if (SharedPreparationItem(input.ResourceId, actor, returnTo) is { } stock)
                inputs.Add(StockCollectionStep(stock, input.Amount));
            else if (MaterialSource(input.ResourceId, actor, returnTo) is { } source &&
                ProjectMaterialHarvest(actor, input.ResourceId, source) is { } harvest)
                inputs.Add(new(input.ResourceId, source.Position, ResourceInteractionRange,
                    checked(harvest.Quantity + harvest.TreeSeedQuantity), Harvest: source, HarvestToolLotId: harvest.ToolLotId));
            else return null;
        }
        return inputs;
    }

    private string? RepairTool(string actor, AgentState state, string lotId,
        IReadOnlyList<string>? protectedToolIds = null)
    {
        var inventory = society.Checkpoint.Inventory;
        var tool = inventory.Lots.FirstOrDefault(lot => lot.Id == lotId &&
            ToolProgressionRules.IsTopLevelCarriedLot(lot, actor) &&
            (lot.OwnerId == actor || lot.OwnerId == society.Checkpoint.GetAgent(actor).HouseholdId) &&
            ToolProgressionRules.Find(lot.ItemKind) is not null && lot.ConditionBasisPoints is > 0 and < 10_000 &&
            UnreservedQuantity(inventory, lot) > 0);
        var householdId = society.Checkpoint.GetAgent(actor).HouseholdId;
        var blacksmith = householdId is null ? null : HouseholdBuildingWithTag(householdId, "blacksmith");
        if (tool is null || blacksmith is null)
            return null;

        var materialNeeds = ToolProgressionRules.RepairMaterials(tool.ItemKind);
        var collectionInputs = ToolRepairCollectionInputs(actor, materialNeeds, blacksmith.Position);
        if (collectionInputs is null)
        {
            // A gathering tool may need collecting before the whole harvest's
            // load can be known. Do that preparation without taking other inputs.
            foreach (var input in materialNeeds)
            {
                if (HasCarriedMaterial(actor, input.ResourceId, input.Amount) ||
                    SharedPreparationItem(input.ResourceId, actor) is not null) continue;
                if (MaterialSource(input.ResourceId, actor, blacksmith.Position) is { } source &&
                    ProjectMaterialHarvest(actor, input.ResourceId, source) is null)
                {
                    GatherProjectMaterial(actor, state, input.ResourceId, source, returnTo: blacksmith.Position);
                    break;
                }
            }
            return null;
        }
        var requiredUnits = collectionInputs.Sum(input => input.CarryUnits);
        if (requiredUnits > FreeCarryCapacity(actor))
        {
            var protectedIds = (protectedToolIds ?? []).Concat(collectionInputs
                .Where(input => input.HarvestToolLotId is not null).Select(input => input.HarvestToolLotId!))
                .Distinct(StringComparer.Ordinal).ToArray();
            MakeRoomForToolRepairInput(actor, state, lotId, requiredUnits, null, materialNeeds, protectedIds);
            return null;
        }
        var collection = PlanInputCollection(actor, collectionInputs, blacksmith.Position);
        if (collection is null) return null;
        if (collection.Count > 0)
        {
            var input = collection[0];
            if (input.Stock is { } stock)
                CollectEquipment(actor, state, input.Kind, blacksmith.Position, plannedLotId: stock.Id);
            else if (input.Harvest is { } source)
                GatherProjectMaterial(actor, state, input.Kind, source, returnTo: blacksmith.Position);
            return null;
        }

        if (state.Position != blacksmith.Position)
        {
            MoveToward(actor, state, blacksmith.Position, "repair_tool", 0);
            return null;
        }

        var selectedMaterials = materialNeeds.Select(input =>
        {
            var material = SpendingGoods(actor, GoodsUse.Spend, [actor], input.ResourceId)
                .FirstOrDefault(match => ToolProgressionRules.IsTopLevelCarriedLot(match.Lot, actor) && match.Quantity >= input.Amount)?.Lot
                ?? throw new InvalidOperationException("Repair materials must be physically carried by the tool owner.");
            return new PlannedRepairMaterial(material.Id, input.ResourceId, input.Amount);
        }).ToArray();
        return CompletePlannedToolRepair(actor, state, tool, blacksmith.InstanceId, selectedMaterials);
    }

    private string CompletePlannedToolRepair(string actor, AgentState state, InventoryLot tool,
        string blacksmithId, IReadOnlyList<PlannedRepairMaterial> materials)
    {
        var repairId = tool.Id;
        var reservations = new List<string>();
        ApplyInventoryTransition(current =>
        {
            var updated = current;
            var currentTool = updated.GetLot(repairId);
            if (currentTool.Quantity > 1)
            {
                repairId = RepairedToolLotId(currentTool.Id, WorldTick, actor);
                updated = InventoryAuthority.SplitLot(updated, currentTool.Id, 1, repairId, holdAccess: InventoryHoldRules.BorrowedBy(actor));
            }

            foreach (var input in materials)
            {
                var material = updated.GetLot(input.StockId);
                var reservationId = ToolRepairReservationId(WorldTick, actor, repairId, input.ItemKind);
                var materialId = material.Id;
                if (currentTool.OwnerId != actor)
                {
                    // Repairing a borrowed household tool, the member gives their materials to the household.
                    var giftId = ToolRepairGiftId(WorldTick, actor, repairId, input.ItemKind);
                    updated = InventoryAuthority.Transfer(updated, giftId, actor, currentTool.OwnerId, material.Id,
                        input.Quantity, "equipment_repair_materials", holdAccess: InventoryHoldRules.BorrowedBy(actor));
                    if (input.Quantity < material.Quantity) materialId = DerivedId.Create($"{material.Id}#transfer:{giftId}");
                }
                updated = InventoryAuthority.Reserve(updated, reservationId, currentTool.OwnerId, materialId,
                    input.Quantity, "equipment_repair", checked(WorldTick + 1), holdAccess: InventoryHoldRules.BorrowedBy(actor));
                reservations.Add(reservationId);
            }

            return InventoryAuthority.RepairSingleUnit(updated, repairId, 10_000, reservations.Select(Identity.Parse).ToArray(), holdAccess: InventoryHoldRules.BorrowedBy(actor));
        });
        RecordNonLandRepairCompletion(actor, tool, repairId,
            $"repair-tool:{WorldTick}:{actor}:{repairId}", state.Position, reservations.Select(Identity.Parse).ToArray());
        AppendEvent(new ToolRepairedEventPayload(actor, repairId, blacksmithId));
        return repairId;
    }

    private bool HasCarriedMaterial(string actor, string itemKind, int quantity) =>
        SpendingGoods(actor, GoodsUse.Spend, [actor], itemKind)
            .Where(match => ToolProgressionRules.IsTopLevelCarriedLot(match.Lot, actor))
            .Sum(match => (long)match.Quantity) >= quantity;

    private static int UnreservedQuantity(InventoryCheckpoint inventory, InventoryLot lot)
    {
        return Math.Max(0, InventoryRules.UnreservedQuantity(InventoryIndex.For(inventory), lot));
    }

}

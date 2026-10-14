using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private const string GatherBlacksmithInputPrefix = SmithGatherChoice.InputPrefix;
    private const string CollectToolPrefix = "collect_tool:";

    private bool MayCollectToolFamily(string actor, ToolFamily family) =>
        family is not (ToolFamily.Hoe or ToolFamily.Sickle) ||
        HouseholdFor(actor) is { } householdId && FarmhouseForHousehold(householdId) is not null;

    private void AddCraftToolCandidates(List<CognitionCandidate> candidates, string actor)
    {
        if (!IsAdult(actor)) return;
        AddToolRepairCandidates(candidates, actor);
        if (FreeCarryCapacity(actor) <= 0) return;
        var inventory = society.Checkpoint.Inventory;
        var pendingHarvest = HouseholdFor(actor) is { } householdId &&
            BlacksmithForHousehold(householdId) is { } smith &&
            BlacksmithHarvestToPrepare(householdId, smith.InstanceId, actor) is { } missing
            ? ProjectMaterialHarvest(actor, missing.ItemKind, missing.Source) : null;
        var pendingToolFamily = pendingHarvest?.ToolLotId is { } requiredToolId
            ? ToolProgressionRules.Find(inventory.GetLot(requiredToolId).ItemKind)?.Family : null;
        foreach (var family in ToolProgressionRules.All.GroupBy(tool => tool.Family)
                     .OrderBy(group => group.Key))
        {
            // Field tools are useful only to a household that can work its fields.
            if (!MayCollectToolFamily(actor, family.Key)) continue;
            var bestShared = family.Where(tool => SharedItem(tool.ItemKind, actor) is { ContainerLotId: null })
                .OrderByDescending(tool => tool.Tier)
                .ThenBy(tool => tool.ItemKind, StringComparer.Ordinal)
                .FirstOrDefault();
            if (bestShared is null) continue;
            // Leave room for the pending whole harvest. Otherwise a spare tool
            // set down for that work would be immediately collected again.
            if (pendingHarvest is { } harvest && bestShared.Family != pendingToolFamily &&
                FreeCarryCapacity(actor) - 1 < checked(harvest.Quantity + harvest.TreeSeedQuantity))
                continue;
            if (pendingHarvest is { } upgradedHarvest && bestShared.Family == pendingToolFamily)
            {
                var roomMissing = checked(bestShared.GatherQuantity + upgradedHarvest.TreeSeedQuantity) -
                    (FreeCarryCapacity(actor) - 1);
                if (roomMissing > 0)
                {
                    // The old tool becomes spare after the upgrade. The new
                    // shared tool is not carried yet and cannot be set down.
                    var cargo = SpareCargoForFood(actor, roomMissing);
                    if (cargo.Count == 0 || SpareCargoDestination(actor, agents[actor],
                            cargo.Sum(move => move.PhysicalQuantity)) is null)
                        continue;
                }
            }

            var carried = ToolProgressionRules.BestUsableTool(inventory, actor, family.Key);
            if (carried is not null && ToolProgressionRules.Find(carried.ItemKind)!.Tier >= bestShared.Tier)
                continue;

            var candidateId = bestShared.ItemKind switch
            {
                "wooden_axe" => "collect_wooden_axe",
                "wooden_pickaxe" => "collect_wooden_pickaxe",
                "wooden_hoe" => "collect_wooden_hoe",
                _ => CollectToolPrefix + bestShared.ItemKind,
            };
            candidates.Add(new CognitionCandidate(candidateId,
                $"Collect an accessible {bestShared.ItemKind.Replace('_', ' ')} for work.", 18));
        }
    }

    private PlacedBuilding? BlacksmithForHousehold(string householdId) =>
        HouseholdBuildingWithTag(householdId, "blacksmith");

    private int BlacksmithOreStocked(string householdId, string blacksmithId) =>
        BlacksmithInputStocked(householdId, blacksmithId, "iron_ore");

    private int BlacksmithInputStocked(string householdId, string blacksmithId, string itemKind) =>
        SmithStoredQuantity(householdId, blacksmithId, itemKind);

    private (string ItemKind, int Target)[] BlacksmithInputTargets(string blacksmithId)
    {
        var placed = worldSimulation.Buildings.SingleOrDefault(building => building.InstanceId == blacksmithId);
        if (placed is null) return [];
        // A handcart is built only from what its builder carries, so the Blacksmith keeps no stock for it.
        return worldContent.Recipes.Where(recipe => recipe.WorkstationBuildingId == placed.DefinitionId && !IsHandcartRecipe(recipe))
            .SelectMany(recipe => recipe.Inputs)
            .GroupBy(input => input.ResourceId, StringComparer.Ordinal)
            .Select(group => (ItemKind: group.Key, Target: checked(group.Max(input => input.Amount) * 2)))
            .OrderBy(item => item.ItemKind, StringComparer.Ordinal)
            .ToArray();
    }

    private int BlacksmithInputTarget(string blacksmithId, string itemKind) =>
        BlacksmithInputTargets(blacksmithId).FirstOrDefault(item => item.ItemKind == itemKind).Target;

    private InventoryLot? PersonalSmithOre(string actor) => FindGoods(new(GoodsUse.CarriedInputs, actor,
        GoodsOwners.One(actor), GoodsKinds.One("iron_ore"))).Matches.Select(match => match.Lot)
        .Where(lot => lot.ContainerLotId is null).OrderBy(lot => lot.Id, StringComparer.Ordinal).FirstOrDefault();

    private void AddBlacksmithOreCandidates(List<CognitionCandidate> candidates, string actor,
        AgentState state)
    {
        var householdId = society.Checkpoint.GetAgent(actor).HouseholdId;
        if (!IsAdult(actor) || householdId is null || CarriedHouseDelivery(actor) is not null ||
            BlacksmithForHousehold(householdId) is not { } blacksmith ||
            DestinationRoom(blacksmith.InstanceId) == 0 ||
            BlacksmithOreStocked(householdId, blacksmith.InstanceId) >= BlacksmithInputTarget(blacksmith.InstanceId, "iron_ore"))
            return;
        if (PersonalSmithOre(actor) is { } ore)
        {
            if (PlanSmithDelivery(actor, SmithDeliveryKind.Ore, ore, blacksmith) is { } plan && CheckActionPlan(plan).Allowed)
                candidates.Add(new CognitionCandidate("deliver_smith_ore",
                    "Carry mined iron ore into the household Blacksmith.", 22, blacksmith.InstanceId)
                { ActionPlan = plan });
            return;
        }
        if (BlacksmithHasDeliverableInput(householdId, blacksmith, actor, "iron_ore"))
            return;
        if (PlanSmithGather(actor, "iron_ore", selectedSmith: blacksmith) is not { } gather || !CheckActionPlan(gather).Allowed)
            return;
        candidates.Add(new CognitionCandidate(gather.CandidateId,
            "Mine iron ore for the household Blacksmith.", 30, gather.Source.Id)
        { ActionPlan = gather });
    }

    private void GatherBlacksmithOre(string actor, AgentState state) => ApplySmithGatherPlan(PlanSmithGather(actor, "iron_ore"));

    private void DeliverBlacksmithOre(string actor, AgentState state) => ApplySmithDeliveryPlan(PlanSmithDelivery(actor, SmithDeliveryKind.Ore));

    private InventoryLot? BlacksmithInputForDelivery(string householdId, PlacedBuilding blacksmith, string actor,
        string? itemKind = null, string? sourceLotId = null)
    {
        var blacksmithId = blacksmith.InstanceId;
        foreach (var (kind, target) in BlacksmithInputTargets(blacksmithId))
        {
            if (itemKind is not null && kind != itemKind) continue;
            var stocked = SmithStoredQuantity(householdId, blacksmithId, kind);
            var incoming = SmithIncomingQuantity(blacksmithId, kind);
            if (stocked + incoming >= target) continue;
            var personal = FindGoods(DeliveryGoodsRequest(actor, actor, kind, blacksmithId)).Matches
                .Select(match => match.Lot)
                .FirstOrDefault(lot => PersonalEquipmentRules.IsCarried(lot, actor) &&
                    (sourceLotId is null || lot.Id == sourceLotId) && lot.ContainerLotId is null &&
                    SpareCarriedQuantity(actor, lot) > 0);
            if (personal is not null) return personal;
            var source = FindGoods(DeliveryGoodsRequest(actor, householdId, kind, blacksmithId)).Matches
                .Select(match => match.Lot)
                .FirstOrDefault(lot => lot.CarrierId is null && lot.ContainerLotId is null &&
                    (sourceLotId is null || lot.Id == sourceLotId));
            source ??= AvailableWarehouseStock(actor, kind).FirstOrDefault(lot =>
                (sourceLotId is null || lot.Id == sourceLotId) &&
                RecheckGoods(DeliveryGoodsRequest(actor, lot.OwnerId, kind, blacksmithId), lot.Id) is not null);
            if (source is not null) return source;
        }
        return null;
    }

    private bool BlacksmithHasDeliverableInput(string householdId, PlacedBuilding blacksmith, string actor, string itemKind)
    {
        if (DestinationRoom(blacksmith.InstanceId) <= 0 ||
            BlacksmithInputForDelivery(householdId, blacksmith, actor, itemKind) is not { } input)
            return false;
        if (input.OwnerId != actor) return FreeCarryCapacity(actor) > 0;
        var position = agents[actor].Position;
        return position == blacksmith.Position || FindUnoccupiedRoute(actor, position, blacksmith.Position, 0).Count > 0;
    }

    private (string ItemKind, MapResource Source)? BlacksmithInputToGather(string householdId, string blacksmithId,
        string actor, string? itemKind = null)
    {
        var blacksmith = worldSimulation.Buildings.SingleOrDefault(building => building.InstanceId == blacksmithId);
        if (blacksmith is null || DestinationRoom(blacksmithId) <= 0) return null;
        foreach (var (kind, target) in BlacksmithInputTargets(blacksmithId))
        {
            if (itemKind is not null && kind != itemKind) continue;
            if (kind == "iron_ore") continue;
            var stocked = SmithStoredQuantity(householdId, blacksmithId, kind);
            var incoming = SmithIncomingQuantity(blacksmithId, kind);
            if (stocked + incoming >= target || BlacksmithHasDeliverableInput(householdId, blacksmith, actor, kind))
                continue;
            if (MaterialSource(kind, actor, blacksmith.Position) is { } source)
                return (kind, source);
        }
        return null;
    }

    private void GatherBlacksmithInput(string actor, AgentState state, string itemKind) => ApplySmithGatherPlan(PlanSmithGather(actor, itemKind));

    private (string ItemKind, MapResource Source)? BlacksmithHarvestToPrepare(string householdId,
        string blacksmithId, string actor)
    {
        var blacksmith = worldSimulation.Buildings.SingleOrDefault(building => building.InstanceId == blacksmithId);
        if (blacksmith is null || DestinationRoom(blacksmithId) <= 0) return null;
        if (BlacksmithInputToGather(householdId, blacksmithId, actor) is { } missing)
            return missing;
        if (PersonalSmithOre(actor) is not null ||
            BlacksmithOreStocked(householdId, blacksmithId) >= BlacksmithInputTarget(blacksmithId, "iron_ore") ||
            BlacksmithHasDeliverableInput(householdId, blacksmith, actor, "iron_ore"))
            return null;
        return MaterialSource("iron_ore", actor, blacksmith.Position) is { } source ? ("iron_ore", source) : null;
    }

    private void AddBlacksmithStockCandidate(List<CognitionCandidate> candidates, string actor,
        AgentState state)
    {
        var householdId = society.Checkpoint.GetAgent(actor).HouseholdId;
        if (!IsAdult(actor) || householdId is null || CarriedHouseDelivery(actor) is not null ||
            BlacksmithForHousehold(householdId) is not { } blacksmith)
        {
            if (IsAdult(actor) && householdId is not null && CarriedHouseDelivery(actor) is null &&
                BlacksmithForHousehold(householdId) is { } gatheringSmith &&
                BlacksmithInputToGather(householdId, gatheringSmith.InstanceId, actor) is { } missing)
            {
                if (PlanSmithGather(actor, missing.ItemKind, missing.Source, gatheringSmith) is { } gather && CheckActionPlan(gather).Allowed)
                    candidates.Add(new CognitionCandidate(gather.CandidateId,
                        $"Gather {missing.ItemKind.Replace('_', ' ')} for the household Blacksmith.", 30,
                        missing.Source.Id)
                    { ActionPlan = gather });
            }
            return;
        }
        if (DestinationRoom(blacksmith.InstanceId) == 0)
            return;
        var input = BlacksmithInputForDelivery(householdId, blacksmith, actor);
        if (input is null)
        {
            if (BlacksmithInputToGather(householdId, blacksmith.InstanceId, actor) is { } missing)
            {
                if (PlanSmithGather(actor, missing.ItemKind, missing.Source, blacksmith) is { } gather && CheckActionPlan(gather).Allowed)
                    candidates.Add(new CognitionCandidate(gather.CandidateId,
                        $"Gather {missing.ItemKind.Replace('_', ' ')} for the household Blacksmith.", 30,
                        missing.Source.Id)
                    { ActionPlan = gather });
            }
            return;
        }
        if (PlanSmithDelivery(actor, SmithDeliveryKind.Input, input, blacksmith) is not { } plan || !CheckActionPlan(plan).Allowed) return;
        candidates.Add(new CognitionCandidate("haul_smith_input",
            $"Carry {input.ItemKind.Replace('_', ' ')} into the household Blacksmith for on-site work.",
            24, blacksmith.InstanceId)
        { ActionPlan = plan });
    }

    private void HaulBlacksmithInput(string actor, AgentState state) => ApplySmithDeliveryPlan(PlanSmithDelivery(actor, SmithDeliveryKind.Input));

    private DeliveryOrderPlan? GetBlacksmithOrderPlan(string actor, AgentState person,
        OrderTask order, int maximumQuantity)
    {
        if (!IsAdult(actor) || HouseholdFor(actor) is not { } householdId ||
            BlacksmithForHousehold(householdId) is not { } blacksmith ||
            !DeliveryDestinationMatches(order, blacksmith) ||
            BlacksmithInputForDelivery(householdId, blacksmith, actor,
                order.TargetItemKind, order.DeliveryLotId) is not { } input)
            return null;
        var direct = input.OwnerId == actor;
        var stocked = BlacksmithInputStocked(householdId, blacksmith.InstanceId, input.ItemKind);
        var incoming = SmithIncomingQuantity(blacksmith.InstanceId, input.ItemKind);
        var missing = BlacksmithInputTarget(blacksmith.InstanceId, input.ItemKind) - stocked - incoming;
        var quantity = Math.Min(maximumQuantity, Math.Min(missing,
            Math.Min(DestinationRoom(blacksmith.InstanceId),
                direct ? SpareCarriedQuantity(actor, input) : AvailableLotQuantity(input))));
        // Personal mined ore has a dedicated native delivery without the loose-input load limit.
        if (!direct || input.ItemKind != "iron_ore") quantity = Math.Min(quantity, HouseHaulLoadQuantity);
        if (!direct) quantity = Math.Min(quantity, PickupCarryCapacity(actor, input, blacksmith.Position));
        return quantity <= 0 ? null : new DeliveryOrderPlan("blacksmith_input", blacksmith, householdId,
            input, input, quantity, quantity, DirectDelivery: direct);
    }
}

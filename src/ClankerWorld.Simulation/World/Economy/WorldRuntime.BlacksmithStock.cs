using DeliveryOrderPlan = ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private const string GatherBlacksmithInputPrefix = SmithGatherChoice.InputPrefix;

    private bool MayCollectToolFamily(string actor, ToolFamily family) =>
        family is not (ToolFamily.Hoe or ToolFamily.Sickle) ||
        HouseholdFor(actor) is { } householdId && FarmhouseForHousehold(householdId) is not null;

    private void AddCraftToolCandidates(List<CognitionCandidate> candidates, string actor)
    {
        if (!IsAdult(actor)) return;
        AddToolRepairCandidates(candidates, actor);
        if (FreeCarryCapacity(actor) <= 0) return;
        var pendingHarvest = PendingSmithHarvest(actor);
        foreach (var family in ToolProgressionRules.All.GroupBy(tool => tool.Family).OrderBy(group => group.Key))
        {
            if (!MayCollectToolFamily(actor, family.Key)) continue;
            var best = family.Select(tool => (Definition: tool, Stock: SharedItem(tool.ItemKind, actor)))
                .Where(item => item.Stock is { ContainerLotId: null }).OrderByDescending(item => item.Definition.Tier)
                .ThenBy(item => item.Definition.ItemKind, StringComparer.Ordinal).FirstOrDefault();
            if (best.Stock is null || PlanToolCollection(actor, best.Definition.ItemKind, best.Stock, pendingHarvest) is not { } plan ||
                !CheckActionPlan(plan).Allowed) continue;
            candidates.Add(new CognitionCandidate(plan.CandidateId,
                $"Collect an accessible {best.Definition.ItemKind.Replace('_', ' ')} for work.", 18)
            { ActionPlan = plan });
        }
    }

    private (string ItemKind, int Target)[] BlacksmithInputTargets(string blacksmithId)
    {
        var placed = worldSimulation.Buildings.SingleOrDefault(building => building.InstanceId == blacksmithId);
        if (placed is null) return [];
        // A handcart is built only from what its builder carries, so the Blacksmith keeps no stock for it.
        return worldContent.Recipes.Where(recipe => recipe.WorkstationBuildingId == placed.DefinitionId && !ProductionSystem.IsHandcartRecipe(recipe))
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
            this.HouseholdBuildingWithTag(householdId, "blacksmith") is not { } blacksmith ||
            DestinationRoom(blacksmith.InstanceId) == 0 ||
            this.SmithStoredQuantity(householdId, blacksmith.InstanceId, "iron_ore") >= BlacksmithInputTarget(blacksmith.InstanceId, "iron_ore"))
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
                    TransportSystem.SpareCarriedQuantity(this.HandcartWorld, actor, lot) > 0);
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
        return position == blacksmith.Position || Reach(actor, blacksmith.Position, ReachPurpose.SmithDelivery, 0, 0, SwimmingPolicy.WhenCapable).Count > 0;
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

    private (string ItemKind, MapResource Source)? BlacksmithHarvestToPrepare(string householdId,
        string blacksmithId, string actor)
    {
        var blacksmith = worldSimulation.Buildings.SingleOrDefault(building => building.InstanceId == blacksmithId);
        if (blacksmith is null || DestinationRoom(blacksmithId) <= 0) return null;
        if (BlacksmithInputToGather(householdId, blacksmithId, actor) is { } missing)
            return missing;
        if (PersonalSmithOre(actor) is not null ||
            this.SmithStoredQuantity(householdId, blacksmithId, "iron_ore") >= BlacksmithInputTarget(blacksmithId, "iron_ore") ||
            BlacksmithHasDeliverableInput(householdId, blacksmith, actor, "iron_ore"))
            return null;
        return MaterialSource("iron_ore", actor, blacksmith.Position) is { } source ? ("iron_ore", source) : null;
    }

    private void AddBlacksmithStockCandidate(List<CognitionCandidate> candidates, string actor,
        AgentState state)
    {
        var householdId = society.Checkpoint.GetAgent(actor).HouseholdId;
        if (!IsAdult(actor) || householdId is null || CarriedHouseDelivery(actor) is not null ||
            this.HouseholdBuildingWithTag(householdId, "blacksmith") is not { } blacksmith)
        {
            if (IsAdult(actor) && householdId is not null && CarriedHouseDelivery(actor) is null &&
                this.HouseholdBuildingWithTag(householdId, "blacksmith") is { } gatheringSmith &&
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

    private DeliveryOrderPlan? GetBlacksmithOrderPlan(string actor, AgentState person,
        OrderTask order, int maximumQuantity)
    {
        if (!IsAdult(actor) || HouseholdFor(actor) is not { } householdId ||
            this.HouseholdBuildingWithTag(householdId, "blacksmith") is not { } blacksmith ||
            !DeliveryDestinationMatches(order, blacksmith) ||
            BlacksmithInputForDelivery(householdId, blacksmith, actor,
                order.TargetItemKind, order.DeliveryLotId) is not { } input)
            return null;
        var direct = input.OwnerId == actor;
        var stocked = this.SmithStoredQuantity(householdId, blacksmith.InstanceId, input.ItemKind);
        var incoming = SmithIncomingQuantity(blacksmith.InstanceId, input.ItemKind);
        var missing = BlacksmithInputTarget(blacksmith.InstanceId, input.ItemKind) - stocked - incoming;
        var quantity = Math.Min(maximumQuantity, Math.Min(missing,
            Math.Min(DestinationRoom(blacksmith.InstanceId),
                direct ? TransportSystem.SpareCarriedQuantity(this.HandcartWorld, actor, input) : AvailableLotQuantity(input))));
        // Personal mined ore has a dedicated native delivery without the loose-input load limit.
        if (!direct || input.ItemKind != "iron_ore") quantity = Math.Min(quantity, HouseHaulLoadQuantity);
        if (!direct) quantity = Math.Min(quantity, PickupCarryCapacity(actor, input, blacksmith.Position));
        return quantity <= 0 ? null : new DeliveryOrderPlan("blacksmith_input", blacksmith, householdId,
            input, input, quantity, quantity, DirectDelivery: direct);
    }
}

using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using DeliveryOrderPlan = ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan;

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

    private static ActorCapabilityPolicy SmithDeliveryPolicy => new(ActionLifeStages.Adults, AllowUrgentFood: false,
        AllowUrgentWarmth: true, AllowActiveOrder: true);

    private int SmithDeliveryQuantity(string actor, SmithDeliveryKind kind, string household, PlacedBuilding smith, InventoryLot stock)
    {
        var direct = stock.OwnerId == actor;
        var missing = BlacksmithInputTarget(smith.InstanceId, stock.ItemKind) - SmithStoredQuantity(household, smith.InstanceId, stock.ItemKind) -
            (kind == SmithDeliveryKind.Ore ? 0 : SmithIncomingQuantity(smith.InstanceId, stock.ItemKind));
        var available = kind == SmithDeliveryKind.Ore || !direct ? AvailableLotQuantity(stock) : TransportSystem.SpareCarriedQuantity(this.HandcartWorld, actor, stock);
        var quantity = Math.Min(DestinationRoom(smith.InstanceId), Math.Min(missing, available));
        if (kind != SmithDeliveryKind.Ore) quantity = Math.Min(quantity, HouseHaulLoadQuantity);
        return direct ? quantity : Math.Min(quantity, PickupCarryCapacity(actor, stock, smith.Position));
    }

    private SmithDeliveryActionPlan? PlanSmithDelivery(string actor, SmithDeliveryKind kind,
        InventoryLot? selectedStock = null, PlacedBuilding? selectedSmith = null)
    {
        var candidate = kind == SmithDeliveryKind.Ore ? "deliver_smith_ore" : "haul_smith_input";
        if (!ActorCapabilities.Check(CapabilitiesFor(actor), candidate, SmithDeliveryPolicy).Allowed ||
            HouseholdFor(actor) is not { } household || CarriedHouseDelivery(actor) is not null ||
            (selectedSmith ?? this.HouseholdBuildingWithTag(household, "blacksmith")) is not { } smith)
            return null;
        var stock = selectedStock ?? (kind == SmithDeliveryKind.Ore ? PersonalSmithOre(actor) : BlacksmithInputForDelivery(household, smith, actor));
        if (stock is null) return null;
        var quantity = SmithDeliveryQuantity(actor, kind, household, smith, stock);
        if (quantity <= 0) return null;
        var direct = stock.OwnerId == actor;
        var target = direct ? smith.Position : HouseholdStockPosition(stock);
        var range = direct ? 0 : HouseholdStockInteractionRange(stock);
        var inventory = society.Checkpoint.Inventory;
        var vessel = InventoryContainerRules.IsContainer(stock.ItemKind);
        return new(actor, kind, household, smith.InstanceId, smith.Position, stock.Id, stock.OwnerId, stock.ItemKind,
            quantity, vessel ? ContainerFamilyQuantity(inventory, stock.Id) : quantity, direct, target, range,
            vessel ? GoodsPlanFamily.Capture(inventory, stock) : null,
            Reach(actor, target, ReachPurpose.SmithDelivery, range, 0, SwimmingPolicy.WhenCapable));
    }

    private ActionCheck CheckSmithDeliveryPlan(SmithDeliveryActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId, policy ?? SmithDeliveryPolicy);
        if (!capability.Allowed) return capability;
        if (HouseholdFor(plan.Actor) != plan.HouseholdId || this.HouseholdBuildingWithTag(plan.HouseholdId, "blacksmith") is not { } smith ||
            smith.InstanceId != plan.SmithId || smith.Position != plan.Destination || CarriedHouseDelivery(plan.Actor) is not null)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their Blacksmith or delivery work has changed");
        var request = plan.Kind == SmithDeliveryKind.Ore
            ? new GoodsRequest(GoodsUse.CarriedInputs, plan.Actor, GoodsOwners.One(plan.OwnerId), GoodsKinds.One(plan.ItemKind))
            : DeliveryGoodsRequest(plan.Actor, plan.OwnerId, plan.ItemKind, plan.SmithId);
        var inventory = society.Checkpoint.Inventory;
        if (RecheckGoods(request, plan.StockId) is not { } match || match.Lot.ContainerLotId is not null ||
            !GoodsPlanFamily.Matches(inventory, match.Lot, plan.Family) || plan.DirectDelivery != (match.Lot.OwnerId == plan.Actor) ||
            plan.DirectDelivery && !PersonalEquipmentRules.IsCarried(match.Lot, plan.Actor) ||
            !plan.DirectDelivery && (match.Lot.CarrierId is not null || HouseholdStockPosition(match.Lot) != plan.Target ||
                HouseholdStockInteractionRange(match.Lot) != plan.InteractionRange))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected Blacksmith supplies are no longer available");
        if (DestinationRoom(plan.SmithId) < plan.PhysicalQuantity)
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, "the Blacksmith has no room for the selected load");
        if (SmithDeliveryQuantity(plan.Actor, plan.Kind, plan.HouseholdId, smith, match.Lot) < plan.Quantity)
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected load is no longer needed or available");
        return IsWithinInteractionRange(agents[plan.Actor].Position, plan.Target, plan.InteractionRange) ||
            Reach(plan.Actor, plan.Target, ReachPurpose.SmithDelivery, plan.InteractionRange, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected Blacksmith trip is out of reach now");
    }

    private void ApplyCheckedSmithDeliveryPlan(SmithDeliveryActionPlan plan)
    {
        var state = agents[plan.Actor];
        if (!IsWithinInteractionRange(state.Position, plan.Target, plan.InteractionRange))
        {
            MoveToward(plan.Actor, state, plan.Target, plan.Kind == SmithDeliveryKind.Ore ? "smith_ore" : "smith_input", plan.InteractionRange);
            return;
        }
        var purpose = plan.Kind == SmithDeliveryKind.Ore ? "smith_ore_delivered" : plan.DirectDelivery ? "smith_input_delivered" : "smith_input_picked_up";
        var operationId = DerivedId.Create(plan.Kind == SmithDeliveryKind.Ore ? $"smith-ore-delivery:{WorldTick}:{plan.Actor}" :
            plan.DirectDelivery ? $"smith-input-delivery:{WorldTick}:{plan.Actor}" : $"smith-input-pickup:{WorldTick}:{plan.Actor}");
        RunInventoryCommand(InventorySystem.ApplyCheckedSmithDeliveryPlan(plan, operationId, purpose));
        AppendEvent(plan.Kind == SmithDeliveryKind.Ore
            ? new SmithOreDeliveredEventPayload(plan.Actor, plan.StockId, plan.Quantity, plan.SmithId)
            : plan.DirectDelivery
                ? new SmithInputDeliveredEventPayload(plan.Actor, plan.StockId, plan.Quantity, plan.SmithId)
                : new SmithInputPickedUpEventPayload(plan.Actor, plan.StockId, plan.Quantity, plan.SmithId));
    }

    private SmithGatherActionPlan? PlanSmithGather(string actor, string itemKind,
        MapResource? selectedSource = null, PlacedBuilding? selectedSmith = null)
    {
        var choice = new SmithGatherChoice(itemKind);
        if (!ActorCapabilities.Check(CapabilitiesFor(actor), choice.Encode(), SmithDeliveryPolicy).Allowed ||
            HouseholdFor(actor) is not { } household || CarriedHouseDelivery(actor) is not null ||
            (selectedSmith ?? this.HouseholdBuildingWithTag(household, "blacksmith")) is not { } smith)
            return null;
        var source = selectedSource ?? (itemKind == "iron_ore" ? MaterialSource(itemKind, actor, smith.Position) :
            BlacksmithInputToGather(household, smith.InstanceId, actor, itemKind)?.Source);
        if (source is null) return null;
        var harvest = ProjectMaterialHarvest(actor, itemKind, source);
        PlannedCargoStorage? preparation = null;
        PlannedEquipmentPickup? pickup = null;
        if (harvest is { } load)
        {
            var missing = checked(load.Quantity + load.TreeSeedQuantity) - FreeCarryCapacity(actor);
            if (missing > 0)
            {
                var cargo = SpareCargoForFood(actor, missing, load.ToolLotId);
                if (cargo.Count == 0 || SpareCargoDestination(actor, agents[actor], cargo.Sum(move => move.PhysicalQuantity)) is not { } storage)
                    return null;
                preparation = new(actor, household, CaptureCargoMoves(cargo), storage.Position, storage.Range, storage.StorageBuildingId,
                    Reach(actor, storage.Position, ReachPurpose.FoodCargoStorage, storage.Range, 0, SwimmingPolicy.WhenCapable));
            }
        }
        else
        {
            if (itemKind == "iron_ore" || RequiredGatheringTool(itemKind, source) is not { } required ||
                FindReachableSharedGatheringTool(actor, required) is not { } available ||
                SharedItem(available.ItemKind, actor) is not { ContainerLotId: null } tool)
                return null;
            pickup = new(tool.Id, tool.OwnerId, tool.ItemKind, HouseholdStockPosition(tool), HouseholdStockInteractionRange(tool));
        }
        var inventory = society.Checkpoint.Inventory;
        return new(actor, choice, household, smith.InstanceId, smith.Position, source, harvest,
            harvest?.ToolLotId is { } toolId ? inventory.GetLot(toolId).OwnerId : null,
            harvest?.ToolLotId is { } selectedTool ? inventory.GetLot(selectedTool).ItemKind : null,
            pickup, preparation, Reach(actor, source.Position, ReachPurpose.MaterialGathering, ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable),
            harvest is null ? Array.AsReadOnly<GridPoint>([]) : HarvestReturnRoute(actor, source, smith.Position,
                ReachPurpose.MaterialGathering, checked(harvest.Quantity + harvest.TreeSeedQuantity)));
    }

    private ActionCheck CheckSmithGatherPlan(SmithGatherActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId, policy ?? SmithDeliveryPolicy);
        if (!capability.Allowed) return capability;
        if (HouseholdFor(plan.Actor) != plan.HouseholdId || this.HouseholdBuildingWithTag(plan.HouseholdId, "blacksmith") is not { } smith ||
            smith.InstanceId != plan.SmithId || smith.Position != plan.Destination || CarriedHouseDelivery(plan.Actor) is not null)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their Blacksmith or delivery work has changed");
        var kind = plan.Choice.ItemKind;
        var missing = BlacksmithInputTarget(plan.SmithId, kind) - SmithStoredQuantity(plan.HouseholdId, plan.SmithId, kind) -
            (kind == "iron_ore" ? 0 : SmithIncomingQuantity(plan.SmithId, kind));
        if (missing <= 0 || kind == "iron_ore" && PersonalSmithOre(plan.Actor) is not null ||
            BlacksmithHasDeliverableInput(plan.HouseholdId, smith, plan.Actor, kind))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the Blacksmith no longer needs this gathering trip");
        if (DestinationRoom(plan.SmithId) <= 0)
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, "the Blacksmith has no room for materials");
        if (!map.Resources.Contains(plan.Source) || resources.GetValueOrDefault(plan.Source.Id) != ResourceState.Available ||
            !EcologyRules.Harvest(worldSystems.Ecology.GetResource(plan.Source.Id), 1).IsValid)
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected material source is no longer available");
        if (plan.Harvest is { ToolLotId: { } toolId } &&
            (RecheckGoods(new(GoodsUse.CarriedInputs, plan.Actor, GoodsOwners.One(plan.ToolOwnerId!), GoodsKinds.One(plan.ToolKind!)), toolId) is not { Lot: var tool } ||
                !ToolProgressionRules.IsTopLevelCarriedLot(tool, plan.Actor)))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected gathering tool is no longer available");
        if (plan.CargoPreparation is { } cargo)
        {
            if (FreeCarryCapacity(plan.Actor) + cargo.Cargo.Sum(move => move.PhysicalQuantity) < plan.PhysicalQuantity)
                return ActionCheck.Refuse(ActionRefusalReason.Capacity, "the selected cargo cannot make room for this harvest");
            var storage = CheckCargoStorage(cargo);
            if (!storage.Allowed) return storage;
        }
        else if (plan.ToolPickup is { } pickup)
        {
            if (FreeCarryCapacity(plan.Actor) <= 0 || HasCarriedEquipmentAtLeast(plan.Actor, pickup.ItemKind))
                return ActionCheck.Refuse(ActionRefusalReason.Capacity, "the selected tool is no longer needed or cannot be carried");
            var sharedTool = CheckEquipmentPickup(plan.Actor, pickup);
            if (!sharedTool.Allowed) return sharedTool;
        }
        else if (FreeCarryCapacity(plan.Actor) < plan.PhysicalQuantity)
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, "there is no room for the selected harvest");
        if (plan.Harvest is not null ? HarvestReturnRoute(plan.Actor, plan.Source, plan.Destination,
                ReachPurpose.MaterialGathering, plan.PhysicalQuantity).Count == 0 :
            !CanReturnWithHarvest(plan.Actor, kind, plan.Source, plan.Destination))
            return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected harvest cannot return to the Blacksmith now");
        return IsWithinInteractionRange(agents[plan.Actor].Position, plan.Source.Position, ResourceInteractionRange) ||
            Reach(plan.Actor, plan.Source.Position, ReachPurpose.MaterialGathering, ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected material source is out of reach now");
    }

    private void ApplyCheckedSmithGatherPlan(SmithGatherActionPlan plan)
    {
        if (plan.CargoPreparation is { } cargo) { ApplyCargoStorage(cargo); return; }
        var state = agents[plan.Actor];
        if (plan.ToolPickup is { } pickup)
        {
            this.ApplyPlannedEquipmentPickup(plan.Actor, pickup);
            return;
        }
        GatherPlannedProjectMaterial(plan.Actor, state, plan.Choice.ItemKind, plan.Source, plan.Harvest!);
    }
}

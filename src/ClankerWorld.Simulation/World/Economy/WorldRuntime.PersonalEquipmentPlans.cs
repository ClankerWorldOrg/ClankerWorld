using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static void ValidateEquipment(IEnumerable<AgentState> people, SocietyCheckpoint society,
        WorldContentSimulationState? simulation, DeclarativeWorldContentState? content, int schema)
    {
        foreach (var person in people)
        {
            if (person.Equipment is not { } equipment) continue;
            ValidateEquipmentShape(equipment, society.WorldTick, schema);
            foreach (var (id, carryAid) in new[] { (equipment.ClothingLotId, false), (equipment.CarryAidLotId, true) })
            {
                if (id is null) continue;
                var unit = PersonalEquipmentRules.EquippedUnit(society.Inventory, person.AgentId, id);
                if (unit is null || !(carryAid ? PersonalEquipmentRules.IsCarryAid(unit.ItemKind) : PersonalEquipmentRules.IsGarment(unit.ItemKind)) ||
                    society.Inventory.Reservations.Any(item => item.LotId == id && item.State is
                        InventoryReservationState.Reserved or InventoryReservationState.PartiallyConsumed or InventoryReservationState.Committed))
                    throw new InvalidDataException("Equipped goods must be one physically carried, unreserved unit owned by that person.");
            }
            if (equipment.OrnamentLotId is { } ornamentId)
            {
                var ornament = PersonalEquipmentRules.EquippedUnit(society.Inventory, person.AgentId, ornamentId);
                if (ornament is null || ornament.ContainerLotId is not null ||
                    !PersonalEquipmentRules.IsOrnament(ornament.ItemKind) ||
                    society.Inventory.Reservations.Any(item => item.LotId == ornamentId && item.State is
                        InventoryReservationState.Reserved or InventoryReservationState.PartiallyConsumed or InventoryReservationState.Committed))
                    throw new InvalidDataException("A worn ornament must be one unreserved, personally carried unit owned by its wearer.");
            }
            if (equipment.Repair is not { } repair) continue;
            var target = society.Inventory.Lots.FirstOrDefault(lot => lot.Id == repair.LotId);
            var building = simulation?.Buildings.FirstOrDefault(item => item.InstanceId == repair.BuildingId);
            var definition = content?.Buildings.FirstOrDefault(item => item.CanonicalId == building?.DefinitionId);
            var household = society.Agents.Single(item => item.Id == person.AgentId).HouseholdId;
            if (target is null || target.Quantity != 1 || !PersonalEquipmentRules.IsCarried(target, person.AgentId) ||
                target.DeliveryBuildingId is not null || target.ConditionBasisPoints >= 10_000 ||
                society.Inventory.Reservations.Any(item => item.LotId == target.Id && item.State is
                    InventoryReservationState.Reserved or InventoryReservationState.PartiallyConsumed or InventoryReservationState.Committed) ||
                PersonalEquipmentRules.RepairMaterials(target.ItemKind).Count == 0 ||
                building is null || definition is null || household is null || building.HouseholdId != household ||
                person.Position != building.Position || !definition.Tags.Contains(target.ItemKind == "basket" ? "house" : "tailor", StringComparer.Ordinal))
                throw new InvalidDataException("The saved equipment repair has no valid carried item and private work site.");
            var actual = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var id in repair.MaterialReservationIds)
            {
                var reservation = society.Inventory.Reservations.FirstOrDefault(item => item.Id == id);
                var lot = society.Inventory.Lots.FirstOrDefault(item => item.Id == reservation?.LotId);
                if (reservation is null || lot is null || reservation.State != InventoryReservationState.Reserved ||
                    reservation.Purpose != "equipment_repair" || reservation.OwnerId != person.AgentId ||
                    reservation.ExpiryTick < society.WorldTick || reservation.ExpiryTick > repair.StartedTick + 120 ||
                    lot.Id == target.Id || !PersonalEquipmentRules.IsCarried(lot, person.AgentId) ||
                    lot.DeliveryBuildingId is not null || lot.ConditionBasisPoints == 0 || lot.FreshnessBasisPoints == 0)
                    throw new InvalidDataException("The saved equipment repair has invalid material reservations.");
                actual[lot.ItemKind] = actual.GetValueOrDefault(lot.ItemKind) + reservation.Quantity;
            }
            var expected = PersonalEquipmentRules.RepairMaterials(target.ItemKind);
            if (actual.Count != expected.Count || expected.Any(input => actual.GetValueOrDefault(input.ResourceId) != input.Amount))
                throw new InvalidDataException("The saved equipment repair does not reserve its exact material cost.");
        }
    }

    private static void ValidateEquipmentShape(PersonalEquipment equipment, long tick, int schema)
    {
        AgentStore.ValidateLocalEquipment(equipment);
        if (schema < PersonalEquipmentSchemaVersion || equipment.OrnamentLotId is not null && schema < OrnamentEquipmentSchemaVersion ||
            equipment.Repair is { } repair && (repair.StartedTick > tick || tick - repair.StartedTick > 120 || repair.WorkDone > tick - repair.StartedTick))
            throw new InvalidDataException("The saved personal equipment is invalid.");
    }
    private static ActorCapabilityPolicy PrivateEquipmentPolicy(PrivateEquipmentKind kind) =>
        new(kind == PrivateEquipmentKind.CarryAid ? ActionLifeStages.Adults : ActionLifeStages.NonInfants,
            AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true);

    private static int CarryAidCapacity(string kind) => kind == "leather_sack" ? 32 :
        kind == "sack" ? PersonalEquipmentRules.SackCapacity : PersonalEquipmentRules.BasketCapacity;

    private bool IsPrivateEquipmentUpgrade(string actor, InventoryLot item, bool carryAid)
    {
        if (!CanEquipPrivateItem(actor, item, carryAid)) return false;
        if (carryAid) return PersonalEquipmentRules.IsCarryAid(item.ItemKind) &&
            CarryAidCapacity(item.ItemKind) > PersonalEquipmentRules.Capacity(society.Checkpoint.Inventory, actor, agents[actor].Equipment);
        if (!PersonalEquipmentRules.IsGarment(item.ItemKind)) return false;
        var current = EquippedGarment(actor);
        var weather = WeatherAt(agents[actor].Position);
        return item.Id != current?.Id && (current is null || PersonalEquipmentRules.Protection(item, weather) > PersonalEquipmentRules.Protection(current, weather));
    }

    private PrivateEquipmentActionPlan? PlanPrivateEquipment(string actor, bool carryAid, InventoryLot? selected = null)
    {
        if ((selected ?? (carryAid ? BetterCarryAid(actor) : BetterGarment(actor))) is not { } item) return null;
        var position = item.OwnerId == actor ? agents[actor].Position : HouseholdStockPosition(item);
        var range = item.OwnerId == actor ? 0 : HouseholdStockInteractionRange(item);
        var equipment = agents[actor].Equipment;
        return new(actor, carryAid ? PrivateEquipmentKind.CarryAid : PrivateEquipmentKind.Garment,
            HouseholdFor(actor), item.Id, item.OwnerId, item.ItemKind,
            carryAid ? equipment?.CarryAidLotId : equipment?.ClothingLotId, position, range,
            Reach(actor, position, ReachPurpose.PrivateEquipment, range, 0, SwimmingPolicy.WhenCapable));
    }

    private ActionCheck CheckPrivateEquipmentPlan(PrivateEquipmentActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var carryAid = plan.Kind == PrivateEquipmentKind.CarryAid;
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId, policy ?? PrivateEquipmentPolicy(plan.Kind));
        if (!capability.Allowed) return capability;
        var equipment = agents[plan.Actor].Equipment;
        if (HouseholdFor(plan.Actor) != plan.HouseholdId ||
            (carryAid ? equipment?.CarryAidLotId : equipment?.ClothingLotId) != plan.PreviousSlotId)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their household or equipped item has changed");
        if (RecheckGoods(new(GoodsUse.Equip, plan.Actor, GoodsOwners.One(plan.OwnerId), GoodsKinds.One(plan.ItemKind)), plan.StockId) is not { Lot: var item } ||
            item.OwnerId != plan.Actor && (item.CarrierId is not null || HouseholdStockPosition(item) != plan.Position || HouseholdStockInteractionRange(item) != plan.InteractionRange))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected equipment is no longer available");
        if (!IsPrivateEquipmentUpgrade(plan.Actor, item, carryAid))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected item is no longer a useful equipment upgrade");
        return item.OwnerId == plan.Actor || Reach(plan.Actor, plan.Position, ReachPurpose.PrivateEquipment,
            plan.InteractionRange, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected equipment is out of reach now");
    }

    private void ApplyCheckedPrivateEquipmentPlan(PrivateEquipmentActionPlan plan)
    {
        var person = agents[plan.Actor];
        if (plan.OwnerId != plan.Actor && !IsWithinInteractionRange(person.Position, plan.Position, plan.InteractionRange))
        {
            MoveToward(plan.Actor, person, plan.Position, "equipment", plan.InteractionRange);
            return;
        }
        CompletePrivateEquipment(plan.Actor, society.Checkpoint.Inventory.GetLot(plan.StockId), plan.Kind == PrivateEquipmentKind.CarryAid);
    }
    private static ActorCapabilityPolicy EquipmentRepairPolicy => new(ActionLifeStages.Adults,
        AllowUrgentFood: false, AllowUrgentWarmth: false, AllowActiveOrder: true);

    private static GoodsRequest EquipmentRepairTargetRequest(string actor) => new(GoodsUse.EquipmentRepair,
        actor, GoodsOwners.One(actor), new(["clothing", "padded_coat", "rain_cloak", "basket", "sack", "leather_sack"]));

    private EquipmentRepairActionPlan? PlanEquipmentRepair(string actor, InventoryLot? selected = null)
    {
        if (!ActorCapabilities.Check(CapabilitiesFor(actor), "repair_equipment", EquipmentRepairPolicy).Allowed ||
            HouseholdFor(actor) is not { } household) return null;
        foreach (var target in selected is null ? WornEquipmentItems(actor) : [selected])
        {
            if (EquipmentRepairSite(actor, target) is not { } site) continue;
            var materials = new List<PlannedRepairMaterial>();
            var inputs = new List<PlannedEquipmentRepairInput>();
            foreach (var need in PersonalEquipmentRules.RepairMaterials(target.ItemKind))
            {
                if (SpendingGoods(actor, GoodsUse.Spend, [actor], need.ResourceId).FirstOrDefault(match =>
                    PersonalEquipmentRules.IsCarried(match.Lot, actor) && match.Quantity >= need.Amount) is { } carried)
                    materials.Add(new(carried.Lot.Id, need.ResourceId, need.Amount));
                else if (SharedItem(need.ResourceId, actor, site.Position) is { } stock)
                    inputs.Add(new(need.Amount, new(stock.Id, stock.OwnerId, need.ResourceId,
                        HouseholdStockPosition(stock), HouseholdStockInteractionRange(stock))));
                else break;
            }
            if (materials.Count + inputs.Count != PersonalEquipmentRules.RepairMaterials(target.ItemKind).Count) continue;
            if (PlanInputCollection(actor, inputs.Select(EquipmentRepairCollectionStep).ToArray(), site.Position) is not { } ordered)
                continue;
            inputs = ordered.Select(step => inputs.Single(input => input.Pickup.ItemKind == step.Kind)).ToList();
            var plan = new EquipmentRepairActionPlan(actor, target.Id, target.ItemKind, household,
                site.InstanceId, site.Position, materials.AsReadOnly(), inputs.AsReadOnly(),
                Reach(actor, site.Position, ReachPurpose.EquipmentRepair, 0, 0, SwimmingPolicy.WhenCapable));
            if (CheckEquipmentRepairPlan(plan).Allowed) return plan;
        }
        return null;
    }

    private static InputCollectionStep EquipmentRepairCollectionStep(PlannedEquipmentRepairInput input) =>
        new(input.Pickup.ItemKind, input.Pickup.Position, input.Pickup.InteractionRange, input.Quantity);

    private ActionCheck CheckEquipmentRepairPlan(EquipmentRepairActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId, policy ?? EquipmentRepairPolicy);
        if (!capability.Allowed) return capability;
        if (HouseholdFor(plan.Actor) != plan.HouseholdId ||
            RecheckGoods(EquipmentRepairTargetRequest(plan.Actor), plan.TargetId) is not { Lot: var target } ||
            target.ItemKind != plan.ItemKind)
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected worn item is no longer available for repair");
        if (EquipmentRepairSite(plan.Actor, target) is not { } site || site.InstanceId != plan.BuildingId || site.Position != plan.Destination)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their equipment repair site has changed");
        foreach (var material in plan.Materials)
            if (RecheckGoods(new(GoodsUse.Spend, plan.Actor, GoodsOwners.One(plan.Actor), GoodsKinds.One(material.ItemKind)),
                    material.StockId) is not { } match || match.Quantity < material.Quantity || !PersonalEquipmentRules.IsCarried(match.Lot, plan.Actor))
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected repair material is no longer available");
        if (plan.Inputs.Sum(input => input.Quantity) > FreeCarryCapacity(plan.Actor))
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, "there is no room for the selected repair inputs");
        foreach (var input in plan.Inputs)
        {
            var pickup = input.Pickup;
            if (HasCarriedOwnItem(plan.Actor, pickup.ItemKind))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "they already carry this repair input");
            if (RecheckGoods(SharedCollectionRequest(plan.Actor, pickup.OwnerId, pickup.ItemKind), pickup.StockId) is not { } match ||
                match.Quantity < input.Quantity || match.Lot.ContainerLotId is not null || match.Lot.CarrierId is not null ||
                HouseholdStockPosition(match.Lot) != pickup.Position || HouseholdStockInteractionRange(match.Lot) != pickup.InteractionRange)
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected repair supplies are no longer available");
        }
        if (!RecheckInputCollection(plan.Actor, plan.Inputs.Select(EquipmentRepairCollectionStep).ToArray(),
                plan.Destination, ReachPurpose.EquipmentRepair))
            return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected repair input trip is no longer possible");
        return agents[plan.Actor].Position == plan.Destination || Reach(plan.Actor, plan.Destination,
            ReachPurpose.EquipmentRepair, 0, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the equipment repair site is out of reach now");
    }

    private void ApplyEquipmentRepairPlan(EquipmentRepairActionPlan? plan, string? orderInstructionId = null)
    {
        if (plan is null) return;
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        ApplyCheckedEquipmentRepairPlan(plan, orderInstructionId);
    }

    private void ApplyCheckedEquipmentRepairPlan(EquipmentRepairActionPlan plan, string? orderInstructionId = null)
    {
        if (plan.Inputs.Count > 0) { ApplyPlannedEquipmentPickup(plan.Actor, plan.Inputs[0].Pickup); return; }
        var person = agents[plan.Actor];
        if (person.Position != plan.Destination) { MoveToward(plan.Actor, person, plan.Destination, "repair_equipment", 0); return; }
        StartEquipmentRepair(plan.Actor, person, plan.TargetId, plan.BuildingId, plan.Materials, orderInstructionId);
    }
    // Pending instructions include suggestions. Mandatory orders are gated by
    // the fresh personal admission in ApplyOrnamentDecision.
    private static ActorCapabilityPolicy OrnamentPolicy => new(ActionLifeStages.Adults,
        AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true);

    private bool IsOrnamentSource(string actor, InventoryLot lot) => lot.ContainerLotId is null &&
        (IsPersonallyCarriedOrnament(lot, actor) || HouseholdFor(actor) is { } household && lot.OwnerId == household &&
            (lot.CarrierId is null || lot.CarrierId == actor) && FreeCarryCapacity(actor) > 0 &&
            (lot.StorageBuildingId is null || worldSimulation.Buildings.Any(building =>
                building.InstanceId == lot.StorageBuildingId && building.HouseholdId == household)));

    private bool IsLocalOrnamentRecipient(string actor, string recipient) => recipient != actor &&
        LivingOrnamentAdult(recipient) && IsWithinInteractionRange(agents[actor].Position, agents[recipient].Position, ResourceInteractionRange) &&
        FreeCarryCapacity(recipient) > 0;

    private WearOrnamentActionPlan PlanOrnamentWear(string actor, InventoryLot stock)
    {
        var position = HouseholdStockPosition(stock);
        var range = HouseholdStockInteractionRange(stock);
        return new(actor, new(stock.Id), stock.OwnerId, stock.ItemKind, agents[actor].Equipment?.OrnamentLotId,
            position, range, Reach(actor, position, ReachPurpose.OrnamentPickup, range, 0, SwimmingPolicy.WhenCapable));
    }

    private GiftOrnamentActionPlan PlanOrnamentGift(string actor, InventoryLot stock, string recipient) =>
        new(actor, new(stock.Id, recipient), stock.ItemKind, agents[actor].Equipment?.OrnamentLotId);

    private ActionCheck CheckOrnamentPlan(OrnamentActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId, policy ?? OrnamentPolicy);
        if (!capability.Allowed) return capability;
        if (BusyWithOrnamentIncompatibleWork(plan.Actor))
            return ActionCheck.Refuse(ActionRefusalReason.ActiveOrder, "their current work comes first");
        var slot = agents[plan.Actor].Equipment?.OrnamentLotId;
        switch (plan)
        {
            case WearOrnamentActionPlan wear:
                if (slot != wear.PreviousSlotId || slot == wear.Selected.StockId)
                    return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their worn ornament has changed");
                if (RecheckGoods(OrnamentCollectionRequest(plan.Actor, wear.OwnerId), wear.Selected.StockId) is not { Lot: var stock } ||
                    stock.ItemKind != wear.ItemKind || !IsOrnamentSource(plan.Actor, stock))
                    return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected ornament is no longer available");
                if (PersonalEquipmentRules.IsCarried(stock, plan.Actor)) return new();
                if (HouseholdStockPosition(stock) != wear.Position || HouseholdStockInteractionRange(stock) != wear.InteractionRange)
                    return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected ornament has moved");
                return Reach(plan.Actor, wear.Position, ReachPurpose.OrnamentPickup, wear.InteractionRange, 0, SwimmingPolicy.WhenCapable).Count > 0
                    ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected ornament is out of reach now");
            case RemoveOrnamentActionPlan remove:
                return slot == remove.StockId ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "they no longer wear the selected ornament");
            case GiftOrnamentActionPlan gift:
                if ((slot == gift.Selected.StockId) != (gift.PreviousSlotId == gift.Selected.StockId))
                    return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their worn ornament has changed");
                if (RecheckGoods(new(GoodsUse.Spend, plan.Actor, GoodsOwners.One(plan.Actor), GoodsKinds.One(gift.ItemKind)),
                        gift.Selected.StockId) is not { Lot: var given } || !IsPersonallyCarriedOrnament(given, plan.Actor))
                    return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected gift is no longer available");
                return IsLocalOrnamentRecipient(plan.Actor, gift.Selected.RecipientId) ? new() :
                    ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected recipient is no longer nearby with room for the gift");
            default: throw new InvalidOperationException("An ornament plan has no registered operation.");
        }
    }

    private void ApplyOrnamentPlan(OrnamentActionPlan plan)
    {
        TryRegisteredAction(ActionPhase.FreshAdmission, new(plan.Actor, agents[plan.Actor], plan.CandidateId, false, plan));
    }

    private void ApplyCheckedOrnament(OrnamentActionPlan plan)
    {
        switch (plan)
        {
            case WearOrnamentActionPlan wear:
                var stock = society.Checkpoint.Inventory.GetLot(wear.Selected.StockId);
                if (!PersonalEquipmentRules.IsCarried(stock, plan.Actor) &&
                    !IsWithinInteractionRange(agents[plan.Actor].Position, wear.Position, wear.InteractionRange))
                    MoveToward(plan.Actor, agents[plan.Actor], wear.Position, "ornament", wear.InteractionRange);
                else CompleteOrnamentWear(plan.Actor, stock);
                break;
            case RemoveOrnamentActionPlan remove: CompleteOrnamentRemoval(plan.Actor, remove.StockId); break;
            case GiftOrnamentActionPlan gift:
                CompleteOrnamentGift(plan.Actor, gift.Selected.RecipientId, society.Checkpoint.Inventory.GetLot(gift.Selected.StockId));
                break;
        }
    }
    private ToolRequestConsentActionPlan? PlanToolRequestConsent(string actor, ToolRequestConsentChoice choice)
    {
        if (!agents.ContainsKey(actor)) return null;
        if (choice.Kind == ToolRequestConsentKind.Place)
        {
            foreach (var (shop, recipe) in ToolRequestOptions(actor))
                if (ToolRequestConsentChoice.Encode(choice.Kind, shop.InstanceId, recipe.CanonicalId) == choice.Encode())
                    return new(actor, choice, shop, recipe, null);
            return null;
        }
        var request = toolMakingRequests.FirstOrDefault(item => ToolRequestConsentChoice.Encode(choice.Kind, item.Id) == choice.Encode());
        if (request is null || ToolRequestShop(request) is not { } building ||
            worldContent.Recipes.FirstOrDefault(recipe => recipe.CanonicalId == request.RecipeId) is not { } definition) return null;
        return new(actor, choice, building, definition, request);
    }

    private ActionCheck CheckToolRequestConsentPlan(ToolRequestConsentActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var placing = plan.Choice.Kind == ToolRequestConsentKind.Place;
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? ToolRequestConsentCapabilities(plan.Choice.Kind));
        if (!capability.Allowed) return capability;
        if (PendingInstructionFor(plan.Actor)?.Kind == OwnerInstructionKind.Order ||
            worldSimulation.Buildings.SingleOrDefault(item => item.InstanceId == plan.Shop.InstanceId) != plan.Shop ||
            !worldContent.Recipes.Any(recipe => recipe.CanonicalId == plan.Recipe.CanonicalId))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected Blacksmith, recipe or personal choice is unavailable");
        if (placing)
            return IdleForToolRequest(plan.Actor) && IsWithinInteractionRange(agents[plan.Actor].Position, plan.Shop.Position, ResourceInteractionRange) &&
                MayRequestToolAt(plan.Actor, plan.Shop, plan.Recipe)
                ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "that tool request is no longer available at the selected Blacksmith");
        if (plan.Original is not { } original || toolMakingRequests.FirstOrDefault(item => item.Id == original.Id) is not { } request ||
            (plan.Choice.Kind == ToolRequestConsentKind.Withdraw
                ? request.RequesterId != original.RequesterId || request.SellerHouseholdId != original.SellerHouseholdId ||
                    request.BuildingInstanceId != original.BuildingInstanceId || request.RecipeId != original.RecipeId ||
                    request.ItemKind != original.ItemKind || request.RequestedTick != original.RequestedTick
                : request != original))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected tool request has changed");
        var allowed = plan.Choice.Kind switch
        {
            ToolRequestConsentKind.Withdraw => request.RequesterId == plan.Actor && !ToolMakingRequestRules.IsTerminal(request.Status),
            ToolRequestConsentKind.Accept => request.Status == ToolMakingRequestStatus.Requested && IsToolRequestSeller(plan.Actor, request) && IdleForToolRequest(plan.Actor),
            _ => request.Status == ToolMakingRequestStatus.Requested && IsToolRequestSeller(plan.Actor, request),
        };
        return allowed ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "that tool-request response is no longer available");
    }

    private void ApplyToolRequestConsentPlan(ToolRequestConsentActionPlan plan)
    {
        TryRegisteredAction(ActionPhase.FreshAdmission, new(plan.Actor, agents[plan.Actor], plan.CandidateId, false, plan));
    }
    private ToolRequestVisitActionPlan? PlanToolRequestVisit(string actor, ToolRequestVisitChoice choice)
    {
        if (!agents.ContainsKey(actor)) return null;
        foreach (var (smith, recipe) in ToolRequestOptions(actor))
            if (ToolRequestVisitChoice.Encode(smith.InstanceId, recipe.CanonicalId) == choice.Encode())
                return new(actor, choice, smith, recipe,
                    Reach(actor, smith.Position, ReachPurpose.ToolRequest, ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable));
        return null;
    }

    private ActionCheck CheckToolRequestVisitPlan(ToolRequestVisitActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? ToolRequestVisitCapabilities);
        if (!capability.Allowed) return capability;
        if (worldSimulation.Buildings.SingleOrDefault(item => item.InstanceId == plan.Smith.InstanceId) != plan.Smith ||
            !worldContent.Recipes.Any(recipe => recipe.CanonicalId == plan.Recipe.CanonicalId) ||
            !IdleForToolRequest(plan.Actor) || !MayRequestToolAt(plan.Actor, plan.Smith, plan.Recipe))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected Blacksmith or tool request is no longer available");
        return new();
    }
}

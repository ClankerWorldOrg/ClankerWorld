using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private void ApplyCheckedVesselPlan(VesselActionPlan plan)
    {
        var state = agents[plan.Actor];
        var kind = plan.Choice.Kind;
        var movement = kind switch
        {
            VesselActionKind.CollectJug => "collect_water_jug",
            VesselActionKind.FillJug => "fill_water_jug",
            VesselActionKind.ReturnJug => "return_water_jug",
            VesselActionKind.StoreFood => "store_food_in_pot",
            VesselActionKind.TakeFood => "take_food_from_pot",
            _ => "return_empty_vessel",
        };
        if (state.Position != plan.Target) { MoveToward(plan.Actor, state, plan.Target, movement, 0); return; }
        switch (kind)
        {
            case VesselActionKind.CollectJug:
                RunInventoryCommand(InventorySystem.CollectWaterJug(InventoryEconomyWorld, plan));
                AppendEvent(new WaterJugCollectedEventPayload(plan.Actor, plan.VesselId));
                break;
            case VesselActionKind.FillJug:
                RunInventoryCommand(InventorySystem.FillCarriedWaterJug(InventoryEconomyWorld, plan));
                AppendEvent(new WaterJugFilledEventPayload(plan.Actor, plan.VesselId, plan.Quantity, plan.Target.X, plan.Target.Y));
                break;
            case VesselActionKind.ReturnJug:
                RunInventoryCommand(InventorySystem.ReturnFilledWaterJug(InventoryEconomyWorld, plan));
                AppendEvent(new WaterJugReturnedEventPayload(plan.Actor, plan.VesselId, plan.HouseId));
                break;
            case VesselActionKind.ReturnEmpty:
                RunInventoryCommand(InventorySystem.CollectEmptyVessel(InventoryEconomyWorld, plan));
                AppendEvent(new EmptyVesselPickedUpEventPayload(plan.Actor, plan.VesselId, plan.SourceBuildingId!, plan.HouseId));
                break;
            case VesselActionKind.TakeFood:
                RunInventoryCommand(InventorySystem.TakeFoodFromPot(InventoryEconomyWorld, plan));
                AppendEvent(new FoodTakenFromPotEventPayload(plan.Actor, plan.VesselId, plan.Food!.StockId, plan.Quantity));
                break;
            case VesselActionKind.StoreFood:
                StorePlannedPotFood(plan);
                break;
        }
    }

    private void StorePlannedPotFood(VesselActionPlan plan)
    {
        var food = plan.Food!;
        var operationId = FoodPotTransferId(WorldTick, plan.Actor);
        RunInventoryCommand(InventorySystem.StorePlannedPotFood(food, plan, operationId));
        AppendEvent(new FoodStoredInPotEventPayload(plan.Actor, plan.VesselId, food.StockId, plan.Quantity));
    }
    private static ActorCapabilityPolicy VesselPolicy(VesselActionKind kind) =>
        new(ActionLifeStages.Adults, AllowUrgentFood: kind == VesselActionKind.TakeFood,
            AllowUrgentWarmth: true, AllowActiveOrder: true);

    private ActionCheck CheckVesselPlan(VesselActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var kind = plan.Choice.Kind;
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId, policy ?? VesselPolicy(kind));
        if (!capability.Allowed) return capability;
        if (HouseholdFor(plan.Actor) != plan.HouseholdId || HouseForHousehold(plan.HouseholdId) is not { } house ||
            house.InstanceId != plan.HouseId || house.Position != plan.HousePosition)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their household or House has changed");
        if (plan.Quantity <= 0) return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "there is no selected quantity to move");
        var use = kind == VesselActionKind.ReturnJug ? GoodsUse.ReturnVessel : GoodsUse.VesselFor;
        var extra = kind is VesselActionKind.FillJug or VesselActionKind.StoreFood ? plan.Quantity : kind == VesselActionKind.CollectJug ? 1 : 0;
        var contents = kind is VesselActionKind.FillJug or VesselActionKind.CollectJug ? InventoryContainerRules.FreshWater : null;
        var request = new GoodsRequest(use, plan.Actor, GoodsOwners.One(plan.VesselOwner), GoodsKinds.One(plan.VesselKind),
            ExtraUnits: extra, ContentsKind: contents);
        if (RecheckGoods(request, plan.VesselId) is not { Lot: var vessel } ||
            !GoodsPlanFamily.Matches(society.Checkpoint.Inventory, vessel, plan.Family))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected vessel or its contents are no longer available");
        var inventory = society.Checkpoint.Inventory;
        var index = InventoryIndex.For(inventory);
        var access = kind == VesselActionKind.ReturnJug ? InventoryHoldRules.DeliveryFor(plan.Actor, plan.HouseId) : InventoryHoldRules.BorrowedBy(plan.Actor);
        if (index.ContentsOf(vessel.Id).Prepend(vessel).Any(lot => !InventoryHoldRules.Allows(index, lot, access)))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected vessel or its contents are promised to another task");
        var physical = ContainerFamilyQuantity(inventory, vessel.Id);
        if (kind is VesselActionKind.FillJug or VesselActionKind.ReturnJug)
        {
            if (!PersonalEquipmentRules.IsCarried(vessel, plan.Actor) || vessel.OwnerId != plan.Actor)
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "they no longer carry the selected jug");
            if (kind == VesselActionKind.ReturnJug && DestinationRoom(plan.HouseId) < physical)
                return ActionCheck.Refuse(ActionRefusalReason.Capacity, "the House has no room for the selected jug and contents");
            if (kind == VesselActionKind.FillJug && (!FreshWaterShorePositions().Contains(plan.Target) ||
                WaterJugRefillReturnDestination(plan.Actor, vessel) != plan.ReturnTo || WaterFillQuantity(plan.Actor, vessel, plan.Target, plan.ReturnTo) < plan.Quantity))
                return ActionCheck.Refuse(ActionRefusalReason.Capacity, "the selected water load no longer fits its planned trip");
        }
        else if (kind is VesselActionKind.CollectJug or VesselActionKind.ReturnEmpty)
        {
            var source = worldSimulation.Buildings.SingleOrDefault(building => building.InstanceId == plan.SourceBuildingId);
            if (vessel.OwnerId != plan.HouseholdId || vessel.CarrierId is not null || vessel.GroundPosition is not null ||
                vessel.StorageBuildingId != plan.SourceBuildingId || source is null || source.HouseholdId != plan.HouseholdId || source.Position != plan.Target)
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected vessel is no longer at the household pickup place");
            if (kind == VesselActionKind.CollectJug)
            {
                if (WaterJugStockLocation(vessel, plan.HouseholdId)?.InstanceId != source.InstanceId ||
                    !CanCollectJugForRefill(plan.Actor, vessel))
                    return ActionCheck.Refuse(ActionRefusalReason.Capacity, "they cannot collect the selected jug and leave room for water");
                if (plan.Shore is not { } shore || !FreshWaterShorePositions().Contains(shore) ||
                    ReachLeg(plan.Actor, source.Position, shore, ReachPurpose.HouseholdVessel, 0, physical, SwimmingPolicy.WhenCapable).Count == 0 ||
                    PickupCarryCapacity(plan.Actor, shore, plan.HousePosition) <= physical)
                    return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected jug's water trip is out of reach now");
            }
            else if (!IsEmptyWorkplaceVessel(vessel, plan.HouseId) || !CanStartEmptyVesselReturn(plan.Actor, plan.HouseId) ||
                PickupCarryCapacity(plan.Actor, vessel, plan.HousePosition) < physical)
                return ActionCheck.Refuse(ActionRefusalReason.Capacity, "they cannot return the selected empty household vessel");
        }
        else
        {
            if (vessel.OwnerId != plan.HouseholdId || vessel.StorageBuildingId != plan.HouseId || plan.Food is not { } food ||
                RecheckGoods(new(GoodsUse.ConsumeAt, plan.Actor, GoodsOwners.One(food.OwnerId), GoodsKinds.One(food.ItemKind)), food.StockId) is not { Lot: var lot, Quantity: var available } ||
                !InventoryHoldRules.Allows(index, lot, access))
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected pot or food is no longer available");
            if (kind == VesselActionKind.StoreFood)
            {
                if (lot.ContainerLotId is not null || available - (lot.OwnerId == plan.Actor ? 1 : 0) < plan.Quantity ||
                    (lot.OwnerId == plan.Actor ? !PersonalEquipmentRules.IsCarried(lot, plan.Actor) || lot.DeliveryBuildingId is not null || DestinationRoom(plan.HouseId) < plan.Quantity :
                        lot.OwnerId != plan.HouseholdId || lot.StorageBuildingId != plan.HouseId))
                    return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected food load no longer fits the pot");
            }
            else if (lot.OwnerId != plan.HouseholdId || lot.ContainerLotId != vessel.Id || available < plan.Quantity ||
                !WantsFoodFromPot(plan.Actor))
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected serving is no longer available for their food needs");
        }
        return Reach(plan.Actor, plan.Target, ReachPurpose.HouseholdVessel, 0, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected vessel action is out of reach now");
    }
    private VesselActionPlan CreateVesselPlan(string actor, VesselChoice choice, string household,
        PlacedBuilding house, InventoryLot vessel, GridPoint target, int quantity,
        PotFoodChoice? food = null, GridPoint? shore = null, GridPoint? returnTo = null) =>
        new(actor, choice, household, house.InstanceId, house.Position, vessel.Id, vessel.OwnerId, vessel.ItemKind,
            target, vessel.StorageBuildingId, quantity,
            choice.Kind is VesselActionKind.StoreFood or VesselActionKind.TakeFood ? null : GoodsPlanFamily.Capture(society.Checkpoint.Inventory, vessel),
            food is null ? null : new(food.Food.Id, food.Food.OwnerId, food.Food.ItemKind), shore, returnTo,
            Reach(actor, target, ReachPurpose.HouseholdVessel, 0, 0, SwimmingPolicy.WhenCapable));

    private VesselActionPlan? PlanVesselAction(string actor, VesselChoice choice,
        InventoryLot? selectedVessel = null, PotFoodChoice? selectedFood = null)
    {
        if (HouseholdFor(actor) is not { } household || HouseForHousehold(household) is not { } house) return null;
        var inventory = society.Checkpoint.Inventory;
        switch (choice.Kind)
        {
            case VesselActionKind.CollectJug:
                return PlanStoredJugPickup(actor, choice, household, house);
            case VesselActionKind.FillJug:
                if (choice.Shore is not { } shore || (selectedVessel ?? CarriedWaterJugForRefill(actor)) is not { } jug) return null;
                var destination = WaterJugRefillReturnDestination(actor, jug);
                return CreateVesselPlan(actor, choice, household, house, jug, shore,
                    WaterFillQuantity(actor, jug, shore, destination), shore: shore, returnTo: destination);
            case VesselActionKind.ReturnJug:
                return (selectedVessel ?? CarriedWaterJugForReturn(actor, house.InstanceId)) is { } returning
                    ? CreateVesselPlan(actor, choice, household, house, returning, house.Position, 1) : null;
            case VesselActionKind.StoreFood:
                if ((selectedFood ?? FindFoodToStore(actor, household, house.InstanceId)) is not { } storage) return null;
                var room = storage.Food.OwnerId == actor ? DestinationRoom(house.InstanceId) : int.MaxValue;
                var quantity = Math.Min(AvailableLotQuantity(storage.Food) - (storage.Food.OwnerId == actor ? 1 : 0),
                    Math.Min(InventoryContainerRules.StoragePotCapacity - ContainerContentsQuantity(inventory, storage.Pot.Id), room));
                return CreateVesselPlan(actor, choice, household, house, storage.Pot, house.Position, quantity, storage);
            case VesselActionKind.TakeFood:
                return (selectedFood ?? FindFoodInPot(household, house.InstanceId)) is { } serving
                    ? CreateVesselPlan(actor, choice, household, house, serving.Pot, house.Position,
                        Math.Min(1, AvailableLotQuantity(serving.Food)), serving) : null;
            case VesselActionKind.ReturnEmpty:
                return PlanEmptyVesselPickup(actor, choice, household, house);
            default:
                return null;
        }
    }

    private int WaterFillQuantity(string actor, InventoryLot jug, GridPoint shore, GridPoint? returnTo)
    {
        var quantity = Math.Min(InventoryContainerRules.WaterJugCapacity -
            ContainerContentsQuantity(society.Checkpoint.Inventory, jug.Id), FreeCarryCapacity(actor));
        return returnTo is { } destination ? Math.Min(quantity, PickupCarryCapacity(actor, shore, destination)) : quantity;
    }

    private VesselActionPlan? PlanStoredJugPickup(string actor, VesselChoice choice, string household, PlacedBuilding house)
    {
        foreach (var match in FindGoods(new(GoodsUse.VesselFor, actor, GoodsOwners.One(household),
                     GoodsKinds.One(InventoryContainerRules.WaterJug), ExtraUnits: 1, ContentsKind: InventoryContainerRules.FreshWater)).Matches)
        {
            var jug = match.Lot;
            if (jug.CarrierId is not null || !CanCollectJugForRefill(actor, jug) ||
                WaterJugStockLocation(jug, household) is not { } source ||
                FindFreshWaterShore(actor, source.Position, match.MoveUnits, house.Position) is not { } shore) continue;
            var plan = CreateVesselPlan(actor, choice, household, house, jug, source.Position, 1, shore: shore, returnTo: house.Position);
            if (CheckVesselPlan(plan).Allowed) return plan;
        }
        return null;
    }

    private VesselActionPlan? PlanEmptyVesselPickup(string actor, VesselChoice choice, string household, PlacedBuilding house)
    {
        if (!CanStartEmptyVesselReturn(actor, house.InstanceId)) return null;
        foreach (var match in FindGoods(new(GoodsUse.VesselFor, actor, GoodsOwners.One(household),
                     new GoodsKinds([InventoryContainerRules.StoragePot, InventoryContainerRules.WaterJug, InventoryContainerRules.Handcart])))
                     .Matches.Where(match => choice.StockId is null || match.Lot.Id == choice.StockId).OrderBy(match => match.Lot.Id, StringComparer.Ordinal))
        {
            var vessel = match.Lot;
            if (!IsEmptyWorkplaceVessel(vessel, house.InstanceId) ||
                worldSimulation.Buildings.SingleOrDefault(building => building.InstanceId == vessel.StorageBuildingId) is not { } source) continue;
            var plan = CreateVesselPlan(actor, choice with { StockId = vessel.Id }, household, house, vessel, source.Position, 1);
            if (CheckVesselPlan(plan).Allowed) return plan;
        }
        return null;
    }

    private bool CanCollectJugForRefill(string actor, InventoryLot jug) =>
        CarriedContainer(actor, InventoryContainerRules.WaterJug) is null &&
        ContainerFamilyQuantity(society.Checkpoint.Inventory, jug.Id) < FreeCarryCapacity(actor) &&
        CanRemoveWorkstationStock(society.Checkpoint.Inventory, jug, 1);

    private bool CanStartEmptyVesselReturn(string actor, string houseId) =>
        CarriedHouseDelivery(actor) is null && FreeCarryCapacity(actor) >= 1 && DestinationRoom(houseId) >= 1;

    private bool IsEmptyWorkplaceVessel(InventoryLot vessel, string houseId) =>
        vessel.CarrierId is null && vessel.GroundPosition is null && vessel.StorageBuildingId is { } storage && storage != houseId &&
        ContainerContentsQuantity(society.Checkpoint.Inventory, vessel.Id) == 0 &&
        worldSimulation.Buildings.Any(source => source.InstanceId == storage && source.HouseholdId == vessel.OwnerId &&
            worldContent.Buildings.Any(definition => definition.CanonicalId == source.DefinitionId && HouseholdBuildingKind(definition) is not null));
    private const string ReturnEmptyVesselPrefix = VesselChoice.EmptyPrefix;

    private void AddEmptyVesselReturnCandidate(List<CognitionCandidate> candidates, string actor, AgentState person)
    {
        if (PlanVesselAction(actor, new(VesselActionKind.ReturnEmpty)) is not { } plan) return;
        AddVesselCandidate(candidates, plan, "Bring an empty household vessel back to the House for reuse.", 22, plan.SourceBuildingId);
    }

    private static class InventoryActions
    {
        internal static readonly ActionDefinition<OrnamentRequestChoice, OrnamentActionPlan> Ornament = new(
            DecodeReferenceChoice(OrnamentRequestChoice.Decode), static choice => choice.Encode(), OrnamentPolicy,
            static (world, actor, choice, _) => world.CurrentOrnamentPlan(actor, choice.CandidateId),
            static (world, plan, policy) => world.CheckOrnamentPlan(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedOrnament(plan); return true; });

        internal static readonly ActionDefinition<FoodCargoChoice, FoodCargoActionPlan> FoodCargo = new(
            FoodCargoChoice.TryDecode, static _ => FoodCargoChoice.Encode(), FoodCargoPolicy,
            static (world, actor, _, _) => world.PlanFoodCargo(actor),
            static (world, plan, policy) => world.CheckFoodCargoPlan(plan, policy),
            static (world, plan, _, _) => { world.ApplyCargoStorage(plan.Storage); return true; });
    }

    private void AddHandcartBuildMaterialCandidate(List<CognitionCandidate> candidates, HandcartBuildMaterialActionPlan? plan)
    {
        if (plan is not null && CheckActionPlan(plan).Allowed)
            candidates.Add(new(plan.CandidateId,
                $"Collect nearby {plan.Choice.ItemKind.Replace('_', ' ')} and carry it to build a handcart at your household Blacksmith.",
                25)
            { ActionPlan = plan });
    }

    private void ApplyHandcartBuildMaterialPlan(HandcartBuildMaterialActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        TransportSystem.ApplyCheckedHandcartBuildMaterial(HandcartWorld, plan);
    }

    private void AddHandcartCargoCandidate(List<CognitionCandidate> candidates, HandcartCargoActionPlan? plan)
    {
        if (plan is not null && CheckActionPlan(plan).Allowed)
            candidates.Add(new(plan.CandidateId, plan.Choice.Kind switch
            {
                HandcartCargoKind.Load => $"Load up to {plan.DisplayQuantity} nearby {plan.ItemKind.Replace('_', ' ')} into your handcart.",
                HandcartCargoKind.Unload => "Unload cart goods into your carried load, within your carrying limit.",
                _ => "Unload cart goods onto the ground here, keeping your ownership.",
            }, UnassignedCartPriority)
            { ActionPlan = plan });
    }

    private void ApplyHandcartCargoPlan(HandcartCargoActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        TransportSystem.ApplyCheckedHandcartCargo(HandcartWorld, plan);
    }

    private void AddHandcartControlCandidate(List<CognitionCandidate> candidates, HandcartControlActionPlan? plan, string description)
    {
        if (plan is not null && CheckActionPlan(plan).Allowed)
            candidates.Add(new(plan.CandidateId, description, UnassignedCartPriority) { ActionPlan = plan });
    }

    private void ApplyHandcartControlPlan(HandcartControlActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        TransportSystem.ApplyCheckedHandcartControl(HandcartWorld, plan);
    }

    private void AddHandcartPullCandidate(List<CognitionCandidate> candidates, HandcartPullActionPlan? plan)
    {
        if (plan is not null && agents[plan.Actor].Position != plan.Destination && CheckActionPlan(plan).Allowed)
            candidates.Add(new(plan.CandidateId, plan.Route.Count > 0
                ? "Pull the handcart and its cargo to this building along a legal route."
                : "The cart's route to this building is blocked. Wait for a route or park the cart here.",
                UnassignedCartPriority, plan.Choice.BuildingId)
            { ActionPlan = plan });
    }

    private void ApplyHandcartPullPlan(HandcartPullActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        TransportSystem.ApplyCheckedHandcartPull(HandcartWorld, plan);
    }

    private void AddHandcartRepairCandidate(List<CognitionCandidate> candidates, ActionPlan? plan)
    {
        if (plan is not null && CheckActionPlan(plan).Allowed)
            candidates.Add(new(plan.CandidateId, plan is HandcartRepairInputActionPlan input
                ? $"Collect physically accessible {input.Choice.ItemKind.Replace('_', ' ')} to repair your handcart."
                : "Reach and repair your handcart using one carried wood, iron fittings and rope.",
                plan is HandcartRepairInputActionPlan ? 21 : 18)
            { ActionPlan = plan });
    }

    private void ApplyHandcartRepairPlan(HandcartRepairActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        TransportSystem.ApplyCheckedHandcartRepair(HandcartWorld, plan);
    }

    private void ApplyHandcartRepairInputPlan(HandcartRepairInputActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        TransportSystem.ApplyCheckedHandcartRepairInput(HandcartWorld, plan);
    }

    private static ActorCapabilityPolicy TransportCapabilities =>
        new(ActionLifeStages.Adults, AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true);
    private static ActorCapabilityPolicy BoatTripCapabilities =>
        new(ActionLifeStages.Adults, AllowMounted: false, AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true);
    private static ActorCapabilityPolicy HandcartControlCapabilities(HandcartControlKind kind) =>
        new(ActionLifeStages.Adults, AllowMounted: kind != HandcartControlKind.Attach,
            AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true);

    private static class TransportActions
    {
        internal static readonly ActionDefinition<BoatTripRequestChoice, BoatTripActionPlan> BoatTrip = new(
            DecodeReferenceChoice(BoatTripRequestChoice.Decode), static choice => choice.Encode(), BoatTripCapabilities,
            static (world, actor, choice, _) => TransportSystem.BoatTripChoices(world.BoatWorld, actor).FirstOrDefault(item => item.Id == choice.CandidateId)?.Plan,
            static (world, plan, policy) => TransportSystem.CheckBoatTripPlan(world.BoatWorld, plan, policy),
            static (world, plan, _, _) => { TransportSystem.ApplyCheckedBoatTrip(world.BoatWorld, plan); return true; });

        internal static readonly ActionDefinition<BoatWaitingChoice, BoatWaitingActionPlan> BoatWaiting = new(
            DecodeReferenceChoice(BoatWaitingChoice.Decode), static choice => choice.Encode(), TransportCapabilities,
            static (world, actor, choice, _) => TransportSystem.PlanBoatWaiting(world.BoatWorld, actor, choice),
            static (world, plan, policy) => TransportSystem.CheckBoatWaitingPlan(world.BoatWorld, plan, policy),
            static (world, plan, _, _) => { TransportSystem.ApplyCheckedBoatWaiting(world.BoatWorld, plan); return true; });

        internal static readonly ActionDefinition<HandcartControlChoice, HandcartControlActionPlan> Control = new(
            DecodeReferenceChoice(HandcartControlChoice.Decode), static choice => choice.Encode(), HandcartControlCapabilities(HandcartControlKind.Attach),
            static (world, actor, choice, _) => TransportSystem.PlanHandcartControl(world.HandcartWorld, actor, choice, null),
            static (world, plan, policy) => TransportSystem.CheckHandcartControlPlan(world.HandcartWorld, plan, policy),
            static (world, plan, _, _) => { TransportSystem.ApplyCheckedHandcartControl(world.HandcartWorld, plan); return true; },
            PlanCapabilities: static plan => HandcartControlCapabilities(plan.Choice.Kind));

        internal static readonly ActionDefinition<HandcartBuildMaterialChoice, HandcartBuildMaterialActionPlan> BuildMaterial = new(
            DecodeReferenceChoice(HandcartBuildMaterialChoice.Decode), static choice => choice.Encode(), TransportCapabilities,
            static (world, actor, choice, _) => TransportSystem.PlanHandcartBuildMaterial(world.HandcartWorld, actor, choice, null),
            static (world, plan, policy) => TransportSystem.CheckHandcartBuildMaterialPlan(world.HandcartWorld, plan, policy),
            static (world, plan, _, _) => { TransportSystem.ApplyCheckedHandcartBuildMaterial(world.HandcartWorld, plan); return true; });

        internal static readonly ActionDefinition<HandcartRepairInputChoice, HandcartRepairInputActionPlan> RepairInput = new(
            DecodeReferenceChoice(HandcartRepairInputChoice.Decode), static choice => choice.Encode(), TransportCapabilities,
            static (world, actor, choice, _) => TransportSystem.PlanHandcartRepairInput(world.HandcartWorld, actor, choice),
            static (world, plan, policy) => TransportSystem.CheckHandcartRepairInputPlan(world.HandcartWorld, plan, policy),
            static (world, plan, _, _) => { TransportSystem.ApplyCheckedHandcartRepairInput(world.HandcartWorld, plan); return true; });

        internal static readonly ActionDefinition<HandcartPullChoice, HandcartPullActionPlan> Pull = new(
            DecodeReferenceChoice(HandcartPullChoice.Decode), static choice => choice.Encode(), TransportCapabilities,
            static (world, actor, choice, _) => TransportSystem.PlanHandcartPull(world.HandcartWorld, actor, choice),
            static (world, plan, policy) => TransportSystem.CheckHandcartPullPlan(world.HandcartWorld, plan, policy),
            static (world, plan, _, _) => { TransportSystem.ApplyCheckedHandcartPull(world.HandcartWorld, plan); return true; });

        internal static readonly ActionDefinition<HandcartCargoChoice, HandcartCargoActionPlan> Cargo = new(
            DecodeReferenceChoice(HandcartCargoChoice.Decode), static choice => choice.Encode(), TransportCapabilities,
            static (world, actor, choice, _) => TransportSystem.PlanHandcartCargo(world.HandcartWorld, actor, choice, null, int.MaxValue),
            static (world, plan, policy) => TransportSystem.CheckHandcartCargoPlan(world.HandcartWorld, plan, policy),
            static (world, plan, _, _) => { TransportSystem.ApplyCheckedHandcartCargo(world.HandcartWorld, plan); return true; });

        internal static readonly ActionDefinition<HandcartRepairChoice, HandcartRepairActionPlan> Repair = new(
            DecodeReferenceChoice(HandcartRepairChoice.Decode), static choice => choice.Encode(), TransportCapabilities,
            static (world, actor, choice, _) => TransportSystem.PlanHandcartRepair(world.HandcartWorld, actor, choice),
            static (world, plan, policy) => TransportSystem.CheckHandcartRepairPlan(world.HandcartWorld, plan, policy),
            static (world, plan, _, _) => { TransportSystem.ApplyCheckedHandcartRepair(world.HandcartWorld, plan); return true; });
    }
}

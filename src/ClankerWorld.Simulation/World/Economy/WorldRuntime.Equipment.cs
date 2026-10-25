using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using System.Collections.ObjectModel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private int FreeCarryCapacity(string actor) => Math.Max(0, PersonalEquipmentRules.Capacity(
        society.Checkpoint.Inventory, actor, agents[actor].Equipment) + HorseCargoCapacity(actor) -
        PersonalEquipmentRules.CarriedQuantity(society.Checkpoint.Inventory, actor, agents[actor].Equipment) - this.marketSystem.ReservedShopCarrySpace(this.MarketShopWorld, actor, null));

    private string EquipmentNote(string actor)
    {
        var inventory = society.Checkpoint.Inventory;
        var equipment = agents[actor].Equipment;
        var clothing = PersonalEquipmentRules.EquippedUnit(inventory, actor, equipment?.ClothingLotId);
        var aid = PersonalEquipmentRules.EquippedUnit(inventory, actor, equipment?.CarryAidLotId);
        var ornament = PersonalEquipmentRules.EquippedUnit(inventory, actor, equipment?.OrnamentLotId);
        return $"Carrying {PersonalEquipmentRules.CarriedQuantity(inventory, actor, equipment)} of " +
            $"{PersonalEquipmentRules.Capacity(inventory, actor, equipment) + HorseCargoCapacity(actor)} units. Wearing " +
            $"{clothing?.ItemKind.Replace('_', ' ') ?? "no garment"}. Carry aid: {aid?.ItemKind ?? "none"}." +
            (ornament is null ? string.Empty : $" Ornament: {ornament.ItemKind.Replace('_', ' ')}.");
    }

    private InventoryLot? EquippedGarment(string actor) =>
        PersonalEquipmentRules.EquippedUnit(society.Checkpoint.Inventory, actor,
            agents[actor].Equipment?.ClothingLotId) is { } selected && AvailableLotQuantity(selected) > 0
            ? selected : null;

    private int ClothingProtection(string actor, GridPoint position) => EquippedGarment(actor) is { } garment
        ? PersonalEquipmentRules.Protection(garment, WeatherAt(position)) : 0;

    private bool CanEquipPrivateItem(string actor, InventoryLot item, bool carryAid)
    {
        var inventory = society.Checkpoint.Inventory;
        var equipment = agents[actor].Equipment;
        var previous = PersonalEquipmentRules.EquippedUnit(inventory, actor,
            carryAid ? equipment?.CarryAidLotId : equipment?.ClothingLotId);
        var before = PersonalEquipmentRules.CarriedQuantity(inventory, actor, equipment);
        // The new unit fills the slot; a displaced unit stays real cargo.
        var after = before + (previous is null ? 0 : 1) -
            (PersonalEquipmentRules.IsCarried(item, actor) ? 1 : 0);
        var capacity = carryAid ? CarryAidCapacity(item.ItemKind) : PersonalEquipmentRules.Capacity(inventory, actor, equipment);
        return after <= before && capacity >= PersonalEquipmentRules.Capacity(inventory, actor, equipment) ||
            after + this.marketSystem.ReservedShopCarrySpace(this.MarketShopWorld, actor, null) <= capacity + HorseCargoCapacity(actor);
    }

    private int MissingRepairInputUnits(string actor, InventoryLot item) =>
        PersonalEquipmentRules.RepairMaterials(item.ItemKind).Sum(input => Math.Max(0, input.Amount -
            (int)Math.Min(int.MaxValue, SpendingGoods(actor, GoodsUse.Spend, [actor], input.ResourceId)
                .Where(match => PersonalEquipmentRules.IsCarried(match.Lot, actor)).Sum(match => (long)match.Quantity))));

    private static GoodsRequest PrivateEquipmentRequest(string actor, string owner) => new(
        GoodsUse.Equip, actor, GoodsOwners.One(owner),
        new GoodsKinds(["clothing", "padded_coat", "rain_cloak", "basket", "sack", "leather_sack"]));

    private IEnumerable<InventoryLot> PrivateEquipmentSources(string actor) =>
        FindGoods(PrivateEquipmentRequest(actor, actor)).Matches.Select(match => match.Lot)
        // Borrowed goods, carried by this agent or by another member, are not theirs to take.
        .Where(lot => PersonalEquipmentRules.IsCarried(lot, actor))
        .Concat(society.Checkpoint.GetAgent(actor).HouseholdId is { } household
            ? FindGoods(PrivateEquipmentRequest(actor, household)).Matches.Select(match => match.Lot)
                .Where(lot => lot.CarrierId is null)
            : [])
        .Concat(AvailableWarehouseStock(actor).Where(lot => RecheckGoods(PrivateEquipmentRequest(actor, lot.OwnerId), lot.Id) is not null)).DistinctBy(lot => lot.Id);

    private InventoryLot? BetterGarment(string actor)
    {
        var weather = WeatherAt(agents[actor].Position);
        return PrivateEquipmentSources(actor).Where(lot => IsPrivateEquipmentUpgrade(actor, lot, false))
            .OrderByDescending(lot => PersonalEquipmentRules.Protection(lot, weather))
            .ThenBy(lot => lot.Id, StringComparer.Ordinal).FirstOrDefault();
    }

    private InventoryLot? BetterCarryAid(string actor)
    {
        return PrivateEquipmentSources(actor).Where(lot => IsPrivateEquipmentUpgrade(actor, lot, true))
            .OrderByDescending(lot => lot.ItemKind == "leather_sack" ? 2 : lot.ItemKind == "sack" ? 1 : 0)
            .ThenBy(lot => lot.Id, StringComparer.Ordinal).FirstOrDefault();
    }

    private InventoryLot? RepairableEquipment(string actor, AgentState person) =>
        WornEquipmentItems(actor).FirstOrDefault(lot => CanPrepareEquipmentRepair(actor, person, lot));

    private bool CanPrepareEquipmentRepair(string actor, AgentState person, InventoryLot lot) =>
        PlanEquipmentRepair(actor, lot) is not null;

    private IEnumerable<InventoryLot> WornEquipmentItems(string actor) =>
        FindGoods(EquipmentRepairTargetRequest(actor)).Matches.Select(match => match.Lot)
            .OrderBy(lot => lot.Id == agents[actor].Equipment?.CarryAidLotId ? 0 : 1)
            .ThenBy(lot => lot.Id, StringComparer.Ordinal);

    private PlacedBuilding? EquipmentRepairSite(string actor, InventoryLot lot) =>
        HouseholdBuildingWithTag(HouseholdFor(actor), lot.ItemKind == "basket" ? "house" : "tailor");

    private void AddEquipmentCandidates(List<CognitionCandidate> candidates, string actor, AgentState person)
    {
        if (OutdoorExposure(person.Position) > 0 && BetterGarment(actor) is { } garment)
            AddPrivateEquipmentCandidate(candidates, PlanPrivateEquipment(actor, false, garment), $"Collect and wear {garment.ItemKind.Replace('_', ' ')} to keep warm outdoors.", 3);
        if (!IsAdult(actor)) return;
        if (BetterCarryAid(actor) is { } aid)
            AddPrivateEquipmentCandidate(candidates, PlanPrivateEquipment(actor, true, aid), $"Equip a {aid.ItemKind} to carry more supplies.", 14);
        if (global::ClankerWorld.Simulation.World.WorldRuntime.EquipmentActions.Repair.Offer(this, actor, new()) is { } repair)
            candidates.Add(new(EquipmentRepairChoice.Encode(), $"Bring materials to repair the worn {repair.ItemKind.Replace('_', ' ')}.", 12, repair.BuildingId)
            { ActionPlan = repair });
    }

    private void AddPrivateEquipmentCandidate(List<CognitionCandidate> candidates, PrivateEquipmentActionPlan? plan, string description, int priority)
    {
        if (plan is not null && CheckActionPlan(plan).Allowed)
            candidates.Add(new(plan.CandidateId, description, priority) { ActionPlan = plan });
    }

    private void CompletePrivateEquipment(string actor, InventoryLot item, bool carryAid)
    {
        var equippedId = item.Id;
        var previous = agents[actor].Equipment;
        var equipment = previous ?? new PersonalEquipment();
        try
        {
            RunInventoryCommand(InventorySystem.EquipPrivateEquipment(InventoryEconomyWorld, actor, item, carryAid, equippedId, inventoryResultValue => equippedId = inventoryResultValue, equipment));
        }
        catch
        {
            agentStore.RecordEquipment(actor, agents[actor], previous);
            throw;
        }
        checkpointSchemaVersion = StateSchemaVersion;
        AppendEvent(new EquipmentEquippedEventPayload(actor, equippedId, item.ItemKind));
    }

    private bool CanContinueEquipmentRepair(string actor) => agents[actor].Equipment?.Repair is { } repair &&
        !NonLandEquipmentRepairPaused(actor) &&
        !NeedsUrgentFood(agents[actor]) && !NeedsUrgentWarmth(agents[actor]) &&
        repair.MaterialReservationIds.All(id => society.Checkpoint.Inventory.GetReservation(id) is
        { State: InventoryReservationState.Reserved } reservation && reservation.ExpiryTick >= WorldTick);

    private void RepairEquipment(string actor, AgentState person, string? requestedLotId = null, string? orderInstructionId = null)
    {
        var target = requestedLotId is null ? RepairableEquipment(actor, person) : WornEquipmentItems(actor).FirstOrDefault(lot => lot.Id == requestedLotId);
        if (target is null) return;
        ApplyEquipmentRepairPlan(PlanEquipmentRepair(actor, target), orderInstructionId);
    }

    private void StartEquipmentRepair(string actor, AgentState person, string targetId, string buildingId,
        IReadOnlyList<PlannedRepairMaterial> materials, string? orderInstructionId = null)
    {
        var ids = new List<string>();
        RunInventoryCommand(InventorySystem.StartEquipmentRepair(InventoryEconomyWorld, materials, actor, ids));
        agentStore.RecordEquipment(actor, agents[actor], (person.Equipment ?? new PersonalEquipment()) with
        { Repair = new(targetId, buildingId, WorldTick, 0, ids.Select(Identity.Parse).ToArray(), orderInstructionId) });
        checkpointSchemaVersion = StateSchemaVersion;
        AppendEvent(new EquipmentRepairStartedEventPayload(actor, targetId, buildingId));
    }

    private EquipmentRepairWork? ContinueEquipmentRepair(string actor)
    {
        var person = agents[actor];
        if (person.Equipment?.Repair is not { } repair) return null;
        RecordAttemptedAction(actor, "repair_equipment");
        if (!CanContinueEquipmentRepair(actor) ||
            worldSimulation.Buildings.FirstOrDefault(item => item.InstanceId == repair.BuildingId) is not { } site ||
            site.HouseholdId != HouseholdFor(actor) || person.Position != site.Position ||
            InventoryIndex.For(society.Checkpoint.Inventory).Find(repair.LotId) is not { } target ||
            !PersonalEquipmentRules.IsCarried(target, actor) || target.Quantity != 1 ||
            repair.MaterialReservationIds.Any(id => society.Checkpoint.Inventory.GetReservation(id) is not
            { State: InventoryReservationState.Reserved } reservation || reservation.ExpiryTick < WorldTick))
        {
            CancelEquipmentRepair(actor);
            return null;
        }
        if (!IllnessRules.AllowsWork(actor, WorldTick, person.Survival?.IllnessBasisPoints ?? 0)) return null;
        var progress = repair.WorkDone + 1;
        if (progress < SkilledWorkTicks(actor, SkillKind.Crafting, PersonalEquipmentRules.RepairWorkTicks))
        {
            agentStore.RecordEquipment(actor, person, person.Equipment with { Repair = repair with { WorkDone = progress } });
            return null;
        }
        RunInventoryCommand(InventorySystem.ContinueEquipmentRepair(repair, actor));
        agentStore.RecordEquipment(actor, agents[actor], person.Equipment with { Repair = null });
        CommunitySystem.GainSkill(this.CommunitySkillWorld, actor, SkillKind.Crafting, null);
        RecordNonLandRepairCompletion(actor, target, repair.LotId,
            $"repair-equipment:{repair.StartedTick}:{WorldTick}:{actor}:{repair.LotId}", person.Position, repair.MaterialReservationIds);
        AppendEvent(new EquipmentRepairedEventPayload(actor, repair.LotId));
        return repair;
    }

    private void CancelEquipmentRepair(string actor)
    {
        if (agents[actor].Equipment?.Repair is not { } repair) return;
        RunInventoryCommand(InventorySystem.CancelEquipmentRepair(repair));
        var person = agents[actor];
        agentStore.RecordEquipment(actor, person, person.Equipment! with { Repair = null });
        AppendEvent(new EquipmentRepairInterruptedEventPayload(actor, repair.LotId));
    }

    private void WearEquippedClothing()
    {
        foreach (var person in agents.Values.ToArray())
        {
            if (person.Equipment?.Repair is not null && !CanContinueEquipmentRepair(person.AgentId))
                CancelEquipmentRepair(person.AgentId);
            // Garments wear in cold or wet weather only. The night chill is
            // felt as lost warmth, but adds no wear the agreed design did not ask for.
            if (person.Equipment?.Repair?.LotId == person.Equipment?.ClothingLotId ||
                WeatherExposure(person.Position) == 0 || EquippedGarment(person.AgentId) is not { } garment) continue;
            RunInventoryCommand(InventorySystem.WearEquippedClothing(garment, person));
        }
    }

    private void WearCarryAid(string actor)
    {
        if (agents[actor].Equipment?.Repair is not null ||
            PersonalEquipmentRules.EquippedUnit(society.Checkpoint.Inventory, actor, agents[actor].Equipment?.CarryAidLotId) is not { } aid ||
            AvailableLotQuantity(aid) == 0 || PersonalEquipmentRules.CarriedQuantity(society.Checkpoint.Inventory, actor,
                agents[actor].Equipment) == 0) return;
        RunInventoryCommand(InventorySystem.WearCarryAid(aid, actor));
    }

    private static ActionDefinition<VesselChoice, VesselActionPlan> HouseholdVesselDefinition(VesselActionKind kind) => EquipmentActions.Vessels[(int)kind];

    private static class EquipmentActions
    {
        internal static readonly ActionDefinition<PrivateEquipmentKind, PrivateEquipmentActionPlan> Garment = CreateEquipment(PrivateEquipmentKind.Garment);
        internal static readonly ActionDefinition<PrivateEquipmentKind, PrivateEquipmentActionPlan> CarryAid = CreateEquipment(PrivateEquipmentKind.CarryAid);
        internal static readonly ActionDefinition<EquipmentRepairChoice, EquipmentRepairActionPlan> Repair = new(
            EquipmentRepairChoice.TryDecode, static _ => EquipmentRepairChoice.Encode(), EquipmentRepairPolicy,
            static (world, actor, _, _) => world.PlanEquipmentRepair(actor),
            static (world, plan, policy) => world.CheckEquipmentRepairPlan(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedEquipmentRepairPlan(plan); return true; });
        internal static readonly ReadOnlyCollection<ActionDefinition<VesselChoice, VesselActionPlan>> Vessels =
            Array.AsReadOnly(Enum.GetValues<VesselActionKind>().Select(CreateVessel).ToArray());

        private static ActionDefinition<PrivateEquipmentKind, PrivateEquipmentActionPlan> CreateEquipment(PrivateEquipmentKind kind) => new(
            (string candidate, out PrivateEquipmentKind choice) => PrivateEquipmentActionPlan.TryDecode(candidate, out choice) && choice == kind,
            PrivateEquipmentActionPlan.Candidate, PrivateEquipmentPolicy(kind),
            static (world, actor, choice, _) => world.PlanPrivateEquipment(actor, choice == PrivateEquipmentKind.CarryAid),
            static (world, plan, policy) => world.CheckPrivateEquipmentPlan(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedPrivateEquipmentPlan(plan); return true; });

        private static ActionDefinition<VesselChoice, VesselActionPlan> CreateVessel(VesselActionKind kind) => new(
            (string candidate, out VesselChoice choice) =>
            {
                choice = VesselChoice.Decode(candidate)!;
                return choice is not null && choice.Kind == kind;
            },
            static choice => choice.Encode(), VesselPolicy(kind),
            static (world, actor, choice, _) => world.PlanVesselAction(actor, choice),
            static (world, plan, policy) => world.CheckVesselPlan(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedVesselPlan(plan); return true; });
    }

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

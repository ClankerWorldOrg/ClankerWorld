using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private int FreeCarryCapacity(string actor) => Math.Max(0, PersonalEquipmentRules.Capacity(
        society.Checkpoint.Inventory, actor, agents[actor].Equipment) + HorseCargoCapacity(actor) -
        PersonalEquipmentRules.CarriedQuantity(society.Checkpoint.Inventory, actor, agents[actor].Equipment) - ReservedShopCarrySpace(actor));

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
            after + ReservedShopCarrySpace(actor) <= capacity + HorseCargoCapacity(actor);
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
}

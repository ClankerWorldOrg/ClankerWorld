using System.Text.Json.Serialization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

/// <summary>A completed membership exit, including the once-only allowance and limited collection right.</summary>
public sealed record HouseholdDeparture(string HouseholdId, long Tick, string Cause,
    IReadOnlyList<string> CareGroup, IReadOnlyList<string> AllowanceLotIds, int AllowancePortions,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] AgentProject? SharedProject = null);

public sealed partial class WorldRuntime
{
    private IReadOnlyList<string> MovingCareGroup(string actor) => SocietyRules.MovingCareGroup(society.Checkpoint, actor);

    private bool CanFitCareGroup(string householdId, string actor)
    {
        if (HouseForHousehold(householdId) is not { } house) return false;
        var definition = BuildingStorageRules.EffectiveDefinition(
            worldContent.Buildings.Single(item => item.CanonicalId == house.DefinitionId), house);
        var group = MovingCareGroup(actor).ToHashSet(StringComparer.Ordinal);
        var residents = society.Checkpoint.Agents.Where(person => person.Status == SocietyAgentStatus.Active &&
            (person.HouseholdId == householdId || group.Contains(person.Id)));
        return !HouseResidentCapacityRules.Calculate(residents, definition.Width, definition.Height).IsOvercrowded;
    }

    /// <summary>The displacement caller must decide eligibility and notice; this performs the same authoritative exit as a voluntary move.</summary>
    public bool DisplaceAdult(string actor)
    {
        gate.Wait();
        try { return agents.ContainsKey(actor) && !HasDependentInPrimaryCare(actor) && DepartHousehold(actor, "displaced"); }
        finally { gate.Release(); }
    }

    private bool DepartHousehold(string actor, string cause, DepartureActionPlan? originalPlan = null) =>
        CommunitySystem.DepartHousehold(CommunityDepartureWorld, actor, cause, originalPlan);

    private void RestoreHouseholdDeliveries(string actor, string householdId, string transition)
    {
        // Every moving member's unfinished House delivery is borrowed household stock.
        // Restore its owner and clear the old promise, keeping its actual physical carrier.
        foreach (var lot in society.Checkpoint.Inventory.Lots.Where(lot => lot.OwnerId == actor &&
                     lot.ContainerLotId is null && lot.DeliveryBuildingId is { } delivery && worldSimulation.Buildings.Any(building =>
                         building.InstanceId == delivery && building.HouseholdId == householdId)).ToArray())
        {
            var carrier = lot.CarrierId ?? actor;
            RunInventoryCommand(InventorySystem.RestoreDeliveryOwnership(InventoryCommunityWorld, transition, actor, lot, householdId));
            RunInventoryCommand(InventorySystem.RestoreDeliveryCustody(InventoryCommunityWorld, transition, actor, lot, householdId, carrier));
        }
    }

    private int PhysicalUnreservedQuantity(InventoryLot lot) =>
        Math.Max(0, InventoryRules.UnreservedQuantity(InventoryIndex.For(society.Checkpoint.Inventory), lot));

    private IEnumerable<InventoryLot> PersonalGoodsAwaitingCollection(string actor) =>
        PersonalRecoveryRequest(actor, GoodsUse.RecoveryHoldings) is { } request
            ? FindGoods(request).Matches.Select(match => match.Lot) : [];

    // Children leave in the caregiver's recorded care group. That saved move
    // still authorizes their own belongings after adulthood or the caregiver's
    // death; the surrounding collection checks grant no access to shared goods.
    private bool HasCareGroupDepartureFrom(string actor, string householdId) => agents.Values
        .Concat(deceasedAgents.Values.Select(person => person.LastPhysical))
        .Any(person => person.Departures?.Any(departure => departure.HouseholdId == householdId &&
            departure.CareGroup.Contains(actor, StringComparer.Ordinal)) == true);

    private bool IsStoredSettledBequest(string actor, InventoryLot lot, string storageId) => society.Checkpoint.Estates.Any(estate =>
        estate.Settled && (estate.WillBequests ?? []).Any(bequest => bequest.HeirId == actor &&
            (bequest.LotId == lot.Id || bequest.LotId == lot.ProvenanceLotId) &&
            (estate.FrozenLots ?? []).Any(frozen => frozen.LotId == bequest.LotId && frozen.ItemKind == lot.ItemKind &&
                frozen.StorageBuildingId == storageId)));

    /// <summary>A vessel moves with its contents, so it needs room for all of them and nothing reserved.</summary>
    private bool VesselFits(InventoryLot lot, int room) => !InventoryContainerRules.IsContainer(lot.ItemKind) ||
        !HasActiveContainerReservation(society.Checkpoint.Inventory, lot.Id) &&
        room >= ContainerFamilyQuantity(society.Checkpoint.Inventory, lot.Id);

    private IEnumerable<InventoryLot> CarriedBorrowedGoods(string actor) => society.Checkpoint.Inventory.Lots.Where(lot =>
        lot.OwnerId != actor && lot.CarrierId == actor && lot.ContainerLotId is null && lot.Quantity > 0 &&
        !animalWorld.SupplyTrips.Any(trip => trip.ActorId == actor && trip.LotId == lot.Id));

    private IEnumerable<InventoryLot> BorrowedGoods(string actor) => CarriedBorrowedGoods(actor);

    private IEnumerable<(string Id, string BuildingId, string Worker, long Completion, long PausedAt, IReadOnlyList<Identity> Reservations)> PausedHouseholdWork(string actor) =>
        CommunitySystem.PausedHouseholdWork(CommunityWorkWorld, actor);

    private bool CanResumeHeldInputs(string actor, string buildingId, IReadOnlyList<Identity> reservationIds) =>
        CommunitySystem.CanResumeHeldInputs(CommunityWorkWorld, actor, buildingId, reservationIds);

    /// <summary>
    /// A knife-assisted recipe resumes only with a knife the resuming member
    /// carries: the departed worker's own knife, or this member's best one.
    /// </summary>
    private bool TryResumeKnife(string actor, string jobId, out string? knifeId) =>
        CommunitySystem.TryResumeKnife(CommunityWorkWorld, actor, jobId, out knifeId);

    private void CompleteHouseholdResume(HouseholdResumeActionPlan plan) =>
        CommunitySystem.CompleteHouseholdResume(CommunityWorkWorld, plan);

    private void ReconcilePausedHouseholdWork() =>
        CommunitySystem.ReconcilePausedHouseholdWork(CommunityWorkWorld);

    /// <summary>Maps, field records and books are the belongings the built-in chooser stores at home and leaves there.</summary>
    private static bool KeptAtHomeByRoutine(string itemKind) => AgentKnowledgeRules.IsArtifactKind(itemKind);

    private void AddDepartureCandidates(List<CognitionCandidate> candidates, string actor)
    {
        if (!IsAdult(actor) || !ReadyForBriefInteraction(actor)) return;
        foreach (var job in PausedHouseholdWork(actor))
            if (HouseholdResumeDefinition.Offer(this, actor, new(job.Id)) is { } resume)
                candidates.Add(new CognitionCandidate(resume.CandidateId,
                    "Go to your household's building and take over its paused work using the same committed materials.", 17, job.BuildingId)
                { ActionPlan = resume });
        var home = society.Checkpoint.GetAgent(actor).HouseholdId;
        if (home is not null)
            if (DepartureDefinition.Offer(this, actor, new(DepartureChoiceKind.Leave)) is { } leave)
                candidates.Add(new CognitionCandidate("household_leave", "Leave your household without a vote; keep responsibility for your dependent children and collect your personal belongings physically.", 115) { ActionPlan = leave });
        // Built-in rules never give up a home early: the notice period is time to find room
        // or finish an expansion, and the deadline moves the adult out when neither happens.
        if (agents[actor].Housing?.Request is null && MayFoundHousehold(actor) && !AskableHouseholds(actor).Any())
            if (DepartureDefinition.Offer(this, actor, new(DepartureChoiceKind.Found)) is { } found)
                candidates.Add(new CognitionCandidate("household_found", home is null
                    ? "Start your own household with your dependent children. A House still needs a legal site, materials and work."
                    : "Move out of your overcrowded House and start your own household with your dependent children. A House still needs a legal site, materials and work.",
                home is null ? 19 : 104)
                { ActionPlan = found });
        if (FreeCarryCapacity(actor) > 0)
        {
            foreach (var lot in RecoverablePersonalGoods(actor)
                         .OrderBy(lot => lot.Id, StringComparer.Ordinal))
            {
                var knowledgeAtHome = KeptAtHomeByRoutine(lot.ItemKind) &&
                    lot.StorageBuildingId is { } storageId &&
                    worldSimulation.Buildings.Any(building => building.InstanceId == storageId &&
                        building.HouseholdId == society.Checkpoint.GetAgent(actor).HouseholdId);
                // Knowledge safely stored at home is not a recovery errand.
                // Keep collection available to a deliberate choice without
                // making the built-in chooser undo its own storage next tick.
                var plan = PersonalGoodsDefinition.Offer(this, actor, new(PersonalGoodsKind.Collect, lot.Id));
                if (plan is not null)
                    candidates.Add(new(plan.CandidateId,
                        $"Physically collect your own {lot.ItemKind.Replace('_', ' ')}; other household stock remains private.",
                        knowledgeAtHome ? lot.ItemKind == "book" ? 190 : 110 : 20)
                    { ActionPlan = plan });
            }
        }
        foreach (var lot in BorrowedGoods(actor).OrderBy(lot => lot.Id, StringComparer.Ordinal))
        {
            var foreign = lot.OwnerId != society.Checkpoint.GetAgent(actor).HouseholdId && HouseForHousehold(lot.OwnerId) is { } ownerHouse &&
                DestinationRoom(ownerHouse.InstanceId) > 0 && VesselFits(lot, DestinationRoom(ownerHouse.InstanceId));
            var own = !foreign && lot.OwnerId == home && MarketTradeRules.IsLoose(lot) && !ProtectedMarketItem(actor, lot) &&
                PhysicalUnreservedQuantity(lot) > 0 && HouseForHousehold(lot.OwnerId) is { } ownHouse && DestinationRoom(ownHouse.InstanceId) > 0;
            if (!foreign && !own) continue;
            var plan = PersonalGoodsDefinition.Offer(this, actor, new(PersonalGoodsKind.Return, lot.Id));
            if (plan is not null)
                candidates.Add(new(plan.CandidateId, foreign
                    ? $"Physically return borrowed {lot.ItemKind.Replace('_', ' ')} to its owning household."
                    : $"Carry your household's {lot.ItemKind.Replace('_', ' ')} back to its House.", 22)
                { ActionPlan = plan });
        }
        if (home is not null && HouseForHousehold(home) is { } house && DestinationRoom(house.InstanceId) > 0)
        {
            foreach (var lot in PersonalStorageLots(actor, house.InstanceId))
            {
                var plan = PersonalGoodsDefinition.Offer(this, actor, new(PersonalGoodsKind.Store, lot.Id));
                if (plan is not null)
                    candidates.Add(new(plan.CandidateId, $"Store your own {lot.ItemKind.Replace('_', ' ')} in your House while keeping personal ownership.",
                        KeptAtHomeByRoutine(lot.ItemKind) ? 95 : 110)
                    { ActionPlan = plan });
            }
        }
        foreach (var child in society.Checkpoint.Agents.Where(person => person.Status == SocietyAgentStatus.Active &&
                     LifeStageRules.IsDependent(person.AgeBand) &&
                     person.HouseholdId is not null && person.HouseholdId == society.Checkpoint.GetAgent(actor).HouseholdId &&
                     person.PrimaryCaregiverId != actor &&
                     // A child with no living caregiver is placed through the guardian search instead.
                     SocietyRules.HasActivePrimaryCaregiver(society.Checkpoint, person.Id)))
            if (ReplacementCareDefinition.Offer(this, actor, new(child.Id)) is { } replacement)
                candidates.Add(new CognitionCandidate(replacement.CandidateId, $"Explicitly accept primary care of {child.Name}, so their current caregiver may leave without taking them.", 112) { ActionPlan = replacement });
    }

    /// <summary>
    /// An adult with no household may start one. So may a member of an
    /// overcrowded House who has a move-out notice, or whose House cannot grow
    /// any further even with the Council's land permission, taking their
    /// dependents with them.
    /// </summary>
    private bool MayFoundHousehold(string actor)
    {
        if (society.Checkpoint.GetAgent(actor).HouseholdId is not { } home) return true;
        if (MayRelocateFromHousehold(actor)) return true;
        return HouseResidentCapacity(home) is { IsOvercrowded: true } && HouseForHousehold(home) is { } house &&
            !HouseCanExpandFurther(house);
    }

    /// <summary>
    /// A House can still grow while an expansion is running, or when a larger
    /// footprint fits and the household already has the use of its extra land
    /// or could still get it by asking the Council.
    /// </summary>
    private bool HouseCanExpandFurther(PlacedBuilding house) =>
        HouseExpansionUnderWay(house) || HouseCanExpandNow(house) || HouseCanExpandWithLandPermission(house);

    // A paused expansion does not count: nobody may be able to resume it, and the
    // household must not be left with neither an expansion nor the split.
    private bool HouseExpansionUnderWay(PlacedBuilding house) => (worldSimulation.BuildingExpansions ?? []).Any(job =>
        job.BuildingInstanceId == house.InstanceId && job.State == WorldProductionJobState.Running);

    private bool HouseCanExpandNow(PlacedBuilding house) =>
        ExpansionShapes(house).Any(shape => CanFitExpansion(house, shape.Position, shape.Footprint, out _));

    /// <summary>
    /// A larger footprint fits, and every extra tile the household may not use
    /// yet is the Town's own land that no other household holds or has asked for.
    /// </summary>
    private bool HouseCanExpandWithLandPermission(PlacedBuilding house)
    {
        if (house.TownId is not { } townId || house.HouseholdId is not { } household) return false;
        var definition = worldContent.Buildings.Single(item => item.CanonicalId == house.DefinitionId);
        var original = WorldContentSimulationRules.Footprint(definition, house).ToHashSet();
        var heldByOthers = HouseholdLandHeldByOthers(household);
        return ExpansionShapes(house).Any(shape =>
            CanFitExpansion(house, shape.Position, shape.Footprint, out _, requireLandRights: false) &&
            WorldContentSimulationRules.Footprint(
                    BuildingStorageRules.WithSize(definition, shape.Footprint.Width, shape.Footprint.Height), shape.Position)
                .Where(tile => !original.Contains(tile) && !MayExpandOntoLand(house, tile))
                .All(tile => TownLandRightsRules.IsCoveredByTownTitle(tile, townId, townLandTitles) &&
                    !heldByOthers.Contains(tile)));
    }

    private void CompleteDepartureAction(DepartureActionPlan plan) =>
        CommunitySystem.CompleteDepartureAction(CommunityDepartureWorld, plan);

    private void CompleteReplacementCare(ReplacementCareActionPlan plan) =>
        CommunitySystem.CompleteReplacementCare(CommunityDepartureWorld, plan);

    private static void ValidateDepartures(IEnumerable<AgentState> physical, SocietyCheckpoint society, int schemaVersion)
    {
        var people = society.Agents.Select(person => person.Id).ToHashSet(StringComparer.Ordinal);
        var households = society.Households.Select(home => home.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var person in physical)
        {
            if (person.Departures is not { } departures) continue;
            CommunitySystem.ValidateLocalDepartures(person.AgentId, departures);
            foreach (var departure in departures)
                if (departure.SharedProject is { } plan) ValidateProject(plan, society.WorldTick);
            if (schemaVersion < 39 || departures.Any(item => !households.Contains(item.HouseholdId) || item.Tick > society.WorldTick ||
                item.CareGroup.Any(id => !people.Contains(id))))
                throw new InvalidDataException("The saved household departure record is invalid.");
        }
    }

    private string DepartureNote(string actor)
    {
        var goods = PersonalGoodsAwaitingCollection(actor).Sum(lot => lot.Quantity);
        var borrowed = BorrowedGoods(actor).Sum(lot => lot.Quantity);
        var children = MovingCareGroup(actor).Count(id => id != actor);
        var paused = PausedHouseholdWork(actor).ToArray();
        var blocked = paused.Count(job => !CanResumeHeldInputs(actor, job.BuildingId, job.Reservations) ||
            !TryResumeKnife(actor, job.Id, out _));
        return $"Personal collection: {goods} units; borrowed returns: {borrowed} units. Paused household work: {paused.Length}; {blocked} blocked by unavailable or privately held inputs or a missing knife. " +
            (children > 0 ? $"Primary care: {children} dependents move with you unless care is explicitly accepted." : "No dependent care group.");
    }

    private void MaintainMovingCareGroups()
    {
        foreach (var adult in agents.Values.Where(person => person.Departures is { Count: > 0 }).ToArray())
        {
            foreach (var childId in MovingCareGroup(adult.AgentId).Where(id => id != adult.AgentId))
            {
                if (!agents.TryGetValue(childId, out var child) || child.GuardianPlacement is not null ||
                    IsWithinInteractionRange(child.Position, adult.Position, 1)) continue;
                MoveToward(childId, child, adult.Position, "follow_caregiver", 1);
            }
        }
    }
}

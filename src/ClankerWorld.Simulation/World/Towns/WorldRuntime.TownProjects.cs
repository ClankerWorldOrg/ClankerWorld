using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private const string TownProjectSupplyPrefix = "town_project_supply:";
    private const string TownProjectDeliverPrefix = "town_project_deliver:";
    private const string TownProjectWorkPrefix = "town_project_work:";
    private const string TownProjectGatherPrefix = "town_project_gather:";
    private const string TownProjectRecoverPrefix = "town_project_recover:";
    private const string TownProjectReturnPrefix = "town_project_return:";
    private const string TownProjectDonatePrefix = "town_project_donate:";

    private sealed record TownProjectChoice(string Id, string Kind, TownRuntimeState Town,
        TownProject Project, string ItemKind = "", InventoryLot? Lot = null,
        int Quantity = 0, TownProjectDelivery? Delivery = null, MapResource? Resource = null,
        PlacedBuilding? Warehouse = null);

    private static string TownProjectChoiceId(string prefix, params string[] identity) => DerivedId.Hash(JsonSerializer.Serialize(identity), prefix);

    private static bool IsLiveTownProject(TownProject project) =>
        project.Stage is "supplying" or "working" or "blocked";

    private static bool IsTownProjectDonationCandidate(string id) =>
        id.StartsWith(TownProjectDonatePrefix, StringComparison.Ordinal);

    private static bool IsTownProjectCandidate(string id) =>
        id.StartsWith(TownProjectSupplyPrefix, StringComparison.Ordinal) ||
        id.StartsWith(TownProjectDeliverPrefix, StringComparison.Ordinal) ||
        id.StartsWith(TownProjectWorkPrefix, StringComparison.Ordinal) ||
        id.StartsWith(TownProjectGatherPrefix, StringComparison.Ordinal) ||
        id.StartsWith(TownProjectReturnPrefix, StringComparison.Ordinal) ||
        id.StartsWith(TownProjectRecoverPrefix, StringComparison.Ordinal);

    private bool IsActiveTownProjectDelivery(string lotId) => towns.Any(town => town.Projects.Any(project =>
        IsLiveTownProject(project) && project.Deliveries.Any(delivery => delivery.LotId == lotId &&
            delivery.ReleasedTick is null && delivery.DeliveredTick is null)));

    private bool ReadyForTownProject(string actor) => IsAdult(actor) &&
        !NeedsUrgentFood(agents[actor]) && !NeedsUrgentWarmth(agents[actor]) &&
        !IsConversationBusy(actor) && agents[actor].Equipment?.Repair is null &&
        agents[actor].Project is not { Stage: not ("completed" or "cancelled") } &&
        !fields.Any(field => field.Work?.WorkerId == actor);

    private void SetTownProject(string townId, TownProject project)
    {
        var town = towns.Single(item => item.Id == townId);
        SetTown(town with
        {
            Projects = town.Projects.Where(item => item.Id != project.Id).Append(project)
                .OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
        });
    }

    private bool TownProjectIncomingIsLive(string townId, TownProjectDelivery delivery)
    {
        var lot = society.Checkpoint.Inventory.Lots.SingleOrDefault(item => item.Id == delivery.LotId);
        return delivery.ReleasedTick is null && delivery.DeliveredTick is null && lot is not null &&
            lot.OwnerId == townId && lot.ItemKind == delivery.ItemKind && lot.Quantity == delivery.Quantity &&
            lot.ContainerLotId is null && lot.StorageBuildingId is null && lot.GroundPosition is null &&
            lot.DeliveryBuildingId is null && lot.CarrierId == delivery.ContributorId &&
            IsAdult(delivery.ContributorId) && TownForResident(delivery.ContributorId) == townId &&
            AvailableLotQuantity(lot) == delivery.Quantity;
    }

    private int TownProjectMissing(TownRuntimeState town, TownProject project, string itemKind)
    {
        var budget = project.Plan.Budget.Single(item => item.ResourceId == itemKind).Amount;
        var delivered = TownProjectRules.DeliveredQuantity(project, town.Id, society.Checkpoint.Inventory, itemKind);
        var incoming = project.Deliveries.Where(delivery => delivery.ItemKind == itemKind &&
            TownProjectIncomingIsLive(town.Id, delivery)).Sum(delivery => delivery.Quantity);
        return Math.Max(0, budget - delivered - incoming);
    }

    private bool TownProjectHasAllMaterials(TownRuntimeState town, TownProject project) =>
        project.Plan.Budget.All(cost => TownProjectRules.DeliveredQuantity(project, town.Id,
            society.Checkpoint.Inventory, cost.ResourceId) == cost.Amount);

    private TownProject ReleaseTownProjectClaims(TownProject project, string reason)
    {
        var deliveries = project.Deliveries.Select(delivery => delivery.ReleasedTick is null
            ? delivery with { ReleasedTick = WorldTick, ReleaseReason = reason } : delivery).ToArray();
        var claims = project.Deliveries.Where(delivery => delivery.ReleasedTick is null &&
                delivery.ReservationId is not null)
            .Select(delivery => delivery.ReservationId!).ToHashSet(StringComparer.Ordinal);
        if (claims.Count > 0)
            ApplyInventoryTransition(inventory =>
            {
                foreach (var reservation in inventory.Reservations.Where(item => claims.Contains(item.Id) &&
                             item.State is InventoryReservationState.Reserved or InventoryReservationState.PartiallyConsumed).ToArray())
                    inventory = InventoryAuthority.ReleaseReservation(inventory, reservation.Id, "town_project_material_released");
                return inventory;
            });
        return project with { Deliveries = deliveries, LastTransitionTick = WorldTick };
    }

    private void BlockTownProject(TownRuntimeState town, TownProject project, string failure)
    {
        if (project.Stage == "blocked" && project.Blocker == failure) return;
        project = ReleaseTownProjectClaims(project, failure) with { Stage = "blocked", Blocker = failure };
        SetTownProject(town.Id, project);
        AppendEvent("town_project_blocked", $"{town.Id}:{project.Id}:{failure}", TownProjectRules.WorkSite(project.Plan));
    }

    // A cancelled project keeps its goods where they are, releases its claims and no longer holds its site.
    private void CancelTownProject(TownRuntimeState town, TownProject project, string reason)
    {
        project = ReleaseTownProjectClaims(project, reason) with { Stage = "cancelled", Blocker = reason };
        SetTownProject(town.Id, project);
        AppendEvent("town_project_cancelled", $"{town.Id}:{project.Id}:{reason}", TownProjectRules.WorkSite(project.Plan));
    }

    // Wait only when a pending household request is the site's sole problem; it may still be refused.
    private bool TownProjectWaitsForLandRequest(TownRuntimeState town, TownProject project)
    {
        var footprint = TownProjectRules.Footprint(project.Plan).ToHashSet();
        return householdLandUseRequests.Any(request => request.Status == "pending" && request.Tiles.Any(footprint.Contains)) &&
            TownProjectSiteFailure(town, project.Plan, project.Id, ignorePendingRequests: true) is null;
    }

    private void MaintainTownProjects()
    {
        foreach (var originalTown in towns.ToArray())
        {
            foreach (var proposal in originalTown.Council?.Proposals.Where(item => item.Kind == "project" &&
                         item.Status == "passed" && item.Project is not null) ?? [])
            {
                var town = towns.Single(item => item.Id == originalTown.Id);
                var id = TownProjectRules.ProjectId(proposal);
                if (town.Projects.Any(project => project.Id == id)) continue;
                var project = new TownProject(id, proposal.Id, proposal.Project!,
                    proposal.SettledTick!.Value, "supplying", 0, WorldTick, []);
                SetTownProject(town.Id, project);
                AppendEvent("town_project_approved", $"{town.Id}:{project.Id}:{project.Plan.Name}", TownProjectRules.WorkSite(project.Plan));
            }
            foreach (var originalProject in towns.Single(item => item.Id == originalTown.Id).Projects.Where(IsLiveTownProject).ToArray())
            {
                var town = towns.Single(item => item.Id == originalTown.Id);
                var project = town.Projects.Single(item => item.Id == originalProject.Id);
                if (TownProjectSiteFailure(town, project.Plan, project.Id) is { } failure)
                {
                    // Only a pending household request can still be refused. Any other failure, such as a
                    // granted use right or a site taken during the vote, would hold the site forever.
                    if (TownProjectWaitsForLandRequest(town, project) || project.Plan.BoatPortId is { } portId &&
                        Port(portId) is { } port && PortIsLegal(port) && FreePortDock(port) is null)
                        BlockTownProject(town, project, failure);
                    else CancelTownProject(town, project, failure);
                    continue;
                }
                if (project.Stage == "blocked")
                {
                    project = project with { Stage = "supplying", Blocker = null, LastTransitionTick = WorldTick };
                    SetTownProject(town.Id, project);
                    AppendEvent("town_project_resumed", $"{town.Id}:{project.Id}", TownProjectRules.WorkSite(project.Plan));
                }
                var staleIncoming = project.Deliveries.Where(delivery => delivery.ReleasedTick is null &&
                    delivery.DeliveredTick is null && !TownProjectIncomingIsLive(town.Id, delivery)).Select(delivery => delivery.Id)
                    .ToHashSet(StringComparer.Ordinal);
                if (staleIncoming.Count > 0)
                {
                    project = project with
                    {
                        Deliveries = project.Deliveries.Select(delivery => staleIncoming.Contains(delivery.Id)
                            ? delivery with { ReleasedTick = WorldTick, ReleaseReason = "The load is no longer in resident custody." }
                            : delivery).ToArray(),
                        LastTransitionTick = WorldTick,
                    };
                    SetTownProject(town.Id, project);
                }
                var delivered = project.Deliveries.Where(delivery => delivery.ReleasedTick is null && delivery.DeliveredTick is not null).ToArray();
                if (delivered.GroupBy(delivery => delivery.ItemKind).Any(group =>
                        TownProjectRules.DeliveredQuantity(project, town.Id, society.Checkpoint.Inventory, group.Key) != group.Sum(delivery => delivery.Quantity)))
                {
                    BlockTownProject(town, project, "Delivered materials are no longer usable at the approved site.");
                    continue;
                }
                var stage = TownProjectHasAllMaterials(town, project) ? "working" : "supplying";
                if (project.Stage != stage)
                    SetTownProject(town.Id, project with { Stage = stage, LastTransitionTick = WorldTick });
            }
        }
    }

    private IEnumerable<(TownRuntimeState Town, TownProject Project)> KnownTownProjects(string actor)
    {
        if (!ReadyForTownProject(actor) || TownForResident(actor) is not { } townId) yield break;
        var town = towns.Single(item => item.Id == townId);
        if (town.Council is null) yield break;
        foreach (var project in town.Projects.Where(item => item.Stage is "supplying" or "working")
                     .OrderBy(item => item.Id, StringComparer.Ordinal))
            if (CivicHistory(town).Knows(actor, project.ProposalId) &&
                TownProjectSiteFailure(town, project.Plan, project.Id, actor) is null)
                yield return (town, project);
    }

    private bool CanWalkForTownProject(string actor, GridPoint from, GridPoint target, int range = 0) =>
        IsWithinInteractionRange(from, target, range) || ReachLeg(actor, from, target,
            ReachPurpose.TownProjectRecovery, range, 0, SwimmingPolicy.WhenCapable).Count > 0;

    private IEnumerable<InventoryLot> TownProjectMaterialSources(TownRuntimeState town,
        TownProject project, string actor, string itemKind)
    {
        var warehouses = WarehousesForTown(town.Id).Select(item => item.InstanceId).ToHashSet(StringComparer.Ordinal);
        var site = new InventoryGroundPosition(TownProjectRules.WorkSite(project.Plan).X, TownProjectRules.WorkSite(project.Plan).Y);
        var recoverable = town.Projects.SelectMany(item => item.Deliveries).Where(delivery => delivery.ReleasedTick is not null)
            .Select(delivery => delivery.LotId).ToHashSet(StringComparer.Ordinal);
        var request = new GoodsRequest(GoodsUse.Deliver, actor, GoodsOwners.One(town.Id), GoodsKinds.One(itemKind),
            Near: TownProjectRules.WorkSite(project.Plan), DeliveryPurpose: GoodsDeliveryPurpose.TownProject);
        return FindGoods(request).Matches.Select(match => match.Lot).Where(lot =>
                lot.ContainerLotId is null && lot.DeliveryBuildingId is null && AvailableLotQuantity(lot) > 0 &&
                !IsActiveTownProjectDelivery(lot.Id) &&
                (lot.CarrierId == actor || lot.CarrierId is null &&
                    (lot.StorageBuildingId is { } storage && warehouses.Contains(storage) ||
                     lot.GroundPosition == site || lot.GroundPosition is not null && recoverable.Contains(lot.Id))))
            .OrderBy(lot => lot.Id, StringComparer.Ordinal);
    }

    private IEnumerable<TownProjectChoice> TownProjectChoices(string actor)
    {
        // Released cargo must not prevent its carrier from meeting urgent needs.
        var urgent = NeedsUrgentFood(agents[actor]) || NeedsUrgentWarmth(agents[actor]);
        if (urgent || ReadyForTownProject(actor))
            foreach (var choice in TownProjectReturnChoices(actor)) yield return choice;
        if (!ReadyForTownProject(actor)) yield break;
        var person = agents[actor];
        foreach (var (town, project) in KnownTownProjects(actor))
        {
            foreach (var delivery in project.Deliveries.Where(item => item.ContributorId == actor &&
                         TownProjectIncomingIsLive(town.Id, item)))
                if (IsWithinInteractionRange(person.Position, TownProjectRules.WorkSite(project.Plan), 0) ||
                    Reach(actor, TownProjectRules.WorkSite(project.Plan), ReachPurpose.TownProjectLoad, 0, 0, SwimmingPolicy.WhenCapable).Count > 0)
                    yield return new(TownProjectChoiceId(TownProjectDeliverPrefix, project.Id, delivery.Id), "deliver",
                        town, project, delivery.ItemKind, society.Checkpoint.Inventory.GetLot(delivery.LotId),
                        delivery.Quantity, delivery);
            if (TownProjectHasAllMaterials(town, project))
            {
                if (IsWithinInteractionRange(person.Position, TownProjectRules.WorkSite(project.Plan), 0) ||
                    Reach(actor, TownProjectRules.WorkSite(project.Plan), ReachPurpose.TownProjectWork, 0, 0, SwimmingPolicy.WhenCapable).Count > 0)
                    yield return new(TownProjectChoiceId(TownProjectWorkPrefix, project.Id), "work", town, project);
                continue;
            }
            foreach (var cost in project.Plan.Budget)
            {
                var missing = TownProjectMissing(town, project, cost.ResourceId);
                if (missing <= 0) continue;
                foreach (var lot in TownProjectMaterialSources(town, project, actor, cost.ResourceId))
                {
                    var alreadyCarried = lot.CarrierId == actor;
                    var quantity = TownProjectLoadQuantity(actor, town, project, lot);
                    if (quantity <= 0) continue;
                    var position = HouseholdStockPosition(lot);
                    var range = HouseholdStockInteractionRange(lot);
                    if (!alreadyCarried && !IsWithinInteractionRange(person.Position, position, range) &&
                        Reach(actor, position, ReachPurpose.TownProjectLoad, range, 0, SwimmingPolicy.WhenCapable).Count == 0 ||
                        !IsWithinInteractionRange(alreadyCarried ? person.Position : position, TownProjectRules.WorkSite(project.Plan), 0) &&
                        ReachLeg(actor, alreadyCarried ? person.Position : position, TownProjectRules.WorkSite(project.Plan),
                            ReachPurpose.TownProjectLoad, 0, 0, SwimmingPolicy.WhenCapable).Count == 0) continue;
                    yield return new(TownProjectChoiceId(TownProjectSupplyPrefix, project.Id, lot.Id,
                        quantity.ToString(CultureInfo.InvariantCulture)), "supply", town, project, cost.ResourceId, lot, quantity);
                    break;
                }
                // A personal harvest never promises or donates the resulting lot automatically.
                if (PersonalTownProjectDonationLots(actor, cost.ResourceId).Any()) continue;
                var source = MaterialSource(cost.ResourceId, actor);
                if (source is null) continue;
                var harvest = ProjectMaterialHarvest(actor, cost.ResourceId, source);
                if (FreeCarryCapacity(actor) < (harvest is null ? 1 : checked(harvest.Quantity + harvest.TreeSeedQuantity))) continue;
                yield return new(TownProjectChoiceId(TownProjectGatherPrefix, project.Id, cost.ResourceId), "gather",
                    town, project, cost.ResourceId, Resource: source);
            }
        }
        foreach (var choice in TownProjectRecoveryChoices(actor)) yield return choice;
    }

    private IEnumerable<InventoryLot> PersonalTownProjectDonationLots(string actor, string itemKind) =>
        SpendingGoods(actor, GoodsUse.Spend, [actor], itemKind).Select(match => match.Lot)
            .Where(lot => PersonalEquipmentRules.IsCarried(lot, actor) && lot.ContainerLotId is null &&
                !PersonalEquipmentRules.IsSelected(agents[actor].Equipment, lot.Id))
            .OrderBy(lot => lot.Id, StringComparer.Ordinal);

    private IEnumerable<TownProjectChoice> TownProjectDonationChoices(string actor)
    {
        foreach (var (town, project) in KnownTownProjects(actor))
        {
            if (!CanWalkForTownProject(actor, agents[actor].Position, TownProjectRules.WorkSite(project.Plan))) continue;
            foreach (var cost in project.Plan.Budget)
            {
                var missing = TownProjectMissing(town, project, cost.ResourceId);
                if (missing <= 0) continue;
                foreach (var lot in PersonalTownProjectDonationLots(actor, cost.ResourceId).Take(4))
                {
                    var quantity = Math.Min(missing, AvailableLotQuantity(lot));
                    yield return new(TownProjectChoiceId(TownProjectDonatePrefix, project.Id, lot.Id,
                        quantity.ToString(CultureInfo.InvariantCulture),
                        agents[actor].Position == TownProjectRules.WorkSite(project.Plan) ? "at-site" : "travel"),
                        "donate", town, project, cost.ResourceId, lot, quantity);
                }
            }
        }
    }

    private static bool TownProjectReleasedLotMatches(InventoryLot lot, string townId, TownProjectDelivery delivery) =>
        lot.OwnerId == townId && lot.ItemKind == delivery.ItemKind && lot.ContainerLotId is null && lot.DeliveryBuildingId is null;

    private bool TownProjectReturnWarehouseEligible(string actor, PlacedBuilding warehouse) =>
        !NeedsUrgentFood(agents[actor]) && !NeedsUrgentWarmth(agents[actor]) && DestinationRoom(warehouse.InstanceId) > 0 &&
        (agents[actor].Position == warehouse.Position || Reach(actor, warehouse.Position, ReachPurpose.TownProjectRecovery,
            0, 0, SwimmingPolicy.WhenCapable, ReachObstacles.Terrain).Count > 0);

    private InventoryLot[] TownProjectReleasedCarried(TownRuntimeState town) =>
        society.Checkpoint.Inventory.Lots.Where(lot => lot.OwnerId == town.Id && town.Projects.SelectMany(project => project.Deliveries)
            .Any(delivery => delivery.ReleasedTick is not null && delivery.LotId == lot.Id) && lot.CarrierId is not null &&
            lot.ContainerLotId is null && lot.DeliveryBuildingId is null && !IsActiveTownProjectDelivery(lot.Id)).ToArray();

    private InventoryLot? TownProjectReleasedLot(string lotId) =>
        society.Checkpoint.Inventory.Lots.SingleOrDefault(lot => lot.Id == lotId);

    private int TownProjectRecoveryQuantity(string actor, TownProject project, InventoryLot lot, PlacedBuilding warehouse, int inbound) =>
        Math.Min(project.Plan.Budget.Single(cost => cost.ResourceId == lot.ItemKind).Amount,
            Math.Min(AvailableLotQuantity(lot), Math.Min(WarehouseLoadQuantity,
                Math.Min(Math.Min(FreeCarryCapacity(actor) - 1, PickupCarryCapacity(actor, lot, warehouse.Position)),
                    DestinationRoom(warehouse.InstanceId) - inbound))));

    private TownProjectChoice? CurrentTownProjectReleasedChoice(TownProjectRecoveryActionPlan plan, TownRuntimeState town,
        TownProject project, PlacedBuilding? warehouse)
    {
        var lot = TownProjectReleasedLot(plan.Choice.StockId);
        if (lot is null || !TownProjectReleasedLotMatches(lot, town.Id, plan.ReleasedDelivery) || IsActiveTownProjectDelivery(lot.Id)) return null;
        int quantity;
        if (plan.Choice.Recover)
        {
            if (TownForResident(plan.Actor) != town.Id || FreeCarryCapacity(plan.Actor) <= 1 || lot.GroundPosition is null ||
                AvailableLotQuantity(lot) <= 0 || warehouse is null) return null;
            var carried = TownProjectReleasedCarried(town);
            if (carried.Any(item => item.CarrierId == plan.Actor && PhysicalUnreservedQuantity(item) > 0)) return null;
            quantity = TownProjectRecoveryQuantity(plan.Actor, project, lot, warehouse, carried.Sum(PhysicalUnreservedQuantity));
        }
        else
        {
            if (lot.CarrierId != plan.Actor || PhysicalUnreservedQuantity(lot) <= 0) return null;
            if (warehouse is null && WarehousesForTown(town.Id).Any(item => TownProjectReturnWarehouseEligible(plan.Actor, item)) ||
                warehouse is not null && !TownProjectReturnWarehouseEligible(plan.Actor, warehouse)) return null;
            quantity = warehouse is null ? PhysicalUnreservedQuantity(lot) : Math.Min(PhysicalUnreservedQuantity(lot), DestinationRoom(warehouse.InstanceId));
        }
        return quantity > 0 ? new(plan.CandidateId, plan.Choice.Recover ? "recover" : "return", town, project,
            plan.ItemKind, lot, quantity, plan.ReleasedDelivery, Warehouse: warehouse) : null;
    }

    private IEnumerable<TownProjectChoice> TownProjectReturnChoices(string actor)
    {
        foreach (var town in towns.OrderBy(item => item.Id, StringComparer.Ordinal))
            foreach (var project in town.Projects.OrderBy(item => item.Id, StringComparer.Ordinal))
                foreach (var delivery in project.Deliveries.Where(item => item.ReleasedTick is not null)
                             .DistinctBy(item => item.LotId))
                {
                    var lot = TownProjectReleasedLot(delivery.LotId);
                    if (lot is null || !TownProjectReleasedLotMatches(lot, town.Id, delivery) ||
                        lot.CarrierId != actor || PhysicalUnreservedQuantity(lot) <= 0 || IsActiveTownProjectDelivery(lot.Id)) continue;
                    var person = agents[actor];
                    // A temporary pedestrian obstruction should wait, not cause a drop/pickup loop.
                    var warehouse = NeedsUrgentFood(person) || NeedsUrgentWarmth(person) ? null :
                        WarehousesForTown(town.Id).FirstOrDefault(item => TownProjectReturnWarehouseEligible(actor, item));
                    if (warehouse is null)
                    {
                        yield return new(TownProjectChoiceId(TownProjectReturnPrefix, project.Id, lot.Id, "ground"),
                            "return", town, project, lot.ItemKind, lot, PhysicalUnreservedQuantity(lot), delivery);
                        continue;
                    }
                    var quantity = Math.Min(PhysicalUnreservedQuantity(lot), DestinationRoom(warehouse.InstanceId));
                    if (quantity <= 0) continue;
                    yield return new(TownProjectChoiceId(TownProjectReturnPrefix, project.Id, lot.Id, warehouse.InstanceId),
                        "return", town, project, lot.ItemKind, lot, quantity, delivery, Warehouse: warehouse);
                }
    }

    private IEnumerable<TownProjectChoice> TownProjectRecoveryChoices(string actor)
    {
        if (TownForResident(actor) is not { } townId || FreeCarryCapacity(actor) <= 1) yield break;
        var town = towns.Single(item => item.Id == townId);
        var carried = TownProjectReleasedCarried(town);
        // A retained choice must finish its current return before collecting another load.
        if (carried.Any(lot => lot.CarrierId == actor && PhysicalUnreservedQuantity(lot) > 0)) yield break;
        var inbound = carried.Sum(PhysicalUnreservedQuantity);
        var ground = society.Checkpoint.Inventory.Lots.Where(lot => lot.OwnerId == townId &&
            lot.GroundPosition is not null && lot.ContainerLotId is null && lot.DeliveryBuildingId is null &&
            AvailableLotQuantity(lot) > 0 && !IsActiveTownProjectDelivery(lot.Id))
            .ToDictionary(lot => lot.Id, StringComparer.Ordinal);
        if (ground.Count == 0) yield break;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var project in town.Projects.OrderBy(item => item.Id, StringComparer.Ordinal))
            foreach (var delivery in project.Deliveries.Where(item => item.ReleasedTick is not null))
            {
                if (!ground.TryGetValue(delivery.LotId, out var lot) || lot.ItemKind != delivery.ItemKind ||
                    !seen.Add(delivery.LotId)) continue;
                var position = HouseholdStockPosition(lot);
                if (!CanWalkForTownProject(actor, agents[actor].Position, position)) continue;
                var warehouse = WarehousesForTown(townId).FirstOrDefault(item =>
                    DestinationRoom(item.InstanceId) > inbound &&
                    CanWalkForTownProject(actor, position, item.Position) &&
                    PickupCarryCapacity(actor, lot, item.Position) > 0);
                if (warehouse is null) continue;
                // Keep one carrying space available for food; never promise more than the real storage room.
                var quantity = TownProjectRecoveryQuantity(actor, project, lot, warehouse, inbound);
                if (quantity <= 0) continue;
                yield return new(TownProjectChoiceId(TownProjectRecoverPrefix, project.Id, lot.Id,
                    quantity.ToString(CultureInfo.InvariantCulture), warehouse.InstanceId), "recover",
                    town, project, lot.ItemKind, lot, quantity, delivery, Warehouse: warehouse);
            }
    }

    private TownProjectWorkActionPlan? PlanTownProjectWork(string actor, TownProjectChoice choice)
    {
        var gather = choice.Kind == "gather";
        var hammer = gather ? null : ToolProgressionRules.PlanWork(society.Checkpoint.Inventory, actor, ToolFamily.Hammer);
        var harvest = gather ? ProjectMaterialHarvest(actor, choice.ItemKind, choice.Resource!) : null;
        PlannedEquipmentPickup? pickup = null;
        if (gather && harvest is null)
        {
            if (RequiredGatheringTool(choice.ItemKind, choice.Resource!) is not { } required ||
                FindReachableSharedGatheringTool(actor, required) is not { } available ||
                SharedItem(available.ItemKind, actor) is not { ContainerLotId: null } tool) return null;
            pickup = new(tool.Id, tool.OwnerId, tool.ItemKind, HouseholdStockPosition(tool), HouseholdStockInteractionRange(tool));
        }
        var selectedTool = harvest?.ToolLotId is { } toolId ? TownProjectGatheringTool(actor, toolId) : null;
        var target = gather ? choice.Resource!.Position : TownProjectRules.WorkSite(choice.Project.Plan);
        return new(actor, new(choice.Project.Id, gather ? choice.ItemKind : null), choice.Town.Id, choice.Project.ProposalId,
            choice.Project.Plan, TownProjectRules.WorkSite(choice.Project.Plan), hammer,
            choice.Project.Deliveries.Where(delivery => delivery.DeliveredTick is not null && delivery.ReleasedTick is null).ToArray(),
            choice.Resource, harvest, selectedTool?.OwnerId, selectedTool?.ItemKind, pickup,
            Reach(actor, target, gather ? ReachPurpose.MaterialGathering : ReachPurpose.TownProjectWork,
                gather ? ResourceInteractionRange : 0, 0, SwimmingPolicy.WhenCapable));
    }

    private InventoryLot? TownProjectGatheringTool(string actor, string toolId) =>
        FindGoods(new(GoodsUse.CarriedInputs, actor,
            new(new[] { actor, HouseholdFor(actor), TownForResident(actor) }.OfType<string>().Distinct(StringComparer.Ordinal).ToArray()),
            new(ToolProgressionRules.All.Select(definition => definition.ItemKind).ToArray()))).Matches
            .Select(match => match.Lot).SingleOrDefault(lot => lot.Id == toolId && ToolProgressionRules.IsTopLevelCarriedLot(lot, actor));

    private void AddTownProjectCandidates(List<CognitionCandidate> candidates, string actor)
    {
        var offered = 0;
        foreach (var choice in TownProjectChoices(actor))
        {
            ActionPlan? plan = choice.Kind switch
            {
                "supply" or "deliver" => PlanTownProjectLoad(actor, choice),
                "work" or "gather" => PlanTownProjectWork(actor, choice),
                "recover" or "return" => PlanTownProjectRecovery(actor, choice),
                _ => null,
            };
            if (choice.Kind is "work" or "gather" && plan is null || plan is not null && !CheckActionPlan(plan).Allowed) continue;
            var description = choice.Kind switch
            {
                "recover" => $"Pick up {choice.Quantity} unused Town-owned {choice.ItemKind} from {choice.Project.Plan.Name} to return to its Town Warehouse.",
                "supply" => $"Carry {choice.Quantity} Town-owned {choice.ItemKind} to the approved {choice.Project.Plan.Name} site.",
                "deliver" => $"Deliver your actual Town-owned {choice.ItemKind} load to {choice.Project.Plan.Name}.",
                "work" => $"Help build {choice.Project.Plan.Name} at its approved site using the delivered Town materials.",
                "gather" => $"Gather personal {choice.ItemKind} near {choice.Project.Plan.Name}; donating it is a separate choice.",
                _ => choice.Warehouse is null
                    ? $"Set down the released Town-owned {choice.ItemKind} load at your actual position. It stays Town property."
                    : $"Return the released Town-owned {choice.ItemKind} load to its Town Warehouse.",
            };
            candidates.Add(new(choice.Id, description, choice.Kind == "return" && (NeedsUrgentFood(agents[actor]) || NeedsUrgentWarmth(agents[actor]))
                    ? FreeCarryCapacity(actor) == 0 ? 0 : 10
                    : choice.Kind is "deliver" or "return" ? 24 : choice.Kind == "recover" ? 40 : 36,
                choice.Warehouse?.InstanceId ?? choice.Project.Id, choice.Project.Plan.Name)
            { ActionPlan = plan });
            if (++offered == 16) break;
        }
    }

    private void AddTownProjectDonationCandidates(List<CognitionCandidate> candidates, string actor)
    {
        var eligible = new List<CognitionCandidate>();
        foreach (var choice in TownProjectDonationChoices(actor)) OfferTownProjectDonation(eligible, actor, choice);
        candidates.AddRange(eligible.Take(16));
    }

    // Only a fresh, exact personal-model admission may call this final donation path.
    private bool ApplyTownProjectDonation(string actor, string id, TownProjectDonationActionPlan? originalPlan = null)
    {
        if (!IsTownProjectDonationCandidate(id)) return false;
        TryRegisteredAction(ActionPhase.FreshAdmission, new(actor, agents[actor], id, false, originalPlan));
        return true;
    }

    private bool ApplyCheckedTownProjectDonation(TownProjectDonationActionPlan plan)
    {
        var actor = plan.Actor;
        var id = plan.CandidateId;
        var town = towns.Single(item => item.Id == plan.TownId);
        var project = town.Projects.Single(item => item.Id == plan.Choice.ProjectId);
        var lot = PersonalTownProjectDonationLots(actor, plan.ItemKind).Single(item => item.Id == plan.Choice.StockId);
        var choice = new TownProjectChoice(id, "donate", town, project, plan.ItemKind, lot, plan.Choice.Quantity);
        var person = agents[actor];
        if (person.Position != TownProjectRules.WorkSite(choice.Project.Plan))
        {
            MoveToward(actor, person, TownProjectRules.WorkSite(choice.Project.Plan), "town_project_donation", 0);
            return true;
        }
        var deliveryId = NextTownProjectDeliveryId(choice.Project, actor, choice.Lot!.Id);
        var operation = deliveryId + ":donation";
        var lotId = choice.Quantity == choice.Lot.Quantity ? choice.Lot.Id : DerivedId.Create(choice.Lot.Id + "#transfer:" + operation);
        var delivery = new TownProjectDelivery(deliveryId, actor, choice.Lot.Id, lotId, choice.ItemKind,
            choice.Quantity, WorldTick, WorldTick, TownRecordIds.DeliveryInput(deliveryId));
        ApplyInventoryTransition(inventory => ReserveTownProjectDelivery(InventoryAuthority.Transfer(inventory,
            operation, actor, choice.Town.Id, choice.Lot.Id, choice.Quantity, "town_project_donated",
            destinationGroundPosition: new(TownProjectRules.WorkSite(choice.Project.Plan).X, TownProjectRules.WorkSite(choice.Project.Plan).Y), holdAccess: InventoryHoldRules.BorrowedBy(actor)), choice.Town.Id, choice.Project, delivery));
        SetTownProject(choice.Town.Id, choice.Project with
        {
            Deliveries = choice.Project.Deliveries.Append(delivery).ToArray(),
            LastTransitionTick = WorldTick,
        });
        AppendEvent("town_project_donated", $"{actor}:{choice.Project.Id}:{lotId}:{choice.Quantity}:{choice.ItemKind}", TownProjectRules.WorkSite(choice.Project.Plan));
        return true;
    }

    private void ContinueTownProjectDonationWalk(string actor, string id)
    {
        if (!IsTownProjectDonationCandidate(id)) return;
        var choice = TownProjectDonationChoices(actor).FirstOrDefault(item => item.Id == id);
        if (choice is null) return;
        var plan = PlanTownProjectDonation(actor, choice);
        if (CheckTownProjectDonation(plan).Allowed && !plan.Choice.AtSite)
            MoveToward(actor, agents[actor], plan.Destination, "town_project_donation", 0);
    }

    private string NextTownProjectDeliveryId(TownProject project, string actor, string lotId) =>
        TownProjectRules.DeliveryId(project.Id, actor, lotId, WorldTick, project.Deliveries.Count);

    private static InventoryCheckpoint ReserveTownProjectDelivery(InventoryCheckpoint inventory, string townId,
        TownProject project, TownProjectDelivery delivery) => InventoryAuthority.Reserve(inventory,
        delivery.ReservationId!, townId, delivery.LotId, delivery.Quantity,
        TownProjectRules.ReservationPurpose(project.Id), long.MaxValue);

    private void PickUpTownProjectLoad(string actor, AgentState state, TownProjectChoice choice)
    {
        var lot = choice.Lot!;
        var position = HouseholdStockPosition(lot);
        var range = HouseholdStockInteractionRange(lot);
        if (lot.CarrierId != actor && !IsWithinInteractionRange(state.Position, position, range))
        {
            MoveToward(actor, state, position, "town_project_materials", range);
            return;
        }
        var id = NextTownProjectDeliveryId(choice.Project, actor, lot.Id);
        var operation = id + ":pickup";
        var movedId = choice.Quantity == lot.Quantity ? lot.Id : DerivedId.Create(lot.Id + "#move:" + operation);
        ApplyInventoryTransition(inventory => InventoryAuthority.Relocate(inventory, operation, lot.Id,
            choice.Town.Id, choice.Quantity, carrierId: actor, holdAccess: InventoryHoldRules.BorrowedBy(actor)));
        var delivery = new TownProjectDelivery(id, actor, lot.Id, movedId, lot.ItemKind, choice.Quantity, WorldTick);
        SetTownProject(choice.Town.Id, choice.Project with
        {
            Deliveries = choice.Project.Deliveries.Append(delivery).ToArray(),
            LastTransitionTick = WorldTick,
        });
        AppendEvent("town_project_material_picked_up", $"{actor}:{choice.Project.Id}:{movedId}:{choice.Quantity}:{choice.ItemKind}", TownProjectRules.WorkSite(choice.Project.Plan));
    }

    private void DeliverTownProjectLoad(string actor, AgentState state, TownProjectChoice choice)
    {
        if (state.Position != TownProjectRules.WorkSite(choice.Project.Plan))
        {
            MoveToward(actor, state, TownProjectRules.WorkSite(choice.Project.Plan), "town_project_materials", 0);
            return;
        }
        var delivery = choice.Delivery! with { DeliveredTick = WorldTick, ReservationId = TownRecordIds.DeliveryInput(choice.Delivery.Id) };
        ApplyInventoryTransition(inventory => ReserveTownProjectDelivery(InventoryAuthority.Relocate(inventory,
            TownRecordIds.DeliveryFinal(delivery.Id), delivery.LotId, choice.Town.Id, delivery.Quantity,
            groundPosition: new(TownProjectRules.WorkSite(choice.Project.Plan).X, TownProjectRules.WorkSite(choice.Project.Plan).Y), holdAccess: InventoryHoldRules.BorrowedBy(actor)), choice.Town.Id, choice.Project, delivery));
        SetTownProject(choice.Town.Id, choice.Project with
        {
            Deliveries = choice.Project.Deliveries.Select(item => item.Id == delivery.Id ? delivery : item).ToArray(),
            LastTransitionTick = WorldTick,
        });
        AppendEvent("town_project_material_delivered", $"{actor}:{choice.Project.Id}:{delivery.LotId}:{delivery.Quantity}:{delivery.ItemKind}", TownProjectRules.WorkSite(choice.Project.Plan));
    }

    private void RecoverTownProjectLoad(string actor, AgentState state, TownProjectChoice choice)
    {
        var lot = choice.Lot!;
        var position = HouseholdStockPosition(lot);
        if (state.Position != position)
        {
            MoveToward(actor, state, position, "town_project_recovery", 0);
            return;
        }
        var id = NextTownProjectDeliveryId(choice.Project, actor, lot.Id);
        var operation = id + ":recovery";
        var movedId = choice.Quantity == lot.Quantity ? lot.Id : DerivedId.Create(lot.Id + "#move:" + operation);
        ApplyInventoryTransition(inventory => InventoryAuthority.Relocate(inventory, operation, lot.Id,
            choice.Town.Id, choice.Quantity, carrierId: actor, holdAccess: InventoryHoldRules.BorrowedBy(actor)));
        // Retain custody of a split load without reviving its cancelled construction commitment.
        var delivery = new TownProjectDelivery(id, actor, lot.Id, movedId, lot.ItemKind, choice.Quantity,
            WorldTick, ReleasedTick: WorldTick, ReleaseReason: "Unused construction materials are being returned.");
        SetTownProject(choice.Town.Id, choice.Project with
        {
            Deliveries = choice.Project.Deliveries.Append(delivery).ToArray(),
        });
        AppendEvent("town_project_material_recovered", $"{actor}:{choice.Project.Id}:{movedId}:{choice.Quantity}:{choice.ItemKind}", position);
    }

    private void ReturnTownProjectLoad(string actor, AgentState state, TownProjectChoice choice)
    {
        if (choice.Warehouse is not { } warehouse)
        {
            var lot = choice.Lot!;
            var operation = $"{choice.Id}:{WorldTick}";
            ApplyInventoryTransition(inventory => InventoryAuthority.Relocate(inventory,
                operation, lot.Id, choice.Town.Id, choice.Quantity,
                groundPosition: new(state.Position.X, state.Position.Y), holdAccess: InventoryHoldRules.BorrowedBy(actor)));
            if (choice.Quantity < lot.Quantity)
            {
                // An unrelated claim can keep part of the original load in custody.
                // Retain the new ground lot so its unclaimed portion can be recovered later.
                var delivery = new TownProjectDelivery(NextTownProjectDeliveryId(choice.Project, actor, lot.Id),
                    actor, lot.Id, DerivedId.Create(lot.Id + "#move:" + operation), lot.ItemKind, choice.Quantity, WorldTick,
                    ReleasedTick: WorldTick, ReleaseReason: "Unused construction materials were set down.");
                SetTownProject(choice.Town.Id, choice.Project with
                {
                    Deliveries = choice.Project.Deliveries.Append(delivery).ToArray(),
                });
            }
            AppendEvent("town_project_material_returned",
                $"{actor}:{choice.Project.Id}:{choice.Lot!.Id}:{choice.Quantity}:ground", TownProjectRules.WorkSite(choice.Project.Plan));
            return;
        }
        if (state.Position != warehouse.Position)
        {
            MoveToward(actor, state, warehouse.Position, "town_project_return", 0);
            return;
        }
        ApplyInventoryTransition(inventory => InventoryAuthority.Relocate(inventory,
            $"{choice.Id}:{WorldTick}", choice.Lot!.Id, choice.Town.Id, choice.Quantity,
            storageBuildingId: warehouse.InstanceId, holdAccess: InventoryHoldRules.DeliveryFor(actor, warehouse.InstanceId)));
        AppendEvent("town_project_material_returned", $"{actor}:{choice.Project.Id}:{choice.Lot!.Id}:{choice.Quantity}:{warehouse.InstanceId}", TownProjectRules.WorkSite(choice.Project.Plan));
    }

    private void WorkOnTownProject(string actor, AgentState state, TownProjectChoice choice, ToolWorkPlan? hammer)
    {
        if (state.Position != TownProjectRules.WorkSite(choice.Project.Plan))
        {
            MoveToward(actor, state, TownProjectRules.WorkSite(choice.Project.Plan), "town_project_work", 0);
            return;
        }
        var workNeeded = TownProjectRules.RequiredWork(choice.Project.Plan);
        if (choice.Project.WorkDone == workNeeded)
        {
            CompletePaidTownProject(actor, choice.Town.Id, choice.Project);
            return;
        }
        if (!IllnessRules.AllowsWork(actor, WorldTick, state.Survival?.IllnessBasisPoints ?? 0)) return;
        var done = Math.Min(workNeeded, SkilledWorkProgress(actor, SkillKind.Building,
            choice.Project.WorkDone, hammer?.WorkUnits ?? 1));
        if (hammer is not null) ApplyToolWork(actor, hammer);
        var project = choice.Project with { Stage = "working", WorkDone = done, LastTransitionTick = WorldTick };
        SetTownProject(choice.Town.Id, project);
        AppendEvent("town_project_worked", $"{actor}:{project.Id}:{done}", TownProjectRules.WorkSite(project.Plan));
        if (done == workNeeded) CompletePaidTownProject(actor, choice.Town.Id, project);
    }

    private void CompletePaidTownProject(string actor, string townId, TownProject project)
    {
        var town = towns.Single(item => item.Id == townId);
        if (TownProjectSiteFailure(town, project.Plan, project.Id, actor) is { } failure)
        {
            BlockTownProject(town, project, failure);
            return;
        }
        if (!TownProjectHasAllMaterials(town, project))
        {
            BlockTownProject(town, project, "The exact delivered Town material budget is unavailable.");
            return;
        }
        var id = TownProjectRules.BuildingId(project.Id);
        if (worldSimulation.Buildings.Any(item => item.InstanceId == id))
            throw new InvalidOperationException("The approved Town project already has a building.");
        ApplyInventoryTransition(inventory =>
        {
            foreach (var delivery in project.Deliveries.Where(item => item.ReleasedTick is null &&
                         item.DeliveredTick is not null && item.ReservationId is not null))
                inventory = InventoryAuthority.ConsumeReservation(inventory, delivery.ReservationId!);
            return inventory;
        });
        if (project.Plan.BoatPortId is not null)
        {
            CompletePaidBoatProject(townId, project);
            CreditCompletedWork(actor, "building");
            return;
        }
        var placed = new PlacedBuilding(id, project.Plan.DefinitionId, project.Plan.Site, WorldTick,
            townId, Entrance: project.Plan.Entrance);
        worldSimulation = worldSimulation with
        {
            Buildings = worldSimulation.Buildings.Append(placed).OrderBy(item => item.InstanceId, StringComparer.Ordinal).ToArray(),
        };
        SetTownProject(townId, project with
        {
            Stage = "completed",
            CompletedBuildingId = id,
            Blocker = null,
            LastTransitionTick = WorldTick,
        });
        CompletePaidMarketConstruction(townId, project, placed);
        AssignBuildingToTown(placed, worldContent.Buildings.Single(item => item.CanonicalId == placed.DefinitionId));
        CreditCompletedWork(actor, "building");
        AppendEvent("town_project_completed", $"{actor}:{project.Id}:{id}:{project.Plan.Name}", TownProjectRules.WorkSite(project.Plan));
    }
}

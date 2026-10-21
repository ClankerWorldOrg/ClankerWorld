using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

/// <summary>An exclusive physical attachment; position, property, condition and cargo remain inventory facts.</summary>
public sealed record HandcartHitch(string CartLotId, string PullerId);

public sealed partial class WorldRuntime
{
    private static IReadOnlyList<string> CartRepairKinds => TransportSystem.CartRepairKinds;
    private const string AttachCartPrefix = HandcartControlChoice.AttachPrefix;
    private const string LoadCartPrefix = HandcartCargoChoice.LoadPrefix;
    private const string UnloadCartPrefix = HandcartCargoChoice.UnloadPrefix;
    private const string UnloadCartGroundPrefix = HandcartCargoChoice.GroundPrefix;
    private const string CollectCartRepairPrefix = HandcartRepairInputChoice.Prefix;
    private const string RepairCartPrefix = HandcartRepairChoice.Prefix;
    private const string PullCartPrefix = HandcartPullChoice.Prefix;
    // Generic cart controls have no delivery intention. Keep them selectable by models without
    // letting the built-in chooser shuffle goods or pull between arbitrary buildings ahead of safe idle.
    private const int UnassignedCartPriority = 110;

    private static bool IsHandcartRecipe(RecipeDefinition recipe) => ProductionSystem.IsHandcartRecipe(recipe);

    private RecipeDefinition? HouseholdHandcartRecipe(string? household) =>
        TransportSystem.HouseholdHandcartRecipe(HandcartWorld, household);

    /// <summary>
    /// The cart recipe while the actor may build their own cart: no cart yet, none being built, and every
    /// material either carried already or in stock they may collect, so they do not gather part of a set that
    /// nothing in the world could complete.
    /// </summary>
    private RecipeDefinition? HandcartToBuild(string actor) =>
        TransportSystem.HandcartToBuild(HandcartWorld, actor);

    private bool MayBuildHandcart(string actor, string household, RecipeDefinition recipe) =>
        TransportSystem.MayBuildHandcart(HandcartWorld, actor, household, recipe);

    private int CarriedMaterialQuantity(string actor, string kind) =>
        TransportSystem.CarriedMaterialQuantity(HandcartWorld, actor, kind);

    private int HouseholdMaterialQuantity(string actor, string household, string kind) =>
        TransportSystem.HouseholdMaterialQuantity(HandcartWorld, actor, household, kind);

    private bool ReachableWarehouseStockCovers(string actor, string kind, int missing) =>
        TransportSystem.ReachableWarehouseStockCovers(HandcartWorld, actor, kind, missing);

    // A would-be cart builder keeps the materials they carry for it. Household hauls take only what is
    // beyond that, or they would return it to stock as fast as the builder collects it from there.
    private int SpareCarriedQuantity(string actor, InventoryLot lot) =>
        TransportSystem.SpareCarriedQuantity(HandcartWorld, actor, lot);

    private static bool IsHandcartCargo(InventoryCheckpoint inventory, InventoryLot lot) => lot.ContainerLotId is { } id &&
        InventoryIndex.For(inventory).Find(id)?.ItemKind == InventoryContainerRules.Handcart;

    private InventoryLot? AttachedHandcart(string actor) =>
        TransportSystem.AttachedHandcart(HandcartWorld, actor);

    private IEnumerable<InventoryLot> NearbyOwnedHandcarts(string actor, GridPoint position) =>
        TransportSystem.NearbyOwnedHandcarts(HandcartWorld, actor, position);

    private InventoryLot? NearbyOwnedHandcart(string actor, GridPoint position) =>
        TransportSystem.NearbyOwnedHandcart(HandcartWorld, actor, position);

    private bool CanPullHandcart(InventoryLot cart) =>
        TransportSystem.CanPullHandcart(HandcartWorld, cart);

    private bool LegalHandcartStep(GridPoint from, GridPoint to) =>
        TransportSystem.LegalHandcartStep(HandcartWorld, from, to);

    private int TravelStepCost(string actor, GridPoint from, GridPoint to) =>
        TransportSystem.TravelStepCost(HandcartWorld, actor, from, to);

    private void MoveAttachedHandcart(string actor, GridPoint from, GridPoint to) =>
        TransportSystem.MoveAttachedHandcart(HandcartWorld, actor, from, to);

    private void ParkHandcart(string actor, string reason = "parked") =>
        TransportSystem.ParkHandcart(HandcartWorld, actor, reason);

    private void ReconcileHandcartHitches() =>
        TransportSystem.ReconcileHandcartHitches(HandcartWorld);

    private bool CanLoadCartLot(string actor, AgentState person, InventoryLot lot) =>
        TransportSystem.CanLoadCartLot(HandcartWorld, actor, person, lot);

    private void AddHandcartCandidates(List<CognitionCandidate> candidates, string actor,
        AgentState person)
    {
        var inventory = society.Checkpoint.Inventory;
        if (HandcartToBuild(actor) is { } craftRecipe)
            foreach (var input in craftRecipe.Inputs)
                AddHandcartBuildMaterialCandidate(candidates, PlanHandcartBuildMaterial(actor, new(input.ResourceId), craftRecipe));
        if (AttachedHandcart(actor) is null && !animalWorld.Animals.Any(animal => animal.RiderId == actor || animal.LeaderId == actor))
        {
            foreach (var cart in HandcartControlStock(actor))
                AddHandcartControlCandidate(candidates, PlanHandcartControl(actor, new(HandcartControlKind.Attach, cart.Id), cart),
                    "Reach and attach your parked handcart to carry a larger load.");
        }
        else if (AttachedHandcart(actor) is not null)
        {
            AddHandcartControlCandidate(candidates, PlanHandcartControl(actor, new(HandcartControlKind.Park)),
                "Park the handcart here with its cargo kept inside.");
            foreach (var building in worldSimulation.Buildings.OrderBy(item => item.InstanceId, StringComparer.Ordinal))
                AddHandcartPullCandidate(candidates, PlanHandcartPull(actor, new(building.InstanceId)));
        }
        if (WornHandcarts(actor).Any())
        {
            foreach (var kind in CartRepairKinds)
                AddHandcartRepairCandidate(candidates, PlanHandcartRepairInput(actor, new(kind)));
            foreach (var cart in WornHandcarts(actor))
                AddHandcartRepairCandidate(candidates, PlanHandcartRepair(actor, new(cart.Id)));
        }
        if (NearbyOwnedHandcart(actor, person.Position) is not { } nearby) return;
        foreach (var lot in HandcartLoadStock(actor))
            AddHandcartCargoCandidate(candidates, PlanHandcartCargo(actor, new(HandcartCargoKind.Load, lot.Id), nearby.Id));
        foreach (var cart in NearbyOwnedHandcarts(actor, person.Position))
            foreach (var lot in CartCargoLots(actor, cart.Id))
            {
                AddHandcartCargoCandidate(candidates, PlanHandcartCargo(actor, new(HandcartCargoKind.Unload, lot.Id), cart.Id));
                AddHandcartCargoCandidate(candidates, PlanHandcartCargo(actor, new(HandcartCargoKind.UnloadGround, lot.Id), cart.Id));
            }
        if (AttachedHandcart(actor) is null && !HasActiveContainerReservation(inventory, nearby.Id))
            foreach (var recipient in agents.Values.Where(item => item.AgentId != actor &&
                         IsAdult(item.AgentId) && IsWithinInteractionRange(person.Position, item.Position, 1))
                         .OrderBy(item => item.AgentId, StringComparer.Ordinal))
                AddHandcartControlCandidate(candidates, PlanHandcartControl(actor, new(HandcartControlKind.Give, recipient.AgentId), nearby),
                    $"Give your parked handcart and its cargo to {society.Checkpoint.GetAgent(recipient.AgentId).Name} here.");
    }

    private bool ApplyHandcartCandidate(string actor, AgentState person, string candidateId, string? targetCartId = null, int maximumQuantity = int.MaxValue, ActionPlan? originalPlan = null)
    {
        if (originalPlan is HandcartControlActionPlan controlPlan)
        { ApplyHandcartControlPlan(controlPlan); return true; }
        if (HandcartControlChoice.Decode(candidateId) is { } control)
        {
            if (PlanHandcartControl(actor, control) is { } plan) ApplyHandcartControlPlan(plan);
            return true;
        }
        if (originalPlan is HandcartBuildMaterialActionPlan buildMaterial)
        { ApplyHandcartBuildMaterialPlan(buildMaterial); return true; }
        if (HandcartBuildMaterialChoice.Decode(candidateId) is { } buildMaterialChoice)
        {
            if (PlanHandcartBuildMaterial(actor, buildMaterialChoice) is { } plan) ApplyHandcartBuildMaterialPlan(plan);
            return true;
        }
        if (originalPlan is HandcartRepairInputActionPlan repairInput)
        { ApplyHandcartRepairInputPlan(repairInput); return true; }
        if (HandcartRepairInputChoice.Decode(candidateId) is { } repairInputChoice)
        {
            if (PlanHandcartRepairInput(actor, repairInputChoice) is { } plan) ApplyHandcartRepairInputPlan(plan);
            return true;
        }
        if (originalPlan is HandcartPullActionPlan pullPlan)
        { ApplyHandcartPullPlan(pullPlan); return true; }
        if (HandcartPullChoice.Decode(candidateId) is { } pull)
        {
            if (PlanHandcartPull(actor, pull) is { } plan) ApplyHandcartPullPlan(plan);
            return true;
        }
        if (originalPlan is HandcartCargoActionPlan cargoPlan)
        { ApplyHandcartCargoPlan(cargoPlan); return true; }
        if (HandcartCargoChoice.Decode(candidateId) is { } cargoChoice)
        {
            if (PlanHandcartCargo(actor, cargoChoice, targetCartId, maximumQuantity) is { } plan) ApplyHandcartCargoPlan(plan);
            return true;
        }
        if (originalPlan is HandcartRepairActionPlan repairPlan)
        { ApplyHandcartRepairPlan(repairPlan); return true; }
        if (HandcartRepairChoice.Decode(candidateId) is { } repairChoice)
        {
            if (targetCartId is not null && targetCartId != repairChoice.CartId) return true;
            if (PlanHandcartRepair(actor, repairChoice) is { } plan) ApplyHandcartRepairPlan(plan);
            return true;
        }
        return false;
    }

    private void CompleteHandcartBuildMaterial(HandcartBuildMaterialActionPlan plan) =>
        TransportSystem.CompleteHandcartBuildMaterial(HandcartWorld, plan);

    private void CompleteHandcartRepair(HandcartRepairActionPlan plan, InventoryLot cart) =>
        TransportSystem.CompleteHandcartRepair(HandcartWorld, plan, cart);

    private void CompleteHandcartCargo(HandcartCargoActionPlan plan, InventoryLot cart, InventoryLot lot) =>
        TransportSystem.CompleteHandcartCargo(HandcartWorld, plan, cart, lot);

    private void CompleteHandcartControl(HandcartControlActionPlan plan, InventoryLot cart) =>
        TransportSystem.CompleteHandcartControl(HandcartWorld, plan, cart);

    private static void ValidateHandcarts(IReadOnlyList<HandcartHitch> hitches, InventoryCheckpoint inventory,
        IReadOnlyList<AgentState> people, WorldMap travelMap) =>
        TransportSystem.ValidateHandcarts(hitches, inventory, people, travelMap);
}

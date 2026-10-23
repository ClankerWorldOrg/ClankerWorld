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

    /// <summary>
    /// The cart recipe while the actor may build their own cart: no cart yet, none being built, and every
    /// material either carried already or in stock they may collect, so they do not gather part of a set that
    /// nothing in the world could complete.
    /// </summary>
    private RecipeDefinition? HandcartToBuild(string actor) =>
        TransportSystem.HandcartToBuild(HandcartWorld, actor);

    // A would-be cart builder keeps the materials they carry for it. Household hauls take only what is
    // beyond that, or they would return it to stock as fast as the builder collects it from there.
    private int SpareCarriedQuantity(string actor, InventoryLot lot) =>
        TransportSystem.SpareCarriedQuantity(HandcartWorld, actor, lot);

    private static bool IsHandcartCargo(InventoryCheckpoint inventory, InventoryLot lot) => lot.ContainerLotId is { } id &&
        InventoryIndex.For(inventory).Find(id)?.ItemKind == InventoryContainerRules.Handcart;

    private void AddHandcartCandidates(List<CognitionCandidate> candidates, string actor,
        AgentState person)
    {
        var inventory = society.Checkpoint.Inventory;
        if (HandcartToBuild(actor) is { } craftRecipe)
            foreach (var input in craftRecipe.Inputs)
                AddHandcartBuildMaterialCandidate(candidates, TransportSystem.PlanHandcartBuildMaterial(HandcartWorld,actor,new(input.ResourceId),craftRecipe));
        if (TransportSystem.AttachedHandcart(HandcartWorld,actor) is null && !animalWorld.Animals.Any(animal => animal.RiderId == actor || animal.LeaderId == actor))
        {
            foreach (var cart in HandcartControlStock(actor))
                AddHandcartControlCandidate(candidates, TransportSystem.PlanHandcartControl(HandcartWorld,actor,new(HandcartControlKind.Attach, cart.Id),cart),
                    "Reach and attach your parked handcart to carry a larger load.");
        }
        else if (TransportSystem.AttachedHandcart(HandcartWorld,actor) is not null)
        {
            AddHandcartControlCandidate(candidates, TransportSystem.PlanHandcartControl(HandcartWorld,actor,new(HandcartControlKind.Park),null),
                "Park the handcart here with its cargo kept inside.");
            foreach (var building in worldSimulation.Buildings.OrderBy(item => item.InstanceId, StringComparer.Ordinal))
                AddHandcartPullCandidate(candidates, TransportSystem.PlanHandcartPull(HandcartWorld,actor,new(building.InstanceId)));
        }
        if (TransportSystem.WornHandcarts(HandcartWorld,actor).Any())
        {
            foreach (var kind in CartRepairKinds)
                AddHandcartRepairCandidate(candidates, TransportSystem.PlanHandcartRepairInput(HandcartWorld,actor,new(kind)));
            foreach (var cart in TransportSystem.WornHandcarts(HandcartWorld,actor))
                AddHandcartRepairCandidate(candidates, TransportSystem.PlanHandcartRepair(HandcartWorld,actor,new(cart.Id)));
        }
        if (TransportSystem.NearbyOwnedHandcart(HandcartWorld,actor,person.Position) is not { } nearby) return;
        foreach (var lot in HandcartLoadStock(actor))
            AddHandcartCargoCandidate(candidates, TransportSystem.PlanHandcartCargo(HandcartWorld,actor,new(HandcartCargoKind.Load, lot.Id),nearby.Id,2147483647));
        foreach (var cart in TransportSystem.NearbyOwnedHandcarts(HandcartWorld,actor,person.Position))
            foreach (var lot in CartCargoLots(actor, cart.Id))
            {
                AddHandcartCargoCandidate(candidates, TransportSystem.PlanHandcartCargo(HandcartWorld,actor,new(HandcartCargoKind.Unload, lot.Id),cart.Id,2147483647));
                AddHandcartCargoCandidate(candidates, TransportSystem.PlanHandcartCargo(HandcartWorld,actor,new(HandcartCargoKind.UnloadGround, lot.Id),cart.Id,2147483647));
            }
        if (TransportSystem.AttachedHandcart(HandcartWorld,actor) is null && !HasActiveContainerReservation(inventory, nearby.Id))
            foreach (var recipient in agents.Values.Where(item => item.AgentId != actor &&
                         IsAdult(item.AgentId) && IsWithinInteractionRange(person.Position, item.Position, 1))
                         .OrderBy(item => item.AgentId, StringComparer.Ordinal))
                AddHandcartControlCandidate(candidates, TransportSystem.PlanHandcartControl(HandcartWorld,actor,new(HandcartControlKind.Give, recipient.AgentId),nearby),
                    $"Give your parked handcart and its cargo to {society.Checkpoint.GetAgent(recipient.AgentId).Name} here.");
    }

    private bool ApplyHandcartCandidate(string actor, AgentState person, string candidateId, string? targetCartId = null, int maximumQuantity = int.MaxValue, ActionPlan? originalPlan = null)
    {
        if (originalPlan is HandcartControlActionPlan controlPlan)
        { ApplyHandcartControlPlan(controlPlan); return true; }
        if (HandcartControlChoice.Decode(candidateId) is { } control)
        {
            if (TransportSystem.PlanHandcartControl(HandcartWorld,actor,control,null) is { } plan) ApplyHandcartControlPlan(plan);
            return true;
        }
        if (originalPlan is HandcartBuildMaterialActionPlan buildMaterial)
        { ApplyHandcartBuildMaterialPlan(buildMaterial); return true; }
        if (HandcartBuildMaterialChoice.Decode(candidateId) is { } buildMaterialChoice)
        {
            if (TransportSystem.PlanHandcartBuildMaterial(HandcartWorld,actor,buildMaterialChoice,null) is { } plan) ApplyHandcartBuildMaterialPlan(plan);
            return true;
        }
        if (originalPlan is HandcartRepairInputActionPlan repairInput)
        { ApplyHandcartRepairInputPlan(repairInput); return true; }
        if (HandcartRepairInputChoice.Decode(candidateId) is { } repairInputChoice)
        {
            if (TransportSystem.PlanHandcartRepairInput(HandcartWorld,actor,repairInputChoice) is { } plan) ApplyHandcartRepairInputPlan(plan);
            return true;
        }
        if (originalPlan is HandcartPullActionPlan pullPlan)
        { ApplyHandcartPullPlan(pullPlan); return true; }
        if (HandcartPullChoice.Decode(candidateId) is { } pull)
        {
            if (TransportSystem.PlanHandcartPull(HandcartWorld,actor,pull) is { } plan) ApplyHandcartPullPlan(plan);
            return true;
        }
        if (originalPlan is HandcartCargoActionPlan cargoPlan)
        { ApplyHandcartCargoPlan(cargoPlan); return true; }
        if (HandcartCargoChoice.Decode(candidateId) is { } cargoChoice)
        {
            if (TransportSystem.PlanHandcartCargo(HandcartWorld,actor,cargoChoice,targetCartId,maximumQuantity) is { } plan) ApplyHandcartCargoPlan(plan);
            return true;
        }
        if (originalPlan is HandcartRepairActionPlan repairPlan)
        { ApplyHandcartRepairPlan(repairPlan); return true; }
        if (HandcartRepairChoice.Decode(candidateId) is { } repairChoice)
        {
            if (targetCartId is not null && targetCartId != repairChoice.CartId) return true;
            if (TransportSystem.PlanHandcartRepair(HandcartWorld,actor,repairChoice) is { } plan) ApplyHandcartRepairPlan(plan);
            return true;
        }
        return false;
    }
}

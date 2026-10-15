using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private sealed record InputCollectionStep(string Kind, GridPoint Position, int Range, int CarryUnits,
        InventoryLot? Stock = null, MapResource? Harvest = null, string? HarvestToolLotId = null);

    // These recipes have at most three inputs. Try their existing order first,
    // then the other permutations, using each real interaction point and load.
    // Legacy callers plan each step; action plans keep this selected ordering.
    private List<InputCollectionStep>? PlanInputCollection(string actor,
        IReadOnlyList<InputCollectionStep> inputs, GridPoint? returnTo = null, int returnRange = 0)
    {
        if (inputs.Sum(input => input.CarryUnits) > FreeCarryCapacity(actor)) return null;
        var selected = new List<InputCollectionStep>(inputs.Count);
        var used = new bool[inputs.Count];
        return Search(agents[actor].Position, 0) ? selected : null;

        bool Search(GridPoint position, int addedUnits)
        {
            if (selected.Count == inputs.Count)
                return returnTo is null || ReachLeg(actor, position, returnTo.Value, ReachPurpose.MaterialGathering, returnRange, addedUnits, SwimmingPolicy.WhenCapable).Count > 0;
            for (var index = 0; index < inputs.Count; index++)
            {
                if (used[index]) continue;
                var input = inputs[index];
                var route = ReachLeg(actor, position, input.Position, ReachPurpose.MaterialGathering, input.Range, addedUnits, SwimmingPolicy.WhenCapable);
                if (route.Count == 0) continue;
                used[index] = true;
                selected.Add(input);
                if (Search(route[^1], checked(addedUnits + input.CarryUnits))) return true;
                selected.RemoveAt(selected.Count - 1);
                used[index] = false;
            }
            return false;
        }
    }

    private bool RecheckInputCollection(string actor, IReadOnlyList<InputCollectionStep> inputs, GridPoint returnTo,
        ReachPurpose purpose = ReachPurpose.ToolRepair)
    {
        if (inputs.Sum(input => input.CarryUnits) > FreeCarryCapacity(actor)) return false;
        var origin = agents[actor].Position;
        var load = 0;
        foreach (var input in inputs)
        {
            var route = ReachLeg(actor, origin, input.Position, purpose, input.Range, load, SwimmingPolicy.WhenCapable);
            if (route.Count == 0) return false;
            origin = route[^1];
            load = checked(load + input.CarryUnits);
        }
        return ReachLeg(actor, origin, returnTo, purpose, 0, load, SwimmingPolicy.WhenCapable).Count > 0;
    }

    private InputCollectionStep StockCollectionStep(InventoryLot stock, int quantity) =>
        new(stock.ItemKind, HouseholdStockPosition(stock), HouseholdStockInteractionRange(stock), quantity, stock);
}

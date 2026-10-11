using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Simulation.Playtest;

public sealed partial class PrivateWorldRuntime
{
    private sealed record InputCollectionStep(string Kind, GridPoint Position, int Range, int CarryUnits,
        InventoryLot? Stock = null, MapResource? Harvest = null, string? HarvestToolLotId = null);

    // These recipes have at most three inputs. Try their existing order first,
    // then the other permutations, using each real interaction point and load.
    // The plan is transient: every action resolves permissions and sources again.
    private List<InputCollectionStep>? PlanInputCollection(string actor,
        IReadOnlyList<InputCollectionStep> inputs, GridPoint? returnTo = null, int returnRange = 0)
    {
        if (inputs.Sum(input => input.CarryUnits) > FreeCarryCapacity(actor)) return null;
        var selected = new List<InputCollectionStep>(inputs.Count);
        var used = new bool[inputs.Count];
        return Search(inhabitants[actor].Position, 0) ? selected : null;

        bool Search(GridPoint position, int addedUnits)
        {
            if (selected.Count == inputs.Count)
                return returnTo is null || FindUnoccupiedRoute(actor, position, returnTo.Value, returnRange,
                    additionalCarriedUnits: addedUnits).Count > 0;
            for (var index = 0; index < inputs.Count; index++)
            {
                if (used[index]) continue;
                var input = inputs[index];
                var route = FindUnoccupiedRoute(actor, position, input.Position, input.Range,
                    additionalCarriedUnits: addedUnits);
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

    private InputCollectionStep StockCollectionStep(InventoryLot stock, int quantity) =>
        new(stock.ItemKind, HouseholdStockPosition(stock), HouseholdStockInteractionRange(stock), quantity, stock);
}

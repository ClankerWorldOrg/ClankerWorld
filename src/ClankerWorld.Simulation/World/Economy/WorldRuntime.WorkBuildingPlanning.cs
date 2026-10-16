using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    // This is a prerequisite for choosing work, not a reservation of future
    // production inputs. Offers, delayed choices and unstarted projects read it afresh.
    private bool WorkBuildingHasInputs(string actor, string household, BuildingDefinition building)
    {
        var kind = HouseholdBuildingKind(building);
        if (kind is null or "house" or "store") return true;
        var town = towns.SingleOrDefault(item => item.ResidentIds.Contains(actor, StringComparer.Ordinal));
        var home = HouseForHousehold(household)?.Position ?? agents[actor].Position;
        var owners = new GoodsOwners(agents.Keys.Where(member => HouseholdFor(member) == household)
            .Prepend(household).Concat(WarehousesAccessibleTo(actor).Select(warehouse => warehouse.TownId!))
            .Distinct(StringComparer.Ordinal).ToArray());
        var tools = new Dictionary<(ToolFamily Family, int Tier), ToolDefinition?>();
        bool Reachable(GridPoint position, int range = 0) =>
            IsWithinInteractionRange(agents[actor].Position, position, range) ||
            Reach(actor, position, ReachPurpose.WorkstationSupply, range, 0, SwimmingPolicy.WhenCapable,
                ReachObstacles.Terrain).Count > 0;
        bool Input(string item, int quantity, bool nearTown = false)
        {
            // Construction must not spend the only stock that would give the
            // new station work. Natural supplies can be gathered afterwards.
            var cost = building.BuildCosts.Where(input => input.ResourceId == item).Sum(input => input.Amount);
            var stock = FindGoods(new(GoodsUse.ReachableHoldings, actor, owners, GoodsKinds.One(item))).Matches
                .Where(match => (match.Root.CarrierId is null || match.Root.CarrierId == actor) &&
                    MayCollectGoodsAt(actor, match.Root) && (!nearTown || match.Place.Position is { } position &&
                        map.FootDistance(town?.OriginSite ?? home, position) <= 12))
                .Sum(match => (long)match.Quantity);
            if (stock >= quantity + cost)
                return true;
            if (item == InventoryContainerRules.FreshWater)
                return FindGoods(new(GoodsUse.Collect, actor, owners, GoodsKinds.One(InventoryContainerRules.WaterJug))).First is not null &&
                    FindFreshWaterShore(actor, agents[actor].Position, returnTo: home) is not null;
            return map.Resources.Any(source => (source.Kind == item || item == "wood" && source.Kind == "construction") &&
                (!nearTown || map.FootDistance(town?.OriginSite ?? home, source.Position) <= 12) &&
                resources.GetValueOrDefault(source.Id) == ResourceState.Available &&
                worldSystems.Ecology.GetResource(source.Id).Quantity >= quantity &&
                CanGatherFromSource(actor, item, source, tools) && Reachable(source.Position, ResourceInteractionRange));
        }
        if (kind is "farmhouse" or "silo")
            return fields.Any(field => field.HouseholdId == household && Reachable(field.Position)) ||
                new[] { FarmFieldRules.GrainSeed, FarmFieldRules.GreensSeed, FarmFieldRules.Potatoes }
                    .Any(seed => Input(seed, 1, nearTown: true));
        if (kind == AnimalContent.YardTag)
            return !worldSimulation.Buildings.Any(yard => yard.TownId == town?.Id &&
                    worldContent.BuildingDefinitions[yard.DefinitionId].Tags.Contains(AnimalContent.YardTag) &&
                    yard.HouseholdId is { } holder && HasAnimalSpace(holder, yard)) &&
                Animals.Any(animal => animal.DiedTick is null && animal.HouseholdId is null && animal.LeaderId is null &&
                    (animal.TamingWork is null || animal.TamingWork.ActorId == actor) && MayLeadAnimal(actor, animal) &&
                    (agents[actor].Position == animal.Position || Reach(actor, animal.Position,
                        ReachPurpose.AnimalWork, 0, 0, SwimmingPolicy.Never, ReachObstacles.Terrain).Count > 0));

        var recipes = worldContent.Recipes.Where(recipe => recipe.WorkstationBuildingId == building.CanonicalId && !recipe.IsCrop);
        return recipes.Any(recipe => recipe.Inputs.All(input => Input(input.ResourceId, input.Amount)) &&
            (kind != "restaurant" || RestaurantHasSurplus(actor, household, town, recipe)));
    }

    private bool RestaurantHasSurplus(string actor, string household, TownRuntimeState? town, RecipeDefinition recipe)
    {
        var residents = society.Checkpoint.Agents.Where(person => person.Status == SocietyAgentStatus.Active &&
            (town is null ? person.HouseholdId == household : town.ResidentIds.Contains(person.Id, StringComparer.Ordinal))).ToArray();
        var owners = residents.Select(person => person.Id).Concat(residents.Select(person => person.HouseholdId).OfType<string>())
            .Concat(town is null ? [] : new[] { town.Id }).Distinct(StringComparer.Ordinal).ToArray();
        var stock = FindGoods(new(GoodsUse.ReachableHoldings, actor, new(owners), EdibleFoodGoods)).Total;
        var eatenInputs = recipe.Inputs.Where(input => IsEdibleFood(input.ResourceId)).Sum(input => input.Amount);
        return stock - Math.Max(1, eatenInputs) >= residents.Length * FarmFieldRules.MealsPerPersonPerDay;
    }
}

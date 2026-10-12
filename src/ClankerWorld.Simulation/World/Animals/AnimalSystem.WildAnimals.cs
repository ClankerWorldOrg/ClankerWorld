using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

using static ClankerWorld.Simulation.World.WorldRuntime.AnimalSharedRules;

namespace ClankerWorld.Simulation.World;

internal sealed partial class AnimalSystem
{
    internal AnimalState AdvanceWildAnimalCare(IAnimalWorld world, AnimalState animal, long tick)
    {
        if (AnimalRules.HasCare(animal, tick)) return animal;
        var definition = AnimalRules.Definition(animal.Species);
        if (animal.WildFedUntilTick <= tick)
        {
            var occupied = WildAnimalRouteOccupants(world, animal);
            var forage = WildAnimalForageSources(world.Map, world.Environment.Ecology, animal)
                .FirstOrDefault(source => world.Map.IsReachableOnFoot(animal.Position, source.Position) &&
                    world.SharedUnoccupiedRoute(animal.Position, occupied, source.Position, 1).Count > 0);
            if (forage is null) return animal;
            if (world.Map.FootDistance(animal.Position, forage.Position) > 1)
                return MoveWildAnimalToward(world, animal, forage.Position, tick, 1);
            var harvested = EcologyRules.Harvest(world.Environment.Ecology.GetResource(forage.Id), definition.DailyFeed);
            if (!harvested.IsValid || harvested.Resource is not { } remaining) return animal;
            world.ReplaceEcologyResource(forage.Id, remaining);
            world.SyncEcologyResourceStates();
            SetAnimal(animal = animal with { WildFedUntilTick = tick + AnimalDayTicks(world) });
        }
        if (animal.WildWaterUntilTick <= tick)
        {
            var occupied = WildAnimalRouteOccupants(world, animal);
            var shore = world.FreshWaterShorePositions().Where(point => !State.Animals.Any(other => other.Id != animal.Id &&
                    other.DiedTick is null && other.Position == point) && !world.Inhabitants.Values.Any(person => person.Position == point) &&
                    world.Map.FootDistance(animal.Position, point) <= 12 &&
                    world.Map.IsReachableOnFoot(animal.Position, point)).OrderBy(point => world.Map.FootDistance(animal.Position, point))
                .ThenBy(point => point.Y).ThenBy(point => point.X)
                .Where(point => world.SharedUnoccupiedRoute(animal.Position, occupied, point, 0).Count > 0)
                .Cast<GridPoint?>().FirstOrDefault();
            if (shore is null) return animal;
            if (animal.Position != shore.Value) return MoveWildAnimalToward(world, animal, shore.Value, tick, 0);
            SetAnimal(animal = animal with { WildWaterUntilTick = tick + AnimalDayTicks(world) });
        }
        if (animal.WildFedUntilTick > tick && animal.WildWaterUntilTick > tick)
            SetAnimal(animal = animal with { CareUntilTick = Math.Min(animal.WildFedUntilTick, animal.WildWaterUntilTick) });
        return animal;
    }

    internal static IOrderedEnumerable<MapResource> WildAnimalForageSources(WorldMap map, EcologyState ecology, AnimalState animal)
    {
        var feed = AnimalRules.Definition(animal.Species).DailyFeed;
        return map.Resources.Where(source => source.NaturalObjectKind == "wild_greens" &&
                map.FootDistance(animal.Position, source.Position) <= 12 &&
                ecology.TryGetResource(source.Id, out var resource) && resource.Quantity >= feed)
            .OrderBy(source => map.FootDistance(animal.Position, source.Position)).ThenBy(source => source.Id, StringComparer.Ordinal);
    }

    internal AnimalState MoveWildAnimalToward(IAnimalWorld world, AnimalState animal, GridPoint destination, long tick, int range)
    {
        if (tick % 2 != 0 || animal.TamingWork is not null) return animal;
        var route = world.SharedUnoccupiedRoute(animal.Position, WildAnimalRouteOccupants(world, animal), destination, range);
        if (route.Count > 1) MoveAnimal(world, animal, route[1]);
        return Animal(animal.Id)!;
    }

    internal static HashSet<GridPoint> WildAnimalRouteOccupants(IAnimalWorld world, AnimalState animal) =>
        GroundOccupancy.Combine(world.Inhabitants.Values.Select(person => person.Position), world.Ground.AnimalPositions(animal.Id));
}


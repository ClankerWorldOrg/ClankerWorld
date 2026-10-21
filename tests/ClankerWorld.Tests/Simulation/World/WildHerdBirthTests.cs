using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class AnimalPipelineTests
{
    [Fact]
    public async Task WildBirthConsumesTheEighthReservedPlaceAndReplaysWithoutOverfillingTheHerd()
    {
        using var setup = NormalPathWorld.CreateGenerated("animal-wild-birth-cap", _ => new AnimalChooser());
        var state = setup.ExportState();
        var day = state.WorldSystems!.Config.TicksPerDay;
        var free = state.Map.Tiles.Select(tile => tile.Position).Where(position => state.Map.IsBuildable(position) &&
            !state.Agents.Any(agent => agent.Position == position)).ToArray();
        var motherPosition = free.First(position => state.Map.FootNeighbors(position).Count(free.Contains) >= 2);
        var birthTiles = state.Map.FootNeighbors(motherPosition).ToHashSet();
        var otherPositions = free.Where(position => position != motherPosition && !birthTiles.Contains(position)).Take(6).ToArray();
        Assert.Equal(6, otherPositions.Length);
        var mother = new AnimalState("wild-mother", "Moss", "sheep", "female", -5L * day, motherPosition,
            "wild-herd", CareUntilTick: day, Pregnancy: new("wild-male-0", 4 * day - 1, 0),
            WildFedUntilTick: day, WildWaterUntilTick: day);
        var animals = otherPositions.Select((position, index) => mother with
        {
            Id = "wild-male-" + index,
            Name = "Male " + index,
            Sex = "male",
            Position = position,
            Pregnancy = null,
        }).Append(mother).OrderBy(animal => animal.Id, StringComparer.Ordinal).ToArray();
        var initial = state with { AnimalWorld = new(true, animals, []) };
        var bytes = WorldRuntimeCodec.Encode(initial);
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new AnimalChooser());
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new AnimalChooser());

        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);

        Assert.Equal(AnimalRules.PopulationCap, world.Animals.Count);
        var child = Assert.Single(world.Animals, animal => animal.BornTick == 1);
        Assert.Equal("wild-herd", child.HerdId);
        Assert.Null(child.HouseholdId);
        Assert.InRange(state.Map.FootDistance(mother.Position, child.Position), 1, 1);
        Assert.Null(world.Animals.Single(animal => animal.Id == mother.Id).Pregnancy);
        Assert.Equal(1 + 2 * day, world.Animals.Single(animal => animal.Id == mother.Id).BreedingReadyTick);
        Assert.Single(world.ExportState().Events, item => item.Kind == "animal_born");
        Assert.Equal(7, initial.AnimalWorld.Animals.Count);
        Assert.NotNull(initial.AnimalWorld.Animals.Single(animal => animal.Id == mother.Id).Pregnancy);
        var afterBirth = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.Equal(afterBirth, WorldRuntimeCodec.Encode(replay.ExportState()));
        using var reload = WorldRuntime.Restore(WorldRuntimeCodec.Decode(afterBirth), _ => new AnimalChooser());
        Assert.Equal(afterBirth, WorldRuntimeCodec.Encode(reload.ExportState()));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.True((await reload.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(reload.ExportState()));
        var completed = world.ExportState();
        var overfull = completed with
        {
            AnimalWorld = completed.AnimalWorld with
            {
                Animals = completed.AnimalWorld.Animals.Append(child with
                {
                    Id = "wild-ninth",
                    Position = free.First(position => !world.Animals.Any(animal => animal.Position == position)),
                }).OrderBy(animal => animal.Id, StringComparer.Ordinal).ToArray(),
            },
        };
        Assert.Throws<InvalidDataException>(() => WorldRuntime.Restore(overfull));
    }
}

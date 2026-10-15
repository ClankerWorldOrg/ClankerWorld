using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class FarmingInvariantTests
{
    private static FarmFieldState Field => new(new(1, 1), "home", FarmFieldStage.Prepared);

    [Theory]
    [InlineData("stage")]
    [InlineData("crop")]
    [InlineData("cycle")]
    [InlineData("tick")]
    [InlineData("work")]
    public void InvalidFieldFailsAtFarmingBeforePublication(string value)
    {
        var field = Field;
        var system = new FarmingSystem(new([field]), "local-fields");
        var before = system.State;
        var invalid = value switch
        {
            "stage" => field with { Stage = (FarmFieldStage)999 },
            "crop" => field with { Crop = FarmFieldRules.Grain },
            "cycle" => field with { Cycle = -1 },
            "tick" => field with { LastWorkedTick = -1 },
            "work" => field with { Work = new("worker", FarmWorkKind.Till, 2, 0) },
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };

        Assert.Throws<InvalidDataException>(() => system.SetFarmField(null!, invalid));
        Assert.Same(before, system.State);
    }

    [Fact]
    public void AWorkerCannotBePublishedAtTwoFieldWorkSites()
    {
        var field = Field with { Work = new("worker", FarmWorkKind.Plant, 4, 0, "seed", FarmFieldRules.Grain) };
        var system = new FarmingSystem(new([field]), "local-fields");
        var before = system.State;

        Assert.Throws<InvalidDataException>(() => system.SetFarmField(null!, field with { Position = new(2, 1) }));

        Assert.Same(before, system.State);
        Assert.Same(field, Assert.Single(system.State.Fields));
    }

    [Fact]
    public void LocalFieldMutationKeepsExternalLandChecksAndTheOldSnapshotSeparate()
    {
        var field = Field;
        var system = new FarmingSystem(new([field]), "local-fields");
        var before = system.State;

        system.SetFarmField(null!, field with { Cycle = 1 });

        Assert.Equal(1, Assert.Single(system.State.Fields).Cycle);
        Assert.Same(field, Assert.Single(before.Fields));
    }
}

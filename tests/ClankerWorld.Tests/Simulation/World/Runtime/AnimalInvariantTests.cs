using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class AnimalInvariantTests
{
    private static AnimalState Horse => new("horse", "Horse", "horse", "male", 0, new(1, 1), "herd", "home", "yard");

    [Theory]
    [InlineData("name")]
    [InlineData("permissions")]
    [InlineData("pregnancy")]
    [InlineData("attachment")]
    [InlineData("product")]
    [InlineData("progress")]
    public void InvalidAnimalFailsAtItsOwnerBeforePublication(string field)
    {
        var animal = Horse;
        var system = new AnimalSystem(new(true, [animal], []));
        var before = system.State;
        var invalid = field switch
        {
            "name" => animal with { Name = "" },
            "permissions" => animal with { CarePermissions = ["carer", "carer"] },
            "pregnancy" => animal with { Pregnancy = new("father", 0, 0) },
            "attachment" => animal with { RiderId = "rider", LeaderId = "leader", LeadDestination = new(2, 1), SaddleLotId = "saddle" },
            "product" => animal with { ReadyProductLotId = "product" },
            "progress" => animal with { ProductProgressTicks = -1 },
            _ => throw new ArgumentOutOfRangeException(nameof(field)),
        };

        Assert.Throws<InvalidDataException>(() => system.SetAnimal(invalid));
        Assert.Same(before, system.State);
        Assert.Same(animal, Assert.Single(system.State.Animals));
    }

    [Fact]
    public void LocalAnimalChecksKeepExternalOwnershipForTheLinkValidator()
    {
        var animal = Horse;
        var system = new AnimalSystem(new(true, [animal], []));
        var before = system.State;

        system.SetAnimal(animal with { Name = "Changed" });

        Assert.Equal("Changed", Assert.Single(system.State.Animals).Name);
        Assert.Same(animal, Assert.Single(before.Animals));
    }
}

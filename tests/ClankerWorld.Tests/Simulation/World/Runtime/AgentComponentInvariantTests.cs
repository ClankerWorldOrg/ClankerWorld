using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class AgentComponentInvariantTests
{
    [Theory]
    [InlineData("project")]
    [InlineData("equipment")]
    [InlineData("survival")]
    [InlineData("exploration")]
    [InlineData("departure")]
    [InlineData("child_model")]
    public void InvalidComponentFailsBeforeItsStorePublishes(string component)
    {
        var store = new AgentStore();
        store.PlaceFounder("resident", new(1, 1));
        var before = store.Living["resident"];

        Assert.Throws<InvalidDataException>(() =>
        {
            switch (component)
            {
                case "project": store.RecordProject("resident", before, new("invalid", "Work", 0, "working")); break;
                case "equipment": store.RecordEquipment("resident", before, new(ClothingLotId: "unit", CarryAidLotId: "unit")); break;
                case "survival": store.RecordSurvival("resident", before, new(WarmthBasisPoints: -1)); break;
                case "exploration": store.RecordExploration("resident", before, new([], [new(1, 1), new(1, 1)], 0, false)); break;
                case "departure": store.RecordDepartureAllowance("resident", before, [new("home", 0, "voluntary", [], [], 0)]); break;
                case "child_model": store.SelectChildModel("resident", before, new("unsupported", null, null, null, null, "no_parent_model")); break;
                default: throw new ArgumentOutOfRangeException(nameof(component));
            }
        });

        Assert.Same(before, store.Living["resident"]);
    }

    [Fact]
    public void DetachedPreparationKeepsItsOriginalPublicationPoint()
    {
        var store = new AgentStore();
        store.PlaceFounder("resident", new(1, 1));
        var before = store.Living["resident"];
        var prepared = AgentStore.PrepareSurvival(before, new(IllnessBasisPoints: 10_001));

        Assert.Same(before, store.Living["resident"]);
        Assert.Throws<InvalidDataException>(() => store.ApplyDeveloperNeedEdit("resident", prepared));
        Assert.Same(before, store.Living["resident"]);
    }
}

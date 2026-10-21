using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class CommunitySystemTests
{
    private static AgentKnowledgeFact Fact => new("fact-first", "resident", "resident", new(1, 1), "Grass", [], 0, "firsthand");

    [Fact]
    public void NullRecipeRecordIsRejectedBySaveKnowledgeValidation()
    {
        using var world = new WorldRuntime("null-recipe-validation");
        var state = world.ExportState();
        var malformed = state.Knowledge! with { Recipes = [null!] };

        Assert.Throws<InvalidDataException>(() =>
            WorldRuntime.Restore(state with { Knowledge = malformed }));
    }

    [Fact]
    public void DuplicateKnowledgeFailsAtItsOwnerWithoutPublishingTheInvalidSnapshot()
    {
        var system = new CommunitySystem(new([Fact], []));
        var before = system.State;

        var failure = Assert.Throws<InvalidDataException>(() => system.RecordObservedFact(Fact, null));

        Assert.Contains("Community knowledge", failure.Message, StringComparison.Ordinal);
        Assert.Same(before, system.State);
    }

    [Fact]
    public void ReadingAChangedSiteKeepsTheRecipientsEarlierKnowledge()
    {
        var system = new CommunitySystem(new([Fact], []));
        var artifact = new AgentKnowledgeArtifact("artifact-first", "author", "lot-first", "field_map", "Map", 1,
            [Fact with { Id = "fact-later", OwnerId = "author", Terrain = "Forest" }]);
        var before = system.State;

        var learned = system.LearnArtifactFacts("resident", "author", artifact, "read", 2);

        Assert.Equal(0, learned);
        Assert.Same(before, system.State);
        Assert.Equal("Grass", Assert.Single(system.State.Facts).Terrain);
    }

    [Fact]
    public void ARequestedLessonCannotContainTrainingProgress()
    {
        var lesson = new SkillLesson("mentor", SkillKind.Building, "requested", 1, 0, 0);
        Assert.Throws<InvalidDataException>(() => CommunitySystem.ValidateLocalLesson(lesson));
    }

    [Fact]
    public void HousingAnswersMustBelongToTheFrozenAdmissionRoster()
    {
        var housing = new HousingState(Request: new("household-first", 0, WorldRuntime.HousingRequestTicks,
            ["voter"], ["outsider"], []));
        Assert.Throws<InvalidDataException>(() => CommunitySystem.ValidateLocalHousing(housing));
    }
}

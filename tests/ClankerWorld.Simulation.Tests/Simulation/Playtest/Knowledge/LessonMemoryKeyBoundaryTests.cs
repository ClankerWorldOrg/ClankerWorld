using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Playtest;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.Tests;

public sealed class LessonMemoryKeyBoundaryTests
{
    [Fact]
    public void RecallKeepsItsCanonicalKeyBoundaryAndOwnerFilter()
    {
        using var world = new PrivateWorldRuntime("lesson-memory-boundary");
        var owner = world.Society.Inhabitants[0].Id;
        var teacher = world.Society.Inhabitants[1].Id;
        var valid = new SocietySocialMemory(new string('x', 128), owner, teacher,
            "Learned crafting with the teacher.", "public", 0)
        { Kind = SocietyMemoryKind.Skill };
        // Explicit malformed input checks the recall boundary, independently of
        // the native generator regression. It is not a generated lesson fixture.
        var sources = new[]
        {
            valid,
            valid with { Id = new string('x', 129) },
            valid with { Id = " lesson-gratitude:padded" },
            valid with { Id = "lesson-gratitude:control\0" },
            valid with { Id = "" },
            valid with { Id = "lesson-gratitude:another-owner", OwnerId = teacher },
        };
        var recalled = PrivateWorldMemoryRetrieval.Retrieve(sources, [], null, owner, 0,
            [new CognitionCandidate("safe_idle", "Recall the crafting lesson.", 0)]);
        Assert.Equal(valid.Id, Assert.Single(recalled).Id);
        Assert.Equal(owner, recalled[0].OwnerId);
        Assert.Equal(teacher, recalled[0].SubjectId);
    }
}

using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed class SmallAgentLookupTests
{
    [Theory]
    [InlineData(2, 192)]
    [InlineData(8, 256)]
    public void VerifiedSmallLookupsUseBoundedTemporaryMemory(int count, int bytesPerHit)
    {
        Assert.True(DerivedVerification.RecomputeOnHit);
        var society = CreateSociety(count);
        var expected = society.Agents[^1];
        var id = expected.Id;
        for (var warm = 0; warm < 200; warm++) _ = society.GetAgent(id);
        var result = expected;
        const int samples = 128;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var sample = 0; sample < samples; sample++) result = society.GetAgent(id);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Same(expected, result);
        Assert.True(allocated <= (long)samples * bytesPerHit,
            $"{count} residents allocated {allocated / samples:N0} bytes per verified lookup; budget {bytesPerHit:N0}.");
    }

    [Theory]
    [InlineData(8)]
    [InlineData(9)]
    public void LookupsKeepMalformedIdSemanticsAndDetectUnrelatedResidentMutation(int count)
    {
        Assert.True(DerivedVerification.RecomputeOnHit);
        var society = CreateSociety(count);
        var first = society.Agents[0];
        var last = society.Agents[^1];
        Assert.Same(last, society.GetAgent(last.Id));
        Assert.Throws<InvalidOperationException>(() => society.GetAgent("missing"));
        var duplicates = society with { Agents = society.Agents.Append(first).ToArray() };
        Assert.Same(last, duplicates.GetAgent(last.Id));
        Assert.Throws<InvalidOperationException>(() => duplicates.GetAgent(first.Id));
        var nullId = first with { Id = null! };
        var malformed = society with { Agents = society.Agents.Skip(1).Prepend(nullId).ToArray() };
        Assert.Same(nullId, malformed.GetAgent(null!));
        Assert.Same(last, malformed.GetAgent(last.Id));
        var nullDuplicates = malformed with { Agents = malformed.Agents.Append(nullId).ToArray() };
        Assert.Throws<InvalidOperationException>(() => nullDuplicates.GetAgent(null!));
        Assert.Same(last, nullDuplicates.GetAgent(last.Id));
        var people = society.Agents.ToList();
        var mutable = society with { Agents = people };
        Assert.Same(last, mutable.GetAgent(last.Id));
        people[0] = first with { Name = "Changed Resident" };
        var error = Assert.Throws<InvalidOperationException>(() => mutable.GetAgent(last.Id));
        Assert.Equal("A snapshot-derived value differs from its recomputation.", error.Message);
    }

    private static SocietyCheckpoint CreateSociety(int count) => SocietyRules.CreateGenesis(
        "small-agent-lookup", Enumerable.Range(0, count).Select(index => SocietyRules.CreateFounder(
            $"agent-{index:D32}".PadRight(128, 'x'), $"Noe\u0308l{(char)('a' + index)} Moss")));
}

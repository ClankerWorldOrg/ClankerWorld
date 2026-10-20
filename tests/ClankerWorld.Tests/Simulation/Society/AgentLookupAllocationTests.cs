using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed class AgentLookupAllocationTests
{
    [Theory]
    [InlineData(2, 416)]
    [InlineData(8, 640)]
    public void VerifiedAgentLookupsKeepAllocationBoundedWhileRecomputingTheIndex(int count, int bytesPerHit)
    {
        Assert.True(DerivedVerification.RecomputeOnHit);
        var people = Enumerable.Range(0, count).Select(index => SocietyRules.CreateFounder(
            $"agent-{index:D32}".PadRight(128, 'x'), $"Noe\u0308l{(char)('a' + index)} Moss")).ToArray();
        var society = SocietyRules.CreateGenesis("agent-lookup-allocation", people);
        var id = people[^1].Id;
        var expected = society.GetAgent(id);
        for (var warm = 0; warm < 200; warm++) _ = society.GetAgent(id);
        const int samples = 128;
        var result = expected;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var sample = 0; sample < samples; sample++) result = society.GetAgent(id);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Same(expected, result);
        Assert.True(allocated <= (long)samples * bytesPerHit,
            $"{count} agents allocated {allocated / samples:N0} bytes per verified lookup; budget {bytesPerHit:N0}.");
    }
}

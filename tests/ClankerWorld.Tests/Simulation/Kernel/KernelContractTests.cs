using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Tests;

public sealed class KernelContractTests
{
    [Theory]
    [InlineData("Élodie 🌿", "weather/episode:12/region:3,7/duration",
        new uint[] { 3_410_588_880, 3_859_870_803, 3_556_394_978, 4_173_647_167, 2_485_175_312 })]
    [InlineData("E\u0301lodie 🌿", "weather/episode:12/region:3,7/duration",
        new uint[] { 1_423_641_495, 735_490_022, 2_383_979_996, 3_016_083_998, 2_756_199_667 })]
    [InlineData("sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", "tree:127,63",
        new uint[] { 1_282_861_567, 1_665_370_766, 1_167_485_245, 4_515_284, 3_880_440_497 })]
    public void NamedStreamsKeepExactUnicodeAndLongSeedVectors(string seed, string stream, uint[] expected)
    {
        var random = Pcg32XshRrV1.Create(seed, stream);

        Assert.Equal(expected, Enumerable.Range(0, expected.Length).Select(_ => random.NextUInt()).ToArray());
    }

    [Fact]
    public void RepeatedStreamCreationUsesBoundedTemporaryMemory()
    {
        for (var index = 0; index < 8; index++)
            _ = Pcg32XshRrV1.Create("fixture-seed", "weather").NextUInt();
        ulong checksum = 0;
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 64; index++)
            checksum += Pcg32XshRrV1.Create("fixture-seed", "weather").NextUInt();
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;

        Assert.Equal(64UL * 2_746_376_332, checksum);
        Assert.InRange(allocated, 0, 22L * 1024);
    }

    [Fact]
    public void NamedPcgStreamIsStableAndCannotAccidentallyAliasAnotherStream()
    {
        var first = Pcg32XshRrV1.Create("fixture-seed", "weather");
        var same = Pcg32XshRrV1.Create("fixture-seed", "weather");
        var other = Pcg32XshRrV1.Create("fixture-seed", "resource:berry-patch");
        var actual = Enumerable.Range(0, 5).Select(_ => first.NextUInt()).ToArray();

        Assert.Equal("pcg32-xsh-rr-v1", Pcg32XshRrV1.AlgorithmId);
        Assert.Equal(
            new uint[] { 2_746_376_332, 2_393_862_774, 1_216_660_101, 2_255_332_717, 1_720_709_021 },
            actual);
        Assert.Equal(actual, Enumerable.Range(0, 5).Select(_ => same.NextUInt()).ToArray());
        Assert.NotEqual(actual, Enumerable.Range(0, 5).Select(_ => other.NextUInt()).ToArray());
    }
}

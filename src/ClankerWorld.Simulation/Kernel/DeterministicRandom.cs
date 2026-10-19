using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace ClankerWorld.Simulation.Kernel;

/// <summary>
/// Portable PCG32 XSH-RR with explicit algorithm metadata and deterministic
/// HMAC-SHA-256 derivation of named world-local streams.
/// </summary>
public sealed class Pcg32XshRrV1
{
    public const string AlgorithmId = "pcg32-xsh-rr-v1";

    private const ulong Multiplier = 6_364_136_223_846_793_005UL;
    private ulong state;
    private readonly ulong increment;

    private Pcg32XshRrV1(ulong initialState, ulong initialIncrement)
    {
        increment = initialIncrement | 1UL;
        state = 0;
        _ = NextUInt();
        state = unchecked(state + initialState);
        _ = NextUInt();
    }

    public static Pcg32XshRrV1 Create(string worldSeed, string streamName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worldSeed);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamName);

        var key = Encoding.UTF8.GetBytes(worldSeed);
        var stateBytes = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes($"{streamName}/state"));
        var incrementBytes = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes($"{streamName}/increment"));
        return new Pcg32XshRrV1(
            BinaryPrimitives.ReadUInt64BigEndian(stateBytes),
            BinaryPrimitives.ReadUInt64BigEndian(incrementBytes));
    }

    public uint NextUInt()
    {
        var oldState = state;
        state = unchecked((oldState * Multiplier) + increment);
        var xorshifted = (uint)(((oldState >> 18) ^ oldState) >> 27);
        var rotation = (int)(oldState >> 59);
        return (xorshifted >> rotation) | (xorshifted << ((-rotation) & 31));
    }
}

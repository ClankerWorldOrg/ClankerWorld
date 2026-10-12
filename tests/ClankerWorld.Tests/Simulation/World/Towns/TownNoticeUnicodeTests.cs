using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed class TownNoticeUnicodeTests
{
    private static readonly Lazy<Task<byte[]>> Baseline = new(CreateBaseline);

    [Theory]
    [InlineData(false, -1)]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(true, -1)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    public async Task NativeReadAndRelayExcerptsKeepWholeCharactersAndSourceHistory(bool relay, int offset)
    {
        var state = WorldRuntimeCodec.Decode(await Baseline.Value);
        var town = state.Towns![0];
        var actor = town.ResidentIds[0];
        var source = town.ResidentIds[1];
        var society = state.Society.Society;
        if (relay)
            society = SocietyRules.RenameAgent(society, source, new string('L', 43) + " Vale").Checkpoint;
        var name = society.GetAgent(actor).Name;
        var sourceName = society.GetAgent(source).Name;
        var entryPrefix = relay ? $"Heard from {sourceName}: {town.Name}: " : $"Read Town notice: {town.Name}: ";
        var noticePrefix = $"law proposal by {name}: ";
        var entryCount = relay ? 3 : 1;
        var emojiIndex = relay ? 1023 - 2 * (entryPrefix.Length + 270 + 3) - entryPrefix.Length + offset : 269 + offset;
        Assert.InRange(emojiIndex, noticePrefix.Length, 270);
        var filler = emojiIndex - noticePrefix.Length;
        var text = new string('a', filler) + "😀tail";
        Assert.InRange(text.Length, 1, TownCouncilRules.MaximumProposalText);
        var council = town.Council!;
        var firstNotice = council.Notices.Count;
        for (var index = 0; index < entryCount; index++)
            council = TownCouncilRules.SubmitProposal(council, town.Id, actor, "law", null,
                index == entryCount - 1 ? text : new string((char)('b' + index), 256), "unicode-audit-" + index,
                town.ResidentIds, society.WorldTick, state.WorldSystems!.Config.TicksPerDay);
        var ids = council.Notices.Skip(firstNotice).Select(notice => notice.Id).ToArray();
        if (relay) council = TownCouncilRules.LearnNotices(council, source, ids, society.WorldTick);
        council = TownCouncilRules.LearnNotices(council, actor, ids, society.WorldTick, relay ? source : null);
        state = state with
        {
            JevEnabled = false,
            RoutineHelper = RoutineHelperSettings.Off,
            Towns = state.Towns.Select(item => item.Id == town.Id ? item with { Council = council } : item).ToArray(),
            Society = state.Society with { Society = society },
            Agents = state.Agents.Select(person => person with
            {
                FullnessBasisPoints = 10_000,
                Survival = person.Survival is { } survival ? survival with
                { WarmthBasisPoints = 10_000, NutritionBasisPoints = 10_000, IllnessBasisPoints = 0 } : null,
                LastDecisionContext = null,
                Project = null,
                TravelCooldownTicks = 0,
            }).ToArray(),
        };
        var observed = new ConcurrentQueue<AgentObservation>();
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)),
            _ => new CivicIdleProvider(observed));
        for (var tick = 0; tick < 4 && !observed.Any(item => item.AgentId == actor); tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var context = observed.First(item => item.AgentId == actor).Self!.CivicNote!;
        Assert.NotNull(context);
        Assert.StartsWith(entryPrefix, context, StringComparison.Ordinal);
        var limit = relay ? 1024 : entryPrefix.Length + 270;
        Assert.Equal(limit - (offset == 0 ? 1 : 0), context.Length);
        Assert.Equal(council.Knowledge, world.Towns[0].Council!.Knowledge);
        if (offset == -1) Assert.EndsWith("😀", context, StringComparison.Ordinal);
        else Assert.DoesNotContain("😀", context, StringComparison.Ordinal);
        Assert.Equal(council.Notices, world.Towns[0].Council!.Notices);
        world.Validate();
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var reloaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(reloaded.ExportState()));
        var wire = JsonSerializer.Deserialize<string>(JsonSerializer.Serialize(context))!;

        Assert.Equal(context, wire);
        _ = new UTF8Encoding(false, true).GetBytes(context);
    }

    private static async Task<byte[]> CreateBaseline()
    {
        using var world = NormalPathWorld.CreateGenerated("civic-unicode-e784ddf3", _ => new CivicIdleProvider(new()));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        return WorldRuntimeCodec.Encode(world.ExportState());
    }

    private sealed class CivicIdleProvider(ConcurrentQueue<AgentObservation> observed) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            observed.Enqueue(request.Observation);
            var selected = request.Observation.Candidates.Single(candidate => candidate.Id == "safe_idle").Id;
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId,
                Kind, ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                selected, 1, new Dictionary<string, double> { [selected] = 1 }));
        }
    }
}

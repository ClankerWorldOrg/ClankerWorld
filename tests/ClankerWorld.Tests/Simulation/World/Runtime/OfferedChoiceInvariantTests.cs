using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.World;
using Xunit.Abstractions;

namespace ClankerWorld.Tests;

public sealed class OfferedChoiceInvariantTests(ITestOutputHelper output)
{
    [Fact]
    public async Task EveryOfferedChoiceIsSafeInAStrictTrial()
    {
        var firstSeed = Setting("CLANKERWORLD_CHOICE_SEED", 1815);
        var seeds = Setting("CLANKERWORLD_CHOICE_SEEDS", 1);
        var frames = Setting("CLANKERWORLD_CHOICE_TICKS", 1);
        var timer = Stopwatch.StartNew();
        var cases = 0;
        var failures = new List<string>();
        for (var offset = 0; offset < seeds; offset++)
        {
            var seed = checked(firstSeed + offset);
            output.WriteLine($"Preparing seed={seed}; scenario=mixed.");
            using var baseline = WorldRuntime.Restore(await OfferedChoiceWorld.CreateAsync(seed), _ => new SweepChoiceProvider());
            AssertScenario(baseline.ExportState());
            for (var frame = 0; frame < frames; frame++)
            {
                output.WriteLine($"Sweeping seed={seed}; scenario=mixed; tick={baseline.WorldTick}; frame={frame}.");
                foreach (var person in baseline.Society.Agents.Where(person => person.Status == SocietyAgentStatus.Active &&
                    person.AgeBand != SocietyAgeBand.Infant))
                    baseline.SubmitInstruction(new(DerivedId.Numbered("choice-refresh:" + person.Id + ":", frame + 1),
                        "owner:choice-test", person.Id, OwnerInstructionKind.Suggestion, "Consider the choices available here."));
                var offered = baseline.PrepareChoiceTrialOffers();
                var original = baseline.ExportState();
                var recorder = new SweepChoiceProvider();
                using var discovery = baseline.CopyForChoiceTrial(_ => recorder);
                await discovery.ApplyChoiceTrialAsync();
                StrictCheckpoint(discovery);
                var control = ActionState(discovery.ExportState());
                var observations = recorder.Observations.ToArray();
                Assert.Equal(offered.Select(observation => observation.AgentId).Order(StringComparer.Ordinal),
                    observations.Select(observation => observation.AgentId).Order(StringComparer.Ordinal));
                foreach (var observation in observations)
                    foreach (var candidate in observation.Candidates)
                    {
                        cases++;
                        var context = $"seed={seed}; scenario=mixed; tick={baseline.WorldTick}; frame={frame}; agent={observation.AgentId}; choice={candidate.Id}";
                        try
                        {
                            var provider = new SweepChoiceProvider(observation.AgentId, candidate.Id);
                            using var trial = baseline.CopyForChoiceTrial(_ => provider);
                            var decisions = await trial.ApplyChoiceTrialAsync();
                            var decision = Assert.Single(decisions, decision => decision.AgentId == observation.AgentId);
                            Assert.Contains(provider.Observations, actual => actual.AgentId == observation.AgentId &&
                                actual.Candidates.Any(offered => offered.Id == candidate.Id));
                            StrictCheckpoint(trial);
                            var after = trial.ExportState();
                            if (!decision.Admission.Accepted || decision.Admission.FellBack)
                            {
                                Assert.False(string.IsNullOrWhiteSpace(decision.Admission.Outcome));
                                continue;
                            }
                            Assert.Equal(candidate.Id, decision.Admission.Intention!.CandidateId);
                            var refusal = after.Events.Where(entry => entry.EventId > original.Events[^1].EventId &&
                                entry.Detail.Contains(observation.AgentId, StringComparison.Ordinal))
                                .Any(entry => entry.Kind.EndsWith("_rejected", StringComparison.Ordinal) ||
                                    entry.Kind.EndsWith("_refused", StringComparison.Ordinal) ||
                                    entry.Kind.EndsWith("_blocked", StringComparison.Ordinal) ||
                                    entry.Kind.EndsWith("_failed", StringComparison.Ordinal));
                            var orderProgress = observation.OperativeOrderInstructionId is { } orderId &&
                                (JsonSerializer.Serialize(original.Instructions!.Single(order => order.InstructionId == orderId).Task) !=
                                    JsonSerializer.Serialize(after.Instructions!.Single(order => order.InstructionId == orderId).Task) ||
                                 original.Agents.Single(person => person.AgentId == observation.AgentId).Position !=
                                    after.Agents.Single(person => person.AgentId == observation.AgentId).Position);
                            Assert.True(candidate.Id == "safe_idle" || ActionState(after) != control || orderProgress || refusal,
                                "The admitted choice made no progress and provided no refusal reason.");
                        }
                        catch (Exception exception)
                        {
                            failures.Add(context + "\n" + exception);
                        }
                    }
                if (frame + 1 < frames)
                {
                    Assert.True((await baseline.AdvanceOneTickAsync()).Advanced);
                    StrictCheckpoint(baseline);
                }
            }
        }
        output.WriteLine($"Checked {cases} choices over {seeds} seeds and {frames} snapshots per seed in {timer.Elapsed.TotalSeconds:F1}s.");
        Assert.True(cases > 0);
        Assert.True(failures.Count == 0, string.Join("\n\n", failures));
    }

    private static int Setting(string name, int fallback)
    {
        var value = int.Parse(Environment.GetEnvironmentVariable(name) ?? fallback.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        Assert.InRange(value, 1, 10_000);
        return value;
    }

    private static void StrictCheckpoint(WorldRuntime world) =>
        WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState()));

    private static string ActionState(WorldRuntimeState state) => JsonSerializer.Serialize(new
    {
        Agents = state.Agents.Select(person => new
        {
            person.AgentId,
            person.Position,
            person.Project,
            person.Equipment,
            person.Exploration,
            person.Lesson,
            person.Parenthood,
            person.Housing,
            person.Skills,
            person.SocialStanding,
            person.Departures,
            person.FullnessBasisPoints,
            person.Survival,
        }),
        People = state.Society.Society.Agents,
        state.Society.Society.Inventory,
        state.Society.Society.Relationships,
        state.Society.Society.Households,
        state.Society.Society.Births,
        state.Society.Society.Memories,
        state.WorldSimulation,
        state.Towns,
        state.Knowledge,
        state.AnimalWorld,
        state.BoatTransport,
        state.Fields,
        state.Conversations,
        state.Marriages,
        state.HandcartHitches,
    });

    private static void AssertScenario(WorldRuntimeState state)
    {
        Assert.NotEmpty(state.Society.Society.Births);
        Assert.Contains(state.Society.Society.Agents, person => person.Name.EnumerateRunes().Any(rune => rune.Value > 0xffff));
        Assert.Contains(state.Society.Society.Agents, person => person.Name.Contains('\u0301') || person.Name.Contains('\u0308'));
        Assert.Contains(state.Society.Society.Inventory.Lots, lot => lot.ProvenanceLotId is not null);
        Assert.Contains(state.Society.Society.Inventory.Lots, lot => lot.ItemKind == "storage_pot" && lot.ConditionBasisPoints == 0);
        Assert.Contains(state.Towns!.SelectMany(town => town.Markets), market => market.Occupancies.Any(visit => visit.EndedTick is null));
        Assert.Contains(state.AnimalWorld!.Animals, animal => animal.RiderId is not null);
        Assert.Contains(state.BoatTransport!.Boats, boat => boat.Journey is not null);
        Assert.Contains(state.Agents, person => PersonalEquipmentRules.CarriedQuantity(state.Society.Society.Inventory, person.AgentId, person.Equipment) >=
            PersonalEquipmentRules.Capacity(state.Society.Society.Inventory, person.AgentId, person.Equipment));
        Assert.Contains(state.Agents, person => SwimmingRules.IsSwimmingWater(state.Map, person.Position));
        Assert.True(state.Agents.Count > 4);
    }
}

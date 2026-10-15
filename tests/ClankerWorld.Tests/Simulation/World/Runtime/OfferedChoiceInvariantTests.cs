using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.World;
using Xunit.Abstractions;

namespace ClankerWorld.Tests;

[Collection("Offered choice invariants")]
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
                            Assert.True(decision.Admission.Accepted, decision.Admission.Outcome);
                            Assert.False(decision.Admission.FellBack, decision.Admission.Outcome);
                            Assert.Equal(candidate.Id, decision.Admission.Intention!.CandidateId);
                            var refusal = after.Events.Where(entry => entry.EventId > original.Events[^1].EventId &&
                                entry.Detail.Contains(observation.AgentId, StringComparison.Ordinal))
                                .Any(entry => entry.Kind.EndsWith("_rejected", StringComparison.Ordinal) ||
                                    entry.Kind.EndsWith("_refused", StringComparison.Ordinal) ||
                                    entry.Kind.EndsWith("_blocked", StringComparison.Ordinal) ||
                                    entry.Kind.EndsWith("_failed", StringComparison.Ordinal));
                            var beforeActor = original.Agents.Single(person => person.AgentId == observation.AgentId);
                            var afterActor = after.Agents.Single(person => person.AgentId == observation.AgentId);
                            // Must Do also runs for the idle control. A walking step may pay
                            // its remaining travel cost without changing position this turn.
                            var orderProgress = observation.OperativeOrderInstructionId is { } orderId &&
                                (JsonSerializer.Serialize(original.Instructions!.Single(order => order.InstructionId == orderId).Task) !=
                                    JsonSerializer.Serialize(after.Instructions!.Single(order => order.InstructionId == orderId).Task) ||
                                 beforeActor.Position != afterActor.Position ||
                                 afterActor.TravelCooldownTicks < beforeActor.TravelCooldownTicks);
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MedicalConsentChangesCountAsProgressBeyondAnAdmittedIdleControl(bool allow)
    {
        using var generated = NormalPathWorld.CreateGenerated("choice-medical-progress", _ => new SweepChoiceProvider());
        Assert.True((await generated.AdvanceOneTickAsync()).Advanced);
        var state = generated.ExportState();
        var patient = NormalPathWorld.Founder1;
        var caregiver = NormalPathWorld.Founder2;
        var occupied = state.Map.Resources.Select(item => item.Position)
            .Concat(state.Map.MapObjects.Select(item => item.Position))
            .Concat(state.Agents.Select(person => person.Position))
            .Concat(state.WorldSimulation!.Buildings.SelectMany(building => WorldContentSimulationRules.Footprint(
                state.WorldContent!.Buildings.Single(definition => definition.CanonicalId == building.DefinitionId), building)))
            .ToHashSet();
        bool Free(GridPoint point) => state.Map.IsBuildable(point) && !occupied.Contains(point);
        var center = state.Map.Tiles.Select(tile => tile.Position).First(point => Free(point) &&
            state.Map.FootNeighbors(point).Any(neighbor => Free(neighbor) && !state.Map.IsDiagonalFootStep(point, neighbor)));
        var adjacent = state.Map.FootNeighbors(center).First(point => Free(point) && !state.Map.IsDiagonalFootStep(center, point));
        state = state with
        {
            Agents = state.Agents.Select(person => person with
            {
                Position = person.AgentId == patient ? center : person.AgentId == caregiver ? adjacent : person.Position,
                FullnessBasisPoints = 10_000,
                LastDecisionContext = null,
                MedicalConsent = person.AgentId == patient && !allow ? new([caregiver]) : null,
                Survival = new(WarmthBasisPoints: 10_000, IllnessBasisPoints: person.AgentId == patient ? 4_000 : 0),
            }).ToArray(),
        };
        using var baseline = WorldRuntime.Restore(state, _ => new SweepChoiceProvider());
        var selected = (allow ? "medical_allow:" : "medical_revoke:") + caregiver;
        var offers = baseline.PrepareChoiceTrialOffers();
        Assert.Contains(Assert.Single(offers, observation => observation.AgentId == patient).Candidates,
            candidate => candidate.Id == selected);
        var original = WorldRuntimeCodec.Encode(baseline.ExportState());
        using var control = baseline.CopyForChoiceTrial(_ => new SweepChoiceProvider());
        var idle = Assert.Single(await control.ApplyChoiceTrialAsync(), decision => decision.AgentId == patient);
        Assert.True(idle.Admission.Accepted);
        Assert.False(idle.Admission.FellBack);
        Assert.Equal("safe_idle", idle.Admission.Intention!.CandidateId);
        StrictCheckpoint(control);
        var controlState = ActionState(control.ExportState());
        using var unchanged = baseline.CopyForChoiceTrial(_ => new SweepChoiceProvider());
        Assert.All(await unchanged.ApplyChoiceTrialAsync(), decision => Assert.True(decision.Admission.Accepted));
        StrictCheckpoint(unchanged);
        Assert.Equal(controlState, ActionState(unchanged.ExportState()));
        using var trial = baseline.CopyForChoiceTrial(_ => new SweepChoiceProvider(patient, selected));
        var admitted = Assert.Single(await trial.ApplyChoiceTrialAsync(), decision => decision.AgentId == patient);
        Assert.True(admitted.Admission.Accepted);
        Assert.False(admitted.Admission.FellBack);
        Assert.Equal(selected, admitted.Admission.Intention!.CandidateId);
        StrictCheckpoint(trial);
        Assert.Equal(allow, trial.Agents.Single(person => person.AgentId == patient).MedicalConsent?.CaregiverIds.Contains(caregiver) == true);
        Assert.NotEqual(controlState, ActionState(trial.ExportState()));
        Assert.Equal(original, WorldRuntimeCodec.Encode(baseline.ExportState()));
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
            person.TravelCooldownTicks,
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
            person.MedicalConsent,
            person.MedicalTreatment,
            person.MedicalSupplyTrip,
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

// Keep the bounded PR sweep's timing independent of other CPU-heavy worlds.
[CollectionDefinition("Offered choice invariants", DisableParallelization = true)]
public sealed class OfferedChoiceInvariantGroup;

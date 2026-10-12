using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed class SurvivalPriorityPrototypeTests
{
    [Theory]
    [InlineData(3_900, 5_900)]
    [InlineData(2_100, 5_900)]
    public async Task ComfortableReferencesDoNotVetoAvailableConstruction(int fullness, int warmth)
    {
        var initial = await BoundaryState(fullness, warmth);
        var provider = new CandidateProbe();
        using var world = WorldRuntime.Restore(initial, _ => provider);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Contains(provider.Seen, request => request.Observation.AgentId == "founder-rowan" &&
            request.Observation.Candidates.Any(candidate => candidate.Id.StartsWith("build:", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task UrgentFoodPrefersEatingButDoesNotRemoveCareResponse()
    {
        var initial = await BoundaryState(1_900, 6_000);
        initial = initial with
        {
            Society = initial.Society with
            {
                Society = initial.Society.Society with
                {
                    Agents = initial.Society.Society.Agents.Select(person => person.Id == "founder-mira" ? person with
                    {
                        BirthTick = initial.Society.Society.WorldTick - 3 * initial.Society.Society.Config.TicksPerWorldYear,
                        AgeBand = SocietyAgeBand.Child,
                        CurrentRole = SocietyWorkRole.Unassigned,
                        LastLifecycleYearChecked = 3
                    } : person).ToArray(),
                }
            }
        };
        var provider = new CandidateProbe(chooseLowest: true);
        using var world = WorldRuntime.Restore(initial, _ => provider);
        var step = await world.AdvanceOneTickAsync();
        var scout = Assert.Single(provider.Seen, request => request.Observation.AgentId == "founder-scout");
        Assert.Contains(scout.Observation.Candidates, candidate => candidate.Id == "guardian_accept:founder-mira");
        Assert.Contains(scout.Observation.Candidates, candidate => candidate.Id == "care:founder-mira");
        Assert.DoesNotContain(scout.Observation.Candidates, candidate => candidate.Id == "explore");
        Assert.Contains(step.Decisions, decision => decision.AgentId == "founder-scout" && decision.Admission.Intention?.CandidateId == "consume_food");
        Assert.Null(world.ExportState().Society.Society.GetAgent("founder-mira").PrimaryCaregiverId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ColdAgentOnlyInterruptsForWarmthWhileActuallyLosingIt(bool sheltered)
    {
        var initial = await BoundaryState(4_500, 3_200, WeatherKind.Rain);
        using var setup = WorldRuntime.Restore(initial);
        var site = initial.Map.Tiles.Select(tile => tile.Position).First(point => initial.Map.IsBuildable(point) &&
            !initial.Map.Resources.Any(item => item.Position == point) && !initial.Map.MapObjects.Any(item => item.Position == point) &&
            !initial.Agents.Any(item => item.Position == point));
        var house = setup.WorldContent.Buildings.Single(item => item.LocalId == "house-1x1");
        var placement = setup.PlaceBuilding("warm-home", house.CanonicalId, site, "household:first");
        Assert.True(placement.Applied, placement.Failure);
        initial = setup.ExportState();
        if (sheltered) initial = initial with
        {
            Agents = initial.Agents.Select(person => person.AgentId == "founder-scout"
            ? person with { Position = site } : person).ToArray()
        };
        var provider = new CandidateProbe(chooseLowest: true);
        using var world = WorldRuntime.Restore(initial, _ => provider);
        var step = await world.AdvanceOneTickAsync();
        Assert.True(step.Advanced);
        var candidates = Assert.Single(provider.Seen, request => request.Observation.AgentId == "founder-scout").Observation.Candidates;
        if (sheltered)
        {
            Assert.Contains(candidates, candidate => candidate.Id == "seek_warmth" && candidate.DeterministicPriority == 3);
            Assert.DoesNotContain(candidates, candidate => candidate.Id == "explore");
            for (var tick = 0; tick < 20; tick++) await world.AdvanceOneTickAsync();
            var recovering = world.Agents.Single(person => person.AgentId == "founder-scout");
            Assert.Equal(site, recovering.Position);
            Assert.True(recovering.Survival!.WarmthBasisPoints > 3_200);
        }
        else
        {
            Assert.Contains(candidates, candidate => candidate.Id == "seek_warmth" && candidate.DeterministicPriority <= 2);
            Assert.Contains(step.Decisions, decision => decision.AgentId == "founder-scout" && decision.Admission.Intention?.CandidateId is "seek_warmth" or "tend_fire");
        }
    }

    private static async Task<WorldRuntimeState> BoundaryState(int fullness, int warmth, WeatherKind weather = WeatherKind.Clear)
    {
        using var setup = new WorldRuntime("survival-boundary", _ => new CandidateProbe());
        setup.StageStarterContent();
        for (var tick = 0; tick < 3; tick++) await setup.AdvanceOneTickAsync();
        var state = setup.ExportState();
        var inventory = state.Society.Society.Inventory;
        foreach (var person in state.Agents)
            inventory = InventoryAuthority.AddLot(inventory, "boundary-food-" + person.AgentId, "food", person.AgentId, 4, state.Society.Society.WorldTick);
        return state with
        {
            Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } },
            Agents = state.Agents.Select(person => person with
            { FullnessBasisPoints = fullness, Survival = new SurvivalCondition(WarmthBasisPoints: warmth), LastDecisionContext = null }).ToArray(),
            WorldSystems = state.WorldSystems! with
            {
                RegionalWeather = null,
                Config = state.WorldSystems.Config with
                {
                    WeatherProfiles = Enum.GetValues<SeasonKind>().Select(season =>
                    new WeatherProfile(season, weather == WeatherKind.Clear ? 100 : 0, 0,
                        weather == WeatherKind.Rain ? 100 : 0, weather == WeatherKind.Storm ? 100 : 0, 0)).ToArray()
                },
                Climate = state.WorldSystems.Climate with { Weather = weather },
            },
        };
    }

    private sealed class CandidateProbe(string? selected = null, bool chooseLowest = false) : IDecisionProvider
    {
        public List<CognitionDecisionRequest> Seen { get; } = [];
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            Seen.Add(request);
            var choice = chooseLowest ? request.Observation.Candidates.OrderBy(candidate => candidate.DeterministicPriority).ThenBy(candidate => candidate.Id, StringComparer.Ordinal).First().Id
                : request.Observation.Candidates.Any(candidate => candidate.Id == selected) ? selected! : "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind,
                ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                choice, 1, request.Observation.Candidates.ToDictionary(candidate => candidate.Id, candidate => candidate.Id == choice ? 1d : 0d)));
        }
    }
}

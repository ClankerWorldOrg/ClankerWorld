using System.Threading.Channels;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Tests;

public sealed class NonLandAgreementHeldOfferTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(30);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AHeldRepairOfferChecksItsNewTermIdentityAfterAnotherPersonsConsent(bool anotherRepair)
    {
        var state = await NonLandRuntimeFixture.FindingAsync();
        var actor = NonLandRuntimeFixture.Subject;
        var household = state.Society.Society.Agents.Single(person => person.Id == actor).HouseholdId!;
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "held-repair-shop-fiber", "fiber", household, 2,
            storageBuildingId: "first-town-house-b");
        state = state with { Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } } };
        using (var setup = NonLandRuntimeFixture.Create(state, new NonLandTestProvider()))
        {
            var definition = setup.WorldContent.Buildings.Single(building => building.LocalId == "tailor-shop-1x1");
            var house = setup.WorldSimulation.Buildings.Single(building => building.HouseholdId == household &&
                setup.WorldContent.Buildings.Single(item => item.CanonicalId == building.DefinitionId).Tags.Contains("house"));
            Assert.Contains(Enumerable.Range(-4, 9).SelectMany(dy => Enumerable.Range(-4, 9)
                .Select(dx => new GridPoint(house.Position.X + dx, house.Position.Y + dy))),
                point => setup.PlaceBuilding("held-repair-tailor", definition.CanonicalId, point, household).Applied);
            state = setup.ExportState();
        }
        var board = state.Towns![0].OriginSite!.Value;
        inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "held-repair-coat", "padded_coat", actor, 1, conditionBasisPoints: 3_000);
        inventory = InventoryAuthority.AddLot(inventory, "held-repair-cloth", "cloth", actor, 2);
        state = NonLandRuntimeFixture.Strict(state with
        {
            Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } },
            Agents = state.Agents.Select(person => person with
            {
                Position = board,
                FullnessBasisPoints = 10_000,
                Survival = new(),
                LastDecisionContext = null,
                Equipment = person.AgentId == actor ? new("held-repair-coat") : person.Equipment
            }).ToArray()
        });
        var payload = new CognitionNonLandChoice(Statement: "I propose this named coat repair voluntarily.",
            Terms: [new("repair_equipment", actor, actor, "padded_coat", 1, "held-repair-coat")]);
        var independentPayload = anotherRepair ? payload : payload with
        { Terms = [new("return_goods", actor, NonLandRuntimeFixture.Witness, "wood", 1, NonLandRuntimeFixture.ReturnLot)] };
        var held = new HeldOfferProvider(payload);
        var enabled = false;
        var offered = false;
        var subject = new NonLandTestProvider
        {
            Choose = observation =>
            {
                if (!enabled || observation.AgentId != actor) return null;
                var choice = observation.Candidates.FirstOrDefault(candidate => candidate.Id.Contains("|law_case_inspect|", StringComparison.Ordinal)) ??
                    observation.Candidates.FirstOrDefault(candidate => candidate.Id.Contains("|remedy_read|", StringComparison.Ordinal)) ??
                    observation.Candidates.FirstOrDefault(candidate => candidate.Id.Contains("|remedy_accept|", StringComparison.Ordinal));
                if (choice is null && !offered)
                {
                    choice = observation.Candidates.FirstOrDefault(candidate => candidate.Id.Contains("|remedy_offer|", StringComparison.Ordinal));
                    if (choice is not null) offered = true;
                }
                return choice;
            },
            Payload = (_, candidate) => candidate.Id.Contains("|remedy_offer|", StringComparison.Ordinal) ? independentPayload : null
        };
        using var world = WorldRuntime.Restore(state, person => person == NonLandRuntimeFixture.Judge ? held : subject);
        try
        {
            NonLandRuntimeFixture.Wake(world, NonLandRuntimeFixture.Judge, "hold-voluntary-repair-offer");
            await AdvanceUntilAsync(() => held.Started.Task.IsCompleted, 16);
            var sequence = world.Towns[0].NonLandHearings.Sequence;
            var originalCase = world.Towns[0].NonLandHearings.Cases[0];
            Assert.Empty(world.Towns[0].NonLandHearings.Offers);
            Assert.False(world.ModelDecisionsFinished(NonLandRuntimeFixture.Judge));
            enabled = true;
            NonLandRuntimeFixture.Wake(world, actor, "independently-offer-and-consent-to-repair");
            await AdvanceUntilAsync(() => world.Towns[0].NonLandHearings.Agreements.Count == 1, 16);
            var town = world.Towns[0];
            var agreement = Assert.Single(town.NonLandHearings.Agreements);
            Assert.Equal("pending", agreement.Status);
            Assert.Equal(TownRecordIds.RemedyTerm(originalCase.Id, sequence, 0), Assert.Single(agreement.Terms).Id);
            Assert.True(town.NonLandHearings.Sequence > sequence);
            Assert.DoesNotContain(town.NonLandHearings.Offers, offer => offer.Status == "pending");
            Assert.Equal(originalCase.Evidence, town.NonLandHearings.Cases[0].Evidence);
            Assert.Equal(originalCase.Findings, town.NonLandHearings.Cases[0].Findings);
            Assert.Empty(town.NonLandHearings.Effects);
            Assert.Null(world.Agents.Single(person => person.AgentId == actor).Equipment!.Repair);
            var reservations = world.Society.Inventory.Reservations.ToArray();
            held.Release();
            await TownLandHearingHeldReplyTests.WaitForModelCompletionAsync(world, NonLandRuntimeFixture.Judge);
            NonLandAgreementActionPlan? admitted = null;
            await AdvanceUntilAsync(() => admitted is not null, 8, result =>
            {
                foreach (var decision in result.Decisions.Where(decision => decision.AgentId == NonLandRuntimeFixture.Judge &&
                    decision.ActionPlan is NonLandAgreementActionPlan { Choice.Verb: "remedy_offer" }))
                {
                    Assert.True(decision.Admission.Accepted);
                    Assert.False(decision.Admission.FellBack);
                    admitted = Assert.IsType<NonLandAgreementActionPlan>(decision.ActionPlan);
                    Assert.Equal(sequence, admitted.Sequence);
                    Assert.Equal(agreement.Terms[0].Id, Assert.Single(admitted.Terms!).Id);
                    if (anotherRepair)
                    {
                        Assert.NotNull(decision.PlanRefusal);
                        Assert.Equal(ActionRefusalReason.TargetUnavailable, decision.PlanRefusal.Reason);
                        Assert.Contains(result.Events, entry => entry.Kind == "choice_refused");
                    }
                    else Assert.Null(decision.PlanRefusal);
                }
            });
            Assert.Equal(anotherRepair ? 1 : 2, world.Towns[0].NonLandHearings.Offers.Count);
            if (!anotherRepair)
            {
                var published = world.Towns[0].NonLandHearings.Offers[^1];
                Assert.Equal("repair_equipment", Assert.Single(published.Terms).Kind);
                Assert.NotEqual(agreement.Terms[0].Id, published.Terms[0].Id);
                Assert.Equal("held-repair-coat", published.Terms[0].TargetId);
            }
            Assert.Single(world.Towns[0].NonLandHearings.Agreements);
            Assert.Empty(world.Towns[0].NonLandHearings.Effects);
            Assert.Equal(reservations, world.Society.Inventory.Reservations);
            Assert.Equal(2, world.Society.Inventory.GetLot("held-repair-cloth").Quantity);
            Assert.Null(world.Agents.Single(person => person.AgentId == actor).Equipment!.Repair);
            NonLandRuntimeFixture.Strict(world.ExportState());
        }
        finally { held.Release(); }

        async Task AdvanceUntilAsync(Func<bool> complete, int limit, Action<WorldStepResult>? observe = null)
        {
            for (var step = 0; step < limit && !complete(); step++)
            {
                var result = await world.AdvanceOneTickNonBlockingAsync().AsTask().WaitAsync(Deadline);
                Assert.True(result.Advanced);
                observe?.Invoke(result);
                foreach (var person in result.Events.Where(entry => entry.Kind == "hosted_decision_started").Select(entry => entry.Detail))
                {
                    if (person == NonLandRuntimeFixture.Judge && await held.Entered.Reader.ReadAsync().AsTask().WaitAsync(Deadline)) continue;
                    await TownLandHearingHeldReplyTests.WaitForModelCompletionAsync(world, person);
                }
            }
            Assert.True(complete(), "The native held-offer checkpoint was not reached within " + limit + " ticks.");
        }
    }

    private sealed class HeldOfferProvider(CognitionNonLandChoice payload) : IDecisionProvider
    {
        private readonly TaskCompletionSource<CognitionDecisionResponse> pending = new();
        private CognitionDecisionResponse? response;
        private bool released;
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;
        public Channel<bool> Entered { get; } = Channel.CreateUnbounded<bool>();
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var observation = request.Observation;
            var choice = !released ? observation.Candidates.FirstOrDefault(candidate => candidate.Id.Contains("|law_case_inspect|", StringComparison.Ordinal)) ??
                observation.Candidates.FirstOrDefault(candidate => candidate.Id.Contains("|remedy_offer|", StringComparison.Ordinal)) : null;
            choice ??= observation.Candidates.Single(candidate => candidate.Id == "safe_idle");
            var hold = !released && choice.Id.Contains("|remedy_offer|", StringComparison.Ordinal);
            var value = new CognitionDecisionResponse(request.RequestId, observation.AgentId, Kind, ProviderEpoch,
                observation.RunEpoch, observation.DecisionGeneration, observation.ObservationDigest, choice.Id, 1,
                observation.Candidates.ToDictionary(candidate => candidate.Id, candidate => candidate.Id == choice.Id ? 1d : 0d, StringComparer.Ordinal),
                CivicNonLandHearing: hold ? payload : null);
            Entered.Writer.TryWrite(hold);
            if (!hold) return ValueTask.FromResult(value);
            response = value;
            Started.TrySetResult(true);
            return new(pending.Task.WaitAsync(cancellationToken));
        }
        public void Release() { released = true; if (response is { } value) pending.TrySetResult(value); }
    }
}

using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class NonLandChildStatementTests
{
    [Fact]
    public async Task ABornChildCanSpeakForTheirOwnReportedActAndRequestItsReopening()
    {
        var state = NonLandRuntimeFixture.Prepared();
        var checkpoint = state.Society.Society;
        var household = checkpoint.Households.First(item => item.MemberIds.Count >= 2);
        var parents = household.MemberIds.Take(2).ToArray();
        checkpoint = SocietyRules.ProposeRelationship(checkpoint, new("case-child-partnership", 1,
            SocietyRelationshipType.Partnership, parents[0], parents[1], checkpoint.WorldTick)).Checkpoint;
        checkpoint = SocietyRules.AcceptRelationship(checkpoint, "case-child-partnership", 1, parents[1]).Checkpoint;
        checkpoint = ChosenBirthNameTestFixture.NameParent(checkpoint, parents[0]);
        checkpoint = checkpoint with { Inventory = InventoryAuthority.AddLot(checkpoint.Inventory, "case-birth-food", "food", household.Id, 4) };
        var birth = SocietyRules.CommitBirth(checkpoint, new($"family:{parents[0]}:{checkpoint.WorldTick}", 1,
            parents[0], parents[1], household.Id, household.MemberIds, parents, "case-birth-food", 4,
            checkpoint.WorldTick, ChildName: ChosenBirthNameTestFixture.ChildName(checkpoint, parents[0], "Áda"), PrimaryCaregiverId: parents[0]));
        var child = Assert.IsType<string>(birth.CreatedId);
        checkpoint = birth.Checkpoint;
        var age = Assert.IsType<SocietyDayLifecycle>(checkpoint.Config.DayLifecycle).ChildStartDay;
        var born = checkpoint.LifeTickAt(checkpoint.WorldTick) - age * checkpoint.Config.TicksPerLifecycleAge;
        checkpoint = checkpoint with
        {
            Agents = checkpoint.Agents.Select(person => person.Id == child ? person with
            { BirthTick = born, BirthLifeTick = checkpoint.LifeClock is null ? null : born, AgeBand = SocietyAgeBand.Child, LastLifecycleYearChecked = age } : person).ToArray()
        };
        var town = state.Towns![0];
        var board = town.OriginSite!.Value;
        var start = state.Agents.Single(person => person.AgentId == NonLandRuntimeFixture.Subject).Position;
        state = state with
        {
            Society = state.Society with { Society = checkpoint },
            Agents = state.Agents.Select(person => person.AgentId == parents[0] ? person with { Position = board } : person)
                .Append(new(child, start, 9_000, 0, "curious", "consider the public case")).ToArray(),
            Towns = [town with { ResidentIds = town.ResidentIds.Append(child).Order(StringComparer.Ordinal).ToArray() }]
        };
        var destination = state.Map.FootNeighbors(board).Where(point => state.Map.IsPassable(point) && state.Agents.All(person => person.Position != point))
            .OrderBy(point => state.Map.FootDistance(start, point)).First();
        var follower = new NonLandTestProvider
        {
            Choose = observation => observation.AgentId == child
                ? observation.Candidates.FirstOrDefault(candidate => candidate.Id == "move_to") : null
        };
        using (var walking = NonLandRuntimeFixture.Create(state, follower))
        {
            walking.SubmitInstruction(new("child-public-passage", "owner:test", child, OwnerInstructionKind.Order, $"Move to ({destination.X}, {destination.Y})"));
            await NonLandRuntimeFixture.UntilAsync(walking, () => walking.Agents.Single(person => person.AgentId == child).Position == destination &&
                walking.Towns[0].NonLandHearings.ConductRecords.Any(record => record.ActorId == child), 8);
            state = NonLandRuntimeFixture.Strict(walking.ExportState());
        }
        var reporter = NonLandRuntimeFixture.FilingProvider();
        using (var reporting = NonLandRuntimeFixture.Create(state, reporter))
        {
            NonLandRuntimeFixture.Wake(reporting, NonLandRuntimeFixture.Witness, "report-child-passage");
            await NonLandRuntimeFixture.UntilAsync(reporting, () => reporting.Towns[0].NonLandHearings.Cases.Any(item => item.Allegation.SubjectId == child), 8);
            state = NonLandRuntimeFixture.Strict(reporting.ExportState());
        }
        var speaker = new NonLandTestProvider
        {
            Choose = observation => observation.AgentId == child ? observation.Candidates.FirstOrDefault(candidate =>
                candidate.Id.Contains("|law_case_inspect|", StringComparison.Ordinal)) ?? observation.Candidates.FirstOrDefault(candidate =>
                candidate.Id.Contains("|law_case_statement|", StringComparison.Ordinal)) : null,
            Payload = (_, choice) => choice.Id.Contains("|law_case_statement|", StringComparison.Ordinal)
                ? new(Statement: "I walked toward my caregiver. I speak only for myself.") : null
        };
        using var world = NonLandRuntimeFixture.Create(state, speaker);
        NonLandRuntimeFixture.Wake(world, child, "child-personal-case-statement");
        var plans = new List<NonLandEvidenceActionPlan>();
        await NonLandRuntimeFixture.UntilAsync(world, () => world.Towns[0].NonLandHearings.Cases.Any(item =>
            item.Evidence.Any(evidence => evidence.SubmittedByAgentId == child && evidence.Acquisition == "statement")), 12, result =>
        {
            plans.AddRange(result.Decisions.Where(decision => decision.ActionPlan is NonLandEvidenceActionPlan && decision.Admission.Accepted && decision.PlanRefusal is null)
                .Select(decision => Assert.IsType<NonLandEvidenceActionPlan>(decision.ActionPlan)));
        });
        var statement = Assert.Single(plans, plan => plan.Choice.Verb == "law_case_statement");
        var actual = world.Towns[0].NonLandHearings.Cases.Single(item => item.Id == statement.CaseId);
        Assert.True(statement.Bound);
        Assert.Contains(actual.Evidence, evidence => evidence.SourceAgentId == child && evidence.Text == statement.Statement && evidence.Kind == "allegation");
        Assert.DoesNotContain(speaker.Observations.Where(observation => observation.AgentId == child).SelectMany(observation => observation.Candidates),
            candidate => candidate.Id.Contains("|law_case_answer|", StringComparison.Ordinal));
        Assert.Equal(parents[0], actual.Revisions[^1].Parties.Single(party => party.SubjectId == child).RespondingAdultId);
        state = NonLandRuntimeFixture.Strict(world.ExportState());
        var adults = state.Towns![0].ResidentIds.Where(id => LifeStageRules.IsAdult(state.Society.Society.GetAgent(id).AgeBand)).ToArray();
        var adjudicator = adults.First(id => id != NonLandRuntimeFixture.Witness && state.Society.Society.GetAgent(id).HouseholdId != household.Id);
        town = state.Towns[0];
        var tick = state.Society.Society.WorldTick;
        var (council, government) = TownGovernmentRules.RegisterMayor(town.Council!, town.Government!, adjudicator, "land+non_land", adults, tick);
        (council, government) = TownGovernmentRules.Advance(council, government, town.Id, town.Name, state.WorldSeed, adults, tick, NonLandRuntimeFixture.Day);
        var election = Assert.IsType<TownMayoralContest>(government.Contest);
        foreach (var voter in adults)
            government = TownGovernmentRules.VoteMayor(government, TownGovernmentRules.RoundToken(election), voter, adjudicator, tick);
        state = NonLandRuntimeFixture.Strict(state with { Towns = [town with { Council = council, Government = government }] });
        var judgeProvider = new NonLandTestProvider
        {
            Choose = observation => observation.AgentId == adjudicator ? observation.Candidates.FirstOrDefault(candidate =>
                candidate.Id.Contains("|visit|", StringComparison.Ordinal)) ?? observation.Candidates.FirstOrDefault(candidate =>
                candidate.Id.Contains("|read|", StringComparison.Ordinal)) ?? observation.Candidates.FirstOrDefault(candidate =>
                candidate.Id.Contains("|law_case_inspect|", StringComparison.Ordinal)) ?? observation.Candidates.FirstOrDefault(candidate =>
                candidate.Id.Contains("|law_case_find|", StringComparison.Ordinal) && candidate.Id.EndsWith("|unsupported", StringComparison.Ordinal)) : null,
            Payload = (_, choice) => choice.Id.Contains("|law_case_find|", StringComparison.Ordinal)
                ? new(Statement: "The record does not establish an adverse finding.", Uncertainty: "I do not infer a child's intent from this passage.") : null
        };
        using (var clock = NonLandRuntimeFixture.Create(state, new NonLandTestProvider()))
        {
            await NonLandRuntimeFixture.UntilAsync(clock, () => TownGovernmentRules.CurrentNonLandAuthority(clock.Towns[0].Government!, clock.WorldTick)?.HolderId == adjudicator,
                NonLandRuntimeFixture.Day + 3);
            state = NonLandRuntimeFixture.Strict(clock.ExportState());
        }
        using (var adjudicating = NonLandRuntimeFixture.Create(state, judgeProvider))
        {
            NonLandRuntimeFixture.Wake(adjudicating, adjudicator, "consider-child-case-with-current-mayor-authority");
            var attempts = 0;
            await NonLandRuntimeFixture.UntilAsync(adjudicating, () => adjudicating.Towns[0].NonLandHearings.Cases.Any(item => item.Id == actual.Id && item.Status == "settled"),
                12, result =>
                {
                    if (++attempts == 12)
                    {
                        var file = adjudicating.Towns[0].NonLandHearings.Cases.Single(item => item.Id == actual.Id);
                        Assert.True(file.Status == "settled", "Judge: " + file.Judge + "; read: " + TownNonLandRules.ReadCurrent(file, adjudicator, adjudicating.WorldTick) +
                            "; selected: " + string.Join(", ", judgeProvider.Selected.TakeLast(8)) + "; refusals: " + string.Join(", ", result.Events.Where(item => item.Kind == "choice_refused").Select(item => item.Detail)));
                    }
                });
            state = NonLandRuntimeFixture.Strict(adjudicating.ExportState());
        }
        var requesting = new NonLandTestProvider
        {
            Choose = observation => observation.AgentId == child ? observation.Candidates.FirstOrDefault(candidate =>
                candidate.Id.Contains("|law_case_inspect|", StringComparison.Ordinal)) ?? observation.Candidates.FirstOrDefault(candidate =>
                candidate.Id.Contains("|law_case_reopen|", StringComparison.Ordinal) && candidate.Id.EndsWith("|procedural_error", StringComparison.Ordinal)) : null,
            Payload = (_, choice) => choice.Id.Contains("|law_case_reopen|", StringComparison.Ordinal) ? new(
                Grounds: "I personally ask for an independent assessment of the hearing procedure.",
                EvidenceIds: state.Towns![0].NonLandHearings.Cases.Single(item => item.Id == actual.Id).Evidence.Take(1).Select(evidence => evidence.Id).ToArray()) : null
        };
        using var reopening = NonLandRuntimeFixture.Create(state, requesting);
        NonLandRuntimeFixture.Wake(reopening, child, "child-personal-reopening-request");
        NonLandAdjudicationActionPlan? requestPlan = null;
        await NonLandRuntimeFixture.UntilAsync(reopening, () => reopening.Towns[0].NonLandHearings.Cases.Any(item => item.Id == actual.Id && item.ReopenRequests.Count > 0), 12, result =>
        {
            requestPlan = result.Decisions.Where(decision => decision.ActionPlan is NonLandAdjudicationActionPlan && decision.Admission.Accepted && decision.PlanRefusal is null)
                .Select(decision => Assert.IsType<NonLandAdjudicationActionPlan>(decision.ActionPlan)).LastOrDefault() ?? requestPlan;
        });
        var reopened = reopening.Towns[0].NonLandHearings.Cases.Single(item => item.Id == actual.Id);
        var request = Assert.Single(reopened.ReopenRequests);
        Assert.Equal((child, "pending", "settled"), (request.AgentId, request.Status, reopened.Status));
        Assert.NotNull(requestPlan);
        Assert.True(requestPlan.Bound);
        Assert.Equal((request.AgentId, request.Reasons), (requestPlan.Actor, requestPlan.Reasons));
        Assert.Equal(ActionRefusalReason.TargetUnavailable, reopening.CheckActionPlan(requestPlan).Refusal!.Reason);
        NonLandRuntimeFixture.Strict(reopening.ExportState());
    }
}

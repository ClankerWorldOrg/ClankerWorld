using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class NonLandChildStatementTests
{
    [Fact]
    public async Task ABornChildCanSpeakForTheirOwnReportedActWithoutAnsweringForAnotherPerson()
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
        var follower = new NonLandTestProvider
        {
            Choose = observation => observation.AgentId == child
                ? observation.Candidates.FirstOrDefault(candidate => candidate.Id.StartsWith(GuardianTravelChoice.FollowPrefix, StringComparison.Ordinal)) : null
        };
        using (var walking = NonLandRuntimeFixture.Create(state, follower))
        {
            await NonLandRuntimeFixture.UntilAsync(walking, () => walking.Towns[0].NonLandHearings.ConductRecords.Any(record => record.ActorId == child), 8);
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
        NonLandRuntimeFixture.Strict(world.ExportState());
    }
}

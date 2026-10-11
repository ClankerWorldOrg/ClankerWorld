using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.Playtest;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.Tests;

public sealed partial class GrownAgentHelperMemoryTests
{
    [Theory]
    [InlineData("council-proposal")]
    [InlineData("project-request")]
    [InlineData("project")]
    [InlineData("building")]
    [InlineData("delivery")]
    [InlineData("market")]
    [InlineData("starter-stall")]
    [InlineData("grant-source")]
    [InlineData("grant-right")]
    [InlineData("grant-request")]
    [InlineData("boat-access")]
    [InlineData("town-title")]
    [InlineData("household-use")]
    [InlineData("judge-round")]
    [InlineData("law")]
    [InlineData("law-amend-request")]
    [InlineData("law-repeal-request")]
    [InlineData("market-stock")]
    [InlineData("market-offer")]
    [InlineData("claim-title")]
    [InlineData("claim-request")]
    [InlineData("land-case-key")]
    [InlineData("land-revision")]
    [InlineData("property-record")]
    [InlineData("transfer-terms")]
    [InlineData("transfer-payment")]
    [InlineData("government-vote")]
    [InlineData("mayoral-round")]
    [InlineData("nonviolent-case-key")]
    [InlineData("nonviolent-response")]
    [InlineData("nonviolent-round")]
    [InlineData("judge-contest")]
    [InlineData("non-land-grant")]
    [InlineData("law-record")]
    [InlineData("writing-receipt")]
    [InlineData("marriage")]
    [InlineData("surname-conversation")]
    public async Task NativeDescendantCivicGeneratorInventoryKeepsBoundedRecallKeysAfterStrictReload(string generator)
    {
        var state = PrivateWorldRuntimeCodec.Decode(await NativeFiftyMilkTrace.Value);
        var births = state.Society.Society.Births.OrderBy(birth => birth.CommittedTick).ToArray();
        Assert.Equal(3, births.Length);
        var actor = births[^1].ChildId;
        var relative = births[^2].ChildId;
        Assert.Contains(state.Society.Society.Relationships, edge => edge.Type == SocietyRelationshipType.BiologicalParentage &&
            edge.ProposerId == relative && edge.TargetId == actor);
        var home = state.Society.Society.GetInhabitant(actor).HouseholdId!;
        var householdName = "Household " + new string('h', 118);
        var society = state.Society.Society with
        {
            Households = state.Society.Society.Households.Select(household =>
            household.Id == home ? household with { Name = householdName } : household).ToArray()
        };
        // A controlled longest legal standalone Town key exercises public generators. It is
        // not a synthetic descendant: the people and parentage come from three native births.
        var firstTown = state.Towns![0];
        var definitions = state.WorldContent!.Buildings.ToDictionary(building => building.CanonicalId, StringComparer.Ordinal);
        var occupied = state.Towns.SelectMany(record => record.BorderTiles)
            .Concat(state.Map.Resources.Select(resource => resource.Position))
            .Concat(state.Map.CampObjects.Select(item => item.Position))
            .Concat(state.WorldSimulation!.Buildings.SelectMany(building =>
                WorldContentSimulationRules.Footprint(definitions[building.DefinitionId], building)))
            .Concat((state.Fields ?? []).Select(field => field.Position))
            .Concat((state.HouseholdLandUseRights ?? []).SelectMany(right => right.Tiles)).ToHashSet();
        var origin = state.Map.Tiles.Select(tile => tile.Position).First(point =>
            MarketContent.SiteTiles(point).All(site => state.Map.Contains(site) && state.Map.IsBuildable(site) && !occupied.Contains(site)) &&
            state.Map.IsReachableOnFoot(firstTown.OriginSite!.Value, point));
        var town = new TownRuntimeState("town:" + new string('t', 123), "Town " + new string('n', 115), "founded",
            society.WorldTick, new[] { actor, relative }.Order(StringComparer.Ordinal).ToArray(), [],
            TownLandRightsRules.OrderTiles(MarketContent.SiteTiles(origin)), origin,
            TownGovernanceState.Create([actor, relative]), TownGovernmentState.Create());
        var definition = MarketContent.Hall2x2();
        var plan = new TownProjectPayload(new string('b', TownProjectRules.MaximumNameLength), definition.CanonicalId,
            town.OriginSite!.Value, MarketContent.HallEntrance(town.OriginSite.Value), definition.BuildCosts);
        var council = TownGovernanceState.Create([actor, relative]) with { Sequence = 49 };
        council = TownGovernanceRules.SubmitProposal(council, town.Id, actor, "project", null, "Build our market.",
            "The resident household needs a market.", [actor, relative], society.WorldTick, state.WorldSystems!.Config.TicksPerDay, project: plan);
        var proposal = Assert.Single(council.Proposals);
        Assert.Equal(plan, proposal.Project);
        string? lawHandle = null;
        if (generator is "law" or "law-amend-request" or "law-repeal-request" or "law-record")
        {
            var government = TownGovernmentState.Create();
            (council, government) = TownLawRules.ProposeAdoption(council, government, town.Id, actor,
                "Shared work: Respect private stock.", TownLawRules.Jurisdiction, [], [actor, relative],
                society.WorldTick, state.WorldSystems!.Config.TicksPerDay);
            var adoption = council.Proposals[^1];
            council = TownGovernanceRules.VoteProposal(council, adoption.Id, actor, true, society.WorldTick);
            council = TownGovernanceRules.VoteProposal(council, adoption.Id, relative, true, society.WorldTick);
            (council, government) = TownLawRules.Enact(council, government, town.Id, town.Name, society.WorldTick);
            var law = Assert.Single(government.Laws);
            lawHandle = law.Id;
            if (generator == "law-record") lawHandle = TownLandHearingRules.LawRecordId(law.Id, TownLawRules.Current(law).Version);
            if (generator == "law-amend-request")
            {
                (council, government) = TownLawRules.ProposeAmendment(council, government, town.Id, actor, law.Id,
                    "Shared work: " + new string('a', 150), [actor, relative], society.WorldTick, state.WorldSystems.Config.TicksPerDay);
                lawHandle = council.Proposals[^1].RequestKey;
            }
            else if (generator == "law-repeal-request")
            {
                (council, government) = TownLawRules.ProposeRepeal(council, government, town.Id, actor, law.Id,
                    [actor, relative], society.WorldTick, state.WorldSystems.Config.TicksPerDay);
                lawHandle = council.Proposals[^1].RequestKey;
            }
            town = town with { Government = government };
        }
        town = town with { Governance = council };
        state = state with
        {
            Towns = state.Towns.Select(record =>
            {
                var residents = record.ResidentIds.Except(new[] { actor, relative }, StringComparer.Ordinal).ToArray();
                var adults = residents.Where(id => society.GetInhabitant(id).AgeBand is SocietyAgeBand.Adult or SocietyAgeBand.Elder);
                return record with { ResidentIds = residents, Governance = TownGovernanceState.Create(adults) };
            }).Append(town).ToArray(),
            TownLandTitles = (state.TownLandTitles ?? []).Concat(TownLandRightsRules.InitialTitles(state.Map, town, society.WorldTick))
                .OrderBy(title => title.Id, StringComparer.Ordinal).ToArray(),
        };
        var project = TownProjectRules.ProjectId(proposal);
        var sourceLot = Assert.Single(society.Inventory.Lots, lot => lot.ItemKind == "milk").Id;
        var request = new HouseholdLandUseRequest(proposal.Id, town.Id, home, actor, [town.OriginSite.Value], society.WorldTick);
        var contest = new TownLandCaseJudgeContest(proposal.Id, "voting", int.MaxValue, society.WorldTick,
            society.WorldTick, society.WorldTick + 1, [actor, relative], [actor, relative], [], [], int.MaxValue, []);
        var landCase = new TownLandCase(proposal.Id, "key", "request", society.WorldTick, "open",
            [new(int.MaxValue, town.BorderTiles, [], [], "notice", society.WorldTick, society.WorldTick + 1, new("grant", home))],
            [], [], [], [], null, [], [], null, [], [])
        { TownId = town.Id };
        var transfer = new TownLandTransferRequest(proposal.Id, town.Id, actor, home, town.BorderTiles,
            [], [], "notice", society.WorldTick, []);
        var change = new TownGovernmentChange(proposal.Id, "key", "arrangement", town.Government!.Arrangement,
            actor, "Our household needs a vote.", society.WorldTick, "voting", society.WorldTick,
            society.WorldTick + 1, [actor, relative], []);
        var mayoral = new TownMayoralContest(proposal.Id, "vacancy", "ordinary", null, "voting", int.MaxValue,
            society.WorldTick, society.WorldTick, society.WorldTick + 1, [actor, relative], [actor, relative], [], [], int.MaxValue);
        var allegation = new TownViolationAllegation(sourceLot, actor, "taking", town.OriginSite.Value,
            society.WorldTick, proposal.Id, int.MaxValue, "The stock was moved.", [sourceLot]);
        var ids = generator switch
        {
            "council-proposal" => new[] { proposal.Id },
            "project-request" => [TownProjectRules.RequestKey(plan)],
            "project" => [project],
            "building" => [TownProjectRules.BuildingId(project)],
            "delivery" => [TownProjectRules.DeliveryId(project, actor, sourceLot, society.WorldTick, int.MaxValue)],
            "market" => [MarketContent.MarketId(project)],
            "starter-stall" => [MarketContent.StarterStallBuildingId(project, int.MaxValue)],
            "grant-source" => [HouseholdLandGrantRules.GrantSource(request.Id)],
            "grant-right" => [HouseholdLandGrantRules.RightId(request.Id)],
            "grant-request" => [HouseholdLandGrantRules.RequestKey(request)],
            "boat-access" => [TownBoatAccessRules.RequestKey(new(actor))],
            "town-title" => TownLandRightsRules.InitialTitles(state.Map, town, society.WorldTick).Select(title => title.Id).ToArray(),
            "household-use" => TownLandRightsRules.InitialUseRights(state.Map, town.Id,
                [(home, town.BorderTiles.AsEnumerable())], society.WorldTick).Select(right => right.Id).ToArray(),
            "judge-round" => [TownLandCaseJudgeRules.RoundToken(contest)],
            "law" or "law-amend-request" or "law-repeal-request" or "law-record" => [lawHandle!],
            "market-stock" => [MarketTradeRules.ReceiptId(project, sourceLot, actor, int.MaxValue, society.WorldTick, long.MaxValue, proposal.Id)],
            "market-offer" => [MarketTradeRules.Identity("market-offer:", project, actor, relative, sourceLot, proposal.Id)],
            "claim-title" => [TownLandClaimRules.TitleId(proposal.Id)],
            "claim-request" => [TownLandClaimRules.RequestKey(town.BorderTiles)],
            "land-case-key" => [TownLandHearingRules.CaseKey(town.Id, "request", town.BorderTiles)],
            "land-revision" => [TownLandHearingRules.RevisionToken(landCase)],
            "property-record" => [TownPropertyRules.RecordId(landCase, int.MaxValue)],
            "transfer-terms" => [TownLandTransferRules.TermsToken(transfer)],
            "transfer-payment" => [TownLandTransferRules.PaymentTransferId(transfer, actor, int.MaxValue)],
            "government-vote" => [TownGovernmentRules.VoteNoticeToken(change)],
            "mayoral-round" => [TownGovernmentRules.RoundToken(mayoral)],
            "nonviolent-case-key" => [TownNonviolentRules.CaseKey(town.Id, allegation)],
            "nonviolent-response" => [TownNonviolentRules.ResponseToken(new(int.MaxValue, proposal.Id, actor, relative,
                "response", "The descendant household remembers this stock.", society.WorldTick))],
            "nonviolent-round" => [TownCaseJudgeRules.RoundToken(new(contest.Id, contest.Stage, contest.Round,
                contest.OpenedTick, contest.RoundOpenedTick, contest.RoundDeadlineTick, contest.Voters, contest.Candidates,
                contest.Ballots, contest.TiedCandidates, contest.Interruptions, contest.Rounds))],
            "judge-contest" => [TownHearingProcedure.JudgeContestId(proposal.Id, int.MaxValue)],
            "non-land-grant" => [TownGovernmentRules.NonLandGrantId(change.Id)],
            "writing-receipt" => [AgentKnowledgeRules.WritingReceiptId("knowledge-artifact-000512")],
            "marriage" => [AgentMarriageRules.Id(proposal.Id)],
            "surname-conversation" => [AgentMarriageRules.SurnameConversationId(AgentMarriageRules.Id(proposal.Id))],
            _ => throw new ArgumentOutOfRangeException(nameof(generator)),
        };
        Assert.NotEmpty(ids);
        foreach (var id in ids)
        {
            Assert.InRange(id.Length, 1, DerivedId.MaximumLength);
            var summary = "I remember civic" + generator.Replace("-", "", StringComparison.Ordinal) + " work.";
            var checkpoint = SocietyFixture.RecordSocialMemory(society, new(id, actor, relative, summary,
                "private", society.WorldTick)).Checkpoint;
            var bytes = PrivateWorldRuntimeCodec.Encode(state with { Society = state.Society with { Society = checkpoint } });
            using var restored = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(bytes));
            Assert.Equal(bytes, PrivateWorldRuntimeCodec.Encode(restored.ExportState()));
            var candidates = new CognitionCandidate[] { new("safe_idle", summary, 0) };
            var recalled = PrivateWorldMemoryRetrieval.Retrieve(restored.Society.Memories, restored.Society.Beliefs ?? [],
                null, actor, restored.WorldTick, candidates, restored.Society);
            var memory = Assert.Single(recalled, memory => memory.Id == id);
            Assert.Equal((actor, relative, summary), (memory.OwnerId, memory.SubjectId, memory.Summary));
            new InhabitantObservation(actor, restored.WorldTick, 0, 1, "sha256:civic-generator", 0,
                candidates, RetrievedMemories: recalled).Validate();
            Assert.Equal(householdName, restored.Society.GetHousehold(home).Name);
            Assert.Equal(town.Name, restored.Towns.Single(record => record.Id == town.Id).Name);
            Assert.Equal(plan.Name, Assert.Single(restored.Towns.Single(record => record.Id == town.Id).Governance!.Proposals, proposal => proposal.Kind == "project").Project!.Name);
        }
    }
}

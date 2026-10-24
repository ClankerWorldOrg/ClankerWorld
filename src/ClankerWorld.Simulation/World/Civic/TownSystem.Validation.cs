using System.Globalization;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    private void Commit(CivicState next)
    {
        if (SystemInvariantChecks.Enabled)
        {
            ValidateLocalTowns(next.Towns);
            ValidateLocalFounderSetup(next.FounderSetup);
            ValidateLocalFoodStewardship(next.FoodStewardship);
            ValidateLocalLandRecords(next.TownLandTitles, next.HouseholdLandUseRights, next.HouseholdLandUseRequests);
        }
        State = next;
    }

    internal static void ValidateLocalTowns(IReadOnlyList<TownRuntimeState> towns)
    {
        if (towns.Any(town => town is null) || towns.Select(town => town.Id).Distinct(StringComparer.Ordinal).Count() != towns.Count)
            throw new InvalidDataException("A founder-setup world must keep its original first Town and unique Town identities.");
        foreach (var town in towns)
        {
            TownLandHearingValidation.ValidateLocal(town.Id, town.LandRecords);
            TownNonLandValidation.ValidateLocal(town.Id, town.NonLandHearings);
            ValidateLocalAdmissions(town);
            ValidateLocalProjects(town.Projects);
            if (town.Council is { } council) ValidateLocalCouncil(town.Id, council);
            if (town.Government is { } government) TownGovernmentStateValidation.ValidateLocal(town.Id, government, allowIntermediateVotes: true);
            if (!IdentityRules.Reference(town.Id, DerivedId.MaximumLength, requireTrimmed: true) ||
                string.IsNullOrWhiteSpace(town.Name) || town.Name != town.Name.Trim() || !BoundedText.IsWithinLimit(town.Name, 120) || town.FoundedTick < 0 ||
                town.ResidentIds is null || town.AssignedBuildingIds is null || town.BorderTiles is null ||
                town.FirstBuildingCompletedTick is { } completed && completed < town.FoundedTick ||
                town.AbandonedSinceTick is { } abandoned && (!town.IsAbandoned || abandoned < town.FoundedTick) ||
                town.AssignedBuildingIds.Count > 0 && town.FirstBuildingCompletedTick is null ||
                town.ResidentIds.Distinct(StringComparer.Ordinal).Count() != town.ResidentIds.Count ||
                town.AssignedBuildingIds.Distinct(StringComparer.Ordinal).Count() != town.AssignedBuildingIds.Count ||
                town.BorderTiles.Distinct().Count() != town.BorderTiles.Count || town.BorderTiles.Count == 0)
                throw new InvalidDataException("A Town identity, founding record, membership, or footprint is invalid.");
            if (town.Id == TownBorderRules.FirstTownId)
            {
                if (town.Name != TownBorderRules.FirstTownName || town.FoundedTick != 0)
                    throw new InvalidDataException("The original first Town's identity and founding history are invalid.");
            }
            else if (town.FoundingState != "founded")
                throw new InvalidDataException("An additional recorded Town must have a completed founding record.");
            if (town.OriginSite is { } site && !town.BorderTiles.Contains(site))
                throw new InvalidDataException("The saved Town border does not cover its founding site.");
        }
    }

    internal static void ValidateLocalFounderSetup(FounderSetupState? setup)
    {
        if (setup is null) return;
        if (setup.FounderIds is null || setup.FounderIds.Count > WorldRuntime.RequiredFounders ||
            setup.FounderIds.Distinct(StringComparer.Ordinal).Count() != setup.FounderIds.Count || setup.FounderIds.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("Founder setup references invalid or duplicate agents.");
        if (setup.Started && setup.FounderIds.Count != WorldRuntime.RequiredFounders)
            throw new InvalidDataException("A started world requires four configured founders.");
    }

    internal static void ValidateLocalFoodStewardship(FoodStewardship? saved)
    {
        if (saved is null) return;
        if (saved.FoodPolicy is not ("open" or "essential_first") || saved.LastResolutionTick < 0)
            throw new InvalidDataException("The saved household council is invalid.");
        if (saved.Ballot is { } ballot && (ballot.Policy is not ("open" or "essential_first") || ballot.Policy == saved.FoodPolicy ||
            ballot.Electorate is null || ballot.Approvals is null || ballot.Rejections is null || ballot.ProposedTick < 0 ||
            ballot.ExpiryTick < ballot.ProposedTick || ballot.ExpiryTick - ballot.ProposedTick != 120 || ballot.Electorate.Count == 0 ||
            ballot.Electorate.Distinct(StringComparer.Ordinal).Count() != ballot.Electorate.Count ||
            ballot.Approvals.Concat(ballot.Rejections).Distinct(StringComparer.Ordinal).Count() != ballot.Approvals.Count + ballot.Rejections.Count ||
            ballot.Approvals.Concat(ballot.Rejections).Any(id => !ballot.Electorate.Contains(id, StringComparer.Ordinal))))
            throw new InvalidDataException("The saved household ballot is invalid.");
    }

    internal static bool IsLocallyValidPlot([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] IReadOnlyList<GridPoint>? tiles, long recordedTick, long? agreedEndTick = null) =>
        tiles is { Count: > 0 } && tiles.Distinct().Count() == tiles.Count &&
        tiles.SequenceEqual(TownLandRightsRules.OrderTiles(tiles)) && recordedTick >= 0 &&
        (agreedEndTick is null || agreedEndTick >= recordedTick);

    internal static void ValidateLocalLandRecords(
        [System.Diagnostics.CodeAnalysis.NotNull] IReadOnlyList<TownLandTitleRecord>? titles,
        [System.Diagnostics.CodeAnalysis.NotNull] IReadOnlyList<HouseholdLandUseRight>? rights,
        [System.Diagnostics.CodeAnalysis.NotNull] IReadOnlyList<HouseholdLandUseRequest>? requests)
    {
        if (titles is null || rights is null || requests is null || titles.Any(item => item is null) || rights.Any(item => item is null) || requests.Any(item => item is null))
            throw new InvalidDataException("Saved Town land titles, use rights and requests must be present and cannot contain null entries.");
        if (titles.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != titles.Count ||
            rights.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != rights.Count ||
            requests.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != requests.Count)
            throw new InvalidDataException("Saved Town land record identities must be unique within each record type.");
        foreach (var title in titles)
            if (!IdentityRules.Reference(title.Id, DerivedId.MaximumLength, requireTrimmed: true) || !IdentityRules.Reference(title.TownId, DerivedId.MaximumLength, requireTrimmed: true) || !IsLocallyValidPlot(title.Tiles, title.RecordedTick))
                throw new InvalidDataException("A saved Town title is invalid or outside its recorded Town border.");
        foreach (var right in rights)
            if (!IdentityRules.Reference(right.Id, DerivedId.MaximumLength, requireTrimmed: true) || !IdentityRules.Reference(right.TownId, DerivedId.MaximumLength, requireTrimmed: true) || !IdentityRules.Reference(right.HouseholdId, int.MaxValue, requireTrimmed: true) ||
                !BoundedText.IsValid(right.GrantSource, 64, requireTrimmed: true) || !IsLocallyValidPlot(right.Tiles, right.GrantedTick, right.AgreedEndTick))
                throw new InvalidDataException("A saved household land-use right is invalid or not covered by Town title.");
        foreach (var request in requests)
        {
            ValidateLocalLandRequest(request);
            if (!IdentityRules.Reference(request.Id, DerivedId.MaximumLength, requireTrimmed: true) || !IdentityRules.Reference(request.TownId, DerivedId.MaximumLength, requireTrimmed: true) || !IdentityRules.Reference(request.HouseholdId, int.MaxValue, requireTrimmed: true) ||
                !IdentityRules.Reference(request.RequestedByAgentId, int.MaxValue, requireTrimmed: true) || request.Tiles is not { Count: <= ClankerWorld.Simulation.Cognition.CognitionDecisionResponse.MaximumCivicLandTiles } ||
                !IsLocallyValidPlot(request.Tiles, request.RequestedTick, request.AgreedEndTick))
                throw new InvalidDataException("A saved household land-use request is invalid or not covered by Town title.");
        }
    }
    internal static bool UniqueCouncilIds(IEnumerable<string> ids) => ids.All(id => !string.IsNullOrWhiteSpace(id)) && ids.Distinct(StringComparer.Ordinal).Count() == ids.Count();

    internal static void ValidateLocalCouncil(string townId, TownCouncilState state)
    {
        if (state.Members is null || state.Candidates is null || state.Proposals is null ||
            state.ElectionHistory is null || state.Notices is null || state.Knowledge is null ||
            state.Form is not ("all_adult" or "representative" or "leader") || state.Fallback is not ("initial" or "none" or "demographic" or "candidates" or "arrangement") ||
            state.ArchivedElectionCount < 0 || state.ArchivedNoticeCount < 0 ||
            state.ArchivedNoticeCount > long.MaxValue - state.Notices.Count ||
            state.ArchivedElectionCount > state.Sequence ||
            state.Revision < 0 || state.Sequence < 0 || state.RetryTick < 0 || state.RetryCircumstances is null ||
            !BoundedText.IsWithinLimit(state.RetryCircumstances, 64) || !UniqueCouncilIds(state.Members) ||
            state.Form == "representative" && (state.Members.Count > TownCouncilRules.Seats || state.TermEndTick is null) ||
            state.Form == "leader" && (state.Members.Count != 1 || state.TermEndTick is not null) ||
            state.TermEndTick is < 0 || state.Candidates.Any(c => c is null ||
                !c.FullTerm && c.RemainderTermEndTick is null || c.RemainderTermEndTick is < 0) ||
            !UniqueCouncilIds(state.Candidates.Select(c => c.AgentId)) || state.Proposals.Any(p => p is null) ||
            !UniqueCouncilIds(state.Proposals.Select(p => p.Id)) || state.ElectionHistory.Any(e => e is null) ||
            !UniqueCouncilIds(state.ElectionHistory.Select(e => e.Id).Concat(state.Election is { } live ? [live.Id] : [])) ||
            state.Notices.Any(n => n is null) || !UniqueCouncilIds(state.Notices.Select(n => n.Id)) ||
            state.Knowledge.Any(k => k is null) ||
            state.Knowledge.Select(k => (k.AgentId, k.NoticeId)).Distinct().Count() != state.Knowledge.Count)
            throw new InvalidDataException("A Town's saved council, candidates or civic ledger is invalid.");
        foreach (var proposal in state.Proposals)
        {
            if (!DerivedId.MatchesNumbered(proposal.Id, townId + ":proposal:", state.Sequence) ||
                string.IsNullOrWhiteSpace(proposal.RequestKey) || proposal.Kind is not ("law" or "admission" or "land_claim" or "land_use" or "land_hearing" or "project" or "law_case") ||
                proposal.Kind == "land_use" && string.IsNullOrWhiteSpace(proposal.SubjectId) ||
                proposal.Kind == "admission" && (proposal.SubjectId is null) ||
                proposal.Kind is "law" or "land_claim" or "land_hearing" or "project" or "law_case" && proposal.SubjectId is not null ||
                proposal.Kind == "land_hearing" && (!TownLandGovernmentFilingRules.IsValid(proposal.LandHearingRequest, proposal.OpenedTick) ||
                    proposal.RequestKey != TownLandGovernmentFilingRules.RequestKey(proposal.LandHearingRequest!)) ||
                proposal.Kind != "land_hearing" && proposal.LandHearingRequest is not null ||
                proposal.Kind == "law_case" && (!TownNonLandGovernmentFilingRules.IsValid(proposal.NonLandRequest, proposal.OpenedTick) ||
                    proposal.RequestKey != TownNonLandGovernmentFilingRules.RequestKey(proposal.NonLandRequest!)) ||
                proposal.Kind != "law_case" && proposal.NonLandRequest is not null ||
                proposal.Kind == "land_claim" && proposal.LandClaimTiles is not { Count: > 0 } ||
                proposal.Kind != "land_claim" && proposal.LandClaimTiles is not null ||
                !BoundedText.IsValid(proposal.Text, TownCouncilRules.MaximumProposalText) ||
                proposal.Circumstances is null || !BoundedText.IsWithinLimit(proposal.Circumstances, 512) || proposal.CouncilRevision < 0 || proposal.CouncilRevision > state.Revision ||
                proposal.OpenedTick < 0 || proposal.DeadlineTick <= proposal.OpenedTick ||
                proposal.Voters is null || proposal.Votes is null || !UniqueCouncilIds(proposal.Voters) ||
                proposal.RequiredYes < 1 || proposal.RequiredYes > Math.Max(2, proposal.Voters.Count) ||
                // Zero/one-voter histories can come from either governing
                // form; other rosters determine the threshold unambiguously.
                proposal.Voters.Count >= 2 && proposal.RequiredYes != proposal.Voters.Count / 2 + 1 ||
                proposal.Votes.Any(v => v is null || !proposal.Voters.Contains(v.AgentId, StringComparer.Ordinal) ||
                    proposal.Kind == "admission" && v.AgentId == proposal.SubjectId) || !UniqueCouncilIds(proposal.Votes.Select(v => v.AgentId)) ||
                proposal.Status is not ("pending" or "passed" or "rejected" or "cancelled" or "withdrawn") ||
                proposal.Status == "pending" && (proposal.SettledTick is not null ||
                    proposal.CouncilRevision != state.Revision || !proposal.Voters.SequenceEqual(state.Members) ||
                    proposal.RequiredYes != (state.Form == "representative" ? 2 : state.Members.Count / 2 + 1) ||
                    proposal.Votes.Count(v => v.Yes) >= proposal.RequiredYes) ||
                proposal.Status != "pending" && (proposal.SettledTick is null || proposal.SettledTick < proposal.OpenedTick) ||
                proposal.Status == "passed" && proposal.Votes.Count(v => v.Yes) < proposal.RequiredYes ||
                proposal.Status == "rejected" && proposal.Votes.Count(v => v.Yes) >= proposal.RequiredYes)
                throw new InvalidDataException("A Town's saved proposal or final votes are invalid.");
            if (proposal.Kind == "project")
            {
                TownProjectRules.ValidatePayload(proposal.Project);
                if (proposal.RequestKey != TownProjectRules.RequestKey(proposal.Project!) ||
                    proposal.Text != TownProjectRules.ProposalText(proposal.Project!))
                    throw new InvalidDataException("A Town's construction proposal differs from its named site and budget.");
            }
            else if (proposal.Project is not null)
                throw new InvalidDataException("A law or admission proposal cannot contain construction authority.");
        }
        if (state.Proposals.Where(p => p.Status == "pending").GroupBy(p => p.RequestKey, StringComparer.Ordinal).Any(g => g.Count() > 1))
            throw new InvalidDataException("Equivalent pending Town proposals must share a single window.");
        foreach (var election in state.ElectionHistory.Concat(state.Election is { } current ? [current] : []))
        {
            var active = state.Election?.Id == election.Id;
            if (!DerivedId.MatchesNumbered(election.Id, townId + ":election:", state.Sequence) ||
                election.Kind is not ("initial" or "regular" or "replacement") ||
                election.Stage is not ("main" or "runoff" or "ready" or "completed" or "failed" or "cancelled") ||
                active != (election.Stage is "main" or "runoff" or "ready") || election.OpenedTick < 0 ||
                election.DeadlineTick <= election.OpenedTick || election.TermEndTick < 0 || election.Seats is < 1 or > TownCouncilRules.Seats ||
                election.Voters is null || election.Candidates is null || election.Ballots is null || election.Supported is null ||
                election.SettledSeats is null || election.DrawOrder is null || !UniqueCouncilIds(election.Voters) || !UniqueCouncilIds(election.Candidates) ||
                !UniqueCouncilIds(election.Supported) || !UniqueCouncilIds(election.SettledSeats) || !UniqueCouncilIds(election.DrawOrder) ||
                election.Ballots.Any(b => b is null || b.Choices is null || !election.Voters.Contains(b.AgentId, StringComparer.Ordinal) ||
                    b.Choices.Count > election.Seats || !UniqueCouncilIds(b.Choices) || b.Choices.Any(id => !election.Candidates.Contains(id, StringComparer.Ordinal))) ||
                !UniqueCouncilIds(election.Ballots.Select(b => b.AgentId)) ||
                election.SettledSeats.Any(id => !election.Supported.Contains(id, StringComparer.Ordinal)) ||
                election.DrawOrder.Any(id => !election.Supported.Contains(id, StringComparer.Ordinal)) ||
                active && (election.Stage == "ready" && election.SettledSeats.Count != TownCouncilRules.Seats ||
                    election.Candidates.Concat(election.SettledSeats).Any(id => !state.Candidates.Any(c => c.AgentId == id &&
                        (c.FullTerm || election.Kind == "replacement" && c.RemainderTermEndTick == election.TermEndTick)))))
                throw new InvalidDataException("A Town's saved election, consent, ballots or recorded draw is invalid.");
        }
        long previousNotice = 0;
        for (var index = 0; index < state.Notices.Count; index++)
        {
            var notice = state.Notices[index];
            if (!notice.Id.StartsWith("notice:", StringComparison.Ordinal) ||
                !long.TryParse(notice.Id.AsSpan(7), NumberStyles.None, CultureInfo.InvariantCulture, out var number) ||
                number <= previousNotice || number > state.ArchivedNoticeCount + state.Notices.Count ||
                notice.Id != "notice:" + number.ToString(CultureInfo.InvariantCulture) || string.IsNullOrWhiteSpace(notice.SubjectId) ||
                notice.Kind is not ("council" or "candidate" or "nomination" or "election" or "runoff" or "result" or "proposal" or "cancelled" or
                    "law" or "government" or "mayor" or "land_use" or "land_hearing" or "land_transfer" or "law_case" or "remedy" or "nonviolent_hearing") ||
                string.IsNullOrWhiteSpace(notice.Text) || !BoundedText.IsWithinLimit(notice.Text, 32768) || notice.PostedTick < 0)
                throw new InvalidDataException("A saved Town civic notice is invalid.");
            previousNotice = number;
        }
        if (previousNotice != state.ArchivedNoticeCount + state.Notices.Count)
            throw new InvalidDataException("The saved civic notice sequence is incomplete.");
        ValidateLocalCivicKnowledge(state);
    }

    private static void ValidateLocalCivicKnowledge(TownCouncilState state)
    {
        var notices = state.Notices.ToDictionary(notice => notice.Id, StringComparer.Ordinal);
        var receipts = state.Knowledge.ToDictionary(receipt => (receipt.AgentId, receipt.NoticeId));
        foreach (var receipt in state.Knowledge)
            if (string.IsNullOrWhiteSpace(receipt.NoticeId) ||
                !notices.TryGetValue(receipt.NoticeId, out var notice) || notice.PostedTick > receipt.LearnedTick ||
                receipt.LearnedTick < 0 || receipt.SourceAgentId is { } source &&
                    (source == receipt.AgentId ||
                     !receipts.TryGetValue((source, receipt.NoticeId), out var sourceReceipt) ||
                     sourceReceipt.LearnedTick > receipt.LearnedTick))
                throw new InvalidDataException("Saved civic knowledge requires an actual notice or informed relay.");

        // Same-tick relays are valid, but every chain must reach a direct read.
        // Each receipt enters one path at most once; indexed lookups keep long
        // saved ledgers linear and avoid recursion for long relay chains.
        var rooted = new HashSet<(string AgentId, string NoticeId)>();
        foreach (var receipt in state.Knowledge)
        {
            var key = (receipt.AgentId, receipt.NoticeId);
            if (rooted.Contains(key)) continue;
            var visiting = new HashSet<(string AgentId, string NoticeId)>();
            while (!rooted.Contains(key))
            {
                if (!visiting.Add(key))
                    throw new InvalidDataException("Saved civic relays must lead back to an actual notice read.");
                var current = receipts[key];
                if (current.SourceAgentId is not { } source) break;
                key = (source, current.NoticeId);
            }
            rooted.UnionWith(visiting);
        }
    }
    internal static void ValidateCompletedGovernmentAuthority(TownCouncilState council, TownGovernmentState state)
    {
        var leader = TownGovernmentRules.GoverningOffice(state)?.HolderId;
        if (state.Arrangement.Ordinary == TownArrangementRules.Mayor &&
            (leader is not null ? council.Form != "leader" || !council.Members.SequenceEqual([leader]) : council.Form != "all_adult") ||
            state.Arrangement.Ordinary != TownArrangementRules.Mayor && council.Form == "leader" ||
            state.Arrangement.Ordinary == TownArrangementRules.AllAdultCouncil && council.Form != "all_adult")
            throw new InvalidDataException("Ordinary decision authority does not match the approved arrangement and living officeholder.");
    }
    internal void RefreshTownLandHearings(ITownWorld world)
    {
        foreach (var town in State.Towns.ToArray())
        {
            if (town.Council is { } council && (town.LandRecords.Cases.Any(landCase => landCase.Status != "settled" ||
                    landCase.ReopenRequests.Any(request => request.Status == "pending")) ||
                town.LandRecords.Transfers.Any(transfer => transfer.Status == "pending") ||
                town.NonLandHearings.Cases.Any(item => item.Status == "pending" || item.ReopenRequests.Any(request => request.Status == "pending")) ||
                town.NonLandHearings.Offers.Any(offer => offer.Status == "pending") ||
                town.NonLandHearings.Agreements.Any(agreement => agreement.Status != "completed" && !TownRemedyRules.IsSuperseded(town.NonLandHearings, agreement.Id))))
                SaveTownCouncil(world, town, council, town.Government);
        }
    }

    internal static void ValidateLandHearings(WorldMap map, SocietyCheckpoint checkpoint,
            IReadOnlyList<TownRuntimeState> savedTowns, IReadOnlyList<HouseholdLandUseRight> rights,
            IReadOnlyList<HouseholdLandUseRequest> requests, IReadOnlyList<TownLandTitleRecord> titles, int day)
    {
        var knownAgents = checkpoint.Agents.Select(person => person.Id).ToHashSet(StringComparer.Ordinal);
        var knownHouseholds = checkpoint.Households.Select(household => household.Id).ToHashSet(StringComparer.Ordinal);
        var households = checkpoint.Agents.ToDictionary(person => person.Id, person => person.HouseholdId, StringComparer.Ordinal);
        foreach (var town in savedTowns)
        {
            if (town.LandRecords is null)
                throw new InvalidDataException("A saved Town is missing its land-hearing history.");
            var adults = checkpoint.Agents.Where(person => town.ResidentIds.Contains(person.Id, StringComparer.Ordinal) &&
                    person.Status == SocietyAgentStatus.Active && LifeStageRules.IsAdult(person.AgeBand))
                .Select(person => person.Id).Order(StringComparer.Ordinal).ToArray();
            TownLandHearingValidation.Validate(map, checkpoint.WorldTick, town.Id, town.LandRecords,
                rights.Where(right => right.TownId == town.Id).ToArray(), titles, knownAgents, knownHouseholds,
                town.Council, day, town.Government, adults, households,
                currentPartiesResolver: item => TownLandCasePartyRules.CurrentParties(town,
                    TownLandHearingRules.CurrentRevision(item).Tiles, rights, requests, checkpoint.Agents,
                    checkpoint.WorldTick, item));
            TownLandTransferValidation.ValidateSalePayments(map, town.LandRecords, checkpoint.Inventory);
            foreach (var proposal in town.Council?.Proposals.Where(proposal => proposal.Kind == "land_hearing") ?? [])
                TownLandGovernmentFilingRules.Validate(proposal, town, map, titles, knownHouseholds);
        }
    }
    internal static void ValidateLocalAdmissions(TownRuntimeState town)
    {
        var records = town.Admissions ?? [];
        if (records.Any(record => record is null) || records.Select(record => record.ProposalId).Distinct(StringComparer.Ordinal).Count() != records.Count)
            throw new InvalidDataException("A Town's saved admission records are invalid.");
        foreach (var record in records)
            if (record.DecidedTick < 0 || string.IsNullOrWhiteSpace(record.ProposalId) || string.IsNullOrWhiteSpace(record.SubjectId) ||
                record.PreviousTownId == town.Id || record.Status switch
                {
                    WorldRuntime.CivicSharedRules.AdmissionApproved => record.MemberIds is not null || record.Reason is not null,
                    WorldRuntime.CivicSharedRules.AdmissionAdmitted => record.Reason is not null || record.MemberIds is not { Count: > 0 } members ||
                        !members.Contains(record.SubjectId, StringComparer.Ordinal) || members.Any(string.IsNullOrWhiteSpace) ||
                        !members.SequenceEqual(members.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)),
                    WorldRuntime.CivicSharedRules.AdmissionLapsed => record.PreviousTownId is not null || record.MemberIds is not null ||
                        !WorldRuntime.CivicSharedRules.AdmissionLapseReasons.Contains(record.Reason ?? "", StringComparer.Ordinal),
                    _ => true,
                })
                throw new InvalidDataException("A Town's saved admission record does not match a passed admission proposal.");
    }

    internal static void ValidateLocalLandRequest(HouseholdLandUseRequest request)
    {
        if (request.Status is not ("pending" or "granted" or "rejected" or "withdrawn" or "hearing_resolved") ||
            request.Status == "pending" && request.SettledTick is not null ||
            request.Status != "pending" && (request.SettledTick is null || request.SettledTick < request.RequestedTick) ||
            request.Consents is null || request.GrantAdults is null || request.HearingResolutions is null ||
            request.Consents.Any(c => c is null || string.IsNullOrWhiteSpace(c.AgentId) || c.Tick < request.RequestedTick ||
                request.SettledTick is { } settled && c.Tick > settled) ||
            request.Consents.Select(c => c.AgentId).Distinct(StringComparer.Ordinal).Count() != request.Consents.Count ||
            request.Status != "granted" && request.GrantAdults.Count != 0)
            throw new InvalidDataException("A saved household land request has invalid approval or consent history.");
        if (request.Status == "granted" && (request.GrantAdults.Count == 0 ||
            !request.GrantAdults.SequenceEqual(TownCouncilRules.Ordered(request.GrantAdults)) ||
            request.GrantAdults.Any(id => string.IsNullOrWhiteSpace(id) || !request.Consents.Any(c => c.AgentId == id && c.Accepted)) ||
            request.AgreedEndTick is { } end && end <= request.SettledTick))
            throw new InvalidDataException("A household land grant must retain its Council approval, adult acceptance and recorded terms.");
    }
    internal static void ValidateLocalProjects(IReadOnlyList<TownProject>? projects)
    {
        if (projects is null) throw new InvalidDataException("A Town is missing its construction ledger.");
        var identities = new HashSet<string>(StringComparer.Ordinal);
        var deliveries = new HashSet<string>(StringComparer.Ordinal);
        foreach (var project in projects)
        {
            if (project is null || !identities.Add(project.Id)) throw new InvalidDataException("Town project identities must be unique.");
            TownProjectRules.ValidatePayload(project.Plan);
            var requiredWork = TownProjectRules.RequiredWork(project.Plan);
            if (project.ApprovedTick < 0 || project.LastTransitionTick < project.ApprovedTick ||
                project.Stage is not ("supplying" or "working" or "blocked" or "completed" or "cancelled") ||
                project.WorkDone < 0 || project.WorkDone > requiredWork || project.Deliveries is null ||
                project.Blocker is { } blocker && !BoundedText.IsWithinLimit(blocker, 512) ||
                project.Stage == "blocked" && string.IsNullOrWhiteSpace(project.Blocker) ||
                project.Stage is "supplying" or "working" or "completed" && project.Blocker is not null)
                throw new InvalidDataException("A Town project disagrees with its Council approval or construction stage.");
            var ordinal = 0;
            var liveQuantities = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var delivery in project.Deliveries)
            {
                var cost = delivery is null ? default : project.Plan.Budget.SingleOrDefault(item => item.ResourceId == delivery.ItemKind);
                if (delivery is null || delivery.Id != TownProjectRules.DeliveryId(project.Id, delivery.ContributorId,
                        delivery.SourceLotId, delivery.PickedUpTick, ordinal) || !deliveries.Add(delivery.Id) ||
                    string.IsNullOrWhiteSpace(delivery.SourceLotId) || string.IsNullOrWhiteSpace(delivery.LotId) ||
                    cost.Amount <= 0 || delivery.Quantity <= 0 || delivery.Quantity > cost.Amount || delivery.PickedUpTick < project.ApprovedTick ||
                    (delivery.DeliveredTick is null) != (delivery.ReservationId is null) ||
                    delivery.DeliveredTick is { } deliveredAt && deliveredAt < delivery.PickedUpTick ||
                    delivery.ReleasedTick is { } releasedAt && releasedAt < (delivery.DeliveredTick ?? delivery.PickedUpTick) ||
                    (delivery.ReleasedTick is null) != (delivery.ReleaseReason is null) ||
                    delivery.ReleaseReason is { } reason && (string.IsNullOrWhiteSpace(reason) || !BoundedText.IsWithinLimit(reason, 512)))
                    throw new InvalidDataException("A Town project's retained material delivery is invalid.");
                ordinal++;
                if (delivery.ReservationId is { } receiptId && receiptId != TownRecordIds.DeliveryInput(delivery.Id))
                    throw new InvalidDataException("A Town project's exact material receipt is missing or changed.");
                if (delivery.ReleasedTick is not null) continue;
                if (project.Stage is "blocked" or "cancelled")
                    throw new InvalidDataException("A blocked or cancelled Town project must release unspent claims and incoming bindings.");
                liveQuantities[delivery.ItemKind] = liveQuantities.GetValueOrDefault(delivery.ItemKind) + delivery.Quantity;
                if (liveQuantities[delivery.ItemKind] > cost.Amount)
                    throw new InvalidDataException("Town project inputs exceed the approved material budget.");
                if (project.Stage == "completed" && delivery.DeliveredTick is null)
                    throw new InvalidDataException("A completed Town project cannot retain undelivered cargo.");
            }
            if (project.Stage != "completed" && (project.CompletedBuildingId is not null || project.CompletedBoatId is not null || project.RemovedTick is not null))
                throw new InvalidDataException("An unfinished Town project cannot own a completed building.");
            if (project.Stage == "completed" && (project.WorkDone != requiredWork ||
                (project.Plan.BoatPortId is not null ? project.CompletedBoatId != TownProjectRules.BuildingId(project.Id) || project.CompletedBuildingId is not null
                    : project.CompletedBuildingId != TownProjectRules.BuildingId(project.Id) || project.CompletedBoatId is not null)))
                throw new InvalidDataException("A working or completed Town project lacks its exact delivered material budget.");
            if (project.RemovedTick is { } removed && removed < project.LastTransitionTick)
                throw new InvalidDataException("A completed Town building disagrees with its paid approval or retained removal.");
        }
    }
}

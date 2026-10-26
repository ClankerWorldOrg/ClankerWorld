using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Server.Observation;

/// <summary>
/// Server-side projection of the supported world runtime. It converts protected
/// simulation records into stable owner views, including cognition and native
/// world metadata.
/// </summary>
public sealed partial class OwnerWorldObservationStore
{
    /// <summary>
    /// The duties of a mayoral office as the Towns page names them. Agents' models read
    /// <see cref="TownArrangementRules.MandateLabel"/> instead, whose wording their prompts rely on.
    /// </summary>
    private static string OfficeDuties(string mandates) => string.Join(", plus ", mandates.Split('+').Select(mandate => mandate switch
    {
        "land" => "land disputes and expiring use rights",
        "ordinary" => "everyday laws and letting newcomers join",
        "non_land" => "non-land hearings and voluntary settlements",
        _ => "a duty this version doesn't support",
    }));

    /// <summary>
    /// A Town's way of governing in the Towns page's words: who decides, how the mayor is chosen and what happens
    /// while the office is empty. Agents' models read <see cref="TownArrangementRules.Declaration"/> instead.
    /// </summary>
    private static string GovernmentSummary(TownArrangement arrangement)
    {
        var everyday = arrangement.Ordinary switch
        {
            TownArrangementRules.Council => "the Town council (every adult resident, or three elected representatives with ten-day terms once there are four adults)",
            TownArrangementRules.AllAdultCouncil => "every adult resident, however many there are",
            TownArrangementRules.ElectedCouncil => "three elected representatives with ten-day terms (every adult resident when there are three or fewer)",
            TownArrangementRules.Mayor => "one elected leader, the mayor",
            _ => "an arrangement this version doesn't support",
        };
        var text = "Everyday decisions: " + everyday + ". Land cases: " +
            (arrangement.Land == TownArrangementRules.Mayor ? "the elected mayor" : "nobody holds this duty, so land cases wait") + ". " +
            "Non-land cases: " + (arrangement.NonLand == TownArrangementRules.Mayor ? "the elected mayor, as a separate duty" : "nobody holds this duty, so formal cases wait") + ".";
        if (TownArrangementRules.Mandates(arrangement) is not { } mandates) return text;
        return text + $" Residents elect a willing adult resident as mayor ({OfficeDuties(mandates)}), one vote each; a tie means another vote. " +
            $"A term lasts {TownArrangementRules.MayorTermDays} unpaused days, and voting for the next term is due one day before it ends. " +
            "If the office becomes empty, residents can elect a willing successor for a fresh full term" +
            (arrangement.Ordinary == TownArrangementRules.Mayor ? "; until then every adult resident makes everyday decisions." : "; until then land cases wait.");
    }

    private static OwnerTownGovernment ProjectGovernment(TownGovernmentState government, Func<string, string> name, long tick)
    {
        OwnerMayoralElection Election(TownMayoralContest contest) => new(contest.Id,
            OfficeDuties(contest.Mandates), contest.Stage, contest.Round, contest.RoundDeadlineTick,
            contest.Candidates.Select(id => new OwnerCivicCandidate(id, name(id), contest.Ballots.Count(b => b.CandidateId == id))).ToArray(),
            contest.WinnerId is { } winner ? name(winner) : null, contest.Reason);
        return new(GovernmentSummary(government.Arrangement),
            government.Laws.TakeLast(16).Select(l =>
            {
                var v = TownLawRules.Current(l);
                return new OwnerTownLaw(l.Id, v.Subject, v.Rule, v.BoatAccess is null ? v.Scope : "communal_boats", v.SiteTiles.Count, v.Version, v.AdoptedTick, v.EndedTick)
                { Site = v.SiteTiles.Select(ToPosition).ToArray() };
            }).ToArray(), government.Laws.Count,
            government.Offices.Select(o => new OwnerTownOffice(OfficeDuties(o.Mandates),
                o.HolderId is { } holder ? name(holder) : null, o.TermEndTick, o.VacancyReason)).ToArray(),
            government.Changes.TakeLast(8).Select(c => new OwnerGovernmentChange(c.Id,
                (c.Kind == "replace_mayor" ? "Elect a new mayor for the same duties. " : "") + GovernmentSummary(c.Target),
                c.Status, c.Votes.Count(v => v.Yes), c.Votes.Count(v => !v.Yes), c.Voters.Count / 2 + 1,
                c.DeadlineTick, c.HandoverDeadlineTick, c.Reason)
            {
                NonLandExtension = c.NonLandExtension is { } extension ? new(extension.HolderId, name(extension.HolderId),
                    OfficeDuties(extension.BaseMandate), extension.TermStartTick, extension.TermEndTick, extension.ConsentTick) : null,
            }).ToArray(),
            government.Contest is { } live ? Election(live) : null,
            government.ContestHistory.Count > 0 ? Election(government.ContestHistory[^1]) : null, government.MayoralRetryTick)
        {
            NonLandAuthorized = government.Arrangement.NonLand == TownArrangementRules.Mayor,
            NonLandAuthority = TownGovernmentRules.CurrentNonLandAuthority(government, tick) is { } authority
                ? new(authority.HolderId, name(authority.HolderId), authority.AuthorityId, authority.EffectiveTick,
                    authority.TermStartTick, authority.TermEndTick) : null,
            NonLandGrants = government.NonLandGrants.Select(grant => new OwnerNonLandGrant(grant.Id, grant.HolderId,
                name(grant.HolderId), OfficeDuties(grant.BaseMandate), grant.ConsentTick,
                grant.EffectiveTick, grant.TermStartTick, grant.TermEndTick)).ToArray(),
        };
    }

    private static OwnerTownLandHearing[] ProjectLandHearings(WorldRuntimeState state, TownRuntimeState town)
    {
        var cases = town.LandRecords?.Cases ?? [];
        string AgentName(string id) => state.Society.Society.Agents.FirstOrDefault(person => person.Id == id)?.Name ?? "Unknown adult";
        string HouseholdName(string id) => state.Society.Society.Households.FirstOrDefault(household => household.Id == id)?.Name ?? "Unknown household";
        HashSet<string> Awareness(string noticeId, long tick) => (town.Council?.Knowledge ?? [])
            .Where(receipt => receipt.NoticeId == noticeId && receipt.LearnedTick <= tick)
            .Select(receipt => receipt.AgentId).ToHashSet(StringComparer.Ordinal);
        OwnerLandHearingParty Party(TownLandCaseParty party, IReadOnlySet<string> aware) => new(party.Id, party.Kind,
            party.HouseholdId is { } household ? HouseholdName(household) :
                (state.Towns ?? []).FirstOrDefault(other => other.Id == party.TownId)?.Name ?? town.Name,
            party.AdultIds.ToArray(), party.AdultIds.Select(AgentName).ToArray(), party.RepresentativeId,
            party.RepresentativeId is { } representative ? AgentName(representative) : null,
            party.AdultIds.Where(aware.Contains).Concat(party.RepresentativeId is { } agent && aware.Contains(agent) ? [agent] : [])
                .Distinct(StringComparer.Ordinal).ToArray());
        var names = state.Society.Society.Agents.Select(person => (Id: person.Id, Name: person.Name))
            .Concat(state.Society.Society.Households.Select(household => (Id: household.Id, Name: household.Name)))
            .Concat((state.Towns ?? []).Select(other => (Id: other.Id, Name: other.Name)))
            .Concat((state.WorldContent?.Buildings ?? []).Select(building => (Id: building.CanonicalId, Name: building.DisplayName)))
            .Concat((state.WorldSimulation?.Buildings ?? []).Select(building => (Id: building.InstanceId,
                Name: (state.WorldContent?.Buildings.FirstOrDefault(definition => definition.CanonicalId == building.DefinitionId)?.DisplayName ?? "Building") +
                    " at (" + building.Position.X + ", " + building.Position.Y + ")")))
            .OrderByDescending(item => item.Id.Length).ToArray();
        string Readable(string text)
        {
            foreach (var name in names) text = text.Replace(name.Id, name.Name, StringComparison.Ordinal);
            return text;
        }
        OwnerLandHearingOutcome Outcome(TownLandRequestedOutcome outcome) => new(outcome.Kind, outcome.HouseholdId,
            outcome.HouseholdId is { } household ? HouseholdName(household) : null, outcome.AgreedEndTick);
        OwnerLandHearingJudge Judge(TownLandCaseJudge judge) => new(judge.AgentId, AgentName(judge.AgentId),
            judge.Kind, judge.AuthorityId, judge.AssignedTick);
        OwnerLandHearingElection Election(TownLandCaseJudgeContest contest) => new(contest.Id, contest.Stage,
            contest.Round, contest.RoundDeadlineTick, contest.Candidates.Select(id => new OwnerCivicCandidate(id,
                AgentName(id), contest.Ballots.Count(ballot => ballot.CandidateId == id))).ToArray(),
            contest.WinnerId is { } winner ? AgentName(winner) : null, HearingReasonText.Describe(contest.Reason));
        OwnerLandHearingEvidence Evidence(TownLandEvidence evidence, TownLandCase item)
        {
            var right = item.Revisions.SelectMany(revision => revision.RightVersions)
                .Concat(town.LandRecords?.OriginalRights ?? [])
                .Concat((state.HouseholdLandUseRights ?? []).Select(TownLandHearingRules.Snapshot))
                .Concat((town.LandRecords?.Adjustments ?? []).SelectMany(adjustment => adjustment.ResultRights)
                    .Select(TownLandHearingRules.Snapshot))
                .FirstOrDefault(version => version.Id == evidence.SourceRecordId && version.Version == evidence.SourceVersion)?.Right;
            var title = (state.TownLandTitles ?? []).FirstOrDefault(record => record.Id == evidence.SourceRecordId);
            var lawVersion = town.Government?.Laws.SelectMany(law => law.Versions.Where(version =>
                TownLandHearingRules.LawRecordId(law.Id, version.Version) == evidence.SourceRecordId &&
                TownLandHearingRules.LawVersion(version) == evidence.SourceVersion)).FirstOrDefault();
            return new(evidence.Id, evidence.Revision, evidence.Kind, evidence.Acquisition,
                evidence.SourceAgentId, AgentName(evidence.SourceAgentId), evidence.SourceRecordId,
                evidence.SourceVersion, evidence.ObservedTick, evidence.SubmittedByAgentId,
                AgentName(evidence.SubmittedByAgentId), evidence.SubmittedTick, Readable(evidence.Text))
            {
                PermissionRecord = right is not null ? new(right.Id, right.TownId, right.HouseholdId,
                    right.Tiles.Select(ToPosition).ToArray(), right.GrantedTick, right.GrantSource, right.AgreedEndTick) : null,
                TitleRecord = title is not null ? new(title.Id, title.TownId, title.Tiles.Select(ToPosition).ToArray(), title.RecordedTick) : null,
                RecordPartyName = right is not null ? HouseholdName(right.HouseholdId) : title is not null
                    ? (state.Towns ?? []).FirstOrDefault(other => other.Id == title.TownId)?.Name : null,
                LawVersion = lawVersion?.Version,
            };
        }
        static bool Active(TownLandCase item) => item.SettledTick is null || item.ReopenRequests.Any(request => request.Status == "pending");
        return cases.Where(Active)
            .Concat(cases.Where(item => !Active(item)).OrderBy(item => item.SettledTick)
                .ThenBy(item => item.Id, StringComparer.Ordinal).TakeLast(RecentSettledLandHearingLimit))
            .OrderBy(item => item.FiledTick).ThenBy(item => item.Id, StringComparer.Ordinal)
            .Select(item =>
            {
                var revision = item.Revisions[^1];
                var aware = Awareness(revision.NoticeId, state.Society.Society.WorldTick);
                return new OwnerTownLandHearing(item.Id, item.Kind, item.Status, item.FiledTick, item.SettledTick,
                    revision.Number, revision.Tiles.Select(ToPosition).ToArray(), revision.RightVersions.Select(version =>
                        new OwnerLandHearingRightVersion(version.Id, version.Version, new OwnerWorldHouseholdLandUseRight(
                            version.Right.Id, version.Right.TownId, version.Right.HouseholdId,
                            version.Right.Tiles.Select(ToPosition).ToArray(), version.Right.GrantedTick,
                            version.Right.GrantSource, version.Right.AgreedEndTick))).ToArray(),
                    revision.NoticeId, revision.PublishedTick, revision.DeadlineTick, Outcome(revision.RequestedOutcome),
                    item.Filings.Select(filing => new OwnerLandHearingFiling(filing.AgentId,
                        filing.AgentId is { } filer ? AgentName(filer) : null, filing.Kind, Readable(filing.Text),
                        Outcome(filing.RequestedOutcome), filing.Tick, filing.AuthorityId)).ToArray(),
                    revision.Parties.Select(party => Party(party, aware)).ToArray(),
                    item.Evidence.Select(evidence => Evidence(evidence, item)).ToArray(),
                    item.Responses.Select(response => new OwnerLandHearingResponse(response.Revision, response.PartyId,
                        response.AgentId, AgentName(response.AgentId), response.Kind, Readable(response.Text), response.Tick)).ToArray(),
                    item.Rulings.Select(ruling => new OwnerLandHearingRuling(ruling.Id, ruling.Revision, Judge(ruling.Judge),
                        ruling.Tick, Outcome(ruling.Outcome), item.Revisions.Single(old => old.Number == ruling.Revision)
                            .Tiles.Select(ToPosition).ToArray(), ruling.EvidenceIds.ToArray(), ruling.LawIds.ToArray(),
                        Readable(ruling.Reasons), ruling.AdjustmentIds.ToArray())
                    {
                        Parties = ruling.Parties.Select(party => Party(party, Awareness(
                            item.Revisions.Single(old => old.Number == ruling.Revision).NoticeId, ruling.Tick))).ToArray(),
                    }).ToArray(),
                    item.Judge is { } judge ? Judge(judge) : null,
                    item.JudgeHistory.Select(term => new OwnerLandHearingJudgeTerm(Judge(term.Judge), term.EndedTick, HearingReasonText.Describe(term.Reason)!)).ToArray(),
                    item.Contest is { } contest ? Election(contest) : null,
                    item.ContestHistory.Count > 0 ? Election(item.ContestHistory[^1]) : null,
                    item.ReopenRequests.Select(request => new OwnerLandHearingReopenRequest(request.Id, request.AgentId,
                        AgentName(request.AgentId), request.Tick, request.Kind, request.EvidenceIds.ToArray(), Readable(request.Reasons),
                        request.Status, request.AssessedBy is { } adjudicator ? Judge(adjudicator) : null,
                        request.AssessedTick, request.Assessment is { } assessment ? Readable(assessment) : null)).ToArray())
                {
                    PropertyDetails = item.Property is { } property ? new[]
                    {
                        "Property: " + Readable(property.Request.BuildingId) + ". Shared goods: " +
                            (property.Snapshots[^1].SharedLots.Count == 0 ? "none" : string.Join(", ", property.Snapshots[^1].SharedLots
                                .GroupBy(lot => lot.ItemKind).Select(group => group.Sum(lot => lot.Quantity) + " " + group.Key.Replace('_', ' ')))) + ".",
                        "Agreeing to hand over the property is separate from answering the notice or choosing not to respond. Personal goods stay with their owners."
                    }.Concat(TownPropertyRules.RequiredConsent(property.Snapshots[^1]).Select(actor =>
                        AgentName(actor) + ": " + (property.Consents.LastOrDefault(consent => consent.Revision == property.Snapshots[^1].Revision && consent.AgentId == actor) is { } consent
                            ? consent.Agreed ? "personally agreed to this property transfer" : "refused this property transfer" : "has not agreed to this property transfer") + "."))
                        .Concat(property.Transfer is { } transfer ? new[] { "At the ruling, this property passed to " +
                            (property.Request.TargetHouseholdId is { } recipient ? HouseholdName(recipient) : town.Name) + "." } : [])
                        .ToArray() : [],
                    CurrentParties = item.Status == "pending" ? TownLandCasePartyRules.CurrentParties(town, revision.Tiles,
                        state.HouseholdLandUseRights ?? [], state.HouseholdLandUseRequests ?? [], state.Society.Society.Agents,
                        state.Society.Society.WorldTick, item).Select(party => Party(party, aware)).ToArray() : [],
                    Reads = item.Reads.Select(read => new OwnerLandHearingRead(read.Revision, read.AgentId, AgentName(read.AgentId),
                        read.ReadTick, read.EvidenceIds.ToArray(), read.SourceAgentId,
                        read.SourceAgentId is { } source ? AgentName(source) : null)
                    {
                        ReopenRequestIds = read.ReopenRequestIds.ToArray(),
                    }).ToArray(),
                };
            }).ToArray();
    }

    private static OwnerLandTransfer[] ProjectLandTransfers(WorldRuntimeState state, TownRuntimeState town)
    {
        var transfers = town.LandRecords?.Transfers ?? [];
        var receipts = town.Council?.Knowledge ?? [];
        var adultHouseholds = state.Society.Society.Agents.Where(person => person.Status == SocietyAgentStatus.Active &&
            person.AgeBand is SocietyAgeBand.Adult or SocietyAgeBand.Elder).ToDictionary(person => person.Id, person => person.HouseholdId, StringComparer.Ordinal);
        string AgentName(string id) => state.Society.Society.Agents.FirstOrDefault(person => person.Id == id)?.Name ?? "Unknown adult";
        string HouseholdName(string id) => state.Society.Society.Households.FirstOrDefault(household => household.Id == id)?.Name ?? "Unknown household";
        return transfers.Where(request => request.Status == "pending")
            .Concat(transfers.Where(request => request.Status != "pending").OrderBy(request => request.SettledTick)
                .ThenBy(request => request.Id, StringComparer.Ordinal).TakeLast(RecentClosedLandTransferLimit))
            .OrderBy(request => request.ProposedTick).ThenBy(request => request.Id, StringComparer.Ordinal)
            .Select(request =>
            {
                var parties = request.Status == "pending" ? TownLandTransferRules.PartiesFor(request.RightVersions.Select(version => version.Right),
                    request.Tiles, request.TargetHouseholdId, adultHouseholds) : request.Receipt?.Parties ?? request.Parties;
                var rosterKind = request.Status == "pending" ? "current" : request.Receipt is not null ? "settlement" : "publication";
                var asOf = request.SettledTick ?? state.Society.Society.WorldTick;
                var aware = receipts.Where(receipt => receipt.NoticeId == request.NoticeId && receipt.LearnedTick >= request.ProposedTick &&
                    receipt.LearnedTick <= asOf).Select(receipt => receipt.AgentId).ToHashSet(StringComparer.Ordinal);
                return new OwnerLandTransfer(request.Id, request.FilerId, AgentName(request.FilerId), request.TargetHouseholdId,
                    HouseholdName(request.TargetHouseholdId), request.Tiles.Select(ToPosition).ToArray(), request.RightVersions.Select(version =>
                        new OwnerLandHearingRightVersion(version.Id, version.Version, new OwnerWorldHouseholdLandUseRight(version.Right.Id,
                            version.Right.TownId, version.Right.HouseholdId, version.Right.Tiles.Select(ToPosition).ToArray(),
                            version.Right.GrantedTick, version.Right.GrantSource, version.Right.AgreedEndTick))).ToArray(),
                    parties.Select(party => new OwnerLandTransferParty(party.HouseholdId, party.Kind, HouseholdName(party.HouseholdId), rosterKind,
                        party.AdultIds.ToArray(), party.AdultIds.Select(AgentName).ToArray(),
                        TownLandTransferRules.AcceptedAdults(request, party, receipts, asOf).ToArray(), party.AdultIds.Where(aware.Contains).ToArray())).ToArray(),
                    request.NoticeId, request.ProposedTick, request.Responses.Select(response => new OwnerLandTransferResponse(response.HouseholdId,
                        HouseholdName(response.HouseholdId), response.AgentId, AgentName(response.AgentId), response.Kind, response.Tick,
                        response.PartyAdults.ToArray())).ToArray(), request.Status, request.SettledTick, request.Reason, request.Receipt?.AdjustmentId)
                {
                    Price = request.Price is { } price ? new(price.SellerHouseholdId, HouseholdName(price.SellerHouseholdId), price.ItemKind, price.Quantity)
                    {
                        ItemName = GoodsNameText.ForQuantity(price.ItemKind, price.Quantity, state.Content),
                    } : null,
                    Payment = request.Receipt?.Payment is { } payment ? new(payment.BuyerAgentId, AgentName(payment.BuyerAgentId),
                        payment.SellerAgentId, AgentName(payment.SellerAgentId), ToPosition(payment.Position), request.Receipt.Tick) : null
                };
            }).ToArray();
    }

    private static OwnerLandHearingProposal ProjectLandHearingProposal(WorldRuntimeState state, TownLandFilingRequest request)
    {
        var statement = request.Statement;
        var names = state.Society.Society.Agents.Select(person => (Id: person.Id, Name: person.Name))
            .Concat(state.Society.Society.Households.Select(household => (Id: household.Id, Name: household.Name)))
            .Concat((state.Towns ?? []).Select(town => (Id: town.Id, Name: town.Name))).OrderByDescending(item => item.Id.Length);
        foreach (var name in names) statement = statement.Replace(name.Id, name.Name, StringComparison.Ordinal);
        var outcome = request.RequestedOutcome;
        return new(request.Tiles.Select(ToPosition).ToArray(), new(outcome.Kind, outcome.HouseholdId,
            outcome.HouseholdId is { } householdId ? state.Society.Society.Households.FirstOrDefault(household => household.Id == householdId)?.Name ?? "Unknown household" : null,
            outcome.AgreedEndTick), statement);
    }

    private static string LandRequestApprovalDetail(WorldRuntimeState state, HouseholdLandUseRequest request, bool disputed)
    {
        if (request.Status != "pending") return request.Status == "hearing_resolved"
            ? "A land ruling settled this use request" : "Household request " + request.Status.Replace('_', ' ');
        var adults = state.Society.Society.Agents.Where(p => p.HouseholdId == request.HouseholdId &&
            p.Status == SocietyAgentStatus.Active && p.AgeBand is SocietyAgeBand.Adult or SocietyAgeBand.Elder).ToArray();
        var accepted = adults.Count(p => request.Consents.Any(c => c.AgentId == p.Id && c.Accepted));
        var proposal = state.Towns?.Single(t => t.Id == request.TownId).Council?.Proposals.SingleOrDefault(p => p.Id == request.CouncilProposalId);
        var council = proposal?.Status == "passed" ? "Council approved" : proposal is null ? "Waiting for the Council to consider it" :
            $"Council votes: {proposal.Votes.Count(v => v.Yes)}/{proposal.RequiredYes}";
        var resolved = request.HearingResolutions.SelectMany(receipt => receipt.Tiles).Distinct().Count();
        return $"{council}; household adults who agreed: {accepted}/{adults.Length}" + (disputed ? "; disputed plot" : "") +
            (resolved > 0 ? $"; land rulings settled {resolved} of {request.Tiles.Count} requested tiles; the rest are still waiting" : "");
    }

    private const int AgentKnowledgeArtifactLimit = 8;
    private const int RecentClosedInstructionLimitPerAgent = OwnerQueuedInstruction.RecentClosedOrderLimitPerAgent;
    public const int RecentCivicProposalLimit = 8;
    public const int RecentSettledLandHearingLimit = 8;
    public const int RecentClosedLandTransferLimit = 8;
    private static readonly string[] OwnerServerCapabilities =
    [
        "snapshot.read.v1",
        "event-replay.read.v1",
        "event-history-reset.read.v1",
        "reconnect-baseline.read.v1",
        "seeded-map.read.v1",
        "agent-inspection.read.v1",
        "spatial-knowledge.read.v1",
        "owner-map-layer-delta.v1",
        "owner-device-pairing.v1",
        "owner-observation.read.v1",
        "owner-control.request.v1",
        "owner-provider-configuration.v1",
        "owner-agent-provider-configuration.v1",
        "paused-authoring.request.v1",
        "paused-authoring.request.v2",
        "content-governance.read.v1",
        "content-governance.write.v1",
    ];

    private static readonly string[] OwnerClientCapabilities =
    [
        "snapshot.read.v1",
        "event-replay.read.v1",
        "reconnect-baseline.read.v1",
        "owner-device-pairing.v1",
    ];

    private readonly WorldRuntime worldRuntime;
    private readonly object mapProjectionGate = new();
    private MapProjectionBasis? mapProjectionBasis;

    private sealed record MapProjectionBasis(WorldMap Map, string WorldSeed, bool Generated,
        LandFertility Fertility, string? LayersDigest);

    private MapProjectionBasis MapProjectionFor(WorldRuntimeState state)
    {
        lock (mapProjectionGate)
        {
            // Committed terrain is immutable. Resource/bridge changes and reloads
            // replace the map; switching worlds can also change the fertility seed.
            var generated = state.Geography is not null;
            if (mapProjectionBasis is { } existing && ReferenceEquals(existing.Map, state.Map) &&
                existing.Generated == generated && string.Equals(existing.WorldSeed, state.WorldSeed, StringComparison.Ordinal))
                return existing;
            return mapProjectionBasis = new(state.Map, state.WorldSeed, generated,
                new LandFertility(state.Map, state.WorldSeed),
                generated ? MapLayerManifestCodec.Digest(state.Map) : null);
        }
    }

    public OwnerWorldObservationStore(WorldRuntime runtime)
    {
        worldRuntime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public OwnerWorldHandshake GetOwnerHandshake() => new(
        new OwnerWorldProtocolVersion(Major: 1, Minor: 1),
        [.. OwnerServerCapabilities, "owner-event-presentation.v2", "owner-aging-override.v2", "owner-jev-assistance.v2", "owner-routine-helper.v1", "owner-building-design.v1", "owner-terrain-delta.v1", "owner-observation-timeline.v1"],
        OwnerClientCapabilities.ToArray());

    public OwnerWorldSnapshot GetSnapshot()
    {
        var (state, diagnostics, _, retainedEvents) = CapturePresentationObservation();
        return ToSnapshot(state, diagnostics, retainedEvents);
    }

    public OwnerWorldEventSlice GetEventsAfter(long afterEventId)
    {
        var (state, diagnostics, _, retainedEvents) = CapturePresentationObservation();
        var snapshot = ToSnapshot(state, diagnostics, retainedEvents);
        return new OwnerWorldEventSlice(
            state.Society.Society.WorldTick,
            afterEventId,
            state.Events
                .Where(worldEvent => worldEvent.EventId > afterEventId)
                .Select(item => ToEvent(item, snapshot))
                .ToArray(), state.EventHistoryFloor, afterEventId < state.EventHistoryFloor);
    }

    public OwnerWorldReconnectBaseline GetReconnectBaseline(long afterEventId,
        string? knownTerrainWorldId = null, string? knownTerrainDigest = null,
        string? knownMapLayersDigest = null)
    {
        var (state, diagnostics, timeline, retainedEvents) = CapturePresentationObservation();
        var snapshot = ToSnapshot(state, diagnostics, retainedEvents, knownTerrainWorldId, knownTerrainDigest,
            knownMapLayersDigest);
        return new OwnerWorldReconnectBaseline(
            snapshot,
            new OwnerWorldEventSlice(
                snapshot.WorldTick,
                afterEventId,
                state.Events
                    .Where(worldEvent => worldEvent.EventId > afterEventId)
                    .Select(item => ToEvent(item, snapshot))
                    .ToArray(), state.EventHistoryFloor, afterEventId < state.EventHistoryFloor),
            new OwnerObserverTimeline(timeline.InstanceId, timeline.Generation));
    }

    private static bool RequiresWorldCreation(WorldRuntimeState state)
    {
        // This is a presentation decision about the captured save, not a new
        // world or a save migration. Keep any authored work accessible, even
        // when its founders or buildings have since been removed.
        return state.Geography is null &&
            state.FounderSetup is { Started: false, FounderIds.Count: 0 } &&
            state.Society.Society is { WorldTick: 0, IsPaused: true, Agents.Count: 0, EventHistoryFloor: 0 } &&
            state.Agents.Count == 0 && state.DeceasedAgents is null or { Count: 0 } &&
            state.Content is { Packages.Count: 0, Events.Count: 0 } &&
            state.WorldContent is { Buildings.Count: 0, Recipes.Count: 0 } &&
            state.WorldSimulation is { Buildings.Count: 0, ProductionJobs.Count: 0 } simulation &&
            simulation.CropBuilds is null or { Count: 0 } &&
            simulation.BuildingExpansions is null or { Count: 0 } &&
            simulation.GuestInvitations is null or { Count: 0 } &&
            state.Fields is null or { Count: 0 } && state.RoadTiles is null or { Count: 0 } &&
            state.Bridges is null or { Count: 0 } && state.Instructions is null or { Count: 0 } &&
            state.EventHistoryFloor == 0 && state.HistoryArchiveHead is null &&
            state.Events.All(item => item.Kind is "world_created" or "town_founding_started" or "continuity_rule_on" or "paused") &&
            state.Society.Society.Events.All(item => item.Kind is "household_created" or "paused") &&
            state.Society.Society.Inventory.Events.Count == 0;
    }

    private OwnerWorldSnapshot ToSnapshot(WorldRuntimeState state, WorldDiagnostics diagnostics, WorldEventEntry[] retainedEvents,
        string? knownTerrainWorldId = null, string? knownTerrainDigest = null,
        string? knownMapLayersDigest = null)
    {
        var map = state.Map;
        var mapProjection = MapProjectionFor(state);
        var fertility = mapProjection.Fertility;
        var ecology = state.WorldSystems?.Ecology.Resources.ToDictionary(resource => resource.Id, StringComparer.Ordinal);
        var buildingDefinitions = state.WorldContent?.Buildings.ToDictionary(building => building.CanonicalId, StringComparer.Ordinal);
        var storageChanges = RecentBuildingStorageChanges(state.Society.Society.Inventory);
        var productionRecipes = state.WorldContent?.Recipes.ToDictionary(recipe => recipe.CanonicalId,
            ProjectProductionRecipe, StringComparer.Ordinal);
        var recipesByBuilding = state.WorldContent?.Recipes
            // Match the retired/crop rejection gates in StartProductionCore.
            .Where(recipe => !recipe.IsCrop && recipe.WorkstationBuildingId is not null &&
                !recipe.Outputs.Any(output => output.ResourceId == "bedding") &&
                !(recipe.Inputs.Any(input => input.ResourceId == "food") &&
                    recipe.Outputs.Any(output => output.ResourceId == "food")))
            .ToLookup(recipe => recipe.WorkstationBuildingId!, recipe => productionRecipes![recipe.CanonicalId], StringComparer.Ordinal);
        var inventoryLots = state.Society.Society.Inventory.Lots.ToDictionary(lot => lot.Id, StringComparer.Ordinal);
        var inventoryReservations = state.Society.Society.Inventory.Reservations.ToDictionary(reservation => reservation.Id.Value, StringComparer.Ordinal);
        var activeAgents = state.Society.Society.Agents
            .Where(item => item.Status == SocietyAgentStatus.Active)
            .OrderBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
        var agentsById = state.Society.Society.Agents
            .ToDictionary(item => item.Id, StringComparer.Ordinal);
        OwnerTownElection ProjectElection(TownElection election) => new(election.Id, election.Kind, election.Stage,
            election.Seats, election.DeadlineTick, election.Candidates.Select(id => new OwnerCivicCandidate(
                id, agentsById.GetValueOrDefault(id)?.Name ?? id,
                election.Ballots.Count(ballot => ballot.Choices.Contains(id, StringComparer.Ordinal)))).ToArray(),
            election.SettledSeats.Select(id => agentsById.GetValueOrDefault(id)?.Name ?? id).ToArray());
        OwnerWorldTownProjectPlan ProjectPlan(TownProjectPayload plan, string proposerId)
        {
            var definition = buildingDefinitions?.GetValueOrDefault(plan.DefinitionId) ??
                TownProjectRules.DefinitionFor(plan.DefinitionId) ?? throw new InvalidDataException("Unsupported Town project definition.");
            return new OwnerWorldTownProjectPlan(plan.Name, proposerId,
                agentsById.GetValueOrDefault(proposerId)?.Name ?? proposerId,
                plan.DefinitionId, plan.BoatPortId is null ? definition.DisplayName : "Communal boat", ToPosition(plan.BoatPortId is null ? plan.Site : TownProjectRules.WorkSite(plan)), ToPosition(plan.Entrance),
                plan.BoatPortId is null ? definition.Width : 1, plan.BoatPortId is null ? definition.Height : 1,
                plan.Budget.Select(q => new OwnerWorldTownProjectBudget(q.ResourceId, q.Amount)).ToArray())
            {
                Tags = plan.BoatPortId is null ? definition.Tags.ToArray() : ["boat_project"],
                BoatPortId = plan.BoatPortId,
            };
        }
        OwnerCivicProposal ProjectProposal(TownProposal proposal) => new(proposal.Id, proposal.Kind,
            proposal.Text, proposal.Status, proposal.Votes.Count(v => v.Yes), proposal.Votes.Count(v => !v.Yes),
            proposal.RequiredYes, proposal.DeadlineTick)
        {
            Project = proposal.Project is { } plan ? ProjectPlan(plan, proposal.AuthorId) : null,
        };
        OwnerWorldTownProject ProjectConstruction(TownRuntimeState town, TownProject project)
        {
            // Approval belongs to the full civic ledger, even when it is older than the recent proposal list.
            var approval = town.Council!.Proposals.Single(p => p.Id == project.ProposalId);
            var plan = ProjectPlan(project.Plan, approval.AuthorId);
            return new OwnerWorldTownProject(project.Id, project.ProposalId, plan.Name, plan.ProposerId,
                plan.ProposerName, plan.DefinitionId, plan.DisplayName, plan.Site, plan.Entrance,
                plan.Width, plan.Height, project.Plan.Budget.Select(q => new OwnerWorldTownProjectMaterial(
                    q.ResourceId, q.Amount, TownProjectRules.DeliveredQuantity(project, town.Id,
                        state.Society.Society.Inventory, q.ResourceId))).ToArray(),
                project.WorkDone, TownProjectRules.RequiredWork(project.Plan), project.Stage, project.Blocker,
                project.CompletedBuildingId, ProjectProposal(approval))
            {
                Tags = plan.Tags,
                CompletedBoatId = project.CompletedBoatId,
                RemovedTick = project.RemovedTick,
            };
        }
        string MarketOwnerName(string id) => agentsById.GetValueOrDefault(id)?.Name ??
            state.Society.Society.Households.FirstOrDefault(household => household.Id == id)?.Name ??
            state.Towns?.FirstOrDefault(town => town.Id == id)?.Name ?? id;
        OwnerWorldMarket ProjectMarket(TownMarketState market)
        {
            var inventory = state.Society.Society.Inventory;
            var plaza = MarketContent.PlazaOrigin(market.Site);
            var stalls = market.Stalls.Where(stall => stall.RemovedTick is null)
                .OrderBy(stall => stall.SlotIndex).Select(stall =>
                {
                    var building = state.WorldSimulation?.Buildings.FirstOrDefault(item => item.InstanceId == stall.BuildingId);
                    if (building is null) return null;
                    var occupancy = market.RemovedTick is null ? market.Occupancies
                        .LastOrDefault(item => item.StallBuildingId == stall.BuildingId && item.EndedTick is null) : null;
                    var stock = MarketTradeRules.StockAt(market, stall.BuildingId, building.Position, inventory)
                        .OrderBy(lot => lot.OwnerId, StringComparer.Ordinal).ThenBy(lot => lot.ItemKind, StringComparer.Ordinal)
                        .ThenBy(lot => lot.Id, StringComparer.Ordinal).Select(lot => new OwnerWorldMarketStock(
                            lot.Id, lot.ContainerLotId, lot.OwnerId, MarketOwnerName(lot.OwnerId), lot.ItemKind,
                            lot.Quantity, InventoryRules.UsableQuantity(InventoryIndex.For(inventory), lot))).ToArray();
                    var trades = market.Trades.Where(trade => trade.StallBuildingId == stall.BuildingId)
                        .OrderByDescending(trade => inventory.GetOffer(trade.OfferId).State == DirectBarterState.Open)
                        .ThenByDescending(trade => trade.ProposedTick).ThenBy(trade => trade.OfferId, StringComparer.Ordinal)
                        .Take(8).Select(trade =>
                        {
                            var offer = inventory.GetOffer(trade.OfferId);
                            return new OwnerWorldMarketTrade(trade.OfferId, trade.SellerAgentId, MarketOwnerName(trade.SellerAgentId),
                                trade.GoodsOwnerId, MarketOwnerName(trade.GoodsOwnerId), trade.PaymentOwnerId,
                                MarketOwnerName(trade.PaymentOwnerId), trade.BuyerId, MarketOwnerName(trade.BuyerId),
                                trade.GoodsKind, offer.FirstQuantity, trade.PaymentKind, offer.SecondQuantity,
                                offer.State.ToString().ToLowerInvariant(), trade.CancellationReason,
                                offer.AcceptedBy.Contains(offer.FirstPartyId, StringComparer.Ordinal),
                                offer.AcceptedBy.Contains(offer.SecondPartyId, StringComparer.Ordinal));
                        }).ToArray();
                    return new OwnerWorldMarketStall(stall.BuildingId, stall.SlotIndex, ToPosition(building.Position),
                        occupancy?.SellerAgentId, occupancy is null ? null : MarketOwnerName(occupancy.SellerAgentId),
                        occupancy?.StartedTick, stock, trades);
                }).OfType<OwnerWorldMarketStall>().ToArray();
            return new OwnerWorldMarket(market.Id, market.ProjectId, market.HallBuildingId, ToPosition(market.Site),
                ToPosition(plaza), MarketContent.PlazaWidth, MarketContent.PlazaHeight, stalls, market.RemovedTick);
        }
        var physicalById = state.Agents.ToDictionary(item => item.AgentId, StringComparer.Ordinal);
        var deceasedById = (state.DeceasedAgents ?? []).ToDictionary(item => item.AgentId, StringComparer.Ordinal);
        var resourceStates = state.Resources.ToDictionary(item => item.ResourceId, item => item.State, StringComparer.Ordinal);
        var completedInstructionIds = (state.CompletedInstructionIds ?? []).ToHashSet(StringComparer.Ordinal);
        var visibleInstructions = ProjectPrivateInstructions(state, completedInstructionIds);
        var first = activeAgents.FirstOrDefault();
        OwnerWorldActor? actor = null;
        if (first is not null)
        {
            var physical = physicalById[first.Id];
            var inventory = InventoryFor(state, first.Id);
            actor = new OwnerWorldActor(first.Id, ToPosition(physical.Position), physical.FullnessBasisPoints,
                inventory.Where(item => item.Kind == "food").Sum(item => item.Quantity),
                inventory.Where(item => item.Kind == "wood").Sum(item => item.Quantity));
        }
        var jobs = state.WorldSimulation is { } jobSimulation
            ? WorldHistory.ProductionJobsForInspection(jobSimulation) : [];
        var latestEventId = state.Events.Count == 0 ? 0 : state.Events[^1].EventId;
        var terrainUnchanged = state.Geography is not null &&
            string.Equals(knownTerrainWorldId, state.Society.Society.WorldId, StringComparison.Ordinal) &&
            string.Equals(knownTerrainDigest, map.ManifestDigest, StringComparison.Ordinal);
        var mapLayersDigest = mapProjection.LayersDigest;
        var mapLayersUnchanged = terrainUnchanged && mapLayersDigest is not null &&
            string.Equals(knownMapLayersDigest, mapLayersDigest, StringComparison.Ordinal);
        var packedTerrain = state.Geography is null || terrainUnchanged ? null : PackTerrain(map);
        var weatherAnchor = map.MapObjects.FirstOrDefault(item => item is { Id: "cooking", Kind: "cooking" })?.Position ??
            state.Towns?.FirstOrDefault(item => item.OriginSite is not null)?.OriginSite ??
            map.Resources.First(item => item.Id == "berry-patch").Position;
        var anchorWeather = state.WorldSystems is { } currentSystems
            ? WeatherRules.At(currentSystems,
                weatherAnchor, map.Height)
            : WeatherKind.Clear;
        var snapshot = new OwnerWorldSnapshot(
            state.Society.Society.WorldId,
            state.Society.Society.WorldTick,
            map.ManifestDigest,
            (state.Geography is null ? map.Tiles : [])
                .OrderBy(tile => tile.Position.Y)
                .ThenBy(tile => tile.Position.X)
                .Select(tile => new OwnerWorldTile(tile.Position.X, tile.Position.Y, MapManifestCodec.ToWireValue(tile.Terrain)))
                .ToArray(),
            map.MapObjects
                .Where(mapObject => mapObject.Kind != "bedroll")
                .OrderBy(mapObject => mapObject.Id, StringComparer.Ordinal)
                .Select(mapObject => new OwnerWorldObject(mapObject.Id, mapObject.Kind, ToPosition(mapObject.Position)))
                .ToArray(),
            map.Resources
                .OrderBy(resource => resource.Id, StringComparer.Ordinal)
                .Select(resource => new OwnerWorldResource(
                    resource.Id,
                    resource.Kind,
                    ToPosition(resource.Position),
                    resource.IsRenewable,
                    resourceStates.TryGetValue(resource.Id, out var resourceState)
                        ? ToWireValue(resourceState)
                        : "available",
                    ecology?.GetValueOrDefault(resource.Id)?.Quantity,
                    ecology?.GetValueOrDefault(resource.Id)?.Capacity,
                    ecology?.GetValueOrDefault(resource.Id)?.RegenerationAmount,
                    ecology?.GetValueOrDefault(resource.Id)?.RegenerationIntervalDays,
                    ecology?.GetValueOrDefault(resource.Id)?.RegenerationSeason.ToString().ToLowerInvariant(),
                    resource.TreeKind,
                    ecology?.GetValueOrDefault(resource.Id)?.IsPlanted ?? false,
                    TreeGrowthRules.StageOf(resource.TreeKind, ecology?.GetValueOrDefault(resource.Id),
                        state.WorldSystems?.Climate.Season ?? SeasonKind.Spring),
                    resource.NaturalObjectKind))
                .ToArray(),
            actor,
            latestEventId)
        {
            PackedTerrain = packedTerrain,
            ContinuityRuleActive = state.Continuity?.Active,
            PackedMapLayers = state.Geography is null || mapLayersUnchanged ? null : PackMapLayers(map, state.WorldSeed, fertility),
            MapLayersDigest = mapLayersDigest,
            Fields = (state.Fields ?? []).Select(field => new OwnerWorldFarmField(ToPosition(field.Position), field.HouseholdId,
                field.Stage.ToString().ToLowerInvariant(), field.Crop, fertility.At(field.Position),
                field.Work?.WorkerId, field.Work?.RemainingTicks)).ToArray(),
            Handcarts = ProjectHandcarts(state),
            Animals = ProjectAnimals(state),
            ConstructionSites = ProjectConstructionSites(state),
            Boats = ProjectBoats(state),
            BoatRequests = ProjectBoatRequests(state),
            GroundStocks = state.Society.Society.Inventory.Lots.Where(lot => lot.GroundPosition is not null && lot.Quantity > 0 &&
                lot.ItemKind != InventoryContainerRules.Handcart)
                .GroupBy(lot => (Position: lot.GroundPosition!.Value, lot.OwnerId, lot.ItemKind))
                .OrderBy(group => group.Key.Position.Y).ThenBy(group => group.Key.Position.X)
                .ThenBy(group => group.Key.ItemKind, StringComparer.Ordinal).ThenBy(group => group.Key.OwnerId, StringComparer.Ordinal)
                .Select(group => new OwnerWorldGroundStock(new(group.Key.Position.X, group.Key.Position.Y), group.Key.OwnerId,
                    group.Key.ItemKind, group.Sum(lot => lot.Quantity))
                {
                    OwnerName = MarketOwnerName(group.Key.OwnerId),
                }).ToArray(),
            WrapsEastWest = state.Geography?.WrapEastWest == true,
            Generation = state.Geography is { } geography
                ? new OwnerWorldGeneration(geography.Seed, geography.Size.ToString(), geography.ClimateMode.ToString(),
                    geography.SelectedClimate.ToString(), geography.LatitudeCooling, geography.WrapEastWest)
                : null,
            LastTickMilliseconds = diagnostics.LastTickMilliseconds,
            RefusedActionCount = state.RefusedActionCount,
            Agents = activeAgents
                .Select(agent => ToAgentView(state, agent, physicalById[agent.Id], map) with
                {
                    PlannedRoute = ToPlannedRoute(diagnostics.PlannedRoutes.GetValueOrDefault(agent.Id)),
                })
                .Concat(state.Society.Society.Agents
                    .Where(agent => agent.Status == SocietyAgentStatus.Dead && deceasedById.ContainsKey(agent.Id))
                    .Select(agent => ToDeceasedAgent(state, agent, deceasedById[agent.Id])))
                .OrderBy(agent => agent.Id, StringComparer.Ordinal)
                .ToArray(),
            Conversations = (state.Conversations ?? [])
                .Where(conversation => conversation.Status != AgentConversationStatus.Closed)
                .Concat((state.Conversations ?? [])
                    .Where(conversation => conversation.Status == AgentConversationStatus.Closed)
                    .OrderByDescending(conversation => conversation.LastUpdatedTick)
                    .ThenBy(conversation => conversation.Id, StringComparer.Ordinal)
                    .Take(16))
                .OrderByDescending(conversation => conversation.LastUpdatedTick)
                .ThenBy(conversation => conversation.Id, StringComparer.Ordinal)
                .Select(conversation => new OwnerWorldConversation(
                    conversation.Id,
                    conversation.InitiatorId,
                    agentsById.GetValueOrDefault(conversation.InitiatorId)?.Name ?? conversation.InitiatorId,
                    conversation.InviteeId,
                    agentsById.GetValueOrDefault(conversation.InviteeId)?.Name ?? conversation.InviteeId,
                    ConversationStatus(conversation.Status),
                    conversation.Interruption == AgentConversationInterruption.None
                        ? null : ConversationInterruption(conversation.Interruption),
                    conversation.Outcome,
                    conversation.CreatedTick,
                    conversation.LastUpdatedTick,
                    conversation.Turns.TakeLast(AgentConversationRules.MaximumPublicTurns +
                            AgentConversationRules.MaximumWrapUpTurns)
                        .Select(turn => new OwnerWorldConversationTurn(
                            turn.Id,
                            turn.SpeakerId,
                            agentsById.GetValueOrDefault(turn.SpeakerId)?.Name ?? turn.SpeakerId,
                            turn.Text,
                            turn.WorldTick,
                            turn.ListenerIds.Take(AgentConversationRules.MaximumListenersPerTurn).ToArray(),
                            turn.IsWrapUp, turn.SurnameChoice))
                        .ToArray())
                {
                    Kind = conversation.Kind == AgentConversationKind.MarriageSurname ? "marriage_surname" : "ordinary",
                    ChosenSurname = state.Marriages.FirstOrDefault(item => item.SurnameConversationId == conversation.Id)?.ChosenSurname,
                })
                .ToArray(),
            Stockpiles = state.Society.Society.Households.Select(household =>
                new OwnerWorldStockpile(household.Id, household.Name, InventoryFor(state, household.Id))).ToArray(),
            FoodStewardship = state.FoodStewardship is { } stewardship ? new OwnerWorldFoodStewardship(
                state.Society.Society.Agents.FirstOrDefault(person => person.Id == stewardship.StewardId)?.Name,
                stewardship.FoodPolicy, stewardship.Ballot?.Policy, stewardship.Ballot?.Approvals.Count ?? 0,
                stewardship.Ballot?.Rejections.Count ?? 0, stewardship.Ballot?.Electorate.Count ?? 0) : null,
            AgingOverrideRate = state.Society.Society.LifeClock?.Rate ?? 1,
            JevEnabled = state.JevEnabled ?? true,
            RoutineHelperProvider = state.RoutineHelper.Provider,
            RoutineHelperModel = state.RoutineHelper.Model,
            RoutineHelperCredentialSlotId = state.RoutineHelper.CredentialSlotId,
            FounderSetup = state.FounderSetup is { } setup
                ? new OwnerFounderSetup(WorldRuntime.RequiredFounders, setup.FounderIds.Count, setup.Started)
                {
                    RequiresWorldCreation = RequiresWorldCreation(state),
                    CanChooseTownSite = state.Geography is not null && !setup.Started && setup.FounderIds.Count == 0,
                    HasAcceptedTownSite = (state.Towns ?? []).Any(town => town.OriginSite is not null),
                    LastFounderId = !setup.Started && setup.FounderIds.Count > 0
                        ? setup.FounderIds[^1] : null,
                }
                : null,
            Towns = (state.Towns ?? []).OrderBy(item => item.Id, StringComparer.Ordinal)
                .Select(item => new OwnerWorldTown(item.Id, item.Name, item.FoundingState, item.FoundedTick,
                    item.ResidentIds.ToArray(), item.AssignedBuildingIds.ToArray(),
                    item.BorderTiles.OrderBy(point => point.Y).ThenBy(point => point.X)
                        .Select(ToPosition).ToArray())
                {
                    FallingApart = item.IsAbandoned && AbandonedForASeason(state, item.Id),
                    LandHearings = ProjectLandHearings(state, item),
                    LandHearingCount = item.LandRecords?.Cases.Count ?? 0,
                    NonLandCases = ProjectNonLandCases(state, item),
                    NonLandCaseCount = item.NonLandHearings.Cases.Count,
                    LandTransfers = ProjectLandTransfers(state, item),
                    LandTransferCount = item.LandRecords?.Transfers.Count ?? 0,
                    Government = item.Government is { } government ? ProjectGovernment(government,
                        id => agentsById.GetValueOrDefault(id)?.Name ?? id, state.Society.Society.WorldTick) : null,
                    Council = item.Council is { } civic ? new OwnerTownCouncil(
                        civic.Form, civic.Fallback, civic.Members.Select(id => agentsById.GetValueOrDefault(id)?.Name ?? id).ToArray(),
                        civic.TermEndTick, civic.RetryTick, civic.Candidates.Select(c =>
                            (agentsById.GetValueOrDefault(c.AgentId)?.Name ?? c.AgentId) + (c.FullTerm ? " (full term)" : " (only to fill the empty seat)")).ToArray(),
                        civic.Proposals.TakeLast(RecentCivicProposalLimit).Select(p => new OwnerCivicProposal(p.Id, p.Kind,
                            item.Government?.LawDrafts.SingleOrDefault(d => d.ProposalId == p.Id) is { } draft ? TownLawRules.VoteText(draft) :
                                p.Text + (p.LandClaimTiles is { } tiles ? " Exact tiles: " + TownLandClaimRules.DescribeTiles(tiles) + "." :
                                    p.Kind == "land_use" && state.HouseholdLandUseRequests?.SingleOrDefault(r => r.Id == p.SubjectId) is { } landRequest
                                        ? " Exact tiles: " + TownLandClaimRules.DescribeTiles(landRequest.Tiles) + ". " +
                                            LandRequestApprovalDetail(state, landRequest, landRequest.Tiles.Any(tile => TownLandRightsRules.IsDisputed(tile,
                                                state.HouseholdLandUseRights ?? [], state.HouseholdLandUseRequests ?? []))) : ""), p.Status,
                            p.Votes.Count(v => v.Yes), p.Votes.Count(v => !v.Yes), p.RequiredYes, p.DeadlineTick)
                        {
                            LandHearingRequest = p.LandHearingRequest is { } request ? ProjectLandHearingProposal(state, request) : null,
                            Project = p.Project is { } plan ? ProjectPlan(plan, p.AuthorId) : null,
                        }).ToArray(),
                        civic.Election is { } election ? ProjectElection(election) : null)
                    {
                        LatestElection = civic.ElectionHistory.Count > 0
                            ? ProjectElection(civic.ElectionHistory[^1]) : null,
                    } : null,
                    Projects = item.Projects.OrderBy(project => project.ApprovedTick)
                        .ThenBy(project => project.Id, StringComparer.Ordinal)
                        .Select(project => ProjectConstruction(item, project)).ToArray(),
                    Markets = item.Markets.OrderBy(market => market.Id, StringComparer.Ordinal)
                        .Select(ProjectMarket).ToArray(),
                })
                .ToArray(),
            TownLandTitles = (state.TownLandTitles ?? []).OrderBy(item => item.Id, StringComparer.Ordinal)
                .Select(item => new OwnerWorldLandTitle(item.Id, item.TownId,
                    item.Tiles.Select(ToPosition).ToArray(), item.RecordedTick)).ToArray(),
            HouseholdLandUseRights = (state.HouseholdLandUseRights ?? []).OrderBy(item => item.Id, StringComparer.Ordinal)
                .Select(item => new OwnerWorldHouseholdLandUseRight(item.Id, item.TownId, item.HouseholdId,
                    item.Tiles.Select(ToPosition).ToArray(), item.GrantedTick, item.GrantSource, item.AgreedEndTick))
                .ToArray(),
            HouseholdLandUseRequests = (state.HouseholdLandUseRequests ?? [])
                .Where(item => item.Status == "pending")
                .OrderBy(item => item.Id, StringComparer.Ordinal)
                .Select(item =>
                {
                    var unresolved = item.Tiles.Where(tile => !item.HearingResolutions.Any(receipt => receipt.Tiles.Contains(tile))).ToArray();
                    var claimants = unresolved.SelectMany(tile => TownLandRightsRules.ClaimantsAt(tile,
                            state.HouseholdLandUseRights ?? [], state.HouseholdLandUseRequests ?? []))
                        .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
                    var disputedTiles = unresolved.Where(tile => TownLandRightsRules.IsDisputed(tile,
                            state.HouseholdLandUseRights ?? [], state.HouseholdLandUseRequests ?? []))
                        .OrderBy(tile => tile.Y).ThenBy(tile => tile.X).ToArray();
                    return new OwnerWorldHouseholdLandUseRequest(item.Id, item.TownId, item.HouseholdId,
                        item.RequestedByAgentId, unresolved.Select(ToPosition).ToArray(), item.RequestedTick,
                        item.AgreedEndTick, disputedTiles.Length > 0, claimants,
                        disputedTiles.Select(ToPosition).ToArray())
                    {
                        ApprovalDetail = LandRequestApprovalDetail(state, item, disputedTiles.Length > 0),
                    };
                }).ToArray(),
            RoadTiles = (state.RoadTiles ?? []).OrderBy(point => point.Y).ThenBy(point => point.X)
                .Select(ToPosition).ToArray(),
            Bridges = (state.Bridges ?? []).OrderBy(item => item.Id, StringComparer.Ordinal)
                .Select(item => new OwnerWorldBridge(item.Id, item.Design, item.Trigger,
                    RiverBridgeRules.AxisOf(item) == BridgeAxis.EastWest ? "east_west" : "north_south",
                    item.Entrances.Select(ToPosition).ToArray(), item.Span.Select(ToPosition).ToArray(),
                    item.BuiltTick))
                .ToArray(),
            WeatherRegions = state.WorldSystems is { } weatherSystems
                ? CreateWeatherRegions(weatherSystems, map)
                : [],
            WeatherRegionSize = WeatherRules.RegionSize,
            DarknessBasisPoints = state.WorldSystems is { } daylightSystems
                ? DaylightRules.DarknessBasisPoints(daylightSystems)
                : null,
            CalendarPace = state.WorldSystems is { } worldSystems
                ? new OwnerWorldCalendarPace(worldSystems.Config.TicksPerDay, worldSystems.Config.DaysPerYear,
                    worldSystems.Config.SpringDays, worldSystems.Config.SummerDays,
                    worldSystems.Config.AutumnDays, worldSystems.Config.WinterDays,
                    worldSystems.Config.CalendarOffsetTicks)
                : null,
            Authoring = new OwnerWorldAuthoringState(
                state.Society.Society.IsPaused,
                state.Society.Society.RunEpoch,
                state.EventHistoryFloor + state.Events.Count,
                0,
                map.ManifestDigest,
                map.ManifestDigest,
                anchorWeather.ToString().ToLowerInvariant(),
                state.WorldSystems?.Climate.Season.ToString().ToLowerInvariant() ?? "spring",
                [])
            {
                EditTools = WorldMapEditRules.Choices.GroupBy(choice => choice.Operation).Select(group =>
                    new ClankerWorld.Contracts.OwnerWorldEditTool(group.Key, group.Key switch
                    { "set_terrain" => "Terrain", "place_resource" => "Resource", _ => "Object" },
                    group.Select(choice => new ClankerWorld.Contracts.OwnerWorldEditKind(choice.Value,
                        choice.DisplayName, choice.IsRenewable)).ToArray())).ToArray(),
            },
            Instructions = visibleInstructions
                .Select(instruction => new OwnerWorldInstruction(
                    instruction.InstructionId,
                    instruction.TargetAgentId,
                    ToWireValue(instruction.Kind),
                    instruction.Text,
                    completedInstructionIds.Contains(instruction.InstructionId)
                        ? "completed" : ToWireValue(instruction.State),
                    instruction.SubmittedTick,
                    instruction.RunEpoch,
                    instruction.SubmissionSequence,
                    instruction.ObservedTick,
                    instruction.ObserverReply,
                    instruction.Task is { } order ? new OwnerWorldOrderTask(
                        order.Action, order.Status, order.RequestedUnits, order.CompletedUnits,
                        order.ProgressUnit, order.RepeatUntilCancelled, order.TargetFoodKind,
                        order.TargetResourceId, order.TargetPosition?.X, order.TargetPosition?.Y,
                        order.BlockedReason, order.TargetAgentId, order.TargetMaterialKind, order.TargetEquipmentKind, order.TargetCropKind,
                        order.TargetOutputKind, order.TargetItemKind, order.TargetBuildingKind, order.TargetAnimalId,
                        order.TargetKnowledgeKind, order.TargetCartLotId,
                        order.TalkConversationId,
                        ConversationOrderStatus(state, order),
                        order.TalkOutcome, order.TargetKnowledgeArtifactId) : null))
                .ToArray(),
            Cognition = ToCognition(state),
            ContentPackages = state.Content?.Packages
                .OrderBy(package => package.Manifest.PackageId, StringComparer.Ordinal)
                .Select(package => new OwnerWorldContentPackage(
                    package.Manifest.PackageId,
                    package.Manifest.Version.ToString(),
                    package.Manifest.PackageDigest,
                    package.Lifecycle.ToString().ToLowerInvariant(),
                    package.LockDigest,
                    package.ValidationTick,
                    package.StagedTick,
                    package.ActivationTick,
                    package.ManifestDigest,
                    package.Manifest.Definitions.Count == 0 ? null : package.Manifest.Definitions[0].DisplayName,
                    state.Content.Events.LastOrDefault(item => item.PackageId == package.Manifest.PackageId &&
                        item.Kind == "package_proposed_by_agent")?.Detail))
                .ToArray() ?? [],
            ContentEvents = state.Content?.Events
                .OrderBy(item => item.EventId)
                .Select(item => new OwnerWorldContentGovernanceEvent(
                    item.EventId,
                    item.WorldTick,
                    item.PackageId,
                    item.Kind,
                    item.Detail))
                .ToArray() ?? [],
            WorldSystems = state.WorldSystems is { } systems
                ? new OwnerWorldSystemsSummary(
                    systems.Climate.Season.ToString().ToLowerInvariant(),
                    anchorWeather.ToString().ToLowerInvariant(),
                    systems.Ecology.Resources.Count,
                    systems.Chunks.Count,
                    state.WorldContent?.Buildings.Count ?? 0,
                    state.WorldContent?.Recipes.Count ?? 0,
                    state.WorldSimulation?.Buildings.Count ?? 0,
                    jobs.Length,
                    state.AssetReservations?.Reservations
                        .Select(item => item.NormalizedDigest)
                        .Distinct(StringComparer.Ordinal)
                        .Count() ?? 0,
                    state.AssetReservations is { } assetState
                        ? assetState.Reservations
                            .GroupBy(item => item.NormalizedDigest, StringComparer.Ordinal)
                            .Sum(group => group.First().DurableStorageBytes)
                        : 0,
                    state.AssetReservations is { } cacheState
                        ? cacheState.Reservations
                            .GroupBy(item => $"{item.NormalizedDigest}|{item.DecodeProfile}", StringComparer.Ordinal)
                            .Sum(group => group.First().DecodedCacheBytes)
                        : 0,
                    state.AssetReservations is { } gpuState
                        ? gpuState.Reservations
                            .GroupBy(item => $"{item.NormalizedDigest}|{item.DecodeProfile}", StringComparer.Ordinal)
                            .Sum(group => group.First().GpuBytes)
                        : 0,
                    state.AssetReservations?.Reservations.Sum(item => item.RenderUnits) ?? 0)
                : null,
            PlacedBuildings = state.WorldSimulation?.Buildings
                .OrderBy(item => item.InstanceId, StringComparer.Ordinal)
                .Select(item =>
                {
                    var definition = buildingDefinitions?.GetValueOrDefault(item.DefinitionId);
                    var width = item.Footprint?.Width ?? definition?.Width ?? 1;
                    var height = item.Footprint?.Height ?? definition?.Height ?? 1;
                    var residentCapacity = item.HouseholdId is { } residentHousehold &&
                        definition?.Tags.Contains("house", StringComparer.Ordinal) == true
                            ? HouseResidentCapacityRules.Calculate(
                                state.Society.Society.Agents.Where(person => person.HouseholdId == residentHousehold),
                                width, height)
                            : null;
                    return new OwnerWorldPlacedBuilding(
                    item.InstanceId,
                    item.DefinitionId,
                    ToPosition(item.Position),
                    item.PlacedTick,
                    definition?.DisplayName,
                    definition?.Tags,
                    width,
                    height,
                    item.TownId,
                    item.HouseholdId,
                    item.HouseholdId is { } householdId
                        ? InventoryFor(state, householdId, item.InstanceId)
                        : item.TownId is { } townId && buildingDefinitions?.GetValueOrDefault(item.DefinitionId)?
                            .Tags.Contains("warehouse", StringComparer.Ordinal) == true
                            ? InventoryFor(state, townId, item.InstanceId) : null,
                    item.Entrance is { } entrance ? ToPosition(entrance) : null,
                    buildingDefinitions?.GetValueOrDefault(item.DefinitionId) is { } storageDefinition
                        ? BuildingStorageRules.Capacity(storageDefinition, item) : null,
                    state.Society.Society.Inventory.Lots.Where(lot => lot.StorageBuildingId == item.InstanceId).Sum(lot => lot.Quantity),
                    item.Footprint?.Revision ?? 0,
                    (state.WorldSimulation.GuestInvitations ?? []).Where(invitation => invitation.HouseInstanceId == item.InstanceId && invitation.Active)
                        .Select(invitation => state.Society.Society.Agents.Single(person => person.Id == invitation.GuestId).Name).ToArray(),
                    (state.WorldSimulation.BuildingExpansions ?? []).LastOrDefault(job => job.BuildingInstanceId == item.InstanceId)?.State.ToString().ToLowerInvariant(),
                    (state.WorldSimulation.BuildingExpansions ?? []).LastOrDefault(job => job.BuildingInstanceId == item.InstanceId)?.Failure,
                    residentCapacity?.Limit,
                    residentCapacity?.ResidentCount ?? 0,
                    residentCapacity?.HasDominantFamily ?? false,
                    residentCapacity?.IsOvercrowded ?? false)
                    {
                        Trades = ShopTradesAt(state, item.InstanceId),
                        ToolMakingRequests = ToolMakingRequestsAt(state, item.InstanceId),
                        AllowsHouseholdOwner = definition?.Tags.Any(HouseholdBuildingKinds.IsKindTag) == true,
                        RecentStorageChanges = storageChanges.GetValueOrDefault(item.InstanceId) ?? [],
                        AvailableRecipes = recipesByBuilding?[item.DefinitionId]
                            .OrderBy(recipe => recipe.Name, StringComparer.Ordinal).ThenBy(recipe => recipe.Id, StringComparer.Ordinal).ToArray(),
                    };
                })
                .ToArray() ?? [],
            ProductionJobs = jobs
                .OrderBy(item => item.JobId, StringComparer.Ordinal)
                .Select(item => new OwnerWorldProductionJob(
                    item.JobId,
                    item.RecipeId,
                    item.BuildingInstanceId,
                    item.WorkerId,
                    item.StartedTick,
                    item.CompletionTick,
                    item.State.ToString().ToLowerInvariant())
                {
                    Recipe = productionRecipes?.GetValueOrDefault(item.RecipeId),
                    HeldInputs = item.InputReservationIds.Select(id => inventoryReservations.GetValueOrDefault(id))
                        .Where(reservation => reservation is { State: InventoryReservationState.Reserved or InventoryReservationState.PartiallyConsumed })
                        .Select(reservation => (Reservation: reservation!, Lot: inventoryLots.GetValueOrDefault(reservation!.LotId)))
                        .Where(input => input.Lot is not null)
                        .GroupBy(input => input.Lot!.ItemKind, StringComparer.Ordinal)
                        .OrderBy(group => group.Key, StringComparer.Ordinal)
                        .Select(group => new OwnerWorldMaterialQuantity(group.Key, group.Sum(input => input.Reservation.Quantity)))
                        .ToArray(),
                })
                .ToArray(),
        };
        return snapshot with
        {
            // Devices can retain different prefixes and coalesce model failures
            // differently. Refresh every potentially visible cached row.
            EventTexts = retainedEvents
                .Where(NeedsCurrentEventText)
                .ToDictionary(item => item.EventId, item => ToEvent(item, snapshot).Text),
        };
    }

    // Every open message stays visible. Closed messages are bounded to each
    // agent's newest few, whether or not a personal model heard them: an order
    // the game could not act on, or one done by local rules, still belongs on
    // the card. Orders and suggestions are bounded separately, so heard
    // suggestions never push the latest finished orders off the card. Newest
    // means latest submitted, the order the card reads them in.
    private static OwnerQueuedInstruction[] ProjectPrivateInstructions(
        WorldRuntimeState state,
        HashSet<string> completedInstructionIds) =>
        (state.Instructions ?? [])
            .GroupBy(instruction => (instruction.TargetAgentId, instruction.Kind))
            .SelectMany(group =>
            {
                var pending = group.Where(instruction => !completedInstructionIds.Contains(instruction.InstructionId));
                var recentClosed = group
                    .Where(instruction => completedInstructionIds.Contains(instruction.InstructionId))
                    .OrderByDescending(instruction => instruction.SubmissionSequence)
                    .Take(RecentClosedInstructionLimitPerAgent);
                return pending.Concat(recentClosed);
            })
            .OrderBy(instruction => instruction.SubmissionSequence)
            .ToArray();

    private static string ConversationStatus(AgentConversationStatus status) => status switch
    {
        AgentConversationStatus.Proposed => "proposed",
        AgentConversationStatus.Ready => "ready",
        AgentConversationStatus.AwaitingSpeaker => "awaiting_speaker",
        AgentConversationStatus.WrapUp => "wrap_up",
        AgentConversationStatus.Suspended => "suspended",
        AgentConversationStatus.Closed => "closed",
        _ => "unknown",
    };

    private static string ConversationInterruption(AgentConversationInterruption interruption) => ConversationSystem.ToWire(interruption);

    internal static OwnerWorldPackedTerrain PackTerrain(WorldMap map)
    {
        var bytes = new byte[checked(map.Width * map.Height)];
        foreach (var tile in map.Tiles)
            bytes[tile.Position.Y * map.Width + tile.Position.X] = TerrainCodes.Encode(tile.Terrain.ToString());
        return new OwnerWorldPackedTerrain(map.Width, map.Height, TerrainCodes.TerrainEncoding,
            Convert.ToBase64String(bytes));
    }

    internal static OwnerWorldPackedMapLayers? PackMapLayers(WorldMap map, string? worldSeed = null) =>
        PackMapLayers(map, worldSeed, cachedFertility: null);

    private static OwnerWorldPackedMapLayers? PackMapLayers(WorldMap map, string? worldSeed, LandFertility? cachedFertility)
    {
        if (map.ClimateZones is not { } climate || map.ElevationLevels is not { } elevation ||
            map.HydrologyKinds is not { } hydrology || map.SurfaceKinds is not { } surface ||
            map.VegetationKinds is not { } vegetation) return null;
        var fertility = cachedFertility ?? (worldSeed is null ? null : new LandFertility(map, worldSeed));
        return new OwnerWorldPackedMapLayers(map.Width, map.Height, TerrainCodes.LayersEncoding,
            Convert.ToBase64String(climate), Convert.ToBase64String(elevation),
            Convert.ToBase64String(hydrology), Convert.ToBase64String(surface),
            Convert.ToBase64String(vegetation))
        {
            Fertility = fertility is null ? null : Convert.ToBase64String(map.Tiles.OrderBy(tile => tile.Position.Y)
                .ThenBy(tile => tile.Position.X).Select(tile => checked((byte)fertility.At(tile.Position))).ToArray()),
        };
    }

    private static OwnerWeatherRegion[] CreateWeatherRegions(WorldSystemsState systems, WorldMap map)
    {
        if (map.Height <= WeatherRules.RegionSize)
            return [new OwnerWeatherRegion(0, 0,
                WeatherRules.At(systems, new GridPoint(0, 0), map.Height).ToString().ToLowerInvariant(),
                WeatherRules.SoilMoistureAt(systems, new GridPoint(0, 0), map.Height))];
        var columns = (map.Width + WeatherRules.RegionSize - 1) / WeatherRules.RegionSize;
        var rows = (map.Height + WeatherRules.RegionSize - 1) / WeatherRules.RegionSize;
        return Enumerable.Range(0, rows)
            .SelectMany(y => Enumerable.Range(0, columns).Select(x => new OwnerWeatherRegion(x, y,
                WeatherRules.At(systems, new GridPoint(x * WeatherRules.RegionSize,
                        y * WeatherRules.RegionSize), map.Height,
                    WeatherRules.RegionClimate(map, new GridPoint(x * WeatherRules.RegionSize,
                        y * WeatherRules.RegionSize))).ToString().ToLowerInvariant(),
                WeatherRules.SoilMoistureAt(systems,
                    new GridPoint(x * WeatherRules.RegionSize, y * WeatherRules.RegionSize), map.Height,
                    WeatherRules.RegionClimate(map, new GridPoint(x * WeatherRules.RegionSize,
                        y * WeatherRules.RegionSize))))))
            .ToArray();
    }

    private static OwnerWorldAgent ToAgentView(
        WorldRuntimeState state,
        SocietyAgent agent,
        AgentState physical, WorldMap travelMap)
    {
        // A handcart and the goods inside it stay with the cart, not in its owner's hands.
        var cartIds = state.Society.Society.Inventory.Lots.Where(lot => lot.ItemKind == InventoryContainerRules.Handcart)
            .Select(lot => lot.Id).ToHashSet(StringComparer.Ordinal);
        var inventory = state.Society.Society.Inventory.Lots.Where(lot =>
                PersonalEquipmentRules.IsPhysicallyCarried(state.Society.Society.Inventory, lot, agent.Id) && !cartIds.Contains(lot.Id))
            .GroupBy(lot => lot.ItemKind).Select(group => new OwnerWorldInventoryEntry(group.Key, group.Sum(lot => lot.Quantity))).ToArray();
        var route = DetermineWorldRoute(state, physical, inventory, travelMap);
        var perceived = KnownNearby(state.Map, physical.Position).ToArray();
        var known = KnownLocalTopology(physical.Position, perceived, route);
        var household = state.Society.Society.Households
            .FirstOrDefault(item => item.Id == agent.HouseholdId);
        var decisionFactors = new List<OwnerWorldDecisionFactor>
        {
            new("personality", physical.Personality),
            new("aspiration", physical.Aspiration),
            new("age-band", agent.AgeBand.ToString().ToLowerInvariant()),
            new(state.Society.Society.Config.DayLifecycle is null ? "age-years" : "age-days",
                state.Society.Society.AgeAt(agent, state.Society.Society.WorldTick).ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new("role", agent.CurrentRole.ToString().ToLowerInvariant()),
            new("household", household?.Name ?? "unhoused"),
            new("fullness", $"{physical.FullnessBasisPoints} basis points"),
            new("fullness-state", NeedStates.FullnessState(physical.FullnessBasisPoints)),
            new("warmth-state", physical.Survival is { } needs ? NeedStates.WarmthState(needs.WarmthBasisPoints) : string.Empty),
            new("illness-state", physical.Survival is { } health ? NeedStates.IllnessState(health.IllnessBasisPoints) : string.Empty),
        };
        var personalStored = state.Society.Society.Inventory.Lots.Where(lot => lot.OwnerId == agent.Id &&
            !PersonalEquipmentRules.IsPhysicallyCarried(state.Society.Society.Inventory, lot, agent.Id) &&
            !cartIds.Contains(lot.Id) && (lot.ContainerLotId is null || !cartIds.Contains(lot.ContainerLotId))).Sum(lot => lot.Quantity);
        var borrowed = state.Society.Society.Inventory.Lots.Where(lot => lot.CarrierId == agent.Id && lot.OwnerId != agent.Id).Sum(lot => lot.Quantity);
        decisionFactors.Add(new("personal-goods-awaiting-collection", $"{personalStored} units; ownership stays personal"));
        decisionFactors.Add(new("borrowed-goods", $"{borrowed} units; ownership stays with the lender"));
        if (physical.Departures?.Any(departure => departure.SharedProject is not null) == true)
            decisionFactors.Add(new("departed-household-work", "Previous work stays recorded with its original household."));
        var ownedBuildings = state.WorldSimulation!.Buildings.Where(building => building.HouseholdId == agent.HouseholdId && agent.HouseholdId is not null)
            .Select(building => building.InstanceId).ToHashSet(StringComparer.Ordinal);
        var pausedWork = state.WorldSimulation.ProductionJobs.Count(job => job.State == WorldProductionJobState.Paused && ownedBuildings.Contains(job.BuildingInstanceId)) +
            (state.WorldSimulation.BuildingExpansions ?? []).Count(job => job.State == WorldProductionJobState.Paused && ownedBuildings.Contains(job.BuildingInstanceId));
        if (pausedWork > 0) decisionFactors.Add(new("paused-household-work", $"{pausedWork} jobs; members can take over at the site when the committed inputs are available to them."));
        if (agent.PrimaryCaregiverId is { } primary)
            decisionFactors.Add(new("primary-caregiver", state.Society.Society.GetAgent(primary).Name));
        var dependents = SocietyRules.MovingCareGroup(state.Society.Society, agent.Id).Where(id => id != agent.Id)
            .Select(id => state.Society.Society.GetAgent(id).Name).ToArray();
        if (dependents.Length > 0) decisionFactors.Add(new("dependent-care", string.Join(", ", dependents)));
        var guardianNotes = GuardianCareNotes(state, agent, physical).ToArray();
        decisionFactors.AddRange(guardianNotes.Select(note => new OwnerWorldDecisionFactor("guardian-care", note)));
        if (HousingDetail(state, agent, physical.Housing) is { } housingDetail)
            decisionFactors.Add(new OwnerWorldDecisionFactor("housing", housingDetail));
        if (state.Knowledge?.WritingProjects.SingleOrDefault(project => project.ActorId == agent.Id) is { } writing)
        {
            var kind = writing.Kind.Replace('_', ' ');
            var action = writing.SourceArtifactId is not null ? "Copying" : writing.Kind == "field_map" ? "Drawing" : "Writing";
            decisionFactors.Add(new OwnerWorldDecisionFactor("knowledge-writing",
                $"{action} a {kind} · {writing.WorkDone}/{writing.WorkRequired}"));
        }
        if (TownMembershipText.Describe(state.Towns ?? [], state.Society.Society, agent.Id,
                state.WorldSystems!.Config.TicksPerDay,
                TownMembershipText.TownsWithWarehouse(state.WorldSimulation, state.WorldContent!),
                calendarOffsetTicks: state.WorldSystems.Config.CalendarOffsetTicks) is { } townMembership)
            decisionFactors.Add(new OwnerWorldDecisionFactor("town-membership", townMembership)
            {
                AcceptanceDeadlineTick = TownMembershipText.AcceptanceDeadline(state.Towns ?? [],
                    state.Society.Society, agent.Id, state.WorldSystems!.Config.TicksPerDay),
            });
        decisionFactors.AddRange(IdentityMomentFactors(physical));
        if (physical.ChildModelSelection is { Provider: { } birthProvider } birthModel)
        {
            decisionFactors.Add(new OwnerWorldDecisionFactor("birth-model-provider", birthProvider));
            if (birthModel.ModelId is { } modelId)
                decisionFactors.Add(new OwnerWorldDecisionFactor("birth-model-id", modelId));
        }
        var runtime = state.Society.Cognition.Runtimes
            .FirstOrDefault(item => item.AgentId == agent.Id);
        var modelStatus = physical.LastModelAttempt?.Status ?? "ready";
        if (modelStatus == "waiting" && state.Society.Society.IsPaused) modelStatus = "canceled";
        decisionFactors.Add(new OwnerWorldDecisionFactor("model-status", modelStatus));
        if (physical.LastModelAttempt?.LastAcceptedCandidateId is { } acceptedCandidate)
        {
            decisionFactors.Add(new OwnerWorldDecisionFactor("last-model-choice", acceptedCandidate));
            decisionFactors.Add(new OwnerWorldDecisionFactor("last-model-activity", GameUiText.ActivityPhrase(acceptedCandidate, null)));
        }
        if (physical.LastModelAttempt?.SetupBlocker is { } setupBlocker)
            decisionFactors.Add(new OwnerWorldDecisionFactor("model-setup-blocker", setupBlocker));
        if (state.Society.Cognition.Queue.Any(item => item.AgentId == agent.Id))
            decisionFactors.Add(new OwnerWorldDecisionFactor("decision-pending", "true"));
        if (runtime?.CurrentIntention is { } intention)
        {
            decisionFactors.Add(new OwnerWorldDecisionFactor("current-intention", intention.CandidateId));
            decisionFactors.Add(new OwnerWorldDecisionFactor("intention-provider", intention.Provider.ToString().ToLowerInvariant()));
        }

        return new OwnerWorldAgent(
            agent.Id,
            agent.Name,
            agent.Status.ToString().ToLowerInvariant(),
            ToPosition(physical.Position),
            physical.FullnessBasisPoints,
            inventory,
            decisionFactors,
            route,
            new OwnerWorldSpatialKnowledge(ToPosition(physical.Position), perceived, known),
            IsDraft: false)
        {
            PublicIntention = runtime?.CurrentIntention is { } publicIntention
                ? ToPublicIntention(publicIntention.CandidateId, publicIntention.Provider.ToString().ToLowerInvariant(), publicIntention.WorldTick,
                    OrderExplorationPurpose(state, physical))
                : null,
            Relationships = RelationshipsFor(state, agent.Id),
            RecentPrivateThoughts = (physical.RecentThoughts ?? [])
                .Select(thought => new OwnerWorldPrivateThought(thought.WorldTick, thought.Text)).ToArray(),
            RecentMemories = MemoriesFor(state, agent.Id),
            RecentBeliefs = BeliefsFor(state, agent.Id),
            RecentKnowledgeFacts = KnowledgeFactsFor(state, agent.Id),
            KnownRecipes = RecipesFor(state, agent.Id),
            KnowledgeArtifacts = KnowledgeArtifactsFor(state, agent.Id),
            Project = physical.Project is { } project
                ? new OwnerWorldProject(project.Label, project.Stage, project.WorkDone, 10, project.Blocker, project.StartedTick)
                : null,
            Survival = physical.Survival is { } survival
                ? new OwnerWorldSurvival(survival.WarmthBasisPoints, survival.IllnessBasisPoints,
                    PersonalEquipmentRules.EquippedUnit(state.Society.Society.Inventory, agent.Id, physical.Equipment?.ClothingLotId) is
                    { ConditionBasisPoints: > 0 },
                    inventory.Any(item => item.Kind == "tool" && item.Quantity > 0), survival.NutritionBasisPoints, survival.LastMealKind) : null,
            Equipment = EquipmentFor(state, physical),
            MedicalCareNote = agent.Status == SocietyAgentStatus.Active ? MedicalCareRules.Note(physical) : null,
            ToolMakingRequestNote = agent.Status == SocietyAgentStatus.Active
                ? ToolMakingRequestRules.Note(state.ToolMakingRequests ?? [], agent.Id, agent.HouseholdId) : null,
            Lesson = physical.Lesson is { } lesson ? new OwnerWorldLesson(
                state.Society.Society.GetAgent(lesson.TeacherId).Name, lesson.Skill.ToString().ToLowerInvariant(),
                lesson.Stage, lesson.Progress, 20) : null,
            Proficiency = physical.Proficiency is { } practice
                ? new OwnerWorldProficiency(practice.Building, practice.Farming, practice.Crafting) : null,
            Skills = ProjectSkills(physical, state.Society.Society),
            SocialStanding = SocialStandingFor(state, agent.Id, physical),
            SocialNotes = state.Society.Society.Inventory.Offers.Where(offer => offer.State == DirectBarterState.Open &&
                    (offer.FirstPartyId == agent.Id || offer.SecondPartyId == agent.Id))
                .Select(offer => offer.AcceptedBy.Contains(agent.Id, StringComparer.Ordinal)
                    ? "Waiting for the other agent to accept or decline an exchange."
                    : "An exchange is on offer; they haven't accepted or refused it yet.")
                .Concat(ShopTradeNotes(state, agent))
                .Concat(MarriageNotes(state, agent.Id))
                .Concat(state.Agents.Where(person => person.Parenthood is { } plan &&
                    (person.AgentId == agent.Id || plan.PartnerId == agent.Id)).Select(person =>
                    person.Parenthood!.Stage == "preparing" ? ContinuityPlanDue(state, person.AgentId, person.Parenthood.PartnerId)
                        ? "Preparing for parenthood under the continuity rule. " +
                            WorldRuntime.ParenthoodFoodNote(state.Society.Society, person.Parenthood.PrimaryCaregiverId!, state.Towns ?? [])
                        : "Preparing for parenthood. " +
                            WorldRuntime.ParenthoodFoodNote(state.Society.Society, person.Parenthood.PrimaryCaregiverId!, state.Towns ?? [])
                    : person.Parenthood.Stage == "requested" ? "Parenthood proposed; waiting for a separate decision."
                    : person.Parenthood.Stage == "postponed" ? "Parenthood put off for now."
                    : person.Parenthood.Stage == "completed" ? "Caring for a child in the household." : "Parenthood plan withdrawn."))
                .Concat(guardianNotes)
                .Concat(HousingRequestNotes(state, agent))
                .ToArray(),
        };
    }

    /// <summary>The continuity rule has sent this couple's plan ahead; their two days of "not yet" are up.</summary>
    private static bool ContinuityPlanDue(WorldRuntimeState state, string owner, string partner) =>
        state.Continuity?.Couples.Any(couple => couple.DeadlineTick <= state.Society.Society.WorldTick &&
            (couple.FirstPartnerId == owner && couple.SecondPartnerId == partner ||
             couple.FirstPartnerId == partner && couple.SecondPartnerId == owner)) == true;

    private static OwnerWorldToolMakingRequest[] ToolMakingRequestsAt(WorldRuntimeState state, string building) =>
        (state.ToolMakingRequests ?? []).Where(request => request.BuildingInstanceId == building)
            .OrderBy(request => ToolMakingRequestRules.IsTerminal(request.Status))
            .ThenByDescending(request => request.LastTransitionTick).ThenBy(request => request.Id, StringComparer.Ordinal).Take(8)
            .Select(request => new OwnerWorldToolMakingRequest(request.Id,
                state.Society.Society.GetAgent(request.RequesterId).Name, request.RecipeId,
                state.WorldContent?.Recipes.FirstOrDefault(recipe => recipe.CanonicalId == request.RecipeId)?.DisplayName
                    ?? request.ItemKind.Replace('_', ' '),
                request.ItemKind, request.Status.ToString().ToLowerInvariant(), request.Blocker, request.OfferId)).ToArray();
    private static OwnerWorldShopTrade[] ShopTradesAt(WorldRuntimeState state, string buildingId) =>
        (state.ShopTrades ?? []).Where(trade => trade.BuildingInstanceId == buildingId)
            .OrderByDescending(trade => state.Society.Society.Inventory.GetOffer(trade.OfferId).State == DirectBarterState.Open)
            .ThenByDescending(trade => trade.ProposedTick).ThenBy(trade => trade.OfferId, StringComparer.Ordinal)
            .Take(8).Select(trade =>
            {
                var offer = state.Society.Society.Inventory.GetOffer(trade.OfferId);
                return new OwnerWorldShopTrade(trade.OfferId, state.Society.Society.GetAgent(trade.BuyerId).Name,
                    trade.GoodsKind, offer.FirstQuantity, trade.PaymentKind, offer.SecondQuantity,
                    offer.State.ToString().ToLowerInvariant(), trade.CancellationReason);
            }).ToArray();

    private static IEnumerable<string> ShopTradeNotes(WorldRuntimeState state, SocietyAgent person)
    {
        foreach (var trade in (state.ShopTrades ?? []).Where(trade =>
                     trade.BuyerId == person.Id || trade.SellerHouseholdId == person.HouseholdId)
                     .OrderByDescending(trade => state.Society.Society.Inventory.GetOffer(trade.OfferId).State == DirectBarterState.Open)
                     .ThenByDescending(trade => trade.ProposedTick).ThenBy(trade => trade.OfferId, StringComparer.Ordinal).Take(4))
        {
            var offer = state.Society.Society.Inventory.GetOffer(trade.OfferId);
            var building = state.WorldSimulation?.Buildings.FirstOrDefault(item => item.InstanceId == trade.BuildingInstanceId);
            var name = state.WorldContent?.Buildings.FirstOrDefault(item => item.CanonicalId == building?.DefinitionId)?.DisplayName ?? "shop";
            var terms = $"{offer.FirstQuantity} {trade.GoodsKind.Replace('_', ' ')} for {offer.SecondQuantity} {trade.PaymentKind.Replace('_', ' ')}";
            yield return offer.State switch
            {
                DirectBarterState.Open => $"Exchange at the {name}: {terms}. Both traders must meet there; goods are set aside until then.",
                DirectBarterState.Settled => $"Bought at the {name}: {terms}. The buyer carries the purchase; payment is stored at the shop.",
                _ => $"Exchange at the {name} cancelled: {trade.CancellationReason}",
            };
        }
    }

    /// <summary>Accepted care and a pending move are separate from needing a guardian.</summary>
    private static IEnumerable<string> GuardianCareNotes(
        WorldRuntimeState state, SocietyAgent agent, AgentState physical)
    {
        if (physical.GuardianSearch is not null)
            yield return "Needs a guardian. No adult has accepted care yet; nearby adults may still feed them.";
        else if (agent.AgeBand is SocietyAgeBand.Infant or SocietyAgeBand.Child or SocietyAgeBand.Adolescent &&
            !state.Society.Society.Relationships.Any(edge => edge.Type == SocietyRelationshipType.Caregiver &&
                edge.State == SocietyRelationshipState.Accepted && edge.TargetId == agent.Id &&
                state.Society.Society.GetAgent(edge.ProposerId).Status == SocietyAgentStatus.Active))
            yield return "No active caregiver; household adults may offer support.";

        foreach (var child in state.Agents.OrderBy(person => person.AgentId, StringComparer.Ordinal))
        {
            if (child.GuardianPlacement is not { } placement ||
                child.AgentId != agent.Id && placement.CaregiverId != agent.Id)
                continue;
            var guardianName = state.Society.Society.GetAgent(placement.CaregiverId).Name;
            var childName = state.Society.Society.GetAgent(child.AgentId).Name;
            var hasBlocker = !string.IsNullOrWhiteSpace(placement.Blocker);
            var progress = hasBlocker ? "The move is waiting."
                : placement.HouseId is null ? "Waiting for a suitable home."
                : placement.Stage == "escorting" ? $"They are travelling together to {guardianName}'s House."
                : $"{guardianName} is going to collect {childName}.";
            var blocker = !hasBlocker
                ? string.Empty : " " + placement.Blocker!.TrimEnd('.') + ".";
            yield return $"{guardianName} accepted care for {childName}. {progress}{blocker}";
        }
    }

    /// <summary>The current housing need, including overcrowding and a saved move-out notice.</summary>
    private static string? HousingDetail(WorldRuntimeState state, SocietyAgent agent,
        HousingState? housing)
    {
        if (housing is not null && (housing.Relocation is not null || housing.Blocker == HousingBlockers.Overcrowded))
            return OvercrowdedHousingDetail(state, agent, housing);
        if (housing?.Blocker is not { } blocker)
            return null;
        var asked = housing.Request is { } request
            ? state.Society.Society.Households.FirstOrDefault(item => item.Id == request.HouseholdId)?.Name ?? "another"
            : "another";
        return blocker switch
        {
            HousingBlockers.AwaitingAnswer => $"No home yet. Asked the {asked} household to live in their House; every adult member must agree.",
            HousingBlockers.NoHousehold => "No home. Looking first for a household that will take them and has room for everyone in their care; otherwise they start a household and build a House.",
            HousingBlockers.NoAuthorizedHome => "No home. The household holds no House yet and can plan one.",
            HousingBlockers.MissingMaterials => "No home. The household holds no House and lacks the materials to build one.",
            HousingBlockers.NoLegalSite => "No home. The household has the materials for a House but no legal site to build it. " +
                HousingBlockers.LandRecoveryGuidance,
            _ => null,
        };
    }

    private static string OvercrowdedHousingDetail(WorldRuntimeState state, SocietyAgent agent,
        HousingState housing)
    {
        var society = state.Society.Society;
        var house = state.WorldSimulation!.Buildings.FirstOrDefault(building =>
            agent.HouseholdId is not null && building.HouseholdId == agent.HouseholdId &&
            state.WorldContent!.Buildings.Any(definition => definition.CanonicalId == building.DefinitionId &&
                definition.Tags.Contains("house", StringComparer.Ordinal)));
        var counts = "Housing need.";
        var construction = string.Empty;
        if (house is not null)
        {
            var definition = state.WorldContent!.Buildings.Single(item => item.CanonicalId == house.DefinitionId);
            var capacity = HouseResidentCapacityRules.Calculate(society.Agents.Where(person =>
                    person.HouseholdId == agent.HouseholdId),
                house.Footprint?.Width ?? definition.Width, house.Footprint?.Height ?? definition.Height);
            counts = $"{(capacity.IsOvercrowded ? "House overcrowded" : "House")}: {capacity.ResidentCount} residents, {capacity.Limit} places.";
            construction = (state.WorldSimulation.BuildingExpansions ?? [])
                .LastOrDefault(job => job.BuildingInstanceId == house.InstanceId)?.State switch
            {
                WorldProductionJobState.Running => " Expansion is under way; it adds places only when finished.",
                WorldProductionJobState.Paused => " Expansion is paused; it adds places only when finished.",
                WorldProductionJobState.Cancelled => " The last expansion was cancelled.",
                _ => string.Empty,
            };
        }
        if (housing.Relocation is { } notice)
        {
            var hours = (long)Math.Ceiling(Math.Max(0, notice.DeadlineTick - society.WorldTick) *
                24d / state.WorldSystems!.Config.TicksPerDay);
            var reason = notice.Reason switch
            {
                HouseRelocationRules.Volunteer => "Volunteered to move",
                HouseRelocationRules.LatestUnrelatedArrival => "Notice issued as the most recent arrival outside the main family",
                _ => "Notice issued as the most recent arrival when no family had a majority",
            };
            var next = housing.Request is { } request
                ? $" Asked the {society.Households.FirstOrDefault(item => item.Id == request.HouseholdId)?.Name ?? "other"} household; every adult member must agree."
                : " Seek an accepting household with room or start a household and build a House.";
            return $"{counts} Move-out notice: about {hours} world {(hours == 1 ? "hour" : "hours")} left. {reason}." +
                next + construction;
        }
        var notices = state.Agents.Count(person => person.Housing?.Relocation is { } relocation &&
            relocation.HouseholdId == agent.HouseholdId);
        return counts + (notices > 0
            ? $" {notices} {(notices == 1 ? "adult has" : "adults have")} notice to move out; completed expansion may let them stay."
            : " Nobody has notice to move out. The household can expand the House, or someone can choose to move out while keeping children with their carers.") + construction;
    }

    /// <summary>Requests to live in this adult's House that they must answer, or have answered.</summary>
    private static IEnumerable<string> HousingRequestNotes(WorldRuntimeState state, SocietyAgent member)
    {
        if (member.HouseholdId is null)
            yield break;
        foreach (var applicant in state.Agents.OrderBy(person => person.AgentId, StringComparer.Ordinal))
        {
            if (applicant.Housing?.Request is not { } request || request.HouseholdId != member.HouseholdId ||
                !request.Members.Contains(member.Id, StringComparer.Ordinal))
                continue;
            var name = state.Society.Society.GetAgent(applicant.AgentId).Name;
            yield return request.Approvals.Contains(member.Id, StringComparer.Ordinal)
                ? $"Agreed to let {name} live in the House; waiting for the other adults."
                : request.Rejections.Contains(member.Id, StringComparer.Ordinal)
                    ? $"Refused {name}'s request to live in the House."
                    : $"{name} asked to live in the household's House. Every adult member must answer.";
        }
    }

    private static OwnerWorldSkill[] ProjectSkills(AgentState physical, SocietyCheckpoint society) =>
        (physical.Skills ?? []).Select(skill => new OwnerWorldSkill(skill.Kind.ToString().ToLowerInvariant(),
            skill.LearnedTick, skill.TeacherId,
            skill.TeacherId is { } teacher ? society.GetAgent(teacher).Name : null)).ToArray();

    private static OwnerWorldAgent ToDeceasedAgent(
        WorldRuntimeState state,
        SocietyAgent agent,
        DeceasedAgentState archived)
    {
        var lastPhysical = archived.LastPhysical;
        var position = ToPosition(lastPhysical.Position);
        var estate = state.Society.Society.Estates.FirstOrDefault(item => item.DeceasedId == agent.Id);
        return new OwnerWorldAgent(
            agent.Id,
            agent.Name,
            "dead",
            position,
            lastPhysical.FullnessBasisPoints,
            [],
            new OwnerWorldDecisionFactor[]
            {
                new("personality", lastPhysical.Personality),
                new("aspiration", lastPhysical.Aspiration),
                new("age-band", agent.AgeBand.ToString().ToLowerInvariant()),
                new(state.Society.Society.Config.DayLifecycle is null ? "age-years" : "age-days",
                    archived.AgeAtDeath.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new("role", agent.CurrentRole.ToString().ToLowerInvariant()),
                new("death-tick", archived.DeathTick.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new("death-cause", agent.DeathCause?.ToString().ToLowerInvariant() ?? "unknown"),
                new("will-status", estate?.WillStatus ?? "not_requested"),
            }.Concat(IdentityMomentFactors(lastPhysical)).ToArray(),
            new OwnerWorldRoute("deceased", null, null, [], string.Empty),
            new OwnerWorldSpatialKnowledge(position, [position], [position]),
            IsDraft: false)
        {
            Relationships = RelationshipsFor(state, agent.Id),
            RecentPrivateThoughts = (lastPhysical.RecentThoughts ?? [])
                .Select(thought => new OwnerWorldPrivateThought(thought.WorldTick, thought.Text)).ToArray(),
            RecentMemories = MemoriesFor(state, agent.Id),
            RecentBeliefs = BeliefsFor(state, agent.Id),
            RecentKnowledgeFacts = KnowledgeFactsFor(state, agent.Id),
            KnownRecipes = RecipesFor(state, agent.Id),
            KnowledgeArtifacts = KnowledgeArtifactsFor(state, agent.Id),
            Proficiency = lastPhysical.Proficiency is { } practice
                ? new OwnerWorldProficiency(practice.Building, practice.Farming, practice.Crafting) : null,
            Skills = ProjectSkills(lastPhysical, state.Society.Society),
            SocialStanding = SocialStandingFor(state, agent.Id, lastPhysical),
            SocialNotes = MarriageNotes(state, agent.Id).ToArray(),
            FinalWill = estate is { WillStatus: { } status } ? FinalWillFor(state, estate, status) : null,
        };
    }

    /// <summary>The will as written: each named heir's exact goods, not later fallbacks.</summary>
    private static OwnerWorldFinalWill FinalWillFor(WorldRuntimeState state, SocietyEstate estate, string status)
    {
        var frozen = (estate.FrozenLots ?? []).ToDictionary(item => item.LotId, StringComparer.Ordinal);
        var heirs = (estate.WillHeirIds ?? []).Select(heirId =>
        {
            var town = (state.Towns ?? []).FirstOrDefault(item => item.Id == heirId);
            var items = (estate.WillBequests ?? []).Where(item => item.HeirId == heirId && frozen.ContainsKey(item.LotId))
                .GroupBy(item => frozen[item.LotId].ItemKind, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new OwnerWorldInventoryEntry(group.Key, group.Sum(item => item.Quantity)))
                .ToArray();
            return new OwnerWorldWillHeir(heirId,
                town?.Name ?? state.Society.Society.Agents.FirstOrDefault(item => item.Id == heirId)?.Name ?? heirId,
                town is not null, items);
        }).ToArray();
        return new OwnerWorldFinalWill(status, estate.WillSplit, heirs, estate.FinalWords);
    }

    private static OwnerWorldAgentMemory[] MemoriesFor(WorldRuntimeState state, string ownerId) =>
        (state.Society.Society.GetAgent(ownerId).Status == SocietyAgentStatus.Dead
            ? state.Society.Society.AllMemories() : state.Society.Society.Memories)
            .Where(memory => memory.OwnerId == ownerId && memory.TombstonedTick is null)
            .OrderByDescending(memory => memory.Permanent)
            .ThenByDescending(memory => memory.SourceTick)
            .ThenByDescending(memory => memory.Permanent ? memory.Id : string.Empty, StringComparer.Ordinal)
            .ThenBy(memory => memory.Id, StringComparer.Ordinal)
            .Take(16)
            .Select(memory => new OwnerWorldAgentMemory(
                memory.SourceTick,
                memory.SubjectId,
                state.Society.Society.Agents.FirstOrDefault(person => person.Id == memory.SubjectId)?.Name ?? memory.SubjectId,
                memory.Summary,
                memory.Visibility,
                memory.Permanent))
            .ToArray();

    private static OwnerWorldAgentBelief[] BeliefsFor(WorldRuntimeState state, string ownerId) =>
        (state.Society.Society.GetAgent(ownerId).Status == SocietyAgentStatus.Dead
            ? state.Society.Society.AllBeliefs() : state.Society.Society.Beliefs ?? [])
            .Where(belief => belief.OwnerId == ownerId)
            .OrderByDescending(belief => belief.FormedTick)
            .ThenBy(belief => belief.Id, StringComparer.Ordinal)
            .Take(16)
            .Select(belief => new OwnerWorldAgentBelief(
                belief.FormedTick,
                belief.Statement,
                belief.Provenance.ToString().ToLowerInvariant(),
                belief.ConfidenceBasisPoints,
                belief.SourceAgentId,
                belief.SourceAgentId is { } sourceId
                    ? state.Society.Society.Agents.FirstOrDefault(person => person.Id == sourceId)?.Name ?? sourceId
                    : null,
                belief.SourceEventId,
                belief.AboutAgentId,
                belief.SupersededByBeliefId is not null,
                belief.SupersededTick))
            .ToArray();

    private static OwnerWorldKnowledgeFact[] KnowledgeFactsFor(WorldRuntimeState state, string ownerId)
    {
        var names = state.Society.Society.Agents.ToDictionary(item => item.Id, item => item.Name, StringComparer.Ordinal);
        return (state.Knowledge?.Facts ?? []).Where(fact => fact.OwnerId == ownerId)
            .OrderByDescending(fact => fact.LearnedTick)
            .ThenBy(fact => fact.Id, StringComparer.Ordinal)
            .Take(16)
            .Select(fact => new OwnerWorldKnowledgeFact(
                fact.LearnedTick,
                fact.Position.X,
                fact.Position.Y,
                fact.Terrain,
                fact.ResourceKinds,
                names.GetValueOrDefault(fact.DiscovererId, fact.DiscovererId),
                fact.Acquisition,
                fact.SourceAgentId is { } sourceId ? names.GetValueOrDefault(sourceId, sourceId) : null))
            .ToArray();
    }

    private static OwnerWorldRecipe[] RecipesFor(WorldRuntimeState state, string ownerId) =>
        (state.Knowledge?.Recipes ?? []).Where(item => item.OwnerId == ownerId)
            .OrderByDescending(item => item.LearnedTick).ThenBy(item => item.RecipeId, StringComparer.Ordinal).Take(16)
            .Select(item => new OwnerWorldRecipe(item.LearnedTick,
                state.WorldContent!.Recipes.Single(recipe => recipe.CanonicalId == item.RecipeId).DisplayName, item.Acquisition,
                item.SourceAgentId is { } source ? state.Society.Society.GetAgent(source).Name : null)).ToArray();

    private static OwnerWorldKnowledgeArtifact[] KnowledgeArtifactsFor(WorldRuntimeState state, string ownerId)
    {
        var names = state.Society.Society.Agents.ToDictionary(item => item.Id, item => item.Name, StringComparer.Ordinal);
        var heldLotIds = state.Society.Society.Inventory.Lots.Where(lot => lot.OwnerId == ownerId)
            .Select(lot => lot.Id).ToHashSet(StringComparer.Ordinal);
        return (state.Knowledge?.Artifacts ?? []).Where(artifact => heldLotIds.Contains(artifact.LotId))
            .OrderByDescending(artifact => artifact.CreatedTick)
            .ThenBy(artifact => artifact.Id, StringComparer.Ordinal)
            .Take(AgentKnowledgeArtifactLimit)
            .Select(artifact => new OwnerWorldKnowledgeArtifact(
                artifact.Id,
                artifact.Kind,
                artifact.Title,
                artifact.CreatedTick,
                names.GetValueOrDefault(artifact.CreatorId, artifact.CreatorId),
                artifact.Facts.Select(fact => new OwnerWorldKnowledgeSite(
                    fact.Position.X,
                    fact.Position.Y,
                    fact.Terrain,
                    fact.ResourceKinds,
                    names.GetValueOrDefault(fact.DiscovererId, fact.DiscovererId))).ToArray())
            {
                RecipeNames = artifact.Recipes.Select(item => state.WorldContent!.Recipes.Single(recipe => recipe.CanonicalId == item.RecipeId).DisplayName).ToArray(),
            })
            .ToArray();
    }

    private static OwnerWorldSocialStanding[] SocialStandingFor(
        WorldRuntimeState state,
        string ownerId,
        AgentState physical)
    {
        var saved = (physical.SocialStanding ?? []).ToDictionary(item => item.SubjectId, item => item.Trust, StringComparer.Ordinal);
        return state.Society.Society.Agents.Where(subject => subject.Id != ownerId)
            .Select(subject => new OwnerWorldSocialStanding(subject.Id, subject.Name,
                saved.GetValueOrDefault(subject.Id, LegacyTrustScore(state, ownerId, subject.Id))))
            .Where(item => item.Trust > 0)
            .OrderByDescending(item => item.Trust).ThenBy(item => item.SubjectId, StringComparer.Ordinal).ToArray();
    }

    private static int LegacyTrustScore(WorldRuntimeState state, string ownerId, string subjectId) =>
        Math.Min(10, state.Society.Society.Memories.Where(memory => memory.OwnerId == ownerId &&
                memory.SubjectId == subjectId && memory.TombstonedTick is null)
            .Sum(memory => memory.Id.Value.StartsWith("project-gratitude:", StringComparison.Ordinal) ? 2
                : memory.Id.Value.StartsWith("lesson-gratitude:", StringComparison.Ordinal) ? 2
                : memory.Id.Value.StartsWith("trust-memory:", StringComparison.Ordinal) ? 1 : 0));

    private static OwnerWorldAgentRelationship[] RelationshipsFor(
        WorldRuntimeState state,
        string agentId) => state.Society.Society.Relationships
        .Where(relationship =>
            (relationship.ProposerId == agentId || relationship.TargetId == agentId) &&
            relationship.State is SocietyRelationshipState.Proposed or SocietyRelationshipState.Accepted or SocietyRelationshipState.EndedByDeath)
        .OrderBy(relationship => relationship.Type)
        .ThenBy(relationship => relationship.Id, StringComparer.Ordinal)
        .Select(relationship => new OwnerWorldAgentRelationship(
            relationship.Id,
            relationship.ProposerId == agentId ? relationship.TargetId : relationship.ProposerId,
            ToWireValue(relationship.Type),
            ToWireValue(relationship.State),
            relationship.PrivacyClass,
            relationship.EffectiveTick,
            relationship.Type == SocietyRelationshipType.BiologicalParentage
                ? relationship.ProposerId == agentId ? "parent" : "child"
                : relationship.Type == SocietyRelationshipType.Partnership ? "partner" : null))
        .ToArray();

    private static IEnumerable<string> MarriageNotes(WorldRuntimeState state, string agentId) =>
        state.Marriages.Where(marriage => AgentMarriageRules.HasParticipant(marriage, agentId))
            .Select(marriage => AgentMarriageRules.Note(marriage, agentId, state.Society.Society));

    private static string? OrderExplorationPurpose(WorldRuntimeState state,
        AgentState physical)
    {
        if (physical.Exploration is not
            { Returning: false, OutingPath.Count: > 0, Goal: { Kind: "resource", OrderInstructionId: { } id } goal } exploration ||
            exploration.OutingPath[^1] != physical.Position)
            return null;
        var instruction = WorldRuntime.FirstActiveOrderFor(state.Instructions ?? [], physical.AgentId);
        if (instruction is null || instruction.InstructionId != id || instruction.Task is not
            { Status: "doing", TargetResourceId: null, TargetPosition: null } order)
            return null;
        var target = order.Action == "gather_material" ? order.TargetMaterialKind
            : order.Action is "seek_food" or "harvest_food" ? order.TargetFoodKind ?? "food" : null;
        return goal.Target == target ? "looking for " + goal.Target.Replace('_', ' ') : null;
    }

    private static OwnerWorldPublicIntention ToPublicIntention(
        string candidateId,
        string provider,
        long worldTick,
        string? purposeSummary = null) => new(
        candidateId,
        GameUiText.ActivityPhrase(candidateId, purposeSummary ?? PublicIntentionSummary(candidateId)),
        provider,
        worldTick);

    private static string PublicIntentionSummary(string candidateId) => candidateId switch
    {
        "seek_food" => "looking for food",
        "move_to" => "walking to the ordered tile",
        "boat_order" => "traveling to the ordered Port by boat",
        "harvest_food" => "gathering food",
        "gather_material" => "gathering the ordered material",
        "work_field" => "working on a household field",
        "repair_tool" => "repairing a personal tool",
        "repair_equipment" => "repairing personal equipment",
        "collect_material" => "collecting personal materials",
        "collect_food" => "collecting personal food",
        "collect_equipment" => "collecting personal equipment",
        "store_material" => "storing personal materials in the House",
        "store_equipment" => "storing personal equipment in the House",
        "inspect_material_site" => "checking the ordered material site",
        "consume_food" => "eating carried food",
        "safe_idle" => "keeping a safe routine",
        _ when candidateId.StartsWith("explore_for:resource:", StringComparison.Ordinal) =>
            "looking for " + candidateId["explore_for:resource:".Length..].Replace('_', ' '),
        _ when candidateId.StartsWith("explore_for:terrain:", StringComparison.Ordinal) =>
            "looking for " + candidateId["explore_for:terrain:".Length..].ToLowerInvariant() + " terrain",
        _ when candidateId.StartsWith("guardian_relocate:", StringComparison.Ordinal) => "bringing a child home",
        _ when candidateId.StartsWith("guardian_follow:", StringComparison.Ordinal) => "following their guardian home",
        _ => candidateId.Replace('_', ' '),
    };

    private static string ToWireValue(SocietyRelationshipType type) => type switch
    {
        SocietyRelationshipType.Partnership => "partnership",
        SocietyRelationshipType.Caregiver => "caregiver",
        SocietyRelationshipType.HouseholdMembership => "household_membership",
        SocietyRelationshipType.BiologicalParentage => "biological_parentage",
        SocietyRelationshipType.LegalGuardian => "legal_guardian",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    private static string ToWireValue(SocietyRelationshipState state) => state switch
    {
        SocietyRelationshipState.Proposed => "proposed",
        SocietyRelationshipState.Accepted => "accepted",
        SocietyRelationshipState.Rejected => "rejected",
        SocietyRelationshipState.Revoked => "revoked",
        SocietyRelationshipState.Dissolved => "dissolved",
        SocietyRelationshipState.EndedByDeath => "ended_by_death",
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };

    private static IEnumerable<OwnerWorldDecisionFactor> IdentityMomentFactors(AgentState physical) =>
        (physical.IdentityMoments ?? []).Where(moment => moment.Outcome == "accepted")
            .Select(moment => new OwnerWorldDecisionFactor("identity-change", moment.Reason + ": " +
                string.Join("; ", new[]
                {
                    moment.Personality is null ? null : "Personality: " + moment.Personality,
                    moment.Aspiration is null ? null : "Aspiration: " + moment.Aspiration,
                }.Where(text => text is not null))));

    private static OwnerWorldEquipment EquipmentFor(WorldRuntimeState state, AgentState person)
    {
        var inventory = state.Society.Society.Inventory;
        var garment = PersonalEquipmentRules.EquippedUnit(inventory, person.AgentId, person.Equipment?.ClothingLotId);
        var aid = PersonalEquipmentRules.EquippedUnit(inventory, person.AgentId, person.Equipment?.CarryAidLotId);
        var ornament = PersonalEquipmentRules.EquippedUnit(inventory, person.AgentId, person.Equipment?.OrnamentLotId);
        var repair = person.Equipment?.Repair;
        return new(PersonalEquipmentRules.CarriedQuantity(inventory, person.AgentId, person.Equipment),
            PersonalEquipmentRules.Capacity(inventory, person.AgentId, person.Equipment) +
                (state.AnimalWorld.Animals.Any(animal => animal.RiderId == person.AgentId && AnimalRules.HasCare(animal, state.Society.Society.WorldTick)) ? AnimalRules.RidingCargo : 0),
            garment?.ItemKind, garment is null ? null : ShownConditionPercent(garment.ConditionBasisPoints),
            aid?.ItemKind, aid is null ? null : ShownConditionPercent(aid.ConditionBasisPoints),
            inventory.Lots.FirstOrDefault(lot => lot.Id == repair?.LotId)?.ItemKind,
            repair?.WorkDone ?? 0, PersonalEquipmentRules.RepairWorkTicks, ornament?.ItemKind);
    }

    private static string BoatPassengerName(WorldRuntimeState state, string id) =>
        state.Society.Society.Agents.Single(person => person.Id == id).Name;

    private static OwnerWorldBoat[] ProjectBoats(WorldRuntimeState state) => state.BoatTransport.Boats.Select(boat =>
    {
        var journey = boat.Journey;
        var roots = boat.GroundCargoLotIds ?? [];
        var cargo = state.Society.Society.Inventory.Lots.Where(lot => lot.Quantity > 0 &&
            (journey is not null && PersonalEquipmentRules.IsCarried(lot, journey.PassengerId) || roots.Contains(lot.Id, StringComparer.Ordinal) ||
             lot.ContainerLotId is { } container && roots.Contains(container, StringComparer.Ordinal)));
        return new OwnerWorldBoat(boat.Id, boat.TownId, state.Towns!.Single(town => town.Id == boat.TownId).Name,
            ToPosition(boat.Position), boat.DockedPortId, journey?.PassengerId,
            journey is null ? null : BoatPassengerName(state, journey.PassengerId),
            journey is null ? null : journey.Returning ? journey.OriginPortId : journey.DestinationPortId,
            journey is null ? "moored" : journey.WaitingSinceTick is not null ? "waiting" : journey.Returning ? "returning" : "underway",
            journey is null ? null : ToPosition(journey.ReservedDock),
            cargo.GroupBy(lot => lot.ItemKind, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new OwnerWorldInventoryEntry(group.Key, group.Sum(lot => lot.Quantity))).ToArray());
    }).ToArray();

    private static OwnerWorldBoatTripRequest[] ProjectBoatRequests(WorldRuntimeState state) =>
        state.BoatTransport.Requests.Where(request => request.Status is "waiting" or "underway")
            .Concat(state.BoatTransport.Requests.Where(request => request.Status is not ("waiting" or "underway"))
                .TakeLast(WorldHistory.RecentBoatRequestLimit))
            .OrderBy(request => request.Sequence).Select(request => new OwnerWorldBoatTripRequest(request.Id, request.Sequence,
            request.PassengerId, BoatPassengerName(state, request.PassengerId), request.BoatTownId,
            request.OriginPortId, request.DestinationPortId, request.Status, request.BoatId)).ToArray();

    private static OwnerWorldHandcart[] ProjectHandcarts(WorldRuntimeState state)
    {
        string Name(string id) => state.Society.Society.Agents.FirstOrDefault(person => person.Id == id)?.Name ??
            state.Society.Society.Households.FirstOrDefault(household => household.Id == id)?.Name ??
            (id == "settlement:communal" ? "Communal property" : "Estate property");
        var inventory = state.Society.Society.Inventory;
        return inventory.Lots.Where(lot => lot.ItemKind == InventoryContainerRules.Handcart)
            .OrderBy(lot => lot.Id, StringComparer.Ordinal).Select(cart =>
            {
                var hitch = state.HandcartHitches!.FirstOrDefault(item => item.CartLotId == cart.Id);
                return new OwnerWorldHandcart(cart.Id, cart.OwnerId, Name(cart.OwnerId),
                    new(cart.GroundPosition!.Value.X, cart.GroundPosition.Value.Y), InventoryContainerRules.HandcartCapacity,
                    ShownConditionPercent(cart.ConditionBasisPoints), hitch?.PullerId, hitch is null ? null : Name(hitch.PullerId),
                    inventory.Lots.Where(lot => lot.ContainerLotId == cart.Id).GroupBy(lot => lot.ItemKind, StringComparer.Ordinal)
                        .OrderBy(group => group.Key, StringComparer.Ordinal)
                        .Select(group => new OwnerWorldInventoryEntry(group.Key, group.Sum(lot => lot.Quantity))).ToArray());
            }).ToArray();
    }

    /// <summary>
    /// A cart's condition as a whole percentage for the owner: rounded down,
    /// except that a cart with any condition left shows at least 1%, so 0%
    /// always means broken and a nearly worn-out cart never looks broken
    /// while it can still be pulled.
    /// </summary>
    internal static int ShownConditionPercent(int basisPoints) => basisPoints <= 0 ? 0 : Math.Max(1, basisPoints / 100);

    /// <summary>
    /// Whether an abandoned Town has stood empty for a full season (a quarter
    /// of the world's year) since its latest abandonment. The saved transition
    /// tick survives event compaction; unknown age never implies a full season.
    /// </summary>
    internal static bool AbandonedForASeason(WorldRuntimeState state, string townId)
    {
        var config = state.WorldSystems?.Config ?? WorldSystemsConfig.Default;
        var season = (long)config.DaysPerYear * config.TicksPerDay / 4;
        var town = state.Towns?.FirstOrDefault(item => item.Id == townId);
        return town is { IsAbandoned: true, AbandonedSinceTick: { } abandoned } &&
            state.Society.Society.WorldTick - abandoned >= season;
    }

    /// <summary>
    /// Every building under construction: each household building an adult is
    /// working toward (its project names the building and the site) and each
    /// approved Town project still being supplied or built. Boats built at a
    /// Port have no site of their own and are left out.
    /// </summary>
    private static OwnerWorldConstructionSite[] ProjectConstructionSites(WorldRuntimeState state)
    {
        if (state.WorldContent is not { } content) return [];
        var sites = new List<OwnerWorldConstructionSite>();
        OwnerWorldConstructionSite Site(string id, BuildingDefinition definition, GridPoint position, GridPoint? entrance,
            int done, int required, string stage, string? town, string? household)
        {
            var tiles = WorldContentSimulationRules.Footprint(definition, position).ToArray();
            var left = tiles.Min(tile => tile.X);
            var top = tiles.Min(tile => tile.Y);
            return new(id, definition.CanonicalId, definition.DisplayName, definition.Tags.ToArray(), new OwnerWorldPosition(left, top),
                tiles.Max(tile => tile.X) - left + 1, tiles.Max(tile => tile.Y) - top + 1,
                entrance is { } door ? ToPosition(door) : null, done, required, stage, town, household);
        }
        foreach (var person in state.Agents.OrderBy(item => item.AgentId, StringComparer.Ordinal))
        {
            if (person.Project is not { Stage: not ("completed" or "cancelled") } project ||
                !TownConstructionCandidateIds.TryParse(project.CandidateId, out var selection) || !selection.IsBuilding ||
                selection.SitePosition is not { } position ||
                content.Buildings.FirstOrDefault(definition => definition.CanonicalId == selection.DefinitionId) is not { } planned)
                continue;
            var household = state.Society.Society.Agents.FirstOrDefault(item => item.Id == person.AgentId)?.HouseholdId;
            sites.Add(Site("household:" + person.AgentId, planned, position, null, project.WorkDone, 10, project.Stage, null, household));
        }
        foreach (var town in (state.Towns ?? []).OrderBy(item => item.Id, StringComparer.Ordinal))
            foreach (var project in town.Projects.Where(item => item.Stage is "supplying" or "working" or "blocked" &&
                         item.Plan.BoatPortId is null && item.RemovedTick is null))
                if (TownProjectRules.DefinitionFor(project.Plan.DefinitionId) is { } definition)
                    sites.Add(Site("town:" + project.Id, definition, project.Plan.Site, project.Plan.Entrance, project.WorkDone,
                        TownProjectRules.RequiredWork(project.Plan), project.Stage, town.Id, null));
        return sites.ToArray();
    }

    private static OwnerWorldAnimal[] ProjectAnimals(WorldRuntimeState state)
    {
        var tick = state.Society.Society.WorldTick;
        var day = state.WorldSystems!.Config.TicksPerDay;
        string Name(string id) => state.Society.Society.Agents.FirstOrDefault(person => person.Id == id)?.Name ?? id;
        return state.AnimalWorld.Animals.Select(animal => new OwnerWorldAnimal(animal.Id, animal.Name, animal.Species, animal.Sex,
            (int)((tick - animal.BornTick) / day), animal.DiedTick is not null ? "deceased" : AnimalRules.IsAdult(animal, tick, day) ? "adult" : "young",
            ToPosition(animal.Position), animal.HouseholdId, state.Society.Society.Households.FirstOrDefault(home => home.Id == animal.HouseholdId)?.Name,
            animal.DiedTick is not null ? "deceased" : AnimalRules.HasCare(animal, tick) ? "cared for" : "needs care",
            animal.ReadyProductLotId is null ? null : AnimalRules.Definition(animal.Species).Product,
            animal.ReadyProductLotId is null ? 0 : AnimalRules.Definition(animal.Species).ProductQuantity,
            animal.Pregnancy is null ? null : Math.Round(AnimalRules.Definition(animal.Species).GestationDays - animal.Pregnancy.ProgressTicks / (double)day, 1),
            animal.RiderId, animal.RiderId is null ? null : Name(animal.RiderId), animal.LeaderId, animal.SaddleLotId is not null,
            animal.CarePermissions.Select(Name).ToArray(), animal.RidingPermissions.Select(Name).ToArray(),
            ProductProgressPercent(animal, tick, day))).ToArray();
    }

    /// <summary>
    /// How far an owned, grown producer is through its current product cycle,
    /// 100 while its product waits to be collected; null when it makes nothing
    /// yet: a wild, young or dead animal, a male where only females produce,
    /// or a horse.
    /// </summary>
    private static int? ProductProgressPercent(AnimalState animal, long tick, int day)
    {
        var definition = AnimalRules.Definition(animal.Species);
        if (animal.DiedTick is not null || animal.HouseholdId is null || !AnimalRules.HasProduct(animal) ||
            !AnimalRules.IsAdult(animal, tick, day) || definition.ProductDays <= 0) return null;
        if (animal.ReadyProductLotId is not null) return 100;
        return (int)Math.Min(99, animal.ProductProgressTicks * 100L / ((long)definition.ProductDays * day));
    }

    private static OwnerWorldInventoryEntry[] InventoryFor(
        WorldRuntimeState state,
        string ownerId,
        string? storageBuildingId = null) => state.Society.Society.Inventory.Lots
        .Where(lot => lot.OwnerId == ownerId && lot.Quantity > 0 &&
            (storageBuildingId is null || lot.StorageBuildingId == storageBuildingId))
        .GroupBy(lot => lot.ItemKind, StringComparer.Ordinal)
        .OrderBy(group => group.Key, StringComparer.Ordinal)
        .Select(group => new OwnerWorldInventoryEntry(group.Key, group.Sum(lot => lot.Quantity)))
        .ToArray();

    private static OwnerWorldRoute DetermineWorldRoute(
        WorldRuntimeState state,
        AgentState physical,
        IReadOnlyList<OwnerWorldInventoryEntry> inventory, WorldMap travelMap)
    {
        if (state.BoatTransport.Boats.FirstOrDefault(boat => boat.Journey?.PassengerId == physical.AgentId) is { Journey: { } journey })
            return new OwnerWorldRoute(journey.WaitingSinceTick is not null ? "boat_waiting" : journey.Returning ? "boat_returning" : "boat_travel",
                journey.Returning ? journey.OriginPortId : journey.DestinationPortId, ToPosition(journey.ReservedDock),
                journey.WaterPath.Skip(journey.PathIndex + 1).Select(ToPosition).ToArray(), state.Map.ManifestDigest);
        if (SwimmingRules.IsSwimmingWater(travelMap, physical.Position))
            return new OwnerWorldRoute("swim", null, null, [], state.Map.ManifestDigest);
        var food = inventory.FirstOrDefault(item => item.Kind == "food");
        if (food is { Quantity: > 0 } && physical.FullnessBasisPoints < 8_500)
        {
            return new OwnerWorldRoute("consume", null, null, [], state.Map.ManifestDigest);
        }

        var berry = state.Map.GetResource("berry-patch");
        var berryState = state.Resources.FirstOrDefault(item => item.ResourceId == berry.Id)?.State;
        if (berryState == ResourceState.Available && IsWithinInteractionRange(physical.Position, berry.Position))
        {
            return new OwnerWorldRoute("harvest", berry.Id, ToPosition(berry.Position), [], state.Map.ManifestDigest);
        }

        if (berryState == ResourceState.Available && physical.FullnessBasisPoints < 7_000)
        {
            // The starter patch is reachable from the starting site. Reject an agent on a
            // separate island using the map's cached starting-site component instead
            // of exhaustively searching the entire world on every reconnect.
            if ((state.Map.IsReachableFromStartingSiteOnFoot(berry.Position) &&
                 !state.Map.IsReachableFromStartingSiteOnFoot(physical.Position)) ||
                !DeterministicRouteFinder.TryFind(state.Map, physical.Position, berry.Position,
                    out var path))
                return new OwnerWorldRoute("food_unreachable", null, null, [], state.Map.ManifestDigest);
            return new OwnerWorldRoute("seek_food", berry.Id, ToPosition(berry.Position),
                path.Skip(1).Select(ToPosition).ToArray(), state.Map.ManifestDigest);
        }

        return new OwnerWorldRoute("idle", null, null, [], state.Map.ManifestDigest);
    }

    /// <summary>A planned route as the owner sees it, with at most <see cref="OwnerWorldPlannedRoute.StepLimit"/> steps.</summary>
    public static OwnerWorldPlannedRoute? ToPlannedRoute(PlannedRoute? route) => route is null ? null :
        new OwnerWorldPlannedRoute(route.Reason, ToPosition(route.Destination),
            route.Steps.Take(OwnerWorldPlannedRoute.StepLimit).Select(ToPosition).ToArray(), route.Steps.Count);

    private static bool IsWithinInteractionRange(GridPoint origin, GridPoint destination) =>
        Math.Abs(origin.X - destination.X) + Math.Abs(origin.Y - destination.Y) <= 1;

    private static IEnumerable<OwnerWorldPosition> KnownNearby(WorldMap map, GridPoint origin) => map.Tiles
        .Where(tile => Math.Abs(tile.Position.X - origin.X) <= 1 && Math.Abs(tile.Position.Y - origin.Y) <= 1)
        .OrderBy(tile => tile.Position.Y)
        .ThenBy(tile => tile.Position.X)
        .Select(tile => ToPosition(tile.Position));

    /// <summary>
    /// The local projection has no persistent cognitive map. Its truthful
    /// knowledge is therefore limited to the current local perception and the
    /// route/destination it has already committed to follow. The server still
    /// owns the full map for routing, but must not accidentally project that
    /// omniscience as agent knowledge.
    /// </summary>
    private static OwnerWorldPosition[] KnownLocalTopology(
        GridPoint currentPosition,
        IReadOnlyList<OwnerWorldPosition> perceived,
        OwnerWorldRoute route)
    {
        var routeKnowledge = route.Destination is null
            ? route.Steps
            : route.Steps.Append(route.Destination);

        return perceived
            .Append(ToPosition(currentPosition))
            .Concat(routeKnowledge)
            .Distinct()
            .OrderBy(position => position.Y)
            .ThenBy(position => position.X)
            .ToArray();
    }

    private static OwnerWorldEvent ToEvent(WorldEventEntry worldEvent, OwnerWorldSnapshot snapshot)
    {
        var view = new OwnerWorldEvent(
        worldEvent.EventId,
        worldEvent.WorldTick,
        worldEvent.Kind,
        worldEvent.Detail,
        worldEvent.Position is { } position ? ToPosition(position) : null);
        view = view with { Payload = worldEvent.Payload };
        return view with { Text = WorldEventFormatter.Describe(view, snapshot) };
    }

    private static OwnerWorldCognition ToCognition(WorldRuntimeState state)
    {
        var runtimes = state.Society.Cognition.Runtimes
            .OrderBy(runtime => runtime.AgentId, StringComparer.Ordinal)
            .ToArray();
        var current = runtimes
            .Select(runtime => runtime.CurrentIntention)
            .FirstOrDefault(intention => intention is not null);
        var provider = current?.Provider.ToString().ToLowerInvariant() ??
            (state.Society.Society.WorldDefaultProviderBindingId is null ? "deterministic" : "configured");
        return new OwnerWorldCognition(
            provider,
            state.Society.Society.IsPaused,
            null,
            current?.CandidateId,
            current?.Provider.ToString().ToLowerInvariant(),
            state.Society.Cognition.Events
                .OrderBy(worldEvent => worldEvent.EventId)
                .TakeLast(12)
                .Select(worldEvent => new OwnerWorldCognitionEvent(
                    worldEvent.EventId,
                    worldEvent.WorldTick,
                    worldEvent.Kind,
                    worldEvent.Detail))
                .ToArray(),
            runtimes.Where(runtime => runtime.CurrentIntention is not null)
                .Select(runtime =>
                {
                    var intention = runtime.CurrentIntention!;
                    return new OwnerWorldAgentDecision(
                        runtime.AgentId, intention.Usage?.ProviderId ?? intention.Provider.ToString().ToLowerInvariant(),
                        intention.CandidateId, intention.WorldTick, intention.Confidence,
                        intention.Usage?.ModelId, intention.Usage?.InputTokens, intention.Usage?.OutputTokens,
                        intention.Usage?.Role, intention.Usage?.LatencyMilliseconds,
                        runtime.Events.LastOrDefault(item => item.WorldTick == intention.WorldTick &&
                            item.Kind is "cognition_fallback_applied" or "cognition_decision_applied")?.Kind == "cognition_fallback_applied");
                }).ToArray());
    }

    private static OwnerWorldPosition ToPosition(GridPoint point) => new(point.X, point.Y);

    private static string ToWireValue(ResourceState state) => state switch
    {
        ResourceState.Available => "available",
        ResourceState.Depleted => "depleted",
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };

    private static string ToWireValue(OwnerInstructionKind kind) => kind switch
    {
        OwnerInstructionKind.Suggestion => "suggestion",
        OwnerInstructionKind.Order => "order",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static string ToWireValue(OwnerInstructionState state) => state switch
    {
        OwnerInstructionState.Queued => "queued",
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };
    private static string? ConversationOrderStatus(WorldRuntimeState state, OrderTask order)
    {
        if (order.Action == "propose_marriage" && (state.Marriages ?? []).FirstOrDefault(item => item.Consent.Id == order.TalkConversationId) is { CompletedTick: null } marriage)
            return (state.Conversations ?? []).FirstOrDefault(item => item.Id == marriage.SurnameConversationId) is { } surname
                ? "surname_" + ConversationStatus(surname.Status) : "surname_pending";
        return (state.Conversations ?? []).FirstOrDefault(item => item.Id == order.TalkConversationId) is { } talk
            ? ConversationStatus(talk.Status) : order.TalkOutcome is not null ? "closed" : null;
    }


    private static OwnerWorldProductionRecipe ProjectProductionRecipe(RecipeDefinition recipe) => new(
        recipe.CanonicalId, recipe.DisplayName,
        recipe.Inputs.Select(input => new OwnerWorldMaterialQuantity(input.ResourceId, input.Amount)).ToArray(),
        recipe.Outputs.Select(output => new OwnerWorldMaterialQuantity(output.ResourceId, output.Amount)).ToArray());

    private static Dictionary<string, OwnerWorldBuildingStorageChange[]> RecentBuildingStorageChanges(InventoryCheckpoint inventory)
    {
        var history = new Dictionary<string, List<OwnerWorldBuildingStorageChange>>(StringComparer.Ordinal);
        // Read the bounded retained stream once, rather than scanning it for every building.
        for (var index = inventory.Events.Count - 1; index >= 0; index--)
        {
            var item = inventory.Events[index];
            foreach (var change in item.StorageChanges ?? [])
            {
                if (!history.TryGetValue(change.BuildingId, out var rows))
                    history[change.BuildingId] = rows = [];
                if (rows.Count < 10)
                    rows.Add(new(item.EventId, item.WorldTick, change.ItemKind, change.QuantityChange));
            }
        }
        return history.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);
    }

    public const int RecentClosedNonLandCaseLimit = 8;

    private static OwnerTownNonLandCase[] ProjectNonLandCases(WorldRuntimeState state, TownRuntimeState town)
    {
        var ledger = town.NonLandHearings;
        var tick = state.Society.Society.WorldTick;
        string Name(string id) => state.Society.Society.Agents.FirstOrDefault(person => person.Id == id)?.Name ??
            (state.Towns ?? []).FirstOrDefault(other => other.Id == id)?.Name ?? "An earlier participant";
        string? Household(string? id) => id is null ? null :
            state.Society.Society.Households.FirstOrDefault(household => household.Id == id)?.Name ?? "An earlier household";
        string? Target(string? id) => id is null ? null :
            state.Society.Society.Inventory.Lots.FirstOrDefault(lot => lot.Id == id)?.ItemKind.Replace('_', ' ') ??
            ((state.WorldSimulation?.Buildings ?? []).FirstOrDefault(building => building.InstanceId == id) is { } building
                ? state.WorldContent?.Buildings.FirstOrDefault(definition => definition.CanonicalId == building.DefinitionId)?.DisplayName : null)
            ?? "the recorded target";
        var names = state.Society.Society.Agents.Select(person => (person.Id, person.Name))
            .Concat(state.Society.Society.Households.Select(household => (household.Id, household.Name)))
            .Concat((state.Towns ?? []).Select(other => (other.Id, other.Name)))
            .OrderByDescending(pair => pair.Id.Length).ToArray();
        string Readable(string text)
        {
            foreach (var pair in names) text = text.Replace(pair.Id, pair.Name, StringComparison.Ordinal);
            return text;
        }
        bool Aware(string noticeId, long published, string actor, long asOf) => (town.Council?.Knowledge ?? [])
            .Any(receipt => receipt.NoticeId == noticeId && receipt.AgentId == actor &&
                receipt.LearnedTick >= published && receipt.LearnedTick <= asOf);
        OwnerCaseParty Party(TownCaseParty party, TownCaseRevision revision, long asOf) => new(party.Id,
            party.Role, party.SubjectId, party.Role == "town" ? town.Name : Name(party.SubjectId), party.RespondingAdultId,
            party.RespondingAdultId is { } adult ? Name(adult) : null, Household(party.HouseholdId),
            party.RespondingAdultId is { } responder && Aware(revision.NoticeId, revision.PublishedTick, responder, asOf));
        OwnerCaseJudge Judge(TownCaseJudge judge) => new(judge.AgentId, Name(judge.AgentId), judge.Kind,
            judge.AuthorityId, judge.AssignedTick);
        OwnerCaseElection Election(TownCaseJudgeContest contest) => new(contest.Id, contest.Stage, contest.Round,
            contest.RoundDeadlineTick, contest.Candidates.Select(id => new OwnerCivicCandidate(id, Name(id),
                contest.Ballots.Count(ballot => ballot.CandidateId == id))).ToArray(),
            contest.WinnerId is { } winner ? Name(winner) : null, HearingReasonText.Describe(contest.Reason));
        OwnerRemedyTerm Term(TownRemedyTerm term) => new(term.Id, term.Kind, term.ContributorId, Name(term.ContributorId),
            term.BeneficiaryId, term.BeneficiaryId is { } beneficiary ? Name(beneficiary) : null,
            term.ItemKind, term.Quantity, term.TargetId, Target(term.TargetId));
        OwnerRemedyResponse Consent(TownRemedyResponse response) => new(response.AgentId, Name(response.AgentId),
            response.Revision, response.Kind, response.Tick, response.Reason is { } reason ? Readable(reason) : null);
        bool Active(TownNonLandCase item) => item.Status == "pending" || item.ReopenRequests.Any(request => request.Status == "pending") ||
            ledger.Offers.Any(offer => offer.CaseId == item.Id && (offer.Status == "pending" ||
                ledger.Agreements.Any(agreement => agreement.OfferId == offer.Id && !TownRemedyRules.IsSuperseded(ledger, agreement.Id) && agreement.Status is "pending" or "overdue")));
        return ledger.Cases.Where(Active).Concat(ledger.Cases.Where(item => !Active(item))
                .OrderBy(item => item.SettledTick).ThenBy(item => item.Id, StringComparer.Ordinal).TakeLast(RecentClosedNonLandCaseLimit))
            .OrderBy(item => item.FiledTick).ThenBy(item => item.Id, StringComparer.Ordinal).Select(item =>
            {
                var allegation = item.Allegation;
                var law = town.Government?.Laws.FirstOrDefault(candidate => candidate.Id == allegation.LawId)?
                    .Versions.FirstOrDefault(version => version.Version == allegation.LawVersion);
                var offers = ledger.Offers.Where(offer => offer.CaseId == item.Id).ToArray();
                var offerIds = offers.Select(offer => offer.Id.Value).ToHashSet(StringComparer.Ordinal);
                return new OwnerTownNonLandCase(item.Id, item.Status, item.FiledTick, item.SettledTick,
                    allegation.SubjectId, Name(allegation.SubjectId), allegation.ConductKind, ToPosition(allegation.Position),
                    allegation.ConductTick, Readable(allegation.Statement), law is null ? null :
                        new OwnerTownLaw(allegation.LawId, law.Subject, law.Rule, law.Scope, law.SiteTiles.Count,
                            law.Version, law.AdoptedTick, law.EndedTick)
                        { Site = law.SiteTiles.Select(ToPosition).ToArray() },
                    allegation.LawVersion,
                    item.Revisions.Select(revision => new OwnerCaseRevision(revision.Number, revision.NoticeId,
                        revision.PublishedTick, revision.DeadlineTick, revision.Parties.Select(party => Party(party, revision, tick)).ToArray())).ToArray(),
                    item.Filings.Select(filing => new OwnerCaseFiling(filing.AgentId, Name(filing.AgentId), filing.Kind,
                        filing.Tick, Readable(filing.Statement), filing.EvidenceIds.ToArray())).ToArray(),
                    item.Evidence.Select(evidence => new OwnerCaseEvidence(evidence.Id, evidence.Revision, evidence.Kind,
                        evidence.Acquisition, evidence.SourceAgentId, Name(evidence.SourceAgentId), evidence.SourceRecordId,
                        evidence.SourceVersion, evidence.ObservedTick, evidence.SubmittedByAgentId, Name(evidence.SubmittedByAgentId),
                        evidence.SubmittedTick, Readable(evidence.Text))).ToArray(),
                    item.Reads.Select(read => new OwnerCaseRead(read.Revision, read.AgentId, Name(read.AgentId), read.ReadTick,
                        read.EvidenceIds.ToArray(), read.ReopenRequestIds.ToArray(), read.SourceAgentId is { } source ? Name(source) : null)).ToArray(),
                    item.Responses.Select(response => new OwnerCaseResponse(response.Revision, response.PartyId, response.AgentId,
                        Name(response.AgentId), response.RepresentedAgentId,
                        item.Revisions.Single(revision => revision.Number == response.Revision).Parties.Any(party =>
                            party.Id == response.PartyId && party.Role == "town") ? town.Name : Name(response.RepresentedAgentId), response.Kind,
                        Readable(response.Text), response.Tick)).ToArray(),
                    item.Findings.Select(finding => new OwnerViolationFinding(finding.Id, finding.Revision, Judge(finding.Judge),
                        finding.Tick, finding.Result, finding.Standard, finding.EvidenceIds.ToArray(), Readable(finding.Reasons),
                        Readable(finding.Uncertainty), finding.Consequence, finding.Parties.Select(party => Party(party,
                            item.Revisions.Single(revision => revision.Number == finding.Revision), finding.Tick)).ToArray())).ToArray(),
                    item.Judge is { } judge ? Judge(judge) : null,
                    item.JudgeHistory.Select(term => new OwnerCaseJudgeTerm(Judge(term.Judge), term.EndedTick, HearingReasonText.Describe(term.Reason)!)).ToArray(),
                    item.Contest is { } contest ? Election(contest) : null,
                    item.ContestHistory.Count > 0 ? Election(item.ContestHistory[^1]) : null,
                    item.ReopenRequests.Select(request => new OwnerCaseReopenRequest(request.Id, Name(request.AgentId), request.Tick,
                        request.Kind, request.EvidenceIds.ToArray(), Readable(request.Reasons), request.Status,
                        request.AssessedBy is { } assessor ? Judge(assessor) : null, request.AssessedTick,
                        request.Assessment is { } assessment ? Readable(assessment) : null)).ToArray(),
                    offers.Select(offer => new OwnerRemedyOffer(offer.Id, offer.FindingId, offer.Revision,
                        offer.Terms.Select(Term).ToArray(), Readable(offer.Reason), offer.NoticeId, offer.PublishedTick,
                        offer.ResponseDeadlineTick, offer.CompletionTicks, offer.Status, offer.Responses.Select(Consent).ToArray(),
                        offer.Terms.Select(term => term.ContributorId).Distinct(StringComparer.Ordinal)
                            .Where(actor => Aware(offer.NoticeId, offer.PublishedTick, actor, tick)).ToArray(),
                        offer.ReplacesOfferId, offer.AgreementId)).ToArray(),
                    ledger.Agreements.Where(agreement => offerIds.Contains(agreement.OfferId)).Select(agreement =>
                        new OwnerRestorativeAgreement(agreement.Id, agreement.OfferId, agreement.OfferRevision,
                            agreement.Terms.Select(Term).ToArray(), agreement.Consents.Select(Consent).ToArray(),
                            agreement.AcceptedTick, agreement.DeadlineTick, agreement.Status, agreement.ReplacesAgreementId,
                            ledger.Effects.Where(effect => effect.AgreementId == agreement.Id).Select(effect => new OwnerRemedyEffect(
                                effect.Id, effect.TermId, effect.ActorId, Name(effect.ActorId), effect.Tick, effect.Kind,
                                effect.BeneficiaryId is { } beneficiary ? Name(beneficiary) : null, effect.ItemKind,
                                effect.Quantity, Target(effect.TargetId), effect.NativeReceiptId)).ToArray())
                        {
                            Superseded = TownRemedyRules.IsSuperseded(ledger, agreement.Id),
                        }).ToArray())
                {
                    CurrentParties = item.Status == "pending" ? TownNonLandPartyRules.CurrentParties(town, item,
                        state.Society.Society, tick).Select(party => Party(party, item.Revisions[^1], tick)).ToArray() : [],
                };
            }).ToArray();
    }
}

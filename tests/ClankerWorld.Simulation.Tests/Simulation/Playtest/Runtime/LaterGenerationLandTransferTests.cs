using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Playtest;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Viewer.Observation;

namespace ClankerWorld.Simulation.Tests;

public sealed partial class GrownAgentHelperMemoryTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(true, true, true)]
    [InlineData(false, true, true)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true, true)]
    public async Task NativeTransferPartiesSurviveHostedConsentAndCheckpointReload(bool laterGeneration,
        bool formedHousehold = false, bool nativeBornHousehold = true, bool quietNoticeTurn = false)
    {
        PrivateWorldRuntimeState state;
        string? formedTarget = null;
        if (formedHousehold)
        {
            using var formed = await FormNativeSoloHouseholdAsync(nativeBornHousehold, laterGeneration);
            formed.Pause();
            state = PrivateWorldRuntimeCodec.Decode(PrivateWorldRuntimeCodec.Encode(formed.ExportState()));
            var birth = state.Society.Society.Births.OrderBy(item => item.CommittedTick).Last();
            var recipient = nativeBornHousehold ? birth.ChildId : birth.PrimaryCaregiverId;
            formedTarget = Assert.IsType<string>(state.Society.Society.GetInhabitant(recipient).HouseholdId);
            Assert.InRange(formedTarget.Length, 1, DerivedId.MaximumLength);
            Assert.Equal([recipient], state.Society.Society.GetHousehold(formedTarget).MemberIds);
            Assert.Contains(state.Events, item => item.Kind == "household_founded" && item.Detail == recipient + "|" + formedTarget);
        }
        else state = PrivateWorldRuntimeCodec.Decode(await (laterGeneration ? LaterAdult : ConversationAdult).Value);
        var actor = formedHousehold ? state.Society.Society.Inhabitants.First(person =>
            person.HouseholdId == "household:camp-alpha" && person.Status == SocietyInhabitantStatus.Active &&
            person.AgeBand is SocietyAgeBand.Adult or SocietyAgeBand.Elder).Id :
            state.Society.Society.Births.OrderBy(birth => birth.CommittedTick).Last().ChildId;
        if (!formedHousehold) Assert.InRange(actor.Length, 1, DerivedId.MaximumLength);
        var town = state.Towns![0];
        // As in the civic fixtures, arrange board proximity and comfort only.
        // Birth identities, ages, households, titles and permissions stay native.
        state = state with
        {
            Inhabitants = state.Inhabitants.Select(person => person with
            {
                Position = town.OriginSite!.Value,
                HungerBasisPoints = 10_000,
                LastDecisionContext = null,
                Survival = (person.Survival ?? new SurvivalCondition()) with { WarmthBasisPoints = 10_000, IllnessBasisPoints = 0 },
            }).ToArray(),
        };
        var household = state.Society.Society.GetInhabitant(actor).HouseholdId!;
        var source = state.HouseholdLandUseRights!.First(right => right.TownId == town.Id && right.HouseholdId == household);
        var target = formedTarget ?? state.Society.Society.Households.Single(item => item.Id == "household:camp-beta").Id;
        if (quietNoticeTurn)
        {
            // Keep an unrelated weather-dependent clothing choice from waking
            // residents whose actual notice-reading opportunity ended in idle.
            var inventory = state.Society.Society.Inventory;
            var index = 0;
            foreach (var lot in inventory.Lots.Where(lot => PersonalEquipmentRules.IsGarment(lot.ItemKind)).ToArray())
            {
                var quantity = PersonalEquipmentRules.AvailableQuantity(inventory, lot);
                if (quantity > 0)
                    inventory = InventoryFixture.Reserve(inventory, "quiet-transfer-clothing-" + index++,
                        lot.OwnerId, lot.Id, quantity, "fixture-clothing-hold", inventory.WorldTick + 1_000);
            }
            state = state with { Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } } };
        }
        var policy = new NativeTransferChoices(actor, target, source.Tiles.ToArray());
        using var world = PrivateWorldRuntime.Restore(state, _ => policy);
        world.Resume();
        world.SubmitInstruction(new("native-transfer-guidance", "owner:test", actor, OwnerInstructionKind.Suggestive,
            "Consider offering this existing household permission to the specified household at the public notice place."));
        var directory = Directory.CreateTempSubdirectory("native-transfer-checkpoint-");
        try
        {
            var file = new PrivateWorldStateFile(Path.Combine(directory.FullName, "world.json"), _ => policy);
            file.Save(world);
            var presence = new OwnerClientPresenceLease(TimeSpan.FromMinutes(5));
            presence.RecordAuthenticatedReconnect("test-owner");
            using var service = new PrivateWorldRuntimeService(world, file, presence);
            async Task AdvanceTick()
            {
                Assert.True(await service.TryAdvanceOnceAsync());
                await WaitForRequests(world);
                Assert.False(world.Society.IsPaused);
                Assert.Equal(world.WorldTick, PrivateWorldRuntimeCodec.Decode(File.ReadAllBytes(file.Path)).Society.Society.WorldTick);
            }
            async Task AdvanceUntil(Func<bool> finished, string phase)
            {
                for (var tick = 0; tick < 40 && !finished(); tick++)
                    await AdvanceTick();
                Assert.True(finished(), $"The native transfer did not finish {phase} at tick {world.WorldTick}; " +
                    $"provider modes: read={policy.Read}, accept={policy.Accept}, hold={policy.Hold}. " +
                    string.Join("; ", policy.Replies.TakeLast(6).Select(reply =>
                        $"{reply.Request.Observation.InhabitantId}@{reply.Request.Observation.WorldTick}:{reply.Response.SelectedCandidateId}")));
            }
            await AdvanceUntil(() => world.Towns[0].LandHearings.Transfers.Count == 1, "proposal");
            policy.Propose = false;
            var pending = Assert.Single(world.Towns[0].LandHearings.Transfers);
            Assert.Equal(actor, pending.FilerId);
            Assert.Equal(target, pending.TargetHouseholdId);
            new CognitionLandHearingChoice(HouseholdId: target).Validate();
            Assert.Equal("pending", pending.Status);
            Assert.Empty(pending.Responses);
            Assert.Null(pending.Receipt);
            Assert.Contains(pending.Parties, party => party.AdultIds.Contains(actor));
            Assert.Equal(source.Tiles, pending.Tiles);
            Assert.Equal(state.HouseholdLandUseRights, world.ExportState().HouseholdLandUseRights);
            Assert.Contains(policy.Selected, choice => choice.Actor == actor && choice.Id.Contains("|land_transfer_propose|", StringComparison.Ordinal));
            AssertMalformedTransferPeople(world.ExportState(), pending);
            AssertMalformedTransferHouseholds(world.ExportState(), pending);
            using (var pendingReload = file.LoadOrCreate(state.WorldSeed))
                Assert.Equal(PrivateWorldRuntimeCodec.Encode(world.ExportState()), PrivateWorldRuntimeCodec.Encode(pendingReload.ExportState()));

            var adults = pending.Parties.SelectMany(party => party.AdultIds).Distinct(StringComparer.Ordinal).ToArray();
            bool KnowsNotice(string id) => world.Towns[0].Governance!.Knowledge.Any(receipt =>
                receipt.AgentId == id && receipt.NoticeId == pending.NoticeId);
            Dictionary<string, string> SuggestTransfer(string phase, IEnumerable<string> recipients)
            {
                var instructions = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var id in recipients.Distinct(StringComparer.Ordinal))
                {
                    var receipt = world.SubmitInstruction(new($"native-transfer-{phase}-{world.WorldTick}-{instructions.Count}",
                        "owner:test", id, OwnerInstructionKind.Suggestive,
                        "Consider the published permission transfer and make your own informed choice."));
                    instructions.Add(id, receipt.InstructionId);
                }
                return instructions;
            }
            if (quietNoticeTurn)
            {
                var quietReaders = adults.Where(id => !KnowsNotice(id)).ToArray();
                Assert.NotEmpty(quietReaders);
                var instructions = SuggestTransfer("quiet", quietReaders);
                bool IsQuietReply((CognitionDecisionRequest Request, CognitionDecisionResponse Response) reply, string id) =>
                    reply.Request.Observation.InhabitantId == id && reply.Response.SelectedCandidateId == "safe_idle" &&
                    reply.Request.Observation.ObserverGuidance?.Any(message => message.InstructionId == instructions[id]) == true &&
                    reply.Request.Observation.Candidates.Any(candidate => candidate.Id.Contains("|land_transfer_read|", StringComparison.Ordinal));
                await AdvanceUntil(() => quietReaders.All(id => policy.Replies.Any(reply => IsQuietReply(reply, id))),
                    "quiet notice opportunities");
                // Provider return is not admission. Commit the actual idle
                // responses before changing this fixture's personal policy.
                await AdvanceTick();
                foreach (var id in quietReaders)
                {
                    var reply = policy.Replies.Last(item => IsQuietReply(item, id));
                    var intention = Assert.IsType<CognitionIntention>(world.ExportState().Society.Cognition.Runtimes
                        .Single(runtime => runtime.InhabitantId == id).CurrentIntention);
                    Assert.Equal("safe_idle", intention.CandidateId);
                    Assert.Equal(id, intention.InhabitantId);
                    Assert.Equal(reply.Request.Observation.RunEpoch, intention.RunEpoch);
                    Assert.Equal(reply.Request.Observation.DecisionGeneration, intention.DecisionGeneration);
                    Assert.Equal(reply.Request.Observation.ObservationDigest, intention.ObservationDigest);
                }
                Assert.All(quietReaders, id => Assert.False(KnowsNotice(id)));
                Assert.Empty(world.Towns[0].LandHearings.Transfers[0].Responses);
                Assert.Equal(state.HouseholdLandUseRights, world.ExportState().HouseholdLandUseRights);
            }

            // Reading and silence supply no acceptance, even for the filer.
            policy.Read = true;
            SuggestTransfer("read", adults.Where(id => !KnowsNotice(id)));
            await AdvanceUntil(() => adults.All(KnowsNotice), "notice reading");
            Assert.Empty(world.Towns[0].LandHearings.Transfers[0].Responses);
            Assert.Equal(state.HouseholdLandUseRights, world.ExportState().HouseholdLandUseRights);
            policy.Accept = true;
            policy.Hold = actor;
            SuggestTransfer("accept", adults.Where(id => id != actor));
            await AdvanceUntil(() => world.Towns[0].LandHearings.Transfers[0].Responses.Count == pending.Parties.Sum(party => party.AdultIds.Count) - 1,
                "other adults' separate consent");
            var waiting = world.Towns[0].LandHearings.Transfers[0];
            Assert.Equal("pending", waiting.Status);
            Assert.DoesNotContain(waiting.Responses, response => response.AgentId == actor);
            Assert.Null(waiting.Receipt);
            Assert.Equal(state.HouseholdLandUseRights, world.ExportState().HouseholdLandUseRights);
            AssertMalformedTransferPeople(world.ExportState(), waiting);
            var beforeRollback = PrivateWorldRuntimeCodec.Encode(world.ExportState());
            Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
            Assert.Equal(beforeRollback, PrivateWorldRuntimeCodec.Encode(world.ExportState()));
            policy.Hold = null;
            SuggestTransfer("filer", [actor]);
            await AdvanceUntil(() => world.Towns[0].LandHearings.Transfers[0].Status == "transferred", "the held filer's consent");
            var completed = world.Towns[0].LandHearings.Transfers[0];
            Assert.Equal(actor, completed.FilerId);
            Assert.Equal(pending.Parties.Sum(party => party.AdultIds.Count), completed.Responses.Count);
            Assert.Contains(completed.Responses, response => response.AgentId == actor && response.PartyAdults.Contains(actor));
            var receipt = Assert.IsType<TownLandTransferReceipt>(completed.Receipt);
            Assert.Contains(receipt.Parties, party => party.AdultIds.Contains(actor));
            Assert.All(completed.Responses, response => Assert.Contains(world.Towns[0].Governance!.Knowledge,
                learned => learned.AgentId == response.AgentId && learned.NoticeId == completed.NoticeId && learned.LearnedTick <= response.Tick));
            Assert.Single(world.Towns[0].LandHearings.Adjustments, adjustment => adjustment.Id == receipt.AdjustmentId && adjustment.TransferId == completed.Id);
            Assert.All(source.Tiles, tile =>
            {
                var right = Assert.Single(world.HouseholdLandUseRights, item => item.Tiles.Contains(tile));
                Assert.Equal((target, source.GrantedTick, source.AgreedEndTick, source.GrantSource),
                    (right.HouseholdId, right.GrantedTick, right.AgreedEndTick, right.GrantSource));
            });
            AssertMalformedTransferPeople(world.ExportState(), completed);
            AssertMalformedTransferHouseholds(world.ExportState(), completed);
            policy.Read = policy.Accept = false;
            world.Pause();
            world.Resume();
            Assert.True(await service.TryAdvanceOnceAsync());
            await WaitForRequests(world);
            using var reload = file.LoadOrCreate(state.WorldSeed);
            var saved = PrivateWorldRuntimeCodec.Encode(world.ExportState());
            Assert.Equal(saved, PrivateWorldRuntimeCodec.Encode(reload.ExportState()));
            // Both paired worlds start from disk with the same blocking admission mode.
            using var original = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(saved), _ => new QuietProvider());
            using var replay = PrivateWorldRuntime.Restore(reload.ExportState(), _ => new QuietProvider());
            for (var tick = 0; tick < 3; tick++)
            {
                Assert.True((await original.AdvanceOneTickAsync()).Advanced);
                Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
                Assert.Equal(PrivateWorldRuntimeCodec.Encode(original.ExportState()), PrivateWorldRuntimeCodec.Encode(replay.ExportState()));
            }
        }
        finally { directory.Delete(recursive: true); }
    }

    private static void AssertMalformedTransferHouseholds(PrivateWorldRuntimeState state, TownLandTransferRequest request)
    {
        foreach (var invalid in new[] { "", " " + request.TargetHouseholdId, request.TargetHouseholdId + "\0",
                     request.TargetHouseholdId + ":unknown" })
        {
            var corrupted = state with
            {
                Towns = state.Towns!.Select(town => town.Id == request.TownId ? town with
                {
                    LandHearings = town.LandHearings with
                    {
                        Transfers = town.LandHearings.Transfers.Select(item => item.Id == request.Id ? item with
                        { TargetHouseholdId = invalid } : item).ToArray()
                    }
                } : town).ToArray()
            };
            Assert.Throws<InvalidDataException>(() => PrivateWorldRuntimeCodec.Encode(corrupted));
        }
    }

    private static void AssertMalformedTransferPeople(PrivateWorldRuntimeState state, TownLandTransferRequest request)
    {
        var party = request.Parties.Single(item => item.AdultIds.Contains(request.FilerId));
        var people = party.AdultIds;
        foreach (var invalid in new[] { "", " " + request.FilerId, request.FilerId + "\0", request.FilerId + ":unknown" })
        {
            AssertInvalid(request with { FilerId = invalid });
            AssertInvalid(request with
            {
                Parties = request.Parties.Select(item => item == party
                ? item with { AdultIds = people.Select(id => id == request.FilerId ? invalid : id).Order(StringComparer.Ordinal).ToArray() } : item).ToArray()
            });
            foreach (var response in request.Responses)
                AssertInvalid(request with
                {
                    Responses = request.Responses.Select(item => item == response
                    ? item with { PartyAdults = item.PartyAdults.Append(invalid).Order(StringComparer.Ordinal).ToArray() } : item).ToArray()
                });
            if (request.Receipt is { } receipt)
                AssertInvalid(request with
                {
                    Receipt = receipt with
                    {
                        Parties = receipt.Parties.Select(item => item.HouseholdId == party.HouseholdId
                    ? item with { AdultIds = item.AdultIds.Append(invalid).Order(StringComparer.Ordinal).ToArray() } : item).ToArray()
                    }
                });
        }
        foreach (var invalid in new[] { people.Append(people[0]).Order(StringComparer.Ordinal).ToArray(), people.Reverse().ToArray() })
            AssertInvalid(request with { Parties = request.Parties.Select(item => item == party ? item with { AdultIds = invalid } : item).ToArray() });
        AssertInvalid(request with { Id = new string('k', 257) });
        AssertInvalid(request with { RightVersions = request.RightVersions.Select(version => version with { Version = "stale" }).ToArray() });
        if (request.Receipt is not null)
            AssertInvalid(request with { Responses = request.Responses.Where(response => response.AgentId != request.FilerId).ToArray() });

        void AssertInvalid(TownLandTransferRequest damaged)
        {
            var corrupted = state with
            {
                Towns = state.Towns!.Select(town => town.Id == request.TownId
                ? town with { LandHearings = town.LandHearings with { Transfers = town.LandHearings.Transfers.Select(item => item.Id == request.Id ? damaged : item).ToArray() } } : town).ToArray()
            };
            Assert.Throws<InvalidDataException>(() => PrivateWorldRuntimeCodec.Encode(corrupted));
        }
    }

    private sealed class NativeTransferReply(string candidate, string target, GridPoint[] tiles) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var answer = JsonSerializer.Serialize(new
            {
                selected_candidate_id = candidate,
                confidence = 1,
                civic_land_tiles = tiles.Select(tile => new { x = tile.X, y = tile.Y }),
                civic_land_hearing = new { household_id = target }
            });
            var response = JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = answer } } } });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(response, Encoding.UTF8, "application/json") });
        }
    }

    private sealed class NativeTransferChoices(string actor, string target, GridPoint[] tiles) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 0;
        public bool Propose { get; set; } = true;
        public bool Read { get; set; }
        public bool Accept { get; set; }
        public string? Hold { get; set; }
        public ConcurrentQueue<(string Actor, string Id)> Selected { get; } = new();
        public ConcurrentQueue<(CognitionDecisionRequest Request, CognitionDecisionResponse Response)> Replies { get; } = new();
        private async ValueTask<CognitionDecisionResponse> ProposeOverHttpAsync(CognitionDecisionRequest request,
            string candidate, CancellationToken cancellationToken)
        {
            using var client = new HttpClient(new NativeTransferReply(candidate, target, tiles));
            var provider = new OpenAiCompatibleDecisionProvider(client, () => "synthetic-key",
                new Uri("https://model.test/v1/chat/completions"), "synthetic-model");
            return await provider.DecideAsync(request, cancellationToken);
        }

        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var observation = request.Observation;
            CognitionCandidate? Pick(string action) => observation.Candidates.FirstOrDefault(item => item.Id.Contains("|" + action + "|", StringComparison.Ordinal));
            var proposal = Propose && observation.InhabitantId == actor ? Pick("land_transfer_propose") : null;
            var choice = proposal ?? (Read ? Pick("land_transfer_read") ?? Pick("read") : null) ??
                (Accept && observation.InhabitantId != Hold ? Pick("land_transfer_accept") : null) ??
                observation.Candidates.Single(item => item.Id == "safe_idle");
            var proposing = choice == proposal;
            if (proposing)
            {
                Assert.Contains(target, choice.Description, StringComparison.Ordinal);
                Assert.Contains(observation.Candidates, item => item.Id.Contains("|land_transfer_sell|", StringComparison.Ordinal) &&
                    item.Description.Contains(target, StringComparison.Ordinal));
                Propose = false;
            }
            Selected.Enqueue((observation.InhabitantId, choice.Id));
            if (proposing) return ProposeOverHttpAsync(request, choice.Id, cancellationToken);
            var response = new CognitionDecisionResponse(request.RequestId, observation.InhabitantId, Kind, ProviderEpoch,
                observation.RunEpoch, observation.DecisionGeneration, observation.ObservationDigest, choice.Id, 1,
                observation.Candidates.ToDictionary(item => item.Id, item => item.Id == choice.Id ? 1d : 0d, StringComparer.Ordinal));
            Replies.Enqueue((request, response));
            return ValueTask.FromResult(response);
        }
    }
}

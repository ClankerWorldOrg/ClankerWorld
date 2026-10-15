using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Tests;

public sealed partial class TownLandHearingRuntimeTests
{
    private const string PropertyFiler = NormalPathWorld.Founder2;
    private const string PropertyHouse = "first-town-house-b";
    private static readonly JsonSerializerOptions PropertyJsonOptions = new(JsonSerializerDefaults.Web);

    // Only the already tested election and opening are shared: every test runs its own
    // actual personal choices, inventory changes and serialized world boundary.
    private static readonly Lazy<Task<byte[]>> PendingPropertyCase = new(async () =>
    {
        var election = new HearingProvider();
        using var elected = NewWorld(election);
        await UntilAsync(elected, () => elected.Towns[0].Government!.Offices.Any(office => office.HolderId == Judge), Day * 3, election);
        var state = elected.ExportState();
        var household = state.Society.Society.GetAgent(Filer).HouseholdId!;
        var stock = state.Society.Society.Inventory.Lots.First(lot => lot.OwnerId == household &&
            lot.StorageBuildingId == PropertyHouse && lot.ContainerLotId is null && lot.Quantity > 1);
        var inventory = InventoryAuthority.Transfer(state.Society.Society.Inventory, "personal-held", household, Filer,
            stock.Id, 1, "personal gift retained at the House", destinationStorageBuildingId: PropertyHouse);
        using var stocked = WorldRuntime.Restore(state with
        { Society = state.Society with { Society = state.Society.Society with { Inventory = StockHoldTestState.Custody(inventory) } } }, _ => election);
        Assert.True(stocked.DisplaceAdult(Filer));
        Assert.True(stocked.DisplaceAdult(Waiver));
        var provider = new PropertyProvider { Request = true };
        using var world = WorldRuntime.Restore(stocked.ExportState(), _ => provider);
        await PropertyUntil(world, () => world.Towns[0].LandRecords.Cases.Count > 0, 20);
        var item = Assert.Single(world.Towns[0].LandRecords.Cases);
        Assert.Equal("property", item.Kind);
        Assert.Equal(new[] { Filer, Waiver }, item.Property!.Snapshots[0].LivingFormerMemberIds);
        Assert.Equal(item.Revisions[0].PublishedTick + Day, item.Revisions[0].DeadlineTick);
        Assert.Empty(item.Property.Consents);
        Assert.Null(item.Property.Transfer);
        world.Validate();
        return WorldRuntimeCodec.Encode(world.ExportState());
    });

    [Fact]
    public async Task AnEmptyHouseholdsPaidHouseIsOfferedForATownPropertyCaseAndDepartureDoesNotTransferIt()
    {
        var provider = new HearingProvider { SeekMayor = false };
        using var world = NewWorld(provider);
        var source = world.ExportState().Society.Society.GetAgent(Filer).HouseholdId!;
        var house = Assert.Single(world.ExportState().WorldSimulation!.Buildings,
            building => building.HouseholdId == source && world.ExportState().WorldContent!.Buildings.Any(definition =>
                definition.CanonicalId == building.DefinitionId && definition.Tags.Contains("house", StringComparer.Ordinal)));
        Assert.True(world.DisplaceAdult(Filer));
        Assert.True(world.DisplaceAdult(Waiver));
        Assert.Empty(world.ExportState().Society.Society.Households.Single(household => household.Id == source).MemberIds);
        Assert.Equal(source, world.ExportState().WorldSimulation!.Buildings.Single(building => building.InstanceId == house.InstanceId).HouseholdId);
        for (var step = 0; step < 5; step++)
        {
            Prompt(world, Judge, "Consider a Town property hearing for the empty household's House.");
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        }
        Assert.Contains(provider.Offered[Judge], choice => choice.Contains("|hearing_property_request|", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PersonalOwnerAgreementsAndAnIndependentRulingTransferActualStockAndReplayExactly()
    {
        var save = await PendingPropertyCase.Value;
        var provider = new PropertyProvider { Agree = true, Rule = true };
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(save), _ => provider);
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(save), _ => new PropertyProvider { Agree = true, Rule = true });
        var before = world.ExportState();
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(save, WorldRuntimeCodec.Encode(world.ExportState()));
        for (var step = 0; step < 20 && world.Towns[0].LandRecords.Cases[0].Status == "pending"; step++)
        {
            PropertyPromptAll(world);
            PropertyPromptAll(replay);
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        }
        var item = Assert.Single(world.Towns[0].LandRecords.Cases);
        Assert.Equal("settled", item.Status);
        var transfer = Assert.IsType<TownPropertyTransfer>(item.Property!.Transfer);
        var ruling = Assert.Single(item.Rulings);
        Assert.Equal(("reclaim", Judge), (ruling.Outcome.Kind, ruling.Judge.AgentId));
        Assert.All(new[] { Filer, Waiver }, actor => Assert.Contains(item.Property.Consents, consent => consent.AgentId == actor && consent.Agreed));
        Assert.Equal(PropertyHouse, transfer.ResultBuilding.InstanceId);
        Assert.Null(transfer.ResultBuilding.HouseholdId);
        Assert.NotEmpty(transfer.PriorLots);
        Assert.Equal(transfer.PriorLots.Sum(lot => lot.Quantity), transfer.ResultLots.Sum(lot => lot.Quantity));
        Assert.All(transfer.ResultLots, lot => Assert.Equal(world.Towns[0].Id, lot.OwnerId));
        Assert.Equal(before.Towns![0].ResidentIds, world.Towns[0].ResidentIds);
        Assert.Equal(before.Fields, world.ExportState().Fields);
        Assert.All(before.Society.Society.Inventory.Lots.Where(lot => lot.OwnerId == Filer || lot.OwnerId == Waiver), personal =>
            Assert.Equal(personal.OwnerId, world.ExportState().Society.Society.Inventory.GetLot(personal.Id).OwnerId));
        Assert.DoesNotContain(world.HouseholdLandUseRights, right => right.HouseholdId == transfer.PriorBuilding.HouseholdId &&
            right.Tiles.Any(item.Revisions[0].Tiles.Contains));
        world.Validate();
        replay.Validate();
        var final = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.Equal(final, WorldRuntimeCodec.Encode(replay.ExportState()));
        using var reloaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(final), _ => new PropertyProvider());
        Assert.Equal(final, WorldRuntimeCodec.Encode(reloaded.ExportState()));
        AssertDamagedPropertyFilesRefused(world.ExportState());
        var personal = Assert.Single(before.Society.Society.Inventory.Lots, lot => lot.OwnerId == Filer &&
            lot.StorageBuildingId == PropertyHouse && lot.Id.EndsWith("#transfer:personal-held", StringComparison.Ordinal));
        Assert.Equal(Filer, world.ExportState().Society.Society.Inventory.GetLot(personal.Id).OwnerId);
        using var collector = WorldRuntime.Restore(world.ExportState(), _ => new PropertyProvider { Collect = personal.Id });
        await PropertyUntil(collector, () => PersonalEquipmentRules.IsPhysicallyCarried(collector.ExportState().Society.Society.Inventory,
            collector.ExportState().Society.Society.Inventory.GetLot(personal.Id), Filer), 16);
        Assert.Equal(Filer, collector.ExportState().Society.Society.Inventory.GetLot(personal.Id).OwnerId);
        collector.Validate();
    }

    [Fact]
    public async Task AResponseWaiverAndTheFullNoticeDeadlineNeverSupplyPropertyConsent()
    {
        var provider = new PropertyProvider { Waive = true };
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(await PendingPropertyCase.Value), _ => provider);
        await PropertyUntil(world, () => world.Towns[0].LandRecords.Cases[0].Reads.Any(read => read.AgentId == Judge) &&
            world.Towns[0].LandRecords.Cases[0].Responses.Any(response => response.AgentId == Waiver && response.Kind == "waive"), 12);
        var item = world.Towns[0].LandRecords.Cases[0];
        Assert.Empty(item.Property!.Consents);
        Assert.Null(item.Property.Transfer);
        // Invoke the adjudication boundary at the actual notice's deadline: the real case,
        // actual elected mandate and read evidence are retained; elapsed time supplies no agreement.
        Assert.Throws<InvalidOperationException>(() => TownLandHearingRules.Rule(world.Towns[0].LandRecords, world.ExportState().Map,
            item.Id, item.Revisions[0].Number, item.Judge!, item.Revisions[0].DeadlineTick,
            TownPropertyRules.Outcome(item.Property.Request), item.Evidence.Select(evidence => evidence.Id).ToArray(), [],
            "The notice period ended; inspect the property consent requirement.", world.HouseholdLandUseRights,
            item.Revisions[0].Parties, true));
        Assert.Equal(item.Property.Request.SourceHouseholdId, world.ExportState().WorldSimulation!.Buildings.Single(building => building.InstanceId == PropertyHouse).HouseholdId);
        world.Validate();
    }

    [Fact]
    public async Task ALivingFormerMemberCanRefuseAndTheJudgeRejectsWithoutTakingProperty()
    {
        var provider = new PropertyProvider { Agree = true, Refuser = Waiver, Reject = true };
        var state = WorldRuntimeCodec.Decode(await PendingPropertyCase.Value);
        var moved = SocietyRules.CreateHousehold(state.Society.Society, "household:former-member", "Another household", [Waiver]);
        using var world = WorldRuntime.Restore(state with { Society = state.Society with { Society = moved.Checkpoint } }, _ => provider);
        await PropertyUntil(world, () => world.Towns[0].LandRecords.Cases[0].Status == "settled", 20);
        var item = Assert.Single(world.Towns[0].LandRecords.Cases);
        Assert.Contains(item.Property!.Consents, consent => consent.AgentId == Waiver && !consent.Agreed);
        Assert.Equal("reject", Assert.Single(item.Rulings).Outcome.Kind);
        Assert.Null(item.Property.Transfer);
        Assert.Empty(world.Towns[0].LandRecords.Adjustments);
        Assert.Equal(item.Property.Request.SourceHouseholdId, world.ExportState().WorldSimulation!.Buildings.Single(building => building.InstanceId == PropertyHouse).HouseholdId);
        var saved = WorldRuntimeCodec.Encode(world.ExportState());
        using var reloaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), _ => new PropertyProvider());
        reloaded.Validate();
        Assert.Equal(saved, WorldRuntimeCodec.Encode(reloaded.ExportState()));
    }

    [Fact]
    public async Task ChangedSharedStockPublishesAFreshNoticeAndRequiresFreshPropertyConsent()
    {
        using var agreed = WorldRuntime.Restore(WorldRuntimeCodec.Decode(await PendingPropertyCase.Value),
            _ => new PropertyProvider { Agree = true });
        await PropertyUntil(agreed, () => agreed.Towns[0].LandRecords.Cases[0].Property!.Consents.Count == 2, 15);
        var state = agreed.ExportState();
        var item = state.Towns![0].LandRecords.Cases[0];
        var lot = item.Property!.Snapshots[0].SharedLots.First(stock => stock.ContainerLotId is null && stock.Quantity > 1);
        var inventory = InventoryAuthority.Transfer(state.Society.Society.Inventory, "changed-stock", lot.OwnerId, Filer,
            lot.Id, 1, "personal transfer before adjudication", destinationStorageBuildingId: lot.StorageBuildingId,
            destinationGroundPosition: lot.GroundPosition);
        using var world = WorldRuntime.Restore(state with
        { Society = state.Society with { Society = state.Society.Society with { Inventory = StockHoldTestState.Custody(inventory) } } }, _ => new PropertyProvider());
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var current = world.Towns[0].LandRecords.Cases[0];
        Assert.Equal(2, current.Revisions.Count);
        Assert.Equal(current.Revisions[1].PublishedTick + Day, current.Revisions[1].DeadlineTick);
        Assert.Equal(2, current.Property!.Snapshots.Count);
        Assert.All(current.Property.Consents, consent => Assert.Equal(1, consent.Revision));
        Assert.Null(current.Property.Transfer);
        world.Validate();
        var saved = WorldRuntimeCodec.Encode(world.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), _ => new PropertyProvider());
        Assert.Equal(saved, WorldRuntimeCodec.Encode(restored.ExportState()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(153)]
    [InlineData(256)]
    public async Task TheTownCanGrantItsRecoveredHouseAndGoodsToAnAcceptingHousehold(int householdIdLength)
    {
        var provider = new PropertyProvider { Agree = true, Rule = true };
        using var reclaimed = WorldRuntime.Restore(WorldRuntimeCodec.Decode(await PendingPropertyCase.Value), _ => provider);
        await PropertyUntil(reclaimed, () => reclaimed.Towns[0].LandRecords.Cases[0].Status == "settled", 20);
        var state = reclaimed.ExportState();
        // A legitimate receiving household is formed with the same Society operation used by
        // household_found. No House, stock, hearing, vote, permission or Town membership is added.
        const string prefix = "household:property-recipient";
        var recipient = householdIdLength == 0 ? prefix : prefix + new string('r', householdIdLength - prefix.Length);
        Assert.Equal(householdIdLength == 0 ? prefix.Length : householdIdLength, recipient.Length);
        var society = SocietyRules.CreateHousehold(state.Society.Society, recipient, "Receiving household", [Filer]);
        var onward = new PropertyProvider { Request = true, Target = recipient, Agree = true, Rule = true };
        using var world = WorldRuntime.Restore(state with { Society = state.Society with { Society = society.Checkpoint } }, _ => onward);
        await PropertyUntil(world, () => world.Towns[0].LandRecords.Cases.Count == 2 && world.Towns[0].LandRecords.Cases[1].Status == "settled", 25);
        var item = world.Towns[0].LandRecords.Cases[1];
        Assert.Equal("grant", Assert.Single(item.Rulings).Outcome.Kind);
        var transfer = Assert.IsType<TownPropertyTransfer>(item.Property!.Transfer);
        Assert.Equal(recipient, transfer.ResultBuilding.HouseholdId);
        Assert.All(transfer.ResultLots, lot => Assert.Equal(recipient, lot.OwnerId));
        Assert.Contains(item.Property.Consents, consent => consent.AgentId == Filer && consent.Agreed);
        Assert.Contains(world.HouseholdLandUseRights, right => right.HouseholdId == recipient && right.Tiles.SequenceEqual(item.Revisions[0].Tiles));
        Assert.Equal(state.Towns![0].ResidentIds, world.Towns[0].ResidentIds);
        Assert.Equal(state.TownLandTitles, world.ExportState().TownLandTitles);
        world.Validate();
        var saved = WorldRuntimeCodec.Encode(world.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), _ => new PropertyProvider());
        Assert.Equal(saved, WorldRuntimeCodec.Encode(restored.ExportState()));
    }

    [Fact]
    public async Task RemovingTheNoticedHouseAndAddingALandClaimKeepsThePropertyFileValid()
    {
        var state = WorldRuntimeCodec.Decode(await PendingPropertyCase.Value);
        var item = state.Towns![0].LandRecords.Cases[0];
        var plot = item.Revisions[0].Tiles;
        var ground = state.Map.Tiles.First(tile => state.Map.IsBuildable(tile.Position) && !plot.Contains(tile.Position)).Position;
        var inventory = state.Society.Society.Inventory;
        foreach (var lot in inventory.Lots.Where(lot => lot.ContainerLotId is null &&
            (lot.StorageBuildingId == PropertyHouse || lot.DeliveryBuildingId == PropertyHouse)).ToArray())
            inventory = InventoryAuthority.Relocate(inventory, "empty-noticed-house-" + inventory.Events.Count,
                lot.Id, lot.OwnerId, lot.Quantity, groundPosition: new InventoryGroundPosition(ground.X, ground.Y));
        using var world = WorldRuntime.Restore(state with
        { Society = state.Society with { Society = state.Society.Society with { Inventory = StockHoldTestState.Custody(inventory) } } }, _ => new PropertyProvider());
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(2, world.Towns[0].LandRecords.Cases[0].Revisions.Count);
        Assert.True(world.RemoveBuilding(PropertyHouse, state.Towns[0].Id, item.Property!.Request.SourceHouseholdId).Applied);
        Assert.True(world.RequestHouseholdLandUse("removed-property-new-party", Judge, state.Towns[0].Id, [plot[0]]).Applied);
        var current = world.Towns[0].LandRecords.Cases[0];
        Assert.Equal(2, current.Revisions.Count);
        Assert.Equal(2, current.Property!.Snapshots.Count);
        Assert.Null(current.Judge); // The new claimant cannot remain this case's judge.
        Assert.Equal("pending", current.Status);
        Assert.Null(current.Property.Transfer);
        Assert.Empty(current.Rulings);
        Assert.Equal(inventory.Lots.Where(lot => lot.Quantity > 0).Select(lot => (lot.Id, lot.OwnerId, lot.Quantity)),
            world.ExportState().Society.Society.Inventory.Lots.Where(lot => lot.Quantity > 0).Select(lot => (lot.Id, lot.OwnerId, lot.Quantity)));
        world.Validate();
        var saved = WorldRuntimeCodec.Encode(world.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), _ => new PropertyProvider());
        Assert.Equal(saved, WorldRuntimeCodec.Encode(restored.ExportState()));
        Assert.Null(restored.Towns[0].LandRecords.Cases[0].Property!.Transfer);
    }

    private static void AssertDamagedPropertyFilesRefused(WorldRuntimeState state)
    {
        var town = state.Towns![0];
        var item = town.LandRecords.Cases[0];
        var property = item.Property!;
        var duplicateRevision = item with { Revisions = [item.Revisions[0], item.Revisions[0]] };
        var duplicateState = state with { Towns = [town with { LandRecords = town.LandRecords with { Cases = [duplicateRevision] } }] };
        Assert.Throws<InvalidDataException>(() =>
        {
            using var invalid = WorldRuntime.Restore(duplicateState, _ => new PropertyProvider());
        });
        var duplicateDocument = JsonNode.Parse(WorldRuntimeCodec.Encode(state))!;
        var revisions = duplicateDocument["state"]!["towns"]![0]!["landRecords"]!["cases"]![0]!["revisions"]!.AsArray();
        revisions.Add(revisions[0]!.DeepClone());
        Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Decode(System.Text.Encoding.UTF8.GetBytes(duplicateDocument.ToJsonString())));
        TownPropertyCase?[] damages = [null, property with { Consents = [] }, property with { Transfer = null },
            property with { Snapshots = [property.Snapshots[0] with { LivingFormerMemberIds = [Filer] }] },
            property with { Transfer = property.Transfer! with { ResultBuilding = property.Transfer.ResultBuilding with { HouseholdId = property.Request.SourceHouseholdId } } },
            property with { Transfer = property.Transfer! with { ResultLots = property.Transfer.ResultLots.Select(lot => lot with { Quantity = lot.Quantity + 1 }).ToArray() } }];
        foreach (var damage in damages)
        {
            var damaged = state with { Towns = [town with { LandRecords = town.LandRecords with { Cases = [item with { Property = damage }] } }] };
            Assert.Throws<InvalidDataException>(() =>
            {
                using var invalid = WorldRuntime.Restore(damaged, _ => new PropertyProvider());
            });
            var document = JsonNode.Parse(WorldRuntimeCodec.Encode(state))!;
            document["state"]!["towns"]![0]!["landRecords"]!["cases"]![0]!["property"] =
                damage is null ? null : JsonSerializer.SerializeToNode(damage, PropertyJsonOptions);
            Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Decode(System.Text.Encoding.UTF8.GetBytes(document.ToJsonString())));
        }
    }

    private static void PropertyPromptAll(WorldRuntime world)
    {
        foreach (var agent in world.Towns[0].ResidentIds) Prompt(world, agent, "Consider the public property case and make your own choice.");
    }

    private static async Task PropertyUntil(WorldRuntime world, Func<bool> done, int limit)
    {
        for (var step = 0; step < limit && !done(); step++)
        {
            PropertyPromptAll(world);
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        }
        Assert.True(done(), "The native property-case boundary was not reached: " + JsonSerializer.Serialize(world.ExportState().Events.TakeLast(8)));
    }

    private sealed class PropertyProvider : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;
        public bool Request { get; init; }
        public string BuildingId { get; init; } = PropertyHouse;
        public string? Target { get; init; }
        public bool? Agree { get; init; }
        public string? Refuser { get; init; }
        public bool Rule { get; init; }
        public bool Reject { get; init; }
        public bool Waive { get; init; }
        public string? Collect { get; init; }

        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var observation = request.Observation;
            var choices = observation.Candidates;
            CognitionCandidate? Find(string action) => choices.FirstOrDefault(candidate => candidate.Id.Contains("|" + action + "|", StringComparison.Ordinal));
            var selected = Find("read") ?? Find("hearing_inspect") ?? Find("yes");
            if (Collect is { } lot && observation.AgentId == Filer)
                selected ??= choices.FirstOrDefault(candidate => candidate.Id == "household_collect:" + lot);
            if (Request && observation.AgentId == PropertyFiler)
                selected ??= choices.FirstOrDefault(candidate => candidate.Id.Contains("|hearing_property_request|" + BuildingId + "|", StringComparison.Ordinal) &&
                    (Target is null ? candidate.Id.EndsWith("|town", StringComparison.Ordinal) :
                        candidate.Description.Contains("grant household " + Target + " ", StringComparison.Ordinal)));
            if (Agree is { } agree)
                selected ??= Find("hearing_property_" + (observation.AgentId == Refuser || !agree ? "refuse" : "accept"));
            if (Waive) selected ??= Find("hearing_waive");
            selected ??= Find("hearing_answer");
            if (observation.AgentId == Judge && (Rule || Reject))
                selected ??= choices.FirstOrDefault(candidate => candidate.Id.Contains("|hearing_rule|", StringComparison.Ordinal) &&
                    (candidate.Id.EndsWith("|reclaim", StringComparison.Ordinal) || candidate.Id.EndsWith("|grant", StringComparison.Ordinal) || Reject &&
                        candidate.Id.EndsWith("|reject", StringComparison.Ordinal)));
            selected ??= choices.Single(candidate => candidate.Id == "safe_idle");
            var evidence = selected.Description.Split([' ', ';', ','], StringSplitOptions.RemoveEmptyEntries)
                .Where(token => token.StartsWith("land-evidence:", StringComparison.Ordinal)).Select(token => token.Split('=')[0]).Distinct(StringComparer.Ordinal).ToArray();
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, observation.AgentId, Kind, ProviderEpoch,
                observation.RunEpoch, observation.DecisionGeneration, observation.ObservationDigest, selected.Id, 1,
                choices.ToDictionary(candidate => candidate.Id, candidate => candidate.Id == selected.Id ? 1d : 0d, StringComparer.Ordinal),
                CivicLandHearing: new(Statement: "Please decide this exact noticed property request using its ownership records and personal agreements.", EvidenceIds: evidence)));
        }
    }
}

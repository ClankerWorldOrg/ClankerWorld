using System.Collections.Concurrent;
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
    [InlineData(true, true)]
    public async Task OfferedProjectHelpSurvivesHostedReloadAndPersonalRecall(bool nativeBorn, bool fullHouse = false)
    {
        var initial = PrivateWorldRuntimeCodec.Decode(await (nativeBorn ? LaterAdult : ConversationAdult).Value);
        var society = initial.Society.Society;
        var participants = nativeBorn
            ? society.Births.OrderBy(birth => birth.CommittedTick).Select(birth => birth.ChildId)
                .Where(id => society.GetInhabitant(id).Status == SocietyInhabitantStatus.Active &&
                    society.GetInhabitant(id).AgeBand is SocietyAgeBand.Adult or SocietyAgeBand.Elder).TakeLast(2).ToArray()
            : society.Inhabitants.Where(person => person.HouseholdId == "household:camp-alpha" &&
                    person.Status == SocietyInhabitantStatus.Active && !society.Births.Any(birth => birth.ChildId == person.Id))
                .Take(2).Select(person => person.Id).ToArray();
        Assert.Equal(2, participants.Length);
        var requester = participants[0];
        var helper = participants[1];
        Assert.Equal(nativeBorn, requester.Length > 128);
        Assert.Equal(nativeBorn, helper.Length > 128);
        var household = Assert.IsType<string>(society.GetInhabitant(requester).HouseholdId);
        var house = initial.WorldSimulation!.Buildings.First(building => building.HouseholdId == household &&
            initial.WorldContent!.Buildings.Single(definition => definition.CanonicalId == building.DefinitionId)
                .Tags.Contains("house", StringComparer.Ordinal));
        var rope = initial.WorldContent!.Recipes.Single(recipe => recipe.LocalId == "twist-rope");
        // Arrange the project and unreserved stock only. Birth identities, ages,
        // positions, kinship, containers and active reservations remain native.
        var inventory = society.Inventory with
        {
            Lots = society.Inventory.Lots.Where(lot => lot.ItemKind != "fiber" ||
                    lot.OwnerId != household && lot.OwnerId != requester)
                .Select(lot => lot.StorageBuildingId == house.InstanceId && lot.ItemKind is "bread" or "food"
                    ? lot with
                    {
                        Quantity = Math.Max(1, society.Inventory.Reservations
                        .Where(reservation => reservation.LotId == lot.Id).Sum(reservation => reservation.Quantity))
                    }
                    : lot).ToArray(),
        };
        const string fiber = "project-recall-helper-fiber";
        inventory = InventoryFixture.AddLot(inventory, fiber, "fiber", helper, 3);
        var prepared = initial with
        {
            JevEnabled = false,
            RoutineHelper = RoutineHelperSettings.Off,
            Society = initial.Society with { Society = society with { Inventory = inventory } },
            Inhabitants = initial.Inhabitants.Select(person => person.InhabitantId == requester
                ? person with
                {
                    Project = new SettlementProject("build:recipe:" + rope.CanonicalId, rope.DisplayName, society.WorldTick,
                        "paused", Blocker: "Awaiting fiber", LastTransitionTick: society.WorldTick, RequiresFreshChoice: true),
                    LastDecisionContext = null,
                }
                : person with { LastDecisionContext = null }).ToArray(),
        };
        var choices = new ProjectRecallChoices(helper);
        if (fullHouse)
        {
            using var capacity = PrivateWorldRuntime.Restore(prepared, _ => choices);
            var room = capacity.DestinationRoom(house.InstanceId);
            Assert.True(room >= 3);
            prepared = prepared with
            {
                Society = prepared.Society with
                {
                    Society = prepared.Society.Society with
                    {
                        Inventory = InventoryFixture.AddLot(inventory, "full-project-house", "stone", household,
                            room, storageBuildingId: house.InstanceId),
                    },
                },
            };
        }
        using var world = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(PrivateWorldRuntimeCodec.Encode(prepared)), _ => choices);
        world.Resume();
        var directory = Directory.CreateTempSubdirectory("project-gratitude-recall-");
        try
        {
            var file = new PrivateWorldStateFile(Path.Combine(directory.FullName, "world.json"), _ => choices);
            file.Save(world);
            var presence = new OwnerClientPresenceLease(TimeSpan.FromMinutes(5));
            presence.RecordAuthenticatedReconnect("test-owner");
            using var host = new PrivateWorldRuntimeService(world, file, presence);
            async Task Step()
            {
                Assert.True(await host.TryAdvanceOnceAsync());
                await WaitForRequests(world);
                Assert.False(world.Society.IsPaused);
                Assert.Equal(world.WorldTick, PrivateWorldRuntimeCodec.Decode(File.ReadAllBytes(file.Path)).Society.Society.WorldTick);
            }
            bool Gratitude(SocietySocialMemory memory) => memory.OwnerId == requester && memory.SubjectId == helper &&
                memory.Id.StartsWith("project-gratitude:", StringComparison.Ordinal);
            if (fullHouse)
            {
                Assert.Equal(0, world.DestinationRoom(house.InstanceId));
                for (var tick = 0; tick < 8; tick++) await Step();
                Assert.Contains(choices.Observations, observation => observation.InhabitantId == helper);
                Assert.DoesNotContain(choices.Observations.Where(observation => observation.InhabitantId == helper),
                    observation => observation.Candidates.Any(candidate => candidate.Id == "assist:fiber"));
                Assert.Equal(0, choices.AssistChosen);
                Assert.DoesNotContain(world.Society.Memories, Gratitude);
                Assert.Equal(helper, world.Society.Inventory.GetLot(fiber).OwnerId);
                Assert.Equal(3, world.Society.Inventory.GetLot(fiber).Quantity);
                using var fullReload = file.LoadOrCreate(initial.WorldSeed);
                Assert.Equal(PrivateWorldRuntimeCodec.Encode(world.ExportState()), PrivateWorldRuntimeCodec.Encode(fullReload.ExportState()));
                return;
            }
            Assert.True(world.DestinationRoom(house.InstanceId) >= 3);
            for (var tick = 0; tick < 40 && !world.Society.Memories.Any(Gratitude); tick++) await Step();
            var memory = Assert.Single(world.Society.Memories, Gratitude);
            Assert.Equal(1, choices.AssistChosen);
            var delivered = world.Society.Inventory.GetLot(fiber);
            Assert.Equal((household, house.InstanceId, 3), (delivered.OwnerId, delivered.StorageBuildingId, delivered.Quantity));
            Assert.Null(delivered.ContainerLotId);
            Assert.Null(delivered.DeliveryBuildingId);
            Assert.Equal(SocietyMemoryKind.Relationship, memory.Kind);
            Assert.Equal("public", memory.Visibility);
            Assert.Equal($"Grateful for {society.GetInhabitant(helper).Name}'s help with project materials.", memory.Summary);
            var standing = Assert.Single(world.Inhabitants.Single(person => person.InhabitantId == requester).SocialStanding!,
                item => item.SubjectId == helper);
            var previousTrust = initial.Inhabitants.Single(person => person.InhabitantId == requester)
                .SocialStanding?.SingleOrDefault(item => item.SubjectId == helper)?.Trust ?? 0;
            Assert.Equal(Math.Min(10, previousTrust + 2), standing.Trust);
            Assert.Equal(memory.SourceTick, standing.LastChangedTick);
            var handoff = Assert.Single(world.ExportState().Events, item => item.Kind == "project_request_fulfilled" &&
                item.Detail == $"{helper}:{requester}:fiber:3");
            Assert.Equal(house.Position, handoff.Position);

            var capture = new ProjectRecallChoices(null);
            var reloadFile = new PrivateWorldStateFile(file.Path, _ => capture);
            using var restored = reloadFile.LoadOrCreate(initial.WorldSeed);
            Assert.Equal(memory, Assert.Single(restored.Society.Memories, Gratitude));
            Assert.Equal(delivered, restored.Society.Inventory.GetLot(fiber));
            restored.SubmitInstruction(new("project-recall-guidance", "owner:test", requester,
                OwnerInstructionKind.Suggestive, memory.Summary));
            using var resumedHost = new PrivateWorldRuntimeService(restored, reloadFile, presence);
            for (var tick = 0; tick < 12 && !capture.Observations.Any(observation => observation.InhabitantId == requester &&
                     observation.WorldTick > memory.SourceTick); tick++)
            {
                Assert.True(await resumedHost.TryAdvanceOnceAsync());
                await WaitForRequests(restored);
            }
            var observation = capture.Observations.First(item => item.InhabitantId == requester && item.WorldTick > memory.SourceTick);
            observation.Validate();
            var recalled = Assert.Single(observation.RetrievedMemories!, item => item.Id == memory.Id);
            Assert.Equal((memory.Summary, memory.SourceTick, "public"), (recalled.Summary, recalled.SourceTick, recalled.Visibility));
            Assert.InRange(memory.Id.Length, 1, 128);
            if (nativeBorn) Assert.StartsWith("project-gratitude:sha256:", memory.Id, StringComparison.Ordinal);
            else Assert.Equal($"project-gratitude:{requester}:{helper}", memory.Id);

            // Cancel actual external work before the checkpoint fork. Both
            // continuations use the same native blocking admission boundary.
            restored.CancelPendingHostedDecisions();
            reloadFile.Save(restored);
            using var disk = reloadFile.LoadOrCreate(initial.WorldSeed);
            var saved = PrivateWorldRuntimeCodec.Encode(restored.ExportState());
            Assert.Equal(saved, PrivateWorldRuntimeCodec.Encode(disk.ExportState()));
            using var original = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(saved), _ => new ProjectRecallChoices(null));
            using var replay = PrivateWorldRuntime.Restore(disk.ExportState(), _ => new ProjectRecallChoices(null));
            Assert.False((await original.AdvanceOneTickAsync(() => false)).Advanced);
            Assert.Equal(saved, PrivateWorldRuntimeCodec.Encode(original.ExportState()));
            for (var tick = 0; tick < 4; tick++)
            {
                Assert.True((await original.AdvanceOneTickAsync()).Advanced);
                Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
                Assert.Equal(PrivateWorldRuntimeCodec.Encode(original.ExportState()), PrivateWorldRuntimeCodec.Encode(replay.ExportState()));
            }
            Assert.Equal(memory, Assert.Single(original.Society.Memories, Gratitude));
            // Ordinary inventory processing advances its timestamp, while the
            // delivered quantity, owner and physical custody remain unchanged.
            Assert.Equal(delivered with { LastProcessedTick = original.WorldTick }, original.Society.Inventory.GetLot(fiber));
            Assert.Equal(standing, Assert.Single(original.Inhabitants.Single(person => person.InhabitantId == requester)
                .SocialStanding!, item => item.SubjectId == helper));
            Assert.Single(original.ExportState().Events, item => item.Kind == handoff.Kind && item.Detail == handoff.Detail);
        }
        finally { directory.Delete(true); }
    }

    private sealed class ProjectRecallChoices(string? helper) : IDecisionProvider
    {
        public ConcurrentQueue<InhabitantObservation> Observations { get; } = new();
        public int AssistChosen;
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 0;

        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            Observations.Enqueue(request.Observation);
            var selected = request.Observation.InhabitantId == helper &&
                request.Observation.Candidates.Any(candidate => candidate.Id == "assist:fiber") ? "assist:fiber" : "safe_idle";
            if (selected == "assist:fiber") Interlocked.Increment(ref AssistChosen);
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.InhabitantId,
                Kind, ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration,
                request.Observation.ObservationDigest, selected, 1, request.Observation.Candidates.ToDictionary(candidate => candidate.Id,
                    candidate => candidate.Id == selected ? 1d : 0d, StringComparer.Ordinal)));
        }
    }
}

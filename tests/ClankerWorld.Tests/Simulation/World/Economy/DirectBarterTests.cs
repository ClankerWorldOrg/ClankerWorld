using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Server.Observation;

namespace ClankerWorld.Tests;

public sealed class DirectBarterTests
{
    [Theory]
    [InlineData(false, "usable", false)]
    [InlineData(true, "usable", true)]
    [InlineData(true, "reserved", false)]
    [InlineData(true, "worn", false)]
    [InlineData(true, "ground", false)]
    public async Task PersonalBarterUsesUsableSurplusAcrossLotsAndKeepsReserve(bool split, string reserve, bool splitPayment)
    {
        var allowed = reserve == "usable";
        using var setup = NormalPathWorld.CreateGenerated("personal-barter-lots", _ => new TradeProvider("safe_idle"));
        Assert.True((await setup.AdvanceOneTickAsync()).Advanced);
        var state = setup.ExportState();
        var first = state.Agents[0].AgentId;
        var second = state.Agents[1].AgentId;
        var meeting = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == "first-town-warehouse").Position;
        var positions = state.Map.Tiles.Select(tile => tile.Position).Where(point => state.Map.IsPassable(point) &&
                state.Map.FootDistance(point, meeting) <= 1 &&
                state.Agents.All(person => person.AgentId == first || person.AgentId == second || person.Position != point))
            .OrderBy(point => point.Y).ThenBy(point => point.X).Take(2).ToArray();
        Assert.Equal(2, positions.Length);
        var inventory = state.Society.Society.Inventory with
        {
            Lots = state.Society.Society.Inventory.Lots.Where(lot =>
                !(lot.OwnerId == first || lot.OwnerId == second) || lot.ItemKind is not ("clothing" or "food" or "berries")).ToArray(),
        };
        inventory = InventoryAuthority.AddLot(inventory, "barter-clothing-a", "clothing", first, split ? 1 : 2);
        if (split) inventory = InventoryAuthority.AddLot(inventory, "barter-clothing-b", "clothing", first, 1);
        if (reserve == "reserved") inventory = InventoryAuthority.Reserve(inventory, "held-garment", first,
            "barter-clothing-b", 1, "other_work", inventory.WorldTick + 100);
        if (reserve == "ground") inventory = inventory with
        {
            Lots = inventory.Lots.Select(lot => lot.Id == "barter-clothing-b"
                ? lot with { GroundPosition = new(positions[0].X, positions[0].Y) } : lot).ToArray(),
        };
        if (splitPayment)
            for (var index = 0; index < 4; index++)
                inventory = InventoryAuthority.AddLot(inventory, "barter-berries-" + index, "berries", second, 1);
        else inventory = InventoryAuthority.AddLot(inventory, "barter-berries", "berries", second, 4);
        state = WithInventory(state, inventory) with
        {
            JevEnabled = false,
            RoutineHelper = RoutineHelperSettings.Off,
            Agents = state.Agents.Select(person => person with
            {
                Position = person.AgentId == first ? positions[0] : person.AgentId == second ? positions[1] : person.Position,
                FullnessBasisPoints = 7_500,
                Survival = new(),
                Equipment = new(person.AgentId == first && reserve == "worn" ? "barter-clothing-b" : null),
                LastDecisionContext = null,
                Project = null,
            }).ToArray(),
        };
        state = WeatherTestFixture.WithWeather(state, WeatherKind.Clear);
        var proposer = new TradeProvider("trade_propose:" + second);
        IDecisionProvider Provider(string id) => id == first ? proposer : new TradeProvider(id == second ? "trade_accept:" : "safe_idle");
        var bytes = WorldRuntimeCodec.Encode(state);
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), Provider);
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), id =>
            new TradeProvider(id == first ? "trade_propose:" + second : id == second ? "trade_accept:" : "safe_idle"));
        world.Validate();
        for (var tick = 0; tick < 30; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
        if (!allowed)
        {
            Assert.DoesNotContain("trade_propose:" + second, proposer.SeenCandidates);
            Assert.Empty(world.Society.Inventory.Offers);
            Assert.Equal(2, world.Society.Inventory.Lots.Where(lot => lot.OwnerId == first && lot.ItemKind == "clothing").Sum(lot => lot.Quantity));
            Assert.Equal(4, world.Society.Inventory.Lots.Where(lot => lot.OwnerId == second && lot.ItemKind == "berries").Sum(lot => lot.Quantity));
            if (reserve == "reserved") Assert.Equal(InventoryReservationState.Reserved, world.Society.Inventory.GetReservation("held-garment").State);
        }
        else
        {
            Assert.Contains("trade_propose:" + second, proposer.SeenCandidates);
            var offer = Assert.Single(world.Society.Inventory.Offers);
            Assert.Equal(DirectBarterState.Settled, offer.State);
            Assert.Equal(2, offer.AcceptedBy.Count);
            Assert.Equal(1, world.Society.Inventory.Lots.Where(lot => lot.OwnerId == first && lot.ItemKind == "clothing").Sum(lot => lot.Quantity));
            Assert.Equal(1, world.Society.Inventory.Lots.Where(lot => lot.OwnerId == second && lot.ItemKind == "clothing").Sum(lot => lot.Quantity));
            Assert.Equal(1, world.Society.Inventory.Lots.Where(lot => lot.OwnerId == first && lot.ItemKind == "berries").Sum(lot => lot.Quantity));
            Assert.Equal(3, world.Society.Inventory.Lots.Where(lot => lot.OwnerId == second && lot.ItemKind == "berries").Sum(lot => lot.Quantity));
            Assert.All(world.Society.Inventory.Reservations, reservation => Assert.Equal(InventoryReservationState.Completed, reservation.State));
        }
        world.Validate();
        var saved = WorldRuntimeCodec.Encode(world.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), Provider);
        Assert.Equal(saved, WorldRuntimeCodec.Encode(restored.ExportState()));
        restored.Validate();
    }

    [Theory]
    [InlineData(SocietyAgeBand.Adolescent)]
    public async Task AdultsCannotOfferTradeToYoungRecipientsBeforeOrAfterReload(SocietyAgeBand age)
    {
        using var seed = new WorldRuntime("young-barter-recipient");
        var state = seed.ExportState();
        var first = state.Agents[0].AgentId;
        var second = state.Agents[1].AgentId;
        state = WithAge(WithTradeGoods(state, first, second), second, age);
        var bytes = WorldRuntimeCodec.Encode(state);
        for (var round = 0; round < 2; round++)
        {
            var adultProvider = new TradeProvider("trade_propose:" + second);
            var youngProvider = new TradeProvider("trade_accept:");
            using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes),
                id => id == first ? adultProvider : id == second ? youngProvider : new TradeProvider("safe_idle"));
            world.Resume();
            for (var tick = 0; tick < 60; tick++) await world.AdvanceOneTickAsync();
            if (round == 0) Assert.NotEmpty(adultProvider.SeenCandidates);
            Assert.DoesNotContain("trade_propose:" + second, adultProvider.SeenCandidates);
            Assert.DoesNotContain(youngProvider.SeenCandidates, id => id.StartsWith("trade_", StringComparison.Ordinal));
            Assert.Empty(world.Society.Inventory.Offers);
            Assert.Empty(world.Society.Inventory.Reservations);
            Assert.Equal(4, world.Society.Inventory.GetLot("trade-food").Quantity);
            Assert.Equal(2, world.Society.Inventory.GetLot("trade-clothes").Quantity);
            world.Validate();
            bytes = WorldRuntimeCodec.Encode(world.ExportState());
        }
    }

    [Theory]
    [InlineData(SocietyAgeBand.Adolescent, false)]
    [InlineData(SocietyAgeBand.Adolescent, true)]
    public void DirectBarterRejectsEitherYoungPartyBeforeReservingStock(SocietyAgeBand age, bool youngFirst)
    {
        using var seed = new WorldRuntime("direct-young-barter");
        var state = seed.ExportState();
        var first = state.Agents[0].AgentId;
        var second = state.Agents[1].AgentId;
        state = WithAge(WithTradeGoods(state, first, second), youngFirst ? first : second, age);
        state = WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state));
        var society = state.Society.Society;
        var before = SocietyCheckpointCodec.Encode(society);
        Assert.Throws<InvalidOperationException>(() => SocietyRules.CreateBarterOffer(society,
            new("young-offer", 1, first, second, "trade-food", 1, "trade-clothes", 1, 120)));
        Assert.Equal(before, SocietyCheckpointCodec.Encode(society));
        Assert.Empty(society.Inventory.Offers);
        Assert.Empty(society.Inventory.Reservations);
    }

    [Fact]
    public void HouseholdBarterKeepsItsExistingNonAgentParticipation()
    {
        var checkpoint = SocietyRules.CreateGenesis("household-barter",
            [SocietyRules.CreateFounder("first", "First"), SocietyRules.CreateFounder("second", "Second")]);
        checkpoint = SocietyRules.CreateHousehold(checkpoint, "home-first", "First home", ["first"]).Checkpoint;
        checkpoint = SocietyRules.CreateHousehold(checkpoint, "home-second", "Second home", ["second"]).Checkpoint;
        const string first = "home-first";
        const string second = "home-second";
        checkpoint = checkpoint with
        {
            Inventory = InventoryAuthority.AddLot(InventoryAuthority.AddLot(checkpoint.Inventory,
                "household-first-goods", "tool", first, 2), "household-second-goods", "clothing", second, 2),
        };
        var offered = SocietyRules.CreateBarterOffer(checkpoint,
            new("household-offer", 1, first, second, "household-first-goods", 1, "household-second-goods", 1, 120)).Checkpoint;
        var accepted = SocietyRules.AcceptBarterOffer(offered, "household-offer", 1, first).Checkpoint;
        var settled = SocietyRules.AcceptBarterOffer(accepted, "household-offer", 1, second).Checkpoint;
        Assert.Equal(DirectBarterState.Settled, settled.Inventory.GetOffer("household-offer").State);
        Assert.NotNull(SocietyCheckpointCodec.Decode(SocietyCheckpointCodec.Encode(settled)));
    }

    [Fact]
    public async Task DirectBarterCannotOfferContainerContentsAsLooseLots()
    {
        using var seed = new WorldRuntime("container-settlement-trade");
        var state = seed.ExportState();
        var first = state.Agents[0].AgentId;
        var second = state.Agents[1].AgentId;
        state = WithTradeGoods(state, first, second);
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory,
            "trade-pot", InventoryContainerRules.StoragePot, first, 1);
        inventory = InventoryAuthority.PutIntoContainer(inventory, "put-trade-food-in-pot", first,
            "trade-pot", "trade-food", 4);
        state = WithInventory(state, inventory);
        var proposer = new TradeProvider("trade_propose:");
        using var world = WorldRuntime.Restore(state, id => id == first
            ? proposer : new TradeProvider("safe_idle"));

        Assert.True((await world.AdvanceOneTickAsync()).Advanced);

        Assert.DoesNotContain(proposer.SeenCandidates, id => id.StartsWith("trade_propose:", StringComparison.Ordinal));
        Assert.Empty(world.Society.Inventory.Offers);
        var food = Assert.Single(world.Society.Inventory.Lots, lot => lot.ContainerLotId == "trade-pot");
        Assert.Equal("food", food.ItemKind);
        Assert.Equal(4, food.Quantity);
        world.Validate();
    }

    [Fact]
    public async Task AdultAndElderCanIndependentlyCompleteBarterAfterReload()
    {
        using var seed = new WorldRuntime("elder-barter");
        var state = seed.ExportState();
        var first = state.Agents[0].AgentId;
        var second = state.Agents[1].AgentId;
        state = WithAge(WithTradeGoods(state, first, second), second, SocietyAgeBand.Elder);
        IDecisionProvider Provider(string id) => new TradeProvider(id == first ? "trade_propose:" + second :
            id == second ? "trade_accept:" : "safe_idle");
        using var world = WorldRuntime.Restore(state, Provider);
        await world.AdvanceOneTickAsync();
        var offer = Assert.Single(world.Society.Inventory.Offers);
        Assert.Equal([first], offer.AcceptedBy);
        using var reloaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(
            WorldRuntimeCodec.Encode(world.ExportState())), Provider);
        for (var tick = 0; tick < 120 && reloaded.Society.Inventory.GetOffer(offer.Id).State == DirectBarterState.Open; tick++)
            await reloaded.AdvanceOneTickAsync();
        var settled = reloaded.Society.Inventory.GetOffer(offer.Id);
        Assert.Equal(DirectBarterState.Settled, settled.State);
        Assert.Equal(2, settled.AcceptedBy.Count);
        Assert.All(reloaded.Society.Inventory.Reservations, reservation => Assert.Equal(InventoryReservationState.Completed, reservation.State));
        reloaded.Validate();
        Assert.NotNull(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(reloaded.ExportState())));
    }

    private static WorldRuntimeState WithAge(WorldRuntimeState state, string actor, SocietyAgeBand age)
    {
        var society = state.Society.Society;
        var years = age switch
        {
            SocietyAgeBand.Infant => 0,
            SocietyAgeBand.Child => society.Config.InfantYears,
            SocietyAgeBand.Adolescent => society.Config.ChildYears,
            _ => society.Config.ElderYears,
        };
        var birth = society.LifeTickAt(society.WorldTick) - years * society.Config.TicksPerLifecycleAge;
        return state with
        {
            Society = state.Society with
            {
                Society = society with
                {
                    Agents = society.Agents.Select(person => person.Id == actor ? person with
                    {
                        AgeBand = age,
                        BirthTick = birth,
                        BirthLifeTick = society.LifeClock is null ? null : birth,
                        LastLifecycleYearChecked = years,
                        CurrentRole = SocietyWorkRole.Unassigned,
                    } : person).ToArray(),
                },
            },
        };
    }

    private static WorldRuntimeState WithInventory(WorldRuntimeState state, InventoryCheckpoint inventory) => state with
    {
        Society = state.Society with
        {
            Society = state.Society.Society with { Inventory = inventory },
        },
    };

    [Theory]
    [InlineData(false, "food")]
    [InlineData(true, "food")]
    public async Task RecipientIndependentlyAcceptsOrDeclinesAcrossPauseAndRestart(bool decline, string foodKind)
    {
        using var seed = new WorldRuntime("settlement-trade-test");
        var state = seed.ExportState();
        var first = state.Agents[0].AgentId;
        var second = state.Agents[1].AgentId;
        state = WithTradeGoods(state, first, second, foodKind);
        IDecisionProvider Provider(string id) => new TradeProvider(id == first ? "trade_propose:" : id == second
            ? decline ? "trade_decline:" : "trade_accept:" : "safe_idle");
        using var world = WorldRuntime.Restore(state, Provider);
        await world.AdvanceOneTickAsync();
        var offered = Assert.Single(world.ExportState().Society.Society.Inventory.Offers);
        Assert.Equal(DirectBarterState.Open, offered.State);
        Assert.Equal([first], offered.AcceptedBy);
        Assert.Contains(new OwnerWorldObservationStore(world).GetSnapshot().Agents.Single(person => person.Id == second).SocialNotes,
            note => note.Contains("undecided", StringComparison.Ordinal));
        world.Pause();
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), Provider);
        restored.Resume();
        for (var tick = 0; tick < 120 && restored.ExportState().Society.Society.Inventory.GetOffer(offered.Id).State == DirectBarterState.Open; tick++)
        {
            await restored.AdvanceOneTickAsync();
        }
        var final = restored.ExportState();
        var inventory = final.Society.Society.Inventory;
        Assert.Equal(decline ? DirectBarterState.Cancelled : DirectBarterState.Settled, inventory.GetOffer(offered.Id).State);
        Assert.All(inventory.Reservations, reservation => Assert.Equal(decline ? InventoryReservationState.Released : InventoryReservationState.Completed, reservation.State));
        if (decline)
        {
            Assert.DoesNotContain(inventory.Lots, lot => lot.OwnerId == second && lot.ItemKind == foodKind);
            Assert.Empty(final.Society.Society.Memories);
            Assert.All(final.Agents, person => Assert.True(person.SocialStanding is null or []));
            Assert.Contains(final.Events, item => item.Kind == "barter_declined");
        }
        else
        {
            Assert.Contains(inventory.Lots, lot => lot.OwnerId == second && lot.ItemKind == foodKind && lot.Quantity == 1);
            Assert.Contains(inventory.Lots, lot => lot.OwnerId == first && lot.ItemKind == "clothing" && lot.Quantity == 1);
            Assert.Equal(2, final.Society.Society.Memories.Count);
            Assert.Equal(1, final.Agents.Single(person => person.AgentId == first).SocialStanding!
                .Single(item => item.SubjectId == second).Trust);
            Assert.Equal(1, final.Agents.Single(person => person.AgentId == second).SocialStanding!
                .Single(item => item.SubjectId == first).Trust);
            Assert.Contains(final.Events, item => item.Kind == "barter_completed");
        }
    }

    [Fact]
    public async Task ProposerCanWithdrawBeforeTheRecipientAccepts()
    {
        using var seed = new WorldRuntime("withdraw-settlement-trade");
        var state = seed.ExportState();
        var first = state.Agents[0].AgentId;
        var second = state.Agents[1].AgentId;
        var withdraw = false;
        using var world = WorldRuntime.Restore(WithTradeGoods(state, first, second),
            id => new TradeProvider(id == first ? withdraw ? "trade_decline:" : "trade_propose:" : "safe_idle"));
        await world.AdvanceOneTickAsync();
        withdraw = true;
        await world.AdvanceOneTickAsync();
        var inventory = world.ExportState().Society.Society.Inventory;
        Assert.Equal(DirectBarterState.Cancelled, Assert.Single(inventory.Offers).State);
        Assert.All(inventory.Reservations, reservation => Assert.Equal(InventoryReservationState.Released, reservation.State));
        Assert.Equal(4, inventory.GetLot("trade-food").Quantity);
    }

    [Fact]
    public async Task IgnoredOfferExpiresWithoutRepeatedProposalsOrLostItems()
    {
        using var seed = new WorldRuntime("ignored-settlement-trade");
        var state = seed.ExportState();
        var first = state.Agents[0].AgentId;
        var second = state.Agents[1].AgentId;
        using var world = WorldRuntime.Restore(WithTradeGoods(state, first, second),
            id => new TradeProvider(id == first ? "trade_propose:" : "safe_idle"));
        for (var tick = 0; tick < 125; tick++)
        {
            await world.AdvanceOneTickAsync();
        }
        var inventory = world.ExportState().Society.Society.Inventory;
        Assert.Equal(DirectBarterState.Cancelled, Assert.Single(inventory.Offers).State);
        Assert.All(inventory.Reservations, item => Assert.Equal(InventoryReservationState.Released, item.State));
        Assert.Equal(4, inventory.GetLot("trade-food").Quantity);
        Assert.Equal(2, inventory.GetLot("trade-clothes").Quantity);
    }

    [Theory]
    [InlineData("spoiled")]
    [InlineData("reservation_released")]
    public async Task UnusableOfferCancelsAndReleasesBothSidesInsteadOfBlockingTicks(string fault)
    {
        using var seed = new WorldRuntime("spoiled-settlement-trade");
        var state = seed.ExportState();
        var first = state.Agents[0].AgentId;
        var second = state.Agents[1].AgentId;
        using var world = WorldRuntime.Restore(WithTradeGoods(state, first, second),
            id => new TradeProvider(id == first ? "trade_propose:" : "safe_idle"));
        await world.AdvanceOneTickAsync();
        state = world.ExportState();
        state = state with
        {
            Society = state.Society with
            {
                Society = state.Society.Society with
                {
                    Inventory = state.Society.Society.Inventory with
                    {
                        Lots = state.Society.Society.Inventory.Lots.Select(lot => lot.Id != "trade-food" ? lot : fault switch
                        {
                            "spoiled" => lot with { FreshnessBasisPoints = 0 },
                            "ownership_changed" => lot with { OwnerId = "household:camp-alpha" },
                            _ => lot,
                        }).ToArray(),
                        Reservations = state.Society.Society.Inventory.Reservations.Select(reservation =>
                            fault == "reservation_released" && reservation.LotId == "trade-food"
                                ? reservation with { State = InventoryReservationState.Released } : reservation).ToArray(),
                    },
                },
            },
        };
        using var restored = WorldRuntime.Restore(state, _ => new TradeProvider("trade_accept:"));
        Assert.True((await restored.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(DirectBarterState.Cancelled, Assert.Single(restored.ExportState().Society.Society.Inventory.Offers).State);
        Assert.All(restored.ExportState().Society.Society.Inventory.Reservations,
            reservation => Assert.Equal(InventoryReservationState.Released, reservation.State));
        Assert.True((await restored.AdvanceOneTickAsync()).Advanced);
    }

    private static WorldRuntimeState WithTradeGoods(WorldRuntimeState state, string first, string second, string foodKind = "food")
    {
        state = AtTradeMeeting(state, first, second);
        return state with
        {
            Agents = state.Agents.Select(person => person with { FullnessBasisPoints = 7_500 }).ToArray(),
            Society = state.Society with
            {
                Society = state.Society.Society with
                {
                    Inventory = InventoryAuthority.AddLot(
                        InventoryAuthority.AddLot(state.Society.Society.Inventory, "trade-food", foodKind, first, 4),
                        "trade-clothes", "clothing", second, 2),
                },
            },
        };
    }

    internal static WorldRuntimeState AtTradeMeeting(WorldRuntimeState state, string first, string second)
    {
        var meeting = state.Map.GetObject("storage").Position;
        var positions = state.Map.Tiles.Select(tile => tile.Position).Where(point => state.Map.IsPassable(point) &&
            state.Map.FootDistance(point, meeting) <= 1 &&
            state.Agents.All(person => person.AgentId == first || person.AgentId == second || person.Position != point))
            .OrderBy(point => point.Y).ThenBy(point => point.X).Take(2).ToArray();
        Assert.Equal(2, positions.Length);
        Assert.All(positions, point => Assert.True(state.Map.IsReachableOnFoot(point, meeting)));
        return state with
        {
            Agents = state.Agents.Select(person => person.AgentId == first
                ? person with { Position = positions[0] }
                : person.AgentId == second ? person with { Position = positions[1] } : person).ToArray(),
        };
    }

    private sealed class TradeProvider(string prefix) : IDecisionProvider
    {
        public HashSet<string> SeenCandidates { get; } = new(StringComparer.Ordinal);
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            foreach (var item in request.Observation.Candidates) SeenCandidates.Add(item.Id);
            var candidate = request.Observation.Candidates.FirstOrDefault(item => item.Id.StartsWith(prefix, StringComparison.Ordinal))
                ?? request.Observation.Candidates.Single(item => item.Id == "safe_idle");
            return new DeterministicDecisionProvider().DecideAsync(request with
            {
                Observation = request.Observation with { Candidates = [candidate] },
            }, cancellationToken);
        }
    }
}

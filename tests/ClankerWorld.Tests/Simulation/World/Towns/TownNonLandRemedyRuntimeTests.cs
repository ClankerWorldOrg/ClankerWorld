using System.Text;
using System.Text.Json.Nodes;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed class TownNonLandRemedyRuntimeTests
{
    [Fact]
    public async Task PersonalConsentReservesNothingAndOnlyTheActualTransferCompletesTheAgreementOnceAcrossReplay()
    {
        var state = await NonLandRuntimeFixture.AcceptedRemedyAsync();
        var ledger = state.Towns![0].NonLandHearings;
        var agreement = Assert.Single(ledger.Agreements);
        Assert.Equal(agreement.AcceptedTick + 3L * NonLandRuntimeFixture.Day, agreement.DeadlineTick);
        Assert.Equal(NonLandRuntimeFixture.Subject, Assert.Single(agreement.Consents).AgentId);
        Assert.Equal(agreement.TermsHash, agreement.Consents[0].TermsHash);
        Assert.Empty(ledger.Effects);
        Assert.Empty(ledger.NativeReceipts);
        Assert.DoesNotContain(state.Society.Society.Inventory.Reservations, reservation => reservation.LotId == NonLandRuntimeFixture.ReturnLot);
        var total = state.Society.Society.Inventory.Lots.Where(lot => lot.ItemKind == "wood").Sum(lot => lot.Quantity);
        var provider = CompletionProvider();
        using var world = NonLandRuntimeFixture.Create(state, provider);
        NonLandRuntimeFixture.Wake(world, NonLandRuntimeFixture.Subject, "perform-voluntary-return");
        var before = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Contains(provider.Selected, selected => selected.Contains("nonviolent_remedy:", StringComparison.Ordinal));
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = NonLandRuntimeFixture.Create(WorldRuntimeCodec.Decode(before), CompletionProvider());
        for (var tick = 0; tick < 3; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        }

        var completed = world.Towns[0].NonLandHearings;
        Assert.Equal("completed", Assert.Single(completed.Agreements).Status);
        var effect = Assert.Single(completed.Effects);
        var receipt = Assert.Single(completed.NativeReceipts);
        Assert.Equal(receipt.Id, effect.NativeReceiptId);
        Assert.Equal(receipt.Version, effect.NativeReceiptVersion);
        Assert.Equal(1, effect.Quantity);
        Assert.Equal(NonLandRuntimeFixture.Subject, effect.ActorId);
        Assert.Equal(NonLandRuntimeFixture.Witness, effect.BeneficiaryId);
        Assert.Equal("return_goods", receipt.Kind);
        Assert.Equal(NonLandRuntimeFixture.Subject, receipt.PreviousOwnerId);
        Assert.Equal(NonLandRuntimeFixture.Witness, receipt.ResultOwnerId);
        Assert.Equal(1, world.Society.Inventory.GetLot(NonLandRuntimeFixture.ReturnLot).Quantity);
        Assert.Equal(NonLandRuntimeFixture.Witness, world.Society.Inventory.GetLot(receipt.ResultLotId).OwnerId);
        Assert.Equal(total, world.Society.Inventory.Lots.Where(lot => lot.ItemKind == "wood").Sum(lot => lot.Quantity));
        Assert.Single(world.ExportState().Events, item => item.Kind == "voluntary_goods_returned");
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        NonLandRuntimeFixture.Strict(world.ExportState());

        // Duplicating a real receipt is not another completed contribution.
        var document = JsonNode.Parse(WorldRuntimeCodec.Encode(world.ExportState()))!;
        var effects = document["state"]!["towns"]![0]!["nonLandHearings"]!["effects"]!.AsArray();
        effects.Add(effects[0]!.DeepClone());
        Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Decode(Encoding.UTF8.GetBytes(document.ToJsonString())));
    }

    [Fact]
    public async Task ReservedGoodsDoNotBecomeAvailableThroughConsentAndTheSameAgreementResumesAfterNativeRelease()
    {
        var state = await NonLandRuntimeFixture.AcceptedRemedyAsync();
        var inventory = InventoryAuthority.Reserve(state.Society.Society.Inventory, "independent-wood-work",
            NonLandRuntimeFixture.Subject, NonLandRuntimeFixture.ReturnLot, 2, "independent-work", long.MaxValue);
        state = state with { Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } } };
        var provider = CompletionProvider();
        using (var blocked = NonLandRuntimeFixture.Create(NonLandRuntimeFixture.Strict(state), provider))
        {
            NonLandRuntimeFixture.Wake(blocked, NonLandRuntimeFixture.Subject, "blocked-voluntary-return");
            Assert.True((await blocked.AdvanceOneTickAsync()).Advanced);
            Assert.DoesNotContain(provider.Observations.Where(observation => observation.AgentId == NonLandRuntimeFixture.Subject)
                .SelectMany(observation => observation.Candidates), candidate => candidate.Id.StartsWith("nonviolent_remedy:", StringComparison.Ordinal));
            Assert.Empty(blocked.Towns[0].NonLandHearings.Effects);
            Assert.Equal(2, blocked.Society.Inventory.GetLot(NonLandRuntimeFixture.ReturnLot).Quantity);
            state = NonLandRuntimeFixture.Strict(blocked.ExportState());
        }
        inventory = InventoryAuthority.ReleaseReservation(state.Society.Society.Inventory, "independent-wood-work");
        state = state with { Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } } };
        using var released = NonLandRuntimeFixture.Create(state, CompletionProvider());
        NonLandRuntimeFixture.Wake(released, NonLandRuntimeFixture.Subject, "released-voluntary-return");
        await NonLandRuntimeFixture.UntilAsync(released, () => released.Towns[0].NonLandHearings.Effects.Count > 0, 5);
        Assert.Single(released.Towns[0].NonLandHearings.Agreements);
        Assert.Equal("completed", released.Towns[0].NonLandHearings.Agreements[0].Status);
        NonLandRuntimeFixture.Strict(released.ExportState());
    }

    [Fact]
    public async Task AgreedCoatRepairPausesForDependentCareAndSpendsItsReservedMaterialsOnlyOnCompletion()
    {
        var state = await NonLandRuntimeFixture.FindingAsync();
        var actor = NonLandRuntimeFixture.Subject;
        var household = state.Society.Society.Agents.Single(person => person.Id == actor).HouseholdId!;
        var inventory = state.Society.Society.Inventory;
        inventory = InventoryAuthority.AddLot(inventory, "remedy-shop-fiber", "fiber", household, 2, storageBuildingId: "first-town-house-b");
        using (var setup = NonLandRuntimeFixture.Create(WithInventory(state, inventory), new NonLandTestProvider()))
        {
            var definition = setup.WorldContent.Buildings.Single(building => building.LocalId == "tailor-shop-1x1");
            var house = setup.WorldSimulation.Buildings.Single(building => building.HouseholdId == household &&
                setup.WorldContent.Buildings.Single(item => item.CanonicalId == building.DefinitionId).Tags.Contains("house"));
            var placed = Enumerable.Range(-4, 9).SelectMany(dy => Enumerable.Range(-4, 9)
                .Select(dx => new GridPoint(house.Position.X + dx, house.Position.Y + dy)))
                .Any(point => setup.PlaceBuilding("remedy-tailor", definition.CanonicalId, point, household).Applied);
            Assert.True(placed);
            state = setup.ExportState();
        }
        inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "remedy-basket", "basket", actor, 1, conditionBasisPoints: 3_000);
        inventory = InventoryAuthority.AddLot(inventory, "remedy-coat", "padded_coat", actor, 1, conditionBasisPoints: 3_000);
        inventory = InventoryAuthority.AddLot(inventory, "remedy-cloth", "cloth", actor, 1);
        inventory = InventoryAuthority.AddLot(inventory, "remedy-fiber", "fiber", actor, 1);
        inventory = InventoryAuthority.AddLot(inventory, "remedy-rope", "rope", actor, 1);
        state = WithInventory(state, inventory) with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor ? person with
            { Equipment = new(ClothingLotId: "remedy-coat", CarryAidLotId: "remedy-basket") } : person).ToArray(),
        };
        var care = SocietyRules.ProposeRelationship(state.Society.Society,
            new("remedy-dependent-care", 1, SocietyRelationshipType.Caregiver, actor, NonLandRuntimeFixture.Witness, state.Society.Society.WorldTick)).Checkpoint;
        care = SocietyRules.AcceptRelationship(care, "remedy-dependent-care", 1, NonLandRuntimeFixture.Witness).Checkpoint;
        state = state with { Society = state.Society with { Society = care } };
        state = await NonLandRuntimeFixture.AcceptRemedyAsync(state,
            [new("repair_equipment", actor, actor, "padded_coat", 1, "remedy-coat")]);
        var shop = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == "remedy-tailor");
        state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor
            ? person with { Position = shop.Position } : person).ToArray()
        };
        using var starting = NonLandRuntimeFixture.Create(state, CompletionProvider());
        NonLandRuntimeFixture.Wake(starting, actor, "perform-named-repair");
        await NonLandRuntimeFixture.UntilAsync(starting, () => starting.Agents.Single(person => person.AgentId == actor).Equipment!.Repair is not null, 4);
        var repair = starting.Agents.Single(person => person.AgentId == actor).Equipment!.Repair!;
        Assert.Equal("remedy-coat", repair.LotId);
        Assert.Empty(starting.Towns[0].NonLandHearings.Effects);
        Assert.Equal(1, starting.Society.Inventory.GetLot("remedy-cloth").Quantity);
        var recoveringState = starting.ExportState() with
        {
            Agents = starting.ExportState().Agents.Select(person => person.AgentId == actor
                ? person with { Survival = (person.Survival ?? new()) with { IllnessBasisPoints = 5_000 } } : person).ToArray(),
        };
        using var recovering = NonLandRuntimeFixture.Create(NonLandRuntimeFixture.Strict(recoveringState), CompletionProvider());
        for (var tick = 0; tick < 4; tick++) Assert.True((await recovering.AdvanceOneTickAsync()).Advanced);
        var recoveringRepair = recovering.Agents.Single(person => person.AgentId == actor).Equipment!.Repair;
        Assert.NotNull(recoveringRepair);
        Assert.InRange(recoveringRepair.WorkDone - repair.WorkDone, 1, 3);
        Assert.Equal(repair.MaterialReservationIds, recoveringRepair.MaterialReservationIds);
        Assert.All(repair.MaterialReservationIds, id => Assert.Equal(InventoryReservationState.Reserved, recovering.Society.Inventory.GetReservation(id).State));
        Assert.Equal(1, recovering.Society.Inventory.GetLot("remedy-cloth").Quantity);
        Assert.Empty(recovering.Towns[0].NonLandHearings.Effects);
        var needingCare = recovering.ExportState() with
        {
            Agents = recovering.ExportState().Agents.Select(person => person.AgentId == actor
                ? person with { Survival = person.Survival! with { IllnessBasisPoints = 0 } }
                : person.AgentId == NonLandRuntimeFixture.Witness
                    ? person with { Survival = (person.Survival ?? new()) with { IllnessBasisPoints = 5_000 } } : person).ToArray(),
        };
        using var paused = NonLandRuntimeFixture.Create(NonLandRuntimeFixture.Strict(needingCare), CompletionProvider());
        for (var tick = 0; tick < 10; tick++) Assert.True((await paused.AdvanceOneTickAsync()).Advanced);
        Assert.Null(paused.Agents.Single(person => person.AgentId == actor).Equipment!.Repair);
        Assert.InRange(paused.Society.Inventory.GetLot("remedy-coat").ConditionBasisPoints, 2_900, 3_000);
        Assert.Equal(1, paused.Society.Inventory.GetLot("remedy-cloth").Quantity);
        Assert.All(repair.MaterialReservationIds, id => Assert.Equal(InventoryReservationState.Released, paused.Society.Inventory.GetReservation(id).State));
        Assert.Empty(paused.Towns[0].NonLandHearings.Effects);
        Assert.Empty(paused.Towns[0].NonLandHearings.NativeReceipts);
        var cared = paused.ExportState() with
        {
            Agents = paused.ExportState().Agents.Select(person => person.AgentId == NonLandRuntimeFixture.Witness
                ? person with { Survival = person.Survival! with { IllnessBasisPoints = 0 } } : person).ToArray(),
        };
        using var world = NonLandRuntimeFixture.Create(NonLandRuntimeFixture.Strict(cared), CompletionProvider());
        NonLandRuntimeFixture.Wake(world, actor, "resume-after-dependent-care");
        await NonLandRuntimeFixture.UntilAsync(world, () => world.Agents.Single(person => person.AgentId == actor).Equipment!.Repair is not null, 4);
        var resumedRepair = world.Agents.Single(person => person.AgentId == actor).Equipment!.Repair!;
        Assert.Equal(repair.LotId, resumedRepair.LotId);
        var checkpoint = NonLandRuntimeFixture.Strict(world.ExportState());
        using var replay = NonLandRuntimeFixture.Create(checkpoint, CompletionProvider());
        for (var tick = 0; tick < PersonalEquipmentRules.RepairWorkTicks + 2; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
        Assert.Equal("completed", Assert.Single(replay.Towns[0].NonLandHearings.Agreements).Status);
        Assert.Single(replay.Towns[0].NonLandHearings.Effects);
        var receipt = Assert.Single(replay.Towns[0].NonLandHearings.NativeReceipts);
        Assert.Equal("remedy-coat", receipt.TargetId);
        Assert.InRange(replay.Society.Inventory.GetLot("remedy-coat").ConditionBasisPoints, 8_900, 9_000);
        Assert.InRange(replay.Society.Inventory.GetLot("remedy-basket").ConditionBasisPoints, 2_900, 3_000);
        Assert.DoesNotContain(replay.Society.Inventory.Lots, lot => lot.Id == "remedy-cloth");
        Assert.Equal(1, replay.Society.Inventory.GetLot("remedy-fiber").Quantity);
        Assert.Equal(1, replay.Society.Inventory.GetLot("remedy-rope").Quantity);
        Assert.All(resumedRepair.MaterialReservationIds, id => Assert.Equal(InventoryReservationState.Completed, replay.Society.Inventory.GetReservation(id).State));
        var compacted = RepairHistoryTests.SaveAfterLaterRepairs(replay.ExportState());
        Assert.All(receipt.RepairInputs, input => Assert.Contains(input.Reservation, compacted.Society.Society.Inventory.Reservations));
        NonLandRuntimeFixture.Strict(compacted);
        var damagedReceipt = receipt with
        {
            RepairInputs = receipt.RepairInputs.Select(input => input with
            { Reservation = input.Reservation with { Id = null! } }).ToArray(),
        };
        damagedReceipt = damagedReceipt with { Version = TownSystem.NonLandDigest(damagedReceipt with { Version = "" }) };
        Assert.Throws<InvalidDataException>(() => WorldRuntime.Restore(compacted with
        {
            Towns = compacted.Towns!.Select(town => town with
            {
                NonLandHearings = town.NonLandHearings with
                {
                    NativeReceipts = town.NonLandHearings.NativeReceipts.Select(item => item.Id == damagedReceipt.Id
                        ? damagedReceipt : item).ToArray(),
                    Effects = town.NonLandHearings.Effects.Select(effect => effect.NativeReceiptId == damagedReceipt.Id
                        ? effect with { NativeReceiptVersion = damagedReceipt.Version } : effect).ToArray(),
                },
            }).ToArray(),
        }));
    }

    [Fact]
    public async Task CraftingSkillMakesASevenTickCoatRepairOfferAndConsentFeasibleAcrossReload()
    {
        var state = await NonLandRuntimeFixture.FindingAsync();
        var actor = NonLandRuntimeFixture.Subject;
        var boardPosition = state.Agents.Single(person => person.AgentId == actor).Position;
        var inspect = new NonLandTestProvider
        {
            Choose = observation => observation.AgentId == actor
                ? observation.Candidates.FirstOrDefault(candidate => candidate.Id.Contains("|law_case_inspect|", StringComparison.Ordinal)) : null,
        };
        using (var reading = NonLandRuntimeFixture.Create(state, inspect))
        {
            NonLandRuntimeFixture.Wake(reading, actor, "inspect-skilled-repair-case");
            await NonLandRuntimeFixture.UntilAsync(reading, () =>
                TownNonLandRules.ReadCurrent(reading.Towns[0].NonLandHearings.Cases[0], actor, reading.WorldTick), 4);
            state = NonLandRuntimeFixture.Strict(reading.ExportState());
        }
        var household = state.Society.Society.Agents.Single(person => person.Id == actor).HouseholdId!;
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "skilled-remedy-shop-fiber", "fiber",
            household, 2, storageBuildingId: "first-town-house-b");
        using (var setup = NonLandRuntimeFixture.Create(WithInventory(state, inventory), new NonLandTestProvider()))
        {
            var definition = setup.WorldContent.Buildings.Single(building => building.LocalId == "tailor-shop-1x1");
            var house = setup.WorldSimulation.Buildings.Single(building => building.HouseholdId == household &&
                setup.WorldContent.Buildings.Single(item => item.CanonicalId == building.DefinitionId).Tags.Contains("house"));
            Assert.Contains(Enumerable.Range(-4, 9).SelectMany(dy => Enumerable.Range(-4, 9)
                .Select(dx => new GridPoint(house.Position.X + dx, house.Position.Y + dy))),
                point => setup.PlaceBuilding("skilled-remedy-tailor", definition.CanonicalId, point, household).Applied);
            state = setup.ExportState();
        }
        var shop = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == "skilled-remedy-tailor");
        inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "skilled-remedy-coat", "padded_coat", actor, 1,
            conditionBasisPoints: 3_000);
        inventory = InventoryAuthority.AddLot(inventory, "skilled-remedy-cloth", "cloth", actor, 2);
        state = WithInventory(state, inventory) with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor ? person with
            { Position = shop.Position, FullnessBasisPoints = 10_000, Survival = new(), Equipment = new("skilled-remedy-coat") } : person).ToArray(),
        };
        state = SkillWorkTests.WithSkill(state, actor, SkillKind.Crafting);
        var offerProvider = new NonLandTestProvider
        {
            Choose = observation => observation.AgentId == actor
                ? observation.Candidates.FirstOrDefault(candidate => candidate.Id.Contains("|remedy_offer|", StringComparison.Ordinal)) : null,
            Payload = (_, candidate) => candidate.Id.Contains("|remedy_offer|", StringComparison.Ordinal)
                ? new(Statement: "I voluntarily offer my own named coat repair in seven ticks.",
                    Terms: [new("repair_equipment", actor, actor, "padded_coat", 1, "skilled-remedy-coat")], CompletionTicks: 7) : null,
        };
        using (var offering = NonLandRuntimeFixture.Create(state, offerProvider))
        {
            NonLandRuntimeFixture.Wake(offering, actor, "offer-skilled-short-repair");
            await NonLandRuntimeFixture.UntilAsync(offering, () => offering.Towns[0].NonLandHearings.Offers.Count > 0, 4);
            state = NonLandRuntimeFixture.Strict(offering.ExportState());
        }
        var offer = Assert.Single(state.Towns![0].NonLandHearings.Offers);
        Assert.Equal(7, offer.CompletionTicks);
        Assert.Empty(state.Towns[0].NonLandHearings.Agreements);
        state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor ? person with
            { Position = boardPosition } : person).ToArray(),
        };
        var readOffer = new NonLandTestProvider
        {
            Choose = observation => observation.AgentId == actor
                ? observation.Candidates.FirstOrDefault(candidate => candidate.Id.Contains("|remedy_read|", StringComparison.Ordinal)) : null,
        };
        using (var reading = NonLandRuntimeFixture.Create(state, readOffer))
        {
            NonLandRuntimeFixture.Wake(reading, actor, "read-skilled-short-repair-offer");
            await NonLandRuntimeFixture.UntilAsync(reading, () => reading.Towns[0].Council!.Knowledge.Any(receipt =>
                receipt.AgentId == actor && receipt.NoticeId == offer.NoticeId), 4);
            state = NonLandRuntimeFixture.Strict(reading.ExportState());
        }
        Assert.Empty(state.Towns![0].NonLandHearings.Agreements);
        state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor ? person with
            { Position = shop.Position } : person).ToArray(),
        };
        Assert.DoesNotContain(state.Society.Society.Inventory.Reservations, reservation => reservation.LotId == "skilled-remedy-cloth");
        var consent = new NonLandTestProvider
        {
            Choose = observation => observation.AgentId == actor
                ? observation.Candidates.FirstOrDefault(candidate => candidate.Id.Contains("|remedy_accept|", StringComparison.Ordinal)) : null,
        };
        using (var accepting = NonLandRuntimeFixture.Create(state, consent))
        {
            NonLandRuntimeFixture.Wake(accepting, actor, "accept-skilled-short-repair");
            await NonLandRuntimeFixture.UntilAsync(accepting, () => accepting.Towns[0].NonLandHearings.Agreements.Count > 0, 4);
            state = NonLandRuntimeFixture.Strict(accepting.ExportState());
        }
        var agreement = Assert.Single(state.Towns![0].NonLandHearings.Agreements);
        Assert.Equal(agreement.AcceptedTick + 7, agreement.DeadlineTick);
        using var world = NonLandRuntimeFixture.Create(state, CompletionProvider());
        NonLandRuntimeFixture.Wake(world, actor, "perform-skilled-short-repair");
        var before = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var repair = Assert.IsType<EquipmentRepairWork>(world.Agents.Single(person => person.AgentId == actor).Equipment!.Repair);
        Assert.Equal(0, repair.WorkDone);
        Assert.Equal(2, world.Society.Inventory.GetLot("skilled-remedy-cloth").Quantity);
        Assert.All(repair.MaterialReservationIds, id => Assert.Equal(InventoryReservationState.Reserved, world.Society.Inventory.GetReservation(id).State));
        using var replay = NonLandRuntimeFixture.Create(NonLandRuntimeFixture.Strict(world.ExportState()), CompletionProvider());
        for (var tick = 0; tick < 6; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
            if (tick < 5)
            {
                Assert.NotNull(world.Agents.Single(person => person.AgentId == actor).Equipment!.Repair);
                Assert.Empty(world.Towns[0].NonLandHearings.NativeReceipts);
                Assert.Equal(2, world.Society.Inventory.GetLot("skilled-remedy-cloth").Quantity);
            }
        }
        Assert.Equal(agreement.DeadlineTick, world.WorldTick);
        Assert.Null(world.Agents.Single(person => person.AgentId == actor).Equipment!.Repair);
        Assert.Equal("completed", Assert.Single(world.Towns[0].NonLandHearings.Agreements).Status);
        var receipt = Assert.Single(world.Towns[0].NonLandHearings.NativeReceipts);
        Assert.Equal("skilled-remedy-coat", receipt.TargetId);
        Assert.Equal(actor, receipt.ActorId);
        Assert.Equal(receipt.Id, Assert.Single(world.Towns[0].NonLandHearings.Effects).NativeReceiptId);
        Assert.Equal(1, world.Society.Inventory.GetLot("skilled-remedy-cloth").Quantity);
        Assert.InRange(world.Society.Inventory.GetLot("skilled-remedy-coat").ConditionBasisPoints, 8_900, 9_000);
        Assert.All(repair.MaterialReservationIds, id => Assert.Equal(InventoryReservationState.Completed, world.Society.Inventory.GetReservation(id).State));
        NonLandRuntimeFixture.Strict(world.ExportState());
    }

    [Fact]
    public async Task TownServiceCreditsOnlyBoundedLoadsActuallyStoredInTheResidentWarehouseAcrossRestart()
    {
        var state = await NonLandRuntimeFixture.FindingAsync();
        var actor = NonLandRuntimeFixture.Subject;
        var town = state.Towns![0];
        var warehouse = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == "first-town-warehouse");
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "service-stone", "stone", actor, 10);
        state = await NonLandRuntimeFixture.AcceptRemedyAsync(WithInventory(state, inventory),
            [new("public_service_goods", actor, town.Id, "stone", 6, warehouse.InstanceId)]);
        state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor
            ? person with { Position = warehouse.Position } : person).ToArray()
        };
        using var world = NonLandRuntimeFixture.Create(state, CompletionProvider());
        NonLandRuntimeFixture.Wake(world, actor, "perform-town-service");
        await NonLandRuntimeFixture.UntilAsync(world, () => world.Towns[0].NonLandHearings.Effects.Count > 0, 4);
        Assert.Equal(4, Assert.Single(world.Towns[0].NonLandHearings.Effects).Quantity);
        Assert.Equal("pending", Assert.Single(world.Towns[0].NonLandHearings.Agreements).Status);
        Assert.Equal(6, world.Society.Inventory.GetLot("service-stone").Quantity);
        using var replay = NonLandRuntimeFixture.Create(NonLandRuntimeFixture.Strict(world.ExportState()), CompletionProvider());
        for (var tick = 0; tick < 3; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        }
        Assert.Equal("completed", Assert.Single(replay.Towns[0].NonLandHearings.Agreements).Status);
        Assert.Equal(6, replay.Towns[0].NonLandHearings.Effects.Sum(effect => effect.Quantity));
        Assert.Equal(2, replay.Towns[0].NonLandHearings.NativeReceipts.Count);
        Assert.Equal(4, replay.Society.Inventory.GetLot("service-stone").Quantity);
        Assert.Equal(6, replay.Society.Inventory.Lots.Where(lot => lot.ItemKind == "stone" &&
            lot.OwnerId == town.Id && lot.StorageBuildingId == warehouse.InstanceId).Sum(lot => lot.Quantity));
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        NonLandRuntimeFixture.Strict(replay.ExportState());
    }

    [Fact]
    public async Task ConsentCannotSpendTheSameRemainingStockTwiceAcrossIndividuallyFeasibleTerms()
    {
        var state = await NonLandRuntimeFixture.FindingAsync();
        var actor = NonLandRuntimeFixture.Subject;
        state = await NonLandRuntimeFixture.OfferRemedyAsync(state,
            [new("return_goods", actor, NonLandRuntimeFixture.Witness, "wood", 1, NonLandRuntimeFixture.ReturnLot),
                new("return_goods", actor, NonLandRuntimeFixture.Judge, "wood", 1, NonLandRuntimeFixture.ReturnLot)]);
        var inventory = InventoryAuthority.Reserve(state.Society.Society.Inventory, "other-native-work", actor,
            NonLandRuntimeFixture.ReturnLot, 1, "independent-work", long.MaxValue);
        inventory = InventoryAuthority.AddLot(inventory, "unrelated-spare-wood", "wood", actor, 1);
        var provider = new NonLandTestProvider
        {
            Choose = observation => observation.AgentId == actor
                ? observation.Candidates.FirstOrDefault(candidate => candidate.Id.Contains("|remedy_accept|", StringComparison.Ordinal)) : null,
        };
        using var world = NonLandRuntimeFixture.Create(WithInventory(state, inventory), provider);
        NonLandRuntimeFixture.Wake(world, actor, "consider-changed-stock");
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var observation = Assert.Single(provider.Observations, item => item.AgentId == actor);
        Assert.DoesNotContain(observation.Candidates, candidate => candidate.Id.Contains("|remedy_accept|", StringComparison.Ordinal));
        Assert.Contains(observation.Candidates, candidate => candidate.Id.Contains("|remedy_decline|", StringComparison.Ordinal));
        Assert.Empty(world.Towns[0].NonLandHearings.Agreements);
        Assert.Empty(world.Towns[0].NonLandHearings.Effects);
        Assert.Equal(2, world.Society.Inventory.GetLot(NonLandRuntimeFixture.ReturnLot).Quantity);
        Assert.Equal(1, world.Society.Inventory.GetLot("unrelated-spare-wood").Quantity);
        NonLandRuntimeFixture.Strict(world.ExportState());
    }

    [Theory]
    [InlineData(10, "0.5 world days")]
    [InlineData(20, "1 world day")]
    public async Task ThePublishedOfferAndPersonalChoicesDiscloseTheExactCompletionPeriodBeforeConsent(long completionTicks, string period)
    {
        var state = await NonLandRuntimeFixture.OfferRemedyAsync(await NonLandRuntimeFixture.FindingAsync(),
            [new("return_goods", NonLandRuntimeFixture.Subject, NonLandRuntimeFixture.Witness, "wood", 1, NonLandRuntimeFixture.ReturnLot)],
            completionTicks);
        var town = state.Towns![0];
        var offer = Assert.Single(town.NonLandHearings.Offers);
        var timing = "Complete within " + period + " after everyone accepts. Answer within 1 world day of publication.";
        Assert.Contains(timing, town.Council!.Notices.Single(notice => notice.Id == offer.NoticeId).Text, StringComparison.Ordinal);
        state = state with
        {
            Towns = [town with { Council = town.Council with
        { Knowledge = town.Council.Knowledge.Where(receipt => receipt.AgentId != NonLandRuntimeFixture.Subject || receipt.NoticeId != offer.NoticeId).ToArray() } }]
        };
        var provider = new NonLandTestProvider
        {
            Choose = observation => observation.AgentId == NonLandRuntimeFixture.Subject
                ? observation.Candidates.FirstOrDefault(candidate => candidate.Id.Contains("|remedy_read|", StringComparison.Ordinal)) ??
                    observation.Candidates.FirstOrDefault(candidate => candidate.Id.Contains("|remedy_accept|", StringComparison.Ordinal)) : null,
        };
        using var world = NonLandRuntimeFixture.Create(NonLandRuntimeFixture.Strict(state), provider);
        NonLandRuntimeFixture.Wake(world, NonLandRuntimeFixture.Subject, "read-exact-completion-period");
        await NonLandRuntimeFixture.UntilAsync(world, () => world.Towns[0].NonLandHearings.Agreements.Count > 0, 6);
        var choices = provider.Observations.SelectMany(observation => observation.Candidates).ToArray();
        Assert.Contains(choices, candidate => candidate.Id.Contains("|remedy_read|", StringComparison.Ordinal) && candidate.Description.Contains(timing, StringComparison.Ordinal));
        Assert.Contains(choices, candidate => candidate.Id.Contains("|remedy_accept|", StringComparison.Ordinal) && candidate.Description.Contains(timing, StringComparison.Ordinal));
        var agreement = Assert.Single(world.Towns[0].NonLandHearings.Agreements);
        Assert.Equal(agreement.AcceptedTick + completionTicks, agreement.DeadlineTick);
        NonLandRuntimeFixture.Strict(world.ExportState());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConsentRequiresRoomForAllPromisedGoodsAtTheDestination(bool townService)
    {
        var state = await NonLandRuntimeFixture.FindingAsync();
        var actor = NonLandRuntimeFixture.Subject;
        var town = state.Towns![0];
        var warehouse = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == "first-town-warehouse");
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "capacity-test-stone", "stone", actor, 6);
        var kind = townService ? "public_service_goods" : "return_goods";
        var beneficiary = townService ? town.Id : NonLandRuntimeFixture.Witness;
        var target = townService ? warehouse.InstanceId : NonLandRuntimeFixture.ReturnLot;
        state = await NonLandRuntimeFixture.OfferRemedyAsync(WithInventory(state, inventory),
            [new(kind, actor, beneficiary, townService ? "stone" : "wood", 1, target),
                new(kind, actor, beneficiary, townService ? "stone" : "wood", 1, target)]);
        inventory = state.Society.Society.Inventory;
        int room;
        if (townService)
        {
            using var setup = NonLandRuntimeFixture.Create(state, new NonLandTestProvider());
            var definition = setup.WorldContent.Buildings.Single(building => building.CanonicalId == warehouse.DefinitionId);
            room = BuildingStorageRules.Capacity(definition, warehouse)!.Value -
                inventory.Lots.Where(lot => lot.StorageBuildingId == warehouse.InstanceId).Sum(lot => lot.Quantity);
            inventory = InventoryAuthority.AddLot(inventory, "destination-filler", "wood", town.Id, room - 1,
                storageBuildingId: warehouse.InstanceId);
        }
        else
        {
            var recipient = state.Agents.Single(person => person.AgentId == beneficiary);
            room = PersonalEquipmentRules.FreeCapacity(inventory, beneficiary, recipient.Equipment);
            inventory = InventoryAuthority.AddLot(inventory, "destination-filler", "stone", beneficiary, room - 1);
        }
        Assert.True(room >= 2);
        var provider = new NonLandTestProvider
        {
            Choose = observation => observation.AgentId == actor
                ? observation.Candidates.FirstOrDefault(candidate => candidate.Id.Contains("|remedy_accept|", StringComparison.Ordinal)) : null,
        };
        using var world = NonLandRuntimeFixture.Create(WithInventory(state, inventory), provider);
        NonLandRuntimeFixture.Wake(world, actor, "consider-destination-room");
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var observation = Assert.Single(provider.Observations, item => item.AgentId == actor);
        Assert.DoesNotContain(observation.Candidates, candidate => candidate.Id.Contains("|remedy_accept|", StringComparison.Ordinal));
        Assert.Contains(observation.Candidates, candidate => candidate.Id.Contains("|remedy_decline|", StringComparison.Ordinal));
        Assert.Empty(world.Towns[0].NonLandHearings.Agreements);
        Assert.Empty(world.Towns[0].NonLandHearings.Effects);
        Assert.Equal(room - 1, world.Society.Inventory.GetLot("destination-filler").Quantity);
        NonLandRuntimeFixture.Strict(world.ExportState());
    }

    private static WorldRuntimeState WithInventory(WorldRuntimeState state, InventoryCheckpoint inventory) =>
        state with { Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } } };

    private static NonLandTestProvider CompletionProvider() => new()
    {
        Choose = observation => observation.AgentId == NonLandRuntimeFixture.Subject
            ? observation.Candidates.FirstOrDefault(candidate => candidate.Id.StartsWith("nonviolent_remedy:", StringComparison.Ordinal)) : null
    };
}

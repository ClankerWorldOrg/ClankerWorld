using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    public void Validate()
    {
        SocietyRules.Validate(society.Checkpoint);
        ValidateBeliefEventSources(society.Checkpoint.AllBeliefs(), events, eventHistoryFloor);
        ValidateExplorationGoalBindings(agents.Values.Concat(deceasedAgents.Values.Select(person => person.LastPhysical)),
            instructionsByIdempotency.Values);
        society.Validate();
        ValidateShopTrades(ShopTrades, society.Checkpoint, map, WorldTick);
        ValidateToolMakingRequests(ToolMakingRequests, worldSimulation, worldContent, society.Checkpoint, agents.Values, ShopTrades, WorldTick);
        ValidateMedicalCare(agents.Values, deceasedAgents.Values, society.Checkpoint, map);
        contentRegistry.Validate();
        worldContent.Validate();
        var expectedWorldContent = RebuildWorldContent(contentRegistry.ExportState());
        if (!string.Equals(worldContent.StateDigest, expectedWorldContent.StateDigest, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The world typed content does not match active package records.");
        }
        assetReservations.Validate();
        ValidateAssetReservationsAgainstActivePackages();
        WorldContentSimulationRules.Validate(worldSimulation, worldContent, map, WorldTick, TownPropertyValidation.RecoveredBuildings(towns));
        ValidateBuildingExpansionState(worldSimulation, worldContent, society.Checkpoint, map, checkpointSchemaVersion);
        ValidateBoatTransport(CaptureState());
        var stockState = CaptureState();
        ValidateAnimalState(stockState);
        StockHoldValidation.Validate(stockState);
        ValidateTalkOrderBindings(instructionsByIdempotency.Values, conversations, marriages);
        ValidateCartOrderBindings(society.Checkpoint.Inventory, instructionsByIdempotency.Values);
        ValidateHandcarts(handcartHitches, society.Checkpoint.Inventory, agents.Values.ToArray(), map);
        ValidatePhysicalInventoryLocations(society.Checkpoint.Inventory, worldSimulation, worldContent,
            society.Checkpoint.Agents, map, society.Checkpoint.Estates, deceasedAgents.Values,
            towns, agents.Values);
        ValidateFarmFields(fields.ToArray(), map, worldSeed, society.Checkpoint, worldSimulation, worldContent, RoadAndBridgeTiles().ToArray());
        ValidateFieldOrderBindings(fields, instructionsByIdempotency.Values);
        ValidateProductionOrderBindings(worldSimulation, worldContent, agents.Values, instructionsByIdempotency.Values);
        ValidateCustodyOrderBindings(society.Checkpoint, instructionsByIdempotency.Values);
        ValidateDeliveryOrderBindings(worldSimulation, society.Checkpoint, towns, instructionsByIdempotency.Values);
        ValidateConstructionOrderBindings(worldSimulation, worldContent, society.Checkpoint, agents.Values,
            instructionsByIdempotency.Values, WorldTick);
        ValidateExpansionOrderBindings(worldSimulation, worldContent, society.Checkpoint, towns,
            instructionsByIdempotency.Values, WorldTick);
        ValidateShelterOrderBindings(worldSimulation, worldContent, society.Checkpoint,
            instructionsByIdempotency.Values, WorldTick);
        if (worldSimulation.Buildings.Any(building => building.HouseholdId is { } householdId &&
            !society.Checkpoint.Households.Any(household => household.Id == householdId)))
            throw new InvalidDataException("A House references a missing household.");
        var inventoryReservationIds = society.Checkpoint.Inventory.Reservations
            .Select(item => item.Id.Value)
            .ToHashSet(StringComparer.Ordinal);
        var productionOwners = society.Checkpoint.Households.Select(home => home.Id)
            .Concat(society.Checkpoint.Agents.Select(person => person.Id)).ToHashSet(StringComparer.Ordinal);
        if (worldSimulation.ProductionJobs.Any(job => job.OwnerId is { } owner &&
                (!productionOwners.Contains(owner) || job.State is WorldProductionJobState.Running or WorldProductionJobState.Paused && worldSimulation.Buildings.Single(building =>
                    building.InstanceId == job.BuildingInstanceId).HouseholdId is { } home && owner != home && owner != job.WorkerId)))
            throw new InvalidDataException("A production job has an unknown owner or differs from its private building's owner.");
        foreach (var job in worldSimulation.ProductionJobs
                     .Concat(worldSimulation.CropBuilds ?? [])
                     .Where(item => item.State is WorldProductionJobState.Running or WorldProductionJobState.Paused))
        {
            if (job.OwnerId is null)
                throw new InvalidDataException("An active production job has no recorded owner.");
            if (job.InputReservationIds.Any(id => !inventoryReservationIds.Contains(id)))
            {
                throw new InvalidDataException($"Production job '{job.JobId}' has a missing inventory reservation.");
            }
            if (job.OwnerId is { } owner && job.InputReservationIds.Any(id =>
                    society.Checkpoint.Inventory.GetReservation(id) is { State: InventoryReservationState.Reserved or InventoryReservationState.PartiallyConsumed } reservation &&
                    reservation.OwnerId != owner))
                throw new InvalidDataException("A production job differs from its committed materials' owner.");
        }
        WorldSystemsRules.Validate(worldSystems);
        if (worldSystems.WorldTick != WorldTick ||
            !string.Equals(worldSystems.WorldSeed, worldSeed, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The world richer-systems state does not match the authoritative clock or seed.");
        }
        var mapValidation = MapAcceptance.Validate(map, allowEmptyStartingSite: founderSetup is not null);
        if (!mapValidation.IsValid)
        {
            throw new InvalidDataException($"The world map is invalid: {mapValidation.Failure}");
        }
        var activeIds = society.Checkpoint.Agents
            .Where(item => item.Status == SocietyAgentStatus.Active)
            .Select(item => item.Id)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var physicalIds = agents.Keys.OrderBy(item => item, StringComparer.Ordinal).ToArray();
        if (!activeIds.SequenceEqual(physicalIds))
        {
            throw new InvalidDataException("The world physical and society populations disagree.");
        }
        ValidateFounderSetup(founderSetup, society.Checkpoint);
        ValidateTowns(towns, map, founderSetup, society.Checkpoint, worldSimulation, worldContent);
        TownLandRightsRules.ValidateRecords(map, WorldTick, towns, townLandTitles,
            householdLandUseRights, householdLandUseRequests, society.Checkpoint);
        foreach (var town in towns)
        {
            TownCouncilValidation.Validate(town, society.Checkpoint, worldSystems.Config.TicksPerDay);
            TownGovernmentValidation.Validate(town, society.Checkpoint, townLandTitles, worldSystems.Config.TicksPerDay);
        }
        ValidateTownAdmissions(towns, society.Checkpoint, checkpointSchemaVersion);
        ValidateLandHearings(map, society.Checkpoint, towns, householdLandUseRights, householdLandUseRequests, townLandTitles, worldSystems.Config.TicksPerDay);
        TownPropertyValidation.ValidateWorld(map, society.Checkpoint, towns, worldSimulation, worldContent);
        TownProjectValidation.Validate(towns, society.Checkpoint, map, worldSimulation, worldContent,
            townLandTitles, householdLandUseRights, householdLandUseRequests, fields, RoadTiles, Bridges);
        ValidatePaidMarkets(towns, society.Checkpoint, map, worldSimulation, worldContent,
            fields, householdLandUseRights, householdLandUseRequests, RoadTiles, Bridges);
        MarketTradeValidation.Validate(towns, society.Checkpoint, map, worldSimulation, worldContent, WorldTick, agents.Values);
        foreach (var town in towns)
            TownNonLandValidation.Validate(map, society.Checkpoint, town, townLandTitles, worldSystems.Config.TicksPerDay);
        ValidateNonLandPhysicalState(map, society.Checkpoint, towns, townLandTitles, householdLandUseRights);
        ValidateRoads(RoadTiles, map, founderSetup);
        ValidateBridges(Bridges, bridgeTraffic, map, RoadTiles, worldSimulation, worldContent,
            society.Checkpoint, agents.Values);
        if (!RiverBridgeRules.SameDecks(map.BridgeDecks, RiverBridgeRules.Decks(bridges)))
            throw new InvalidDataException("The passable bridge decks do not match the saved bridges.");
        ValidatePlantedTrees();
        ValidateDeceasedArchive(deceasedAgents.Values, society.Checkpoint, map, bridges, checkpointSchemaVersion, towns);
        AgentKnowledgeRules.Validate(knowledge, map, society.Checkpoint, WorldTick);
        AgentKnowledgeRules.ValidateRecipes(knowledge, society.Checkpoint, worldContent, worldSimulation, WorldTick);
        ValidateKnowledgeReadOrderBindings(instructionsByIdempotency.Values, knowledge);
        ValidateKnowledgeOrderBindings(knowledge, instructionsByIdempotency.Values);
        ValidateHousing(agents.Values, society.Checkpoint, checkpointSchemaVersion);
        ValidateDependentCare(agents.Values, society.Checkpoint, towns, checkpointSchemaVersion);
        ValidateGuardianPlacements(agents.Values, society.Checkpoint, towns, map, checkpointSchemaVersion);
        ValidateDepartures(agents.Values, society.Checkpoint, checkpointSchemaVersion);
        ValidateEquipment(agents.Values, society.Checkpoint, worldSimulation, worldContent, checkpointSchemaVersion);
        ValidateRepairOrderBindings(agents.Values, society.Checkpoint.Inventory, instructionsByIdempotency.Values);
        ValidateContinuity(continuity, society.Checkpoint, checkpointSchemaVersion);

        foreach (var agent in agents.Values)
        {
            ValidateProficiency(agent);
            ValidateSocialStanding(agent, society.Checkpoint.Agents.Select(item => item.Id), WorldTick);
            ValidatePrivateThoughts(agent.RecentThoughts, WorldTick);
            AgentIdentityMoment.Validate(agent.IdentityMoments, WorldTick, checkpointSchemaVersion);
            if (agent.Project is { } project)
            {
                ValidateProject(project, WorldTick);
            }
            AgentStore.ValidateLocalAgent(agent, agent.AgentId);
            if (!map.IsPassable(agent.Position) && !SwimmingRules.IsSwimmingWater(map, agent.Position) && !IsSavedBoatPassenger(boatTransport, agent.AgentId, agent.Position))
            {
                throw new InvalidDataException($"Physical state for '{agent.AgentId}' is invalid.");
            }
        }

        var expectedEventId = checked(eventHistoryFloor + 1);
        var previousTick = 0L;
        foreach (var worldEvent in events)
        {
            if (worldEvent.Payload is null || !worldEvent.Payload.FieldsAreValid || worldEvent.EventId != expectedEventId ||
                worldEvent.WorldTick < previousTick ||
                worldEvent.WorldTick > WorldTick ||
                worldEvent.Position is { } eventPosition && !map.Contains(eventPosition))
            {
                throw new InvalidDataException("World events are not a committed ordered sequence.");
            }

            expectedEventId++;
            previousTick = worldEvent.WorldTick;
        }
    }

    private static void ValidateTowns(
        IReadOnlyList<TownRuntimeState>? savedTowns,
        WorldMap map,
        FounderSetupState? setup,
        SocietyCheckpoint society,
        WorldContentSimulationState simulation,
        DeclarativeWorldContentState content)
    {
        ArgumentNullException.ThrowIfNull(savedTowns);
        if (setup is null)
        {
            if (savedTowns.Count != 0 || simulation.Buildings.Any(item => item.TownId is not null))
                throw new InvalidDataException("A legacy world cannot claim an unrecorded Town or Town-assigned building.");
            return;
        }

        if (savedTowns.Count == 0 && !setup.Started && setup.FounderIds.Count == 0 &&
            map.MapObjects.Count == 0 && simulation.Buildings.Count == 0)
            return;

        var firstTown = savedTowns.FirstOrDefault(item => item.Id == TownBorderRules.FirstTownId);
        if (firstTown is null || savedTowns.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != savedTowns.Count)
            throw new InvalidDataException("A founder-setup world must keep its original first Town and unique Town identities.");

        var active = society.Agents.Where(person => person.Status == SocietyAgentStatus.Active)
            .Select(person => person.Id).ToHashSet(StringComparer.Ordinal);
        var assignedResidents = new HashSet<string>(StringComparer.Ordinal);
        var byInstance = simulation.Buildings.ToDictionary(item => item.InstanceId, StringComparer.Ordinal);
        var definitions = content.BuildingDefinitions;
        var townIds = savedTowns.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var town in savedTowns)
        {
            var isFirstTown = town.Id == TownBorderRules.FirstTownId;
            if (string.IsNullOrWhiteSpace(town.Id) || town.Id != town.Id.Trim() || town.Id.Length > DerivedId.MaximumLength ||
                town.Id.Any(char.IsControl) || string.IsNullOrWhiteSpace(town.Name) || town.Name != town.Name.Trim() ||
                town.Name.Length > 120 || town.FoundedTick < 0 || town.FoundedTick > society.WorldTick ||
                town.FirstBuildingCompletedTick is { } completed && (completed < town.FoundedTick || completed > society.WorldTick) ||
                town.AbandonedSinceTick is { } abandoned &&
                    (!town.IsAbandoned || abandoned < town.FoundedTick || abandoned > society.WorldTick) ||
                town.OriginSite is { } origin && !map.IsBuildable(origin) ||
                town.ResidentIds is null || town.AssignedBuildingIds is null || town.BorderTiles is null ||
                town.AssignedBuildingIds.Count > 0 && town.FirstBuildingCompletedTick is null ||
                town.ResidentIds.Distinct(StringComparer.Ordinal).Count() != town.ResidentIds.Count ||
                town.AssignedBuildingIds.Distinct(StringComparer.Ordinal).Count() != town.AssignedBuildingIds.Count ||
                town.BorderTiles.Distinct().Count() != town.BorderTiles.Count || town.BorderTiles.Count == 0 ||
                town.ResidentIds.Any(id => !active.Contains(id) || !assignedResidents.Add(id)))
                throw new InvalidDataException("A Town identity, founding record, membership, or footprint is invalid.");

            if (isFirstTown)
            {
                if (town.Name != TownBorderRules.FirstTownName ||
                    town.FoundingState != (setup.Started ? "founded" : "founding") || town.FoundedTick != 0)
                    throw new InvalidDataException("The original first Town's identity and founding history are invalid.");
            }
            else if (town.FoundingState != "founded")
            {
                throw new InvalidDataException("An additional recorded Town must have a completed founding record.");
            }

            var assignedIds = simulation.Buildings.Where(item => item.TownId == town.Id)
                .Select(item => item.InstanceId).Order(StringComparer.Ordinal).ToArray();
            if (!town.AssignedBuildingIds.Order(StringComparer.Ordinal).SequenceEqual(assignedIds))
                throw new InvalidDataException("Town building assignments disagree with the placed-building state.");

            var border = town.BorderTiles.ToHashSet();
            if (town.BorderTiles.Any(point => !map.Contains(point)) ||
                town.OriginSite is { } site && !border.Contains(site))
                throw new InvalidDataException("The saved Town border does not cover its founding site.");
            foreach (var buildingId in town.AssignedBuildingIds)
            {
                if (!byInstance.TryGetValue(buildingId, out var building) ||
                    !definitions.TryGetValue(building.DefinitionId, out var definition))
                    throw new InvalidDataException("A Town references a building that is not placed.");
                if (WorldContentSimulationRules.Footprint(definition, building).Where(map.IsLand).Any(tile => !border.Contains(tile)) &&
                    !definition.Tags.Contains("warehouse", StringComparer.Ordinal))
                    throw new InvalidDataException("The saved Town border does not cover an assigned building.");
            }

            var warehouses = town.AssignedBuildingIds.Count(id => definitions.TryGetValue(byInstance[id].DefinitionId, out var definition) &&
                definition.Tags.Contains("warehouse", StringComparer.Ordinal));
            if (warehouses > 1)
                throw new InvalidDataException("A Town can have only one assigned Warehouse.");
        }

        if (simulation.Buildings.Any(item => item.TownId is { } townId && !townIds.Contains(townId)))
            throw new InvalidDataException("A placed building references a Town that is not recorded.");
    }

    private static void ValidateFounderSetup(FounderSetupState? setup, SocietyCheckpoint society)
    {
        if (setup is null) return;
        if (setup.FounderIds is null || setup.FounderIds.Count > RequiredFounders ||
            setup.FounderIds.Distinct(StringComparer.Ordinal).Count() != setup.FounderIds.Count ||
            setup.FounderIds.Any(id => string.IsNullOrWhiteSpace(id) ||
                !society.Agents.Any(person => person.Id == id)))
            throw new InvalidDataException("Founder setup references invalid or duplicate agents.");
        if (setup.Started)
        {
            if (setup.FounderIds.Count != RequiredFounders)
                throw new InvalidDataException("A started world requires four configured founders.");
        }
        else if (!society.IsPaused || society.WorldTick != 0 ||
                 society.Agents.Count != setup.FounderIds.Count ||
                 society.Agents.Any(person => person.Status != SocietyAgentStatus.Active))
            throw new InvalidDataException("An incomplete founder setup must remain paused at creation time.");
    }

    private static void ValidatePhysicalInventoryLocations(InventoryCheckpoint inventory,
        WorldContentSimulationState simulation, DeclarativeWorldContentState content,
        IReadOnlyList<SocietyAgent> agents, WorldMap map, IReadOnlyList<SocietyEstate> estates,
        IEnumerable<DeceasedAgentState> deceased, IReadOnlyList<TownRuntimeState>? townStates,
        IEnumerable<AgentState> physical)
    {
        var buildings = simulation.Buildings.ToDictionary(item => item.InstanceId, StringComparer.Ordinal);
        var definitions = content.BuildingDefinitions;
        var people = agents.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var recoveredBuildings = TownPropertyValidation.RecoveredBuildings(townStates);
        var heldEstates = estates.Where(estate => !estate.Settled)
            .ToDictionary(estate => estate.Id, StringComparer.Ordinal);
        var departures = physical.Concat(deceased.Select(person => person.LastPhysical))
            .Where(person => person.Departures is { Count: > 0 })
            .ToDictionary(person => person.AgentId, person => person.Departures!, StringComparer.Ordinal);
        var allowanceAmounts = new Dictionary<HouseholdDeparture, long>();
        bool HasEstateStorage(InventoryLot lot)
        {
            if (heldEstates.TryGetValue(lot.OwnerId, out var estate))
                return people.ContainsKey(estate.DeceasedId) && (estate.FrozenLots ?? []).Any(frozen =>
                    frozen.LotId == lot.Id && frozen.ItemKind == lot.ItemKind && frozen.Quantity == lot.Quantity &&
                    frozen.StorageBuildingId == lot.StorageBuildingId);
            // Refused kinds and overflow may stay in the estate's original House
            // as stock belonging to the deceased resident's recorded Town.
            return estates.Where(item => item.Settled && (lot.OwnerId == "settlement:communal" ||
                    deceased.Any(person => person.AgentId == item.DeceasedId && person.TownId == lot.OwnerId)))
                .SelectMany(item => item.FrozenLots ?? []).Any(frozen =>
                    (frozen.LotId == lot.Id || frozen.LotId == lot.ProvenanceLotId) && frozen.ItemKind == lot.ItemKind &&
                    frozen.Quantity >= lot.Quantity && frozen.StorageBuildingId == lot.StorageBuildingId);
        }
        HouseholdDeparture? AllowanceStorageRecord(InventoryLot lot, PlacedBuilding storage)
        {
            if (!IsEdibleFood(lot.ItemKind) ||
                lot.CarrierId is not null || lot.ContainerLotId is not null || lot.DeliveryBuildingId is not null)
                return null;
            HouseholdDeparture? FromHousehold(string household)
            {
                var visiting = new HashSet<string>(StringComparer.Ordinal);
                HouseholdDeparture? Origin(string owner, string lotId, int quantity, long beforeTick)
                {
                    if (departures.TryGetValue(owner, out var records) && records.FirstOrDefault(record =>
                        record.HouseholdId == household && record.Tick <= beforeTick &&
                        record.AllowancePortions >= quantity && record.AllowanceLotIds.Contains(lotId, StringComparer.Ordinal)) is { } recorded)
                        return recorded;
                    foreach (var estate in estates.Where(item => item.CreatedTick <= beforeTick))
                    {
                        if (!visiting.Add(estate.Id)) continue;
                        try
                        {
                            var held = !estate.Settled && owner == estate.Id;
                            var fallback = owner == "settlement:communal" || deceased.Any(person =>
                                person.AgentId == estate.DeceasedId && person.TownId == owner);
                            if (!held && (!estate.Settled || estate.ExpiryTick > beforeTick)) continue;
                            foreach (var frozen in (estate.FrozenLots ?? []).Where(item =>
                                item.ItemKind == lot.ItemKind && item.StorageBuildingId == storage.InstanceId))
                            {
                                if (held)
                                {
                                    if (frozen.LotId != lotId || frozen.Quantity != quantity) continue;
                                }
                                else
                                {
                                    // Unavailable will shares also follow the saved
                                    // household default, even with an accepted will.
                                    if (frozen.Quantity < quantity || !(fallback ||
                                        estate.BeneficiaryIds.Contains(owner, StringComparer.Ordinal) ||
                                        (estate.WillBequests ?? []).Any(bequest => bequest.HeirId == owner &&
                                            bequest.LotId == frozen.LotId && bequest.Quantity >= quantity))) continue;
                                    var inheritedId = $"{frozen.LotId}#estate:{estate.Id}:{owner}";
                                    if (lotId != DerivedId.Create(inheritedId) &&
                                        lotId != DerivedId.Create(inheritedId + ":storage:" + storage.InstanceId) &&
                                        !(fallback && lotId == frozen.LotId && quantity == frozen.Quantity)) continue;
                                }
                                // Frozen IDs retain exact settlement links through
                                // later deaths; provenance alone does not prove them.
                                if (Origin(estate.DeceasedId, frozen.LotId, frozen.Quantity, estate.CreatedTick) is { } origin)
                                    return origin;
                            }
                        }
                        finally { visiting.Remove(estate.Id); }
                    }
                    return null;
                }
                return Origin(lot.OwnerId, lot.Id, lot.Quantity, inventory.WorldTick);
            }
            // Reclamation and an onward grant do not move a former member's
            // personal allowance. The saved transfer proves this same Store's
            // prior household, without admitting unrelated personal stock.
            if (storage.HouseholdId is { } home && FromHousehold(home) is { } current) return current;
            foreach (var prior in (townStates ?? []).SelectMany(town => town?.LandRecords?.Cases ?? [])
                    .Where(item => item?.Property?.Transfer?.PriorBuilding is { HouseholdId: not null } prior &&
                        prior.InstanceId == storage.InstanceId)
                    .Select(item => item.Property!.Transfer!.PriorBuilding.HouseholdId!))
                if (FromHousehold(prior) is { } recorded) return recorded;
            return null;
        }
        foreach (var lot in inventory.Lots)
        {
            if (lot.CarrierId is { } carrierId && (!people.TryGetValue(carrierId, out var custodian) ||
                custodian.Status != SocietyAgentStatus.Active))
                throw new InvalidDataException("Inventory physical custody references an unavailable person.");
            if (lot.GroundPosition is { } ground && (!map.Contains(new(ground.X, ground.Y)) ||
                lot.StorageBuildingId is not null || lot.DeliveryBuildingId is not null))
                throw new InvalidDataException($"Inventory lot '{lot.Id}' has an invalid ground location.");
            if (lot.StorageBuildingId is { } storageId)
            {
                if (!buildings.TryGetValue(storageId, out var storage) ||
                    !definitions.TryGetValue(storage.DefinitionId, out var definition))
                    throw new InvalidDataException($"Inventory lot '{lot.Id}' has an invalid building storage location.");
                var allowance = definition.Tags.Contains("store", StringComparer.Ordinal) ? AllowanceStorageRecord(lot, storage) : null;
                if (allowance is not null)
                {
                    allowanceAmounts[allowance] = allowanceAmounts.GetValueOrDefault(allowance) + lot.Quantity;
                    if (allowanceAmounts[allowance] > allowance.AllowancePortions)
                        throw new InvalidDataException("The recorded Store allowance exceeds its departure portions.");
                }
                if (!(storage.HouseholdId == lot.OwnerId && definition.Tags.Any(IsHouseholdBuildingTag) ||
                      allowance is not null ||
                      (people.ContainsKey(lot.OwnerId) || HasEstateStorage(lot)) && (storage.HouseholdId is not null || recoveredBuildings.Contains(storageId)) &&
                      definition.Tags.Contains("house", StringComparer.Ordinal) ||
                      storage.TownId == lot.OwnerId && storage.HouseholdId is null &&
                      (recoveredBuildings.Contains(storageId) && definition.Tags.Any(IsHouseholdBuildingTag) ||
                          !InventoryContainerRules.IsFood(lot.ItemKind) && definition.Tags.Contains("warehouse", StringComparer.Ordinal))))
                    throw new InvalidDataException($"Inventory lot '{lot.Id}' has an invalid building storage location.");
            }
            if (lot.DeliveryBuildingId is { } deliveryId &&
                (!buildings.TryGetValue(deliveryId, out var destination) ||
                 destination.HouseholdId is null ||
                 !people.TryGetValue(lot.OwnerId, out var carrier) ||
                 carrier.Status != SocietyAgentStatus.Active ||
                 carrier.HouseholdId != destination.HouseholdId))
                throw new InvalidDataException($"Inventory lot '{lot.Id}' has an invalid House delivery destination.");
        }
    }

    private static bool HasSavedToolUseState(WorldRuntimeState state) =>
        state.WorldSimulation is { } simulation &&
            simulation.ProductionJobs.Concat(simulation.CropBuilds ?? [])
                .Any(job => job is not null && job.ToolLotId is not null) ||
        (state.Fields ?? []).Any(field => field is not null &&
            (field.Work?.HoeLotId is not null || field.Work?.SickleLotId is not null));

    internal static void ValidateMinimumSupportedSchemaVersion(int schemaVersion)
    {
        if (schemaVersion < MinimumSupportedStateSchemaVersion)
            throw new InvalidDataException(
                $"World save schema {schemaVersion} is older than the minimum supported schema {MinimumSupportedStateSchemaVersion}.");
    }

    internal static void ValidateStateForCodec(WorldRuntimeState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        ValidateMinimumSupportedSchemaVersion(state.SchemaVersion);
        if (state.SchemaVersion > StateSchemaVersion || string.IsNullOrWhiteSpace(state.WorldSeed) || state.EventHistoryFloor < 0)
        {
            throw new InvalidDataException("The world runtime state schema or seed is invalid.");
        }
        if (state.SchemaVersion < 34 && (state.Fields is { Count: > 0 } ||
            state.Society.Society.Inventory.Lots.Any(lot => lot.GroundPosition is not null)))
            throw new InvalidDataException("Household fields and ground harvest lots require world schema 34.");
        if (state.SchemaVersion >= 34 && state.Fields is null)
            throw new InvalidDataException("World schema 34 requires authoritative household field state.");
        if (state.SchemaVersion < ConversationSchemaVersion &&
            (state.Conversations is { Count: > 0 } || state.ConversationBudgets is { Count: > 0 }))
            throw new InvalidDataException($"Conversation history and daily budgets require world schema {ConversationSchemaVersion}.");
        try { (state.RoutineHelper ?? throw new ArgumentException("Missing routine helper.")).Validate(); }
        catch (ArgumentException exception) { throw new InvalidDataException("The saved routine helper is invalid.", exception); }
        if ((state.RoutineHelper.Provider != "off") != (state.JevEnabled ?? true))
            throw new InvalidDataException("The saved helper availability is inconsistent.");
        if (state.JevPolicyRevision < 0 || state.JevEnabled is null && state.JevPolicyRevision != 0)
            throw new InvalidDataException("The saved Jev routing policy is invalid.");
        if (state.Geography is not null &&
            (state.FounderSetup is null ||
             !string.Equals(state.Geography.Seed, state.WorldSeed, StringComparison.Ordinal)))
            throw new InvalidDataException("Generated geography does not match the saved world setup.");
        // New worlds can only be created at these sizes, so any other saved size is damage.
        if (state.Geography is { Size: not (WorldSizePreset.Small or WorldSizePreset.Medium) })
            throw new InvalidDataException("Only Small and Medium worlds can be loaded.");
        if (state.HandcartHitches is null)
            throw new InvalidDataException("The current world checkpoint is missing cart attachments.");
        // A map from an older terrain generator cannot be rebuilt; refuse it
        // by name rather than as a mismatched regeneration.
        if (state.Geography is { } geography &&
            geography.BalancedVisibilityVersion != GeographyGenerator.CurrentBalancedVisibilityVersion)
            throw new InvalidDataException(GeographyGenerator.OlderTerrainVersionMessage);
        ValidateFounderSetup(state.FounderSetup, state.Society.Society);
        if (state.Towns is null || state.Knowledge is null || state.RoadTiles is null ||
            state.Bridges is null || state.BridgeTraffic is null || state.TownLandTitles is null ||
            state.HouseholdLandUseRights is null || state.HouseholdLandUseRequests is null)
            throw new InvalidDataException("The current world checkpoint is missing required Town, land-rights, map-knowledge, Road or bridge state.");
        if (state.Content is null || state.WorldSystems is null || state.WorldContent is null ||
            state.WorldSimulation is null || state.AssetReservations is null)
            throw new InvalidDataException("The current world checkpoint is missing required content or world-system state.");
        ValidateAnimalState(state);
        StockHoldValidation.Validate(state);
        if (state.SchemaVersion < ToolProgressionSchemaVersion && HasSavedToolUseState(state))
            throw new InvalidDataException($"Saved tool use links require world schema {ToolProgressionSchemaVersion}.");
        if (state.SchemaVersion >= ConversationSchemaVersion && (state.Conversations is null || state.ConversationBudgets is null))
            throw new InvalidDataException($"World schema {ConversationSchemaVersion} requires conversation state and daily budgets.");
        if (state.SchemaVersion >= OrderLifecycleSchemaVersion && state.OrderCancellations is null)
            throw new InvalidDataException($"World schema {OrderLifecycleSchemaVersion} requires order-cancellation retry state.");
        var hasArchivedEvents = WorldHistory.RequiredRecordCounts(state).Count > 0 || state.EventHistoryFloor > 0 || state.Society.Society.EventHistoryFloor > 0 ||
            state.Society.Society.Inventory.EventHistoryFloor > 0 || state.Society.Cognition.EventHistoryFloor > 0 ||
            state.Society.Cognition.Runtimes.Any(runtime => runtime.EventHistoryFloor > 0);
        if ((hasArchivedEvents && state.HistoryArchiveHead is null) ||
            (state.HistoryArchiveHead is { } head && (head.Length != 64 || head.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))))
        {
            throw new InvalidDataException("The world history reference or schema is invalid.");
        }

        if (state.Geography is not null && (state.Map.ClimateZones is null || state.Map.ElevationLevels is null ||
            state.Map.HydrologyKinds is null || state.Map.SurfaceKinds is null || state.Map.VegetationKinds is null))
            throw new InvalidDataException("The current generated geography is missing required map layers.");

        if (state.MapAuthoring is { } authoring && (!authoring.IsValid(state.Map) ||
            authoring.Resources.Any(resource => !state.Map.Resources.Contains(resource)) ||
            authoring.Objects.Any(item => !state.Map.MapObjects.Contains(item))))
            throw new InvalidDataException("The saved Developer map edits are invalid or do not match the map.");

        if (!MapAcceptance.Validate(state.Map, allowEmptyStartingSite: state.FounderSetup is not null).IsValid)
        {
            throw new InvalidDataException("The world runtime contains an invalid map.");
        }
        // Saved positions and paths may be on bridge decks, so check them
        // against the same bridged map that movement uses.
        var travelMap = TravelMap(state);

        using var society = SocietyRuntime.Restore(state.Society);
        if (state.SchemaVersion >= ObserverGuidanceSchemaVersion &&
            (state.Instructions is null || state.CompletedInstructionIds is null || state.InstructionReceipts is null))
            throw new InvalidDataException($"World schema {ObserverGuidanceSchemaVersion} requires authoritative instruction state.");
        var latestWorldEventId = state.Events.Count == 0 ? state.EventHistoryFloor : state.Events[^1].EventId;
        ValidateSavedInstructions(state.Instructions ?? [], state.CompletedInstructionIds ?? [], society.Checkpoint,
            state.Society.Society.WorldId, latestWorldEventId,
            state.OrderCancellations ?? [], state.WorldContent, state.InstructionReceipts);
        ValidateBeliefEventSources(state.Society.Society.AllBeliefs(), state.Events, state.EventHistoryFloor);
        ValidateConversationState(state, society.Checkpoint);
        AgentMarriageValidation.Validate(state, society.Checkpoint);
        ValidateShopTrades(state.ShopTrades, society.Checkpoint, state.Map, society.Checkpoint.WorldTick);
        ValidateToolMakingRequests(state.ToolMakingRequests, state.WorldSimulation, state.WorldContent, society.Checkpoint, state.Agents, state.ShopTrades!, society.Checkpoint.WorldTick);
        ValidateMedicalCare(state);
        AgentKnowledgeRules.Validate(state.Knowledge, travelMap, society.Checkpoint,
            society.Checkpoint.WorldTick);
        AgentKnowledgeRules.ValidateRecipes(state.Knowledge, society.Checkpoint, state.WorldContent, state.WorldSimulation!, society.Checkpoint.WorldTick);
        ValidateKnowledgeReadOrderBindings(state.Instructions ?? [], state.Knowledge);
        ValidateKnowledgeOrderBindings(state.Knowledge, state.Instructions ?? []);
        ValidateExplorationGoalBindings(state.Agents.Concat((state.DeceasedAgents ?? []).Select(person => person.LastPhysical)),
            state.Instructions ?? []);
        ValidateSurvival(state);
        ValidateFoodStewardship(state);
        foreach (var town in state.Towns ?? [])
        {
            TownCouncilValidation.Validate(town, society.Checkpoint, state.WorldSystems!.Config.TicksPerDay);
            TownGovernmentValidation.Validate(town, society.Checkpoint, state.TownLandTitles!, state.WorldSystems!.Config.TicksPerDay);
        }
        ValidateTownAdmissions(state.Towns ?? [], society.Checkpoint, state.SchemaVersion);
        TownProjectValidation.Validate(state.Towns ?? [], society.Checkpoint, state.Map, state.WorldSimulation!, state.WorldContent!,
            state.TownLandTitles!, state.HouseholdLandUseRights!, state.HouseholdLandUseRequests!, state.Fields!, state.RoadTiles!, state.Bridges!);
        ValidatePaidMarkets(state.Towns ?? [], society.Checkpoint, state.Map, state.WorldSimulation!, state.WorldContent!,
            state.Fields!, state.HouseholdLandUseRights!, state.HouseholdLandUseRequests!, state.RoadTiles!, state.Bridges!);
        MarketTradeValidation.Validate(state.Towns ?? [], society.Checkpoint, state.Map, state.WorldSimulation!, state.WorldContent!, society.Checkpoint.WorldTick, state.Agents);
        ValidateLessons(state);
        ValidateHousing(state.Agents, state.Society.Society, state.SchemaVersion);
        ValidateDependentCare(state.Agents, state.Society.Society, state.Towns ?? [], state.SchemaVersion);
        ValidateGuardianPlacements(state.Agents, state.Society.Society, state.Towns ?? [], travelMap, state.SchemaVersion);
        ValidateDepartures(state.Agents, state.Society.Society, state.SchemaVersion);
        ValidateEquipment(state.Agents, state.Society.Society, state.WorldSimulation, state.WorldContent, state.SchemaVersion);
        ValidateRepairOrderBindings(state.Agents, state.Society.Society.Inventory, state.Instructions ?? []);
        foreach (var person in state.Agents)
        {
            CognitionSystem.ValidateLocalModelAttempt(person.LastModelAttempt);
            if (person.LastModelAttempt is { } attempt && attempt.WorldTick > state.Society.Society.WorldTick)
                throw new InvalidDataException("The saved model attempt is invalid.");
            ValidateSavedChildModelSelection(person, state.Society.Society, state.SchemaVersion);
            ValidateProficiency(person);
            ValidateSocialStanding(person, state.Society.Society.Agents.Select(item => item.Id),
                state.Society.Society.WorldTick);
            ValidatePrivateThoughts(person.RecentThoughts, state.Society.Society.WorldTick);
            AgentIdentityMoment.Validate(person.IdentityMoments, state.Society.Society.WorldTick, state.SchemaVersion);
            ValidateExploration(person.Exploration, travelMap, state.Bridges, state.Society.Society.WorldTick);
        }
        ValidateParenthood(state);
        ValidateContinuity(state.Continuity, state.Society.Society, state.SchemaVersion);
        ContentPackageRegistry.Restore(state.Content);
        WorldSystemsRules.Validate(state.WorldSystems);
        ValidateActionRefusals(state);
        if (state.WorldSystems.WorldTick != state.Society.Society.WorldTick ||
            !string.Equals(state.WorldSystems.WorldSeed, state.WorldSeed, StringComparison.Ordinal) ||
            state.WorldSystems.Config.TicksPerDay != state.Society.Society.Config.TicksPerWorldDay ||
            state.WorldSystems.Config.DaysPerYear != state.Society.Society.Config.DaysPerWorldYear)
        {
            throw new InvalidDataException("The saved world systems do not match the society clock, calendar, or seed.");
        }
        state.WorldContent.Validate();
        WorldContentSimulationRules.Validate(state.WorldSimulation, state.WorldContent, state.Map,
            state.Society.Society.WorldTick, TownPropertyValidation.RecoveredBuildings(state.Towns));
        ValidateProductionOrderBindings(state.WorldSimulation, state.WorldContent, state.Agents, state.Instructions ?? []);
        ValidateCustodyOrderBindings(society.Checkpoint, state.Instructions ?? []);
        ValidateDeliveryOrderBindings(state.WorldSimulation, society.Checkpoint, state.Towns, state.Instructions ?? []);
        ValidateConstructionOrderBindings(state.WorldSimulation, state.WorldContent, society.Checkpoint, state.Agents,
            state.Instructions ?? [], state.Society.Society.WorldTick);
        ValidateExpansionOrderBindings(state.WorldSimulation, state.WorldContent, society.Checkpoint, state.Towns,
            state.Instructions ?? [], state.Society.Society.WorldTick);
        ValidateShelterOrderBindings(state.WorldSimulation, state.WorldContent, society.Checkpoint,
            state.Instructions ?? [], state.Society.Society.WorldTick);
        ValidateBuildingExpansionState(state.WorldSimulation, state.WorldContent, state.Society.Society,
            state.Map, state.SchemaVersion);
        ValidateBoatTransport(state);
        ValidateTalkOrderBindings(state.Instructions ?? [], state.Conversations ?? [], state.Marriages ?? []);
        ValidateCartOrderBindings(state.Society.Society.Inventory, state.Instructions ?? []);
        ValidateHandcarts(state.HandcartHitches, state.Society.Society.Inventory, state.Agents, travelMap);
        ValidatePhysicalInventoryLocations(state.Society.Society.Inventory, state.WorldSimulation,
            state.WorldContent, state.Society.Society.Agents, state.Map, state.Society.Society.Estates,
            state.DeceasedAgents ?? [], state.Towns, state.Agents);
        ValidateFarmFields(state.Fields!.ToArray(), state.Map, state.WorldSeed, state.Society.Society,
            state.WorldSimulation, state.WorldContent, state.RoadTiles.Concat(
                state.Bridges.SelectMany(bridge => bridge.Entrances)).ToArray());
        ValidateFieldOrderBindings(state.Fields!, state.Instructions ?? []);
        if (state.WorldSimulation.Buildings.Any(building => building.HouseholdId is { } householdId &&
            !state.Society.Society.Households.Any(household => household.Id == householdId)))
            throw new InvalidDataException("A House references a missing household.");
        ValidateTowns(state.Towns, state.Map, state.FounderSetup,
            state.Society.Society, state.WorldSimulation, state.WorldContent);
        TownLandRightsRules.ValidateRecords(state.Map, state.Society.Society.WorldTick, state.Towns,
            state.TownLandTitles, state.HouseholdLandUseRights, state.HouseholdLandUseRequests,
            state.Society.Society);
        ValidateLandHearings(state.Map, society.Checkpoint, state.Towns ?? [], state.HouseholdLandUseRights!,
            state.HouseholdLandUseRequests!, state.TownLandTitles!, state.WorldSystems!.Config.TicksPerDay);
        TownPropertyValidation.ValidateWorld(state.Map, society.Checkpoint, state.Towns ?? [], state.WorldSimulation, state.WorldContent);
        foreach (var town in state.Towns ?? [])
            TownNonLandValidation.Validate(state.Map, society.Checkpoint, town, state.TownLandTitles!, state.WorldSystems.Config.TicksPerDay);
        ValidateNonLandPhysicalState(state.Map, society.Checkpoint, state.Towns ?? [], state.TownLandTitles!, state.HouseholdLandUseRights!);
        ValidateRoads(state.RoadTiles, state.Map, state.FounderSetup);
        ValidateBridges(state.Bridges, state.BridgeTraffic, travelMap,
            state.RoadTiles, state.WorldSimulation, state.WorldContent, state.Society.Society,
            state.Agents);
        WorldAssetReservationLedger.Restore(state.AssetReservations);
        var activeIds = state.Society.Society.Agents
            .Where(item => item.Status == SocietyAgentStatus.Active)
            .Select(item => item.Id)
            .OrderBy(item => item, StringComparer.Ordinal);
        var physicalIds = state.Agents
            .Select(item => item.AgentId)
            .OrderBy(item => item, StringComparer.Ordinal);
        if (!activeIds.SequenceEqual(physicalIds))
        {
            throw new InvalidDataException("The saved world populations disagree.");
        }
        ValidateDeceasedArchive(state.DeceasedAgents ?? [], state.Society.Society, travelMap, state.Bridges,
            state.SchemaVersion, state.Towns ?? []);
        foreach (var agent in state.Agents)
        {
            if (agent.Project is { } project)
            {
                ValidateProject(project, state.Society.Society.WorldTick);
            }
        }
    }

    private static void ValidateSavedInstructions(
        IReadOnlyList<OwnerQueuedInstruction> instructions,
        IReadOnlyList<string> completedInstructionIds,
        SocietyCheckpoint checkpoint,
        string worldId,
        long latestEventId,
        IReadOnlyList<OwnerOrderCancellation> cancellations,
        DeclarativeWorldContentState worldContent,
        IReadOnlyList<RecordedInstructionReceipt> receipts)
    {
        var people = checkpoint.Agents.Select(person => person.Id).ToHashSet(StringComparer.Ordinal);
        var instructionIds = new HashSet<string>(StringComparer.Ordinal);
        var completed = completedInstructionIds.ToHashSet(StringComparer.Ordinal);
        var byId = new Dictionary<string, OwnerQueuedInstruction>(StringComparer.Ordinal);
        var idempotencyKeys = new HashSet<string>(StringComparer.Ordinal);
        var sequences = new HashSet<long>();
        foreach (var instruction in instructions)
        {
            if (instruction is not null) OrderStore.ValidateLocalInstruction(instruction);
            if (instruction is null || string.IsNullOrWhiteSpace(instruction.InstructionId) ||
                instruction.InstructionId.Length > OwnerQueuedInstruction.MaximumIdentifierLength ||
                instruction.InstructionId != InstructionIdFor(instruction.SubmissionSequence) ||
                !instructionIds.Add(instruction.InstructionId) ||
                !IdentityRules.Reference(instruction.IdempotencyKey, OwnerQueuedInstruction.MaximumIdentifierLength) ||
                !idempotencyKeys.Add(instruction.IdempotencyKey) ||
                !IdentityRules.Reference(instruction.IssuerId, OwnerQueuedInstruction.MaximumIdentifierLength) ||
                !people.Contains(instruction.TargetAgentId) ||
                instruction.Kind is not (OwnerInstructionKind.Suggestion or OwnerInstructionKind.Order) ||
                instruction.State != OwnerInstructionState.Queued ||
                string.IsNullOrWhiteSpace(instruction.Text) || instruction.Text.Length > OwnerQueuedInstruction.MaximumTextLength ||
                instruction.Text != instruction.Text.Trim() || instruction.Text.Any(char.IsControl) ||
                instruction.SubmittedTick < 0 || instruction.SubmittedTick > checkpoint.WorldTick ||
                instruction.GuidancePromptedTick is { } promptedTick &&
                    (promptedTick < instruction.SubmittedTick || promptedTick > checkpoint.WorldTick) ||
                instruction.RunEpoch < 0 || instruction.RunEpoch > checkpoint.RunEpoch ||
                instruction.SubmissionSequence <= 0 || !sequences.Add(instruction.SubmissionSequence) ||
                instruction.ObservedTick is { } observedTick &&
                    (observedTick < instruction.SubmittedTick || observedTick > checkpoint.WorldTick) ||
                instruction.ObserverReply is not null &&
                    (instruction.ObservedTick is null ||
                     CognitionDecisionResponse.NormalizeObserverReply(instruction.ObserverReply) != instruction.ObserverReply) ||
                instruction.Kind == OwnerInstructionKind.Suggestion && instruction.Task is not null ||
                instruction.Kind == OwnerInstructionKind.Order && instruction.Task is null ||
                instruction.Task is { } order && !IsValidSavedOrder(order, instruction, completed,
                    people, checkpoint.WorldTick, worldContent))
                throw new InvalidDataException("The saved owner instruction or observer response is invalid.");
            byId.Add(instruction.InstructionId, instruction);
        }

        if (completedInstructionIds.Any(id => string.IsNullOrWhiteSpace(id) || !instructionIds.Contains(id)) ||
            completed.Count != completedInstructionIds.Count)
            throw new InvalidDataException("The completed owner instruction list is invalid.");
        if (instructions.Any(item => item.Kind == OwnerInstructionKind.Suggestion &&
                completed.Contains(item.InstructionId) && item.ObservedTick is null))
            throw new InvalidDataException("A suggestion cannot be completed before an agent's personal model observes it.");

        if (instructions.Where(item => item.Kind == OwnerInstructionKind.Order && item.Task?.IsTerminal == true)
                .GroupBy(item => item.TargetAgentId, StringComparer.Ordinal)
                .Any(group => group.Count() > OwnerQueuedInstruction.RecentClosedOrderLimitPerAgent) ||
            cancellations.Where(item => item is not null).GroupBy(item => item.TargetAgentId, StringComparer.Ordinal)
                .Any(group => group.Count() > OwnerQueuedInstruction.RecentClosedOrderLimitPerAgent))
            throw new InvalidDataException("The closed order or cancellation history exceeds its per-agent display limit.");

        var receiptKeys = new HashSet<string>(StringComparer.Ordinal);
        var receiptIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var recorded in receipts)
        {
            if (recorded is null || recorded.Receipt is not { } receipt ||
                !IdentityRules.Reference(receipt.IdempotencyKey, OwnerQueuedInstruction.MaximumIdentifierLength) || !receiptKeys.Add(receipt.IdempotencyKey) ||
                receipt.Revision <= 0 || receipt.InstructionId != InstructionIdFor(receipt.Revision) || !receiptIds.Add(receipt.InstructionId) ||
                receipt.SubmittedTick < 0 || receipt.SubmittedTick > checkpoint.WorldTick ||
                receipt.RunEpoch < 0 || receipt.RunEpoch > checkpoint.RunEpoch ||
                recorded.RequestDigest is not { } digest || !digest.StartsWith(InstructionRequestDigestPrefix, StringComparison.Ordinal) ||
                digest.Length != InstructionRequestDigestPrefix.Length + System.Security.Cryptography.SHA256.HashSizeInBytes * 2 ||
                !digest[InstructionRequestDigestPrefix.Length..].All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f') ||
                byId.TryGetValue(receipt.InstructionId, out var instruction) &&
                    (receipt.IdempotencyKey != instruction.IdempotencyKey || receipt.SubmittedTick != instruction.SubmittedTick ||
                     receipt.RunEpoch != instruction.RunEpoch || recorded.RequestDigest != InstructionRequestDigest(instruction)))
                throw new InvalidDataException("The saved instruction submission receipt is invalid.");
        }
        if (instructions.Any(instruction => !receiptIds.Contains(instruction.InstructionId)))
            throw new InvalidDataException("An owner instruction is missing its stable submission receipt.");

        var cancellationKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var cancellation in cancellations)
        {
            var instruction = cancellation is null ? null : byId.GetValueOrDefault(cancellation.OrderId);
            if (cancellation is null || string.IsNullOrWhiteSpace(cancellation.IdempotencyKey) ||
                cancellation.IdempotencyKey.Length > DerivedId.MaximumLength || cancellation.IdempotencyKey.Any(char.IsControl) ||
                cancellation.IdempotencyKey != cancellation.IdempotencyKey.Trim() ||
                !cancellationKeys.Add(cancellation.IdempotencyKey) ||
                string.IsNullOrWhiteSpace(cancellation.IssuerId) || cancellation.IssuerId.Length > DerivedId.MaximumLength ||
                cancellation.IssuerId.Any(char.IsControl) || cancellation.IssuerId != cancellation.IssuerId.Trim() ||
                cancellation.WorldId != worldId ||
                cancellation.TargetAgentId != instruction?.TargetAgentId ||
                instruction?.Kind != OwnerInstructionKind.Order || instruction.Task is null ||
                cancellation.Receipt is null || cancellation.Receipt.OrderId != cancellation.OrderId ||
                cancellation.Receipt.Status is not ("finished" or "cancelled" or "not_understood") ||
                cancellation.Receipt.Status != instruction.Task.Status ||
                cancellation.Receipt.Changed && cancellation.Receipt.Status != "cancelled" ||
                cancellation.Receipt.WorldTick < 0 || cancellation.Receipt.WorldTick > checkpoint.WorldTick ||
                cancellation.Receipt.LatestEventId < 0 || cancellation.Receipt.LatestEventId > latestEventId)
                throw new InvalidDataException("The saved order-cancellation receipt is invalid or does not match its world, actor, and order.");
        }
    }

    private static bool IsValidSavedOrder(
        OrderTask order, OwnerQueuedInstruction instruction, HashSet<string> completedInstructionIds,
        HashSet<string> people, long worldTick, DeclarativeWorldContentState worldContent) =>
        order.Definition is not null &&
        (order.LifecycleStatus != OrderStatus.Queued || instruction.Queue) &&
        order.BlockedReason is not { Length: > 256 } && order.BlockedReason?.Any(char.IsControl) != true &&
        (order.LastEffectId is null || IdentityRules.Reference(order.LastEffectId.Value, DerivedId.MaximumLength)) &&
        order.TargetResourceId is not { Length: > ContentPackageRules.MaximumContentIdLength } && order.TargetResourceId?.Any(char.IsControl) != true &&
        (order.TargetFoodKind is null or "berries" or "fruit" or "wild_greens" ||
            order.Action is "consume_food" or "collect_food" && IsEdibleFood(order.TargetFoodKind)) &&
        order.TargetPosition is not { X: < -10_000_000 or > 10_000_000 } &&
        order.TargetPosition is not { Y: < -10_000_000 or > 10_000_000 } &&
        (!order.WaitForDecisionAfterFailure || order.LifecycleStatus == OrderStatus.Blocked && order.BlockedReason is not null) &&
        (order.LifecycleStatus != OrderStatus.Blocked || !string.IsNullOrWhiteSpace(order.BlockedReason)) &&
        order.IsTerminal == completedInstructionIds.Contains(instruction.InstructionId) &&
        IsValidCustodyBindingShape(order) &&
        order.Definition.IsValid(order, instruction, people, worldTick, worldContent);

    internal static bool IsValidShelterOrderShape(OrderTask order, OwnerQueuedInstruction instruction, long worldTick)
    {
        var seeking = order.Action == "seek_shelter";
        if (order.TargetAgentId is not null || order.TargetBuildingKind is not (null or "house") || order.TargetFoodKind is not null ||
            order.TargetResourceId is not null || order.RequestedUnits != 1 || order.CompletedUnits is < 0 or > 1 ||
            order.RepeatUntilCancelled || order.QuantityIsExplicit || order.ProgressUnit != (seeking ? "shelters" : "fires") ||
            order.Status == "not_understood" || (order.Status == "finished") != (order.CompletedUnits == 1) ||
            (order.CompletedUnits == 0 ? order.LastEffectId is not null :
                !IsValidCustodyReceipt(order.LastEffectId, seeking ? "shelter:arrival:" : "shelter:fire:")))
            return false;
        if (order.ShelterBinding is not { } binding)
            return order.CompletedUnits == 0 && order.ShelterCompletion is null;
        if (!IsBoundedShelterPosition(binding.Position) ||
            order.TargetPosition is { } requested && requested != binding.Position ||
            order.TargetBuildingKind == "house" && (binding.Kind != "building" || binding.OwnerId is null))
            return false;
        if (binding.Kind == "natural")
        {
            if (!seeking || binding.BuildingInstanceId is not null || binding.DefinitionId is not null ||
                binding.OwnerId is not null || binding.BuildingPosition is not null || binding.BuildingPlacedTick is not null)
                return false;
        }
        else if (binding.Kind == "building")
        {
            if (binding.BuildingInstanceId is not { } buildingId || !IsValidProductionBindingId(buildingId) ||
                binding.DefinitionId is not { } definitionId || !IsValidProductionBindingId(definitionId) ||
                binding.OwnerId is { } ownerId && !IsValidProductionBindingId(ownerId) ||
                binding.BuildingPosition is not { } anchor || !IsBoundedShelterPosition(anchor) ||
                binding.BuildingPlacedTick is not { } placed || placed < 0 || placed > worldTick)
                return false;
        }
        else return false;
        if (order.ShelterCompletion is not { } completion) return order.CompletedUnits == 0;
        return order.CompletedUnits == 1 && completion.WorldTick >= instruction.SubmittedTick && completion.WorldTick <= worldTick &&
            (binding.BuildingPlacedTick is null || binding.BuildingPlacedTick <= completion.WorldTick) &&
            IsBoundedShelterPosition(completion.Position) &&
            (seeking ? completion.Position == binding.Position && completion.FuelReservationId is null :
                !string.IsNullOrWhiteSpace(completion.FuelReservationId));
    }

    private static bool IsBoundedShelterPosition(GridPoint position) =>
        position.X is >= -10_000_000 and <= 10_000_000 && position.Y is >= -10_000_000 and <= 10_000_000;

    internal static bool IsValidProductionBindingId(string id) => id.Length > 0 &&
        id == id.Trim() && !id.Any(char.IsControl);

    private static bool IsValidCustodyBindingShape(OrderTask order)
    {
        if (IsCartCargoOrder(order.Action))
            return order.TargetStorageBuildingId is null && order.TargetStorageOwnerId is null && order.TargetStoragePosition is null &&
                (order.TargetLotId is null || IsValidProductionBindingId(order.TargetLotId));
        var stores = order.Action is "store_material" or "store_equipment" or "store_goods" or "return_borrowed" or "deliver_stock";
        if (!stores && (order.TargetStorageBuildingId is not null || order.TargetStorageOwnerId is not null ||
            order.TargetStoragePosition is not null) || order.Action != "return_borrowed" && order.TargetLotId is not null)
            return false;
        if (order.TargetStorageBuildingId is null)
            return order.TargetStorageOwnerId is null && order.TargetStoragePosition is null && order.TargetLotId is null &&
                (!stores || order.CompletedUnits == 0);
        return IsValidProductionBindingId(order.TargetStorageBuildingId) &&
            order.TargetStorageOwnerId is { } owner && IsValidProductionBindingId(owner) &&
            order.TargetStoragePosition is { X: >= -10_000_000 and <= 10_000_000, Y: >= -10_000_000 and <= 10_000_000 } location &&
            (order.TargetPosition is null || order.TargetPosition == location) &&
            (order.TargetLotId is null || !string.IsNullOrWhiteSpace(order.TargetLotId) &&
                order.TargetLotId == order.TargetLotId.Trim() && !order.TargetLotId.Any(char.IsControl));
    }

    internal static bool IsValidTreePlantingReceipt(string? receipt, GridPoint? target)
    {
        const string prefix = "tree:plant:" + TreeGrowthRules.PlantedTreeIdPrefix;
        if (receipt?.StartsWith(prefix, StringComparison.Ordinal) != true) return false;
        if (target is { } position) return position.X >= 0 && position.Y >= 0 &&
            receipt == TreePlantingReceipt(TreeGrowthRules.PlantedTreeId(position));
        var coordinates = receipt[prefix.Length..].Split('-');
        return coordinates.Length == 2 && int.TryParse(coordinates[0], out var x) && x >= 0 &&
            int.TryParse(coordinates[1], out var y) && y >= 0 &&
            receipt == TreePlantingReceipt(TreeGrowthRules.PlantedTreeId(new(x, y)));
    }

    internal static bool IsValidCustodyReceipt(string? receipt, string prefix) =>
        receipt is not null && receipt.Length == prefix.Length + 64 && receipt.StartsWith(prefix, StringComparison.Ordinal) &&
        receipt.Skip(prefix.Length).All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    internal static bool IsValidProductionReceipt(string? receipt) => receipt is { Length: 76 } &&
        receipt.StartsWith("produce:job:", StringComparison.Ordinal) &&
        receipt.Skip(12).All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static void ValidateConversationState(WorldRuntimeState state, SocietyCheckpoint checkpoint)
    {
        var conversations = state.Conversations ?? [];
        var budgets = state.ConversationBudgets ?? [];
        var knownAgents = checkpoint.Agents.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var worldDay = WorldCalendarRules.FromTick(checkpoint.WorldTick, state.WorldSystems!.Config).DayIndex;
        if (conversations.Any(item => item is null) || budgets.Any(item => item is null) ||
            conversations.Count > AgentConversationRules.MaximumSavedConversations ||
            conversations.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != conversations.Count ||
            budgets.Count > knownAgents.Count ||
            budgets.Select(item => item.AgentId).Distinct(StringComparer.Ordinal).Count() != budgets.Count ||
            budgets.Any(item => !knownAgents.Contains(item.AgentId) || item.WorldDay < 0 || item.WorldDay > worldDay ||
                item.Count is < 1 or > AgentConversationRules.MaximumConversationsPerWorldDay))
            throw new InvalidDataException("The saved conversation list or daily budgets are invalid.");

        foreach (var conversation in conversations)
        {
            AgentConversationRules.Validate(conversation, checkpoint.WorldTick);
            if (!knownAgents.Contains(conversation.InitiatorId) || !knownAgents.Contains(conversation.InviteeId) ||
                conversation.Turns.Any(turn => turn.ListenerIds.Any(id => !knownAgents.Contains(id))))
                throw new InvalidDataException("A saved conversation references an unknown agent.");
        }

        var activeParticipants = conversations
            .Where(item => item.Status != AgentConversationStatus.Closed)
            .SelectMany(item => new[] { item.InitiatorId, item.InviteeId });
        if (activeParticipants.Distinct(StringComparer.Ordinal).Count() != activeParticipants.Count())
            throw new InvalidDataException("An agent cannot take part in overlapping conversations.");

        var turns = conversations.SelectMany(item => item.Turns).ToDictionary(item => item.Id, StringComparer.Ordinal);
        foreach (var group in checkpoint.AllBeliefs().Where(item => item.SourceTurnId is not null)
                     .GroupBy(item => (item.OwnerId, item.SourceTurnId)))
        {
            var sourceTurnId = group.Key.SourceTurnId!;
            var beliefs = group.ToArray();
            if (!turns.TryGetValue(sourceTurnId, out var turn) ||
                !turn.ListenerIds.Contains(group.Key.OwnerId, StringComparer.Ordinal) ||
                beliefs.Any(item => item.SourceAgentId != turn.SpeakerId) ||
                beliefs.Count(item => item.SupersedesBeliefId is null) != 1 ||
                beliefs.Single(item => item.SupersedesBeliefId is null) is not
                { Provenance: SocietyBeliefProvenance.Hearsay } sourceBelief ||
                sourceBelief.FormedTick != turn.WorldTick || sourceBelief.Statement != turn.Text)
                throw new InvalidDataException("An agent belief references a conversation turn it did not hear.");
        }
    }

    private static WorldMap TravelMap(WorldRuntimeState state)
    {
        var bridges = state.Bridges!;
        RiverBridgeRules.ValidateSaved(bridges, state.Map, state.RoadTiles!, state.Society.Society.WorldTick);
        var decks = RiverBridgeRules.Decks(bridges);
        return RiverBridgeRules.SameDecks(state.Map.BridgeDecks, decks) ? state.Map : state.Map with { BridgeDecks = decks };
    }

    private static void ValidateSavedChildModelSelection(
        AgentState person, SocietyCheckpoint society, int schemaVersion)
    {
        if (person.ChildModelSelection is not { } selection) return;
        if (schemaVersion < ChildModelSelectionSchemaVersion || !society.Births.Any(item => item.ChildId == person.AgentId))
            throw new InvalidDataException("A saved child model choice requires schema 33 and a recorded birth.");
        ValidateChildModelSelection(selection);
    }

    private static void ValidateDeceasedArchive(
        IEnumerable<DeceasedAgentState> archive,
        SocietyCheckpoint society,
        WorldMap map,
        IEnumerable<BridgeState> bridges,
        int schemaVersion,
        IReadOnlyList<TownRuntimeState> towns)
    {
        var townIds = towns.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        // A will may name only a Town that exists; the society layer cannot see Towns.
        if (society.Estates.Any(estate => (estate.WillHeirIds ?? []).Any(id =>
                id.StartsWith("town:", StringComparison.Ordinal) && !townIds.Contains(id))))
            throw new InvalidDataException("A will names a Town that does not exist.");
        if (schemaVersion < WillHeirsSchemaVersion && (archive.Any(person => person.TownId is not null) ||
            society.Estates.Any(estate => estate.WillHeirIds is not null || estate.FinalWords is not null)))
            throw new InvalidDataException($"Wills with several heirs or final words require world schema {WillHeirsSchemaVersion}.");
        var archived = archive.ToArray();
        if (archived.Select(item => item.AgentId).Distinct(StringComparer.Ordinal).Count() != archived.Length)
            throw new InvalidDataException("The deceased agent archive contains duplicate identities.");
        var archivedById = archived.ToDictionary(item => item.AgentId, StringComparer.Ordinal);
        if (society.Estates.Any(estate => (estate.WillHeirIds ?? []).Any(id =>
                id.StartsWith("town:", StringComparison.Ordinal) &&
                (!archivedById.TryGetValue(estate.DeceasedId, out var person) || person.TownId != id))))
            throw new InvalidDataException("A will names a Town other than the deceased person's Town.");
        var deceasedById = society.Agents
            .Where(item => item.Status == SocietyAgentStatus.Dead)
            .ToDictionary(item => item.Id, StringComparer.Ordinal);
        foreach (var person in archived)
        {
            // Later construction cannot change the final position or memories
            // of a deceased person, or make impossible old steps valid.
            var deathMap = MapWithBridges(map, bridges.Where(bridge => bridge.BuiltTick <= person.DeathTick));
            if (!deceasedById.TryGetValue(person.AgentId, out var deceased) ||
                deceased.DeathTick != person.DeathTick || person.DeathTick < 0 || person.DeathTick > society.WorldTick ||
                person.AgeAtDeath < 0 || person.LastPhysical.AgentId != person.AgentId ||
                !deathMap.IsPassable(person.LastPhysical.Position) && !SwimmingRules.IsSwimmingWater(deathMap, person.LastPhysical.Position) &&
                    !(person.BoatIdAtDeath is not null && PortNavigationRules.NavigableWater(deathMap, person.LastPhysical.Position)) ||
                person.LastPhysical.FullnessBasisPoints is < 0 or > 10_000 ||
                person.LastPhysical.Equipment?.OrnamentLotId is not null ||
                person.LastPhysical.GuardianPlacement is not null ||
                person.TownId is { } townId && !townIds.Contains(townId))
                throw new InvalidDataException("The deceased agent archive contains an invalid final state.");
            // Archived care-group departures now authorize personal collection.
            // Validate them at death, before they can become a permission source.
            ValidateDepartures([person.LastPhysical], society with { WorldTick = person.DeathTick }, schemaVersion);
            ValidatePrivateThoughts(person.LastPhysical.RecentThoughts, person.DeathTick);
            AgentIdentityMoment.Validate(person.LastPhysical.IdentityMoments, person.DeathTick, schemaVersion);
            ValidateSavedChildModelSelection(person.LastPhysical, society, schemaVersion);
            ValidateSkills(person.LastPhysical, schemaVersion, person.DeathTick,
                society.Agents.Select(item => item.Id).ToHashSet(StringComparer.Ordinal));
            ValidateExploration(person.LastPhysical.Exploration, deathMap, bridges, person.DeathTick);
            if (person.LastPhysical.Equipment is { } equipment)
                ValidateEquipmentShape(equipment, person.DeathTick, schemaVersion);
        }
    }

    private static void ValidatePrivateThoughts(
        IReadOnlyList<RecentThought>? thoughts, long latestTick)
    {
        if (thoughts is null) return;
        CognitionSystem.ValidateLocalThoughts(thoughts);
        if (thoughts.Any(thought => thought.WorldTick > latestTick))
            throw new InvalidDataException("The private-thought history contains an invalid entry.");
    }

    private static string NormalizeRequiredText(string? value, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, name);
        return value.Trim();
    }
}

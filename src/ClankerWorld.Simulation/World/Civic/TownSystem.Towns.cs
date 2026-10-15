using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using ClankerWorld.AgentPlacement;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal string PlaceFounder(ITownWorld world, string founderId, GridPoint position)
    {
        ValidateFounderPlacementUnsafe(world, founderId, position);
        var setup = founderSetup!;

        var ordinal = setup.FounderIds.Count + 1;
        var householdId = ordinal <= 2 ? WorldRuntime.CivicSharedRules.HouseholdId : WorldRuntime.CivicSharedRules.SecondHouseholdId;
        var name = $"Founder {ordinal}";
        var config = world.Society.Config;
        var founder = SocietyRules.CreateFounder(founderId, name, config: config,
            startingAge: SocietyRules.FounderArrivalAge(config, world.WorldSeed, setup.FounderIds.Count));
        world.ApplySocietyTransition(checkpoint => SocietyRules.PlaceFounder(checkpoint, founder, householdId));
        world.PhysicalAgents.PlaceFounder(founderId, position);
        founderSetup = setup with { FounderIds = [.. setup.FounderIds, founderId] };
        AddTownResident(world, TownBorderRules.FirstTownId, founderId, "founder_joined");
        world.AppendEvent("founder_placed", $"{founderId}:{householdId}");
        return householdId;
    }

    internal bool MoveFounder(ITownWorld world, string founderId, GridPoint position)
    {
        if (founderSetup is not { Started: false } setup || !world.Society.IsPaused ||
            world.WorldTick != 0)
            throw new InvalidOperationException("Founders can only be moved during initial paused setup.");
        if (string.IsNullOrWhiteSpace(founderId) || !setup.FounderIds.Contains(founderId, StringComparer.Ordinal) ||
            !world.Agents.TryGetValue(founderId, out var founder))
            throw new ArgumentException("Choose a placed founder to move.", nameof(founderId));
        if (!world.Map.IsBuildable(position) || world.Map.MapObjects.Any(item => item.Position == position) ||
            world.Map.Resources.Any(item => item.Position == position) ||
            world.Agents.Values.Any(person => person.AgentId != founderId && person.Position == position))
            throw new ArgumentException("Choose an empty passable tile for this founder.", nameof(position));
        if (founder.Position == position) return false;
        world.PhysicalAgents.RecordPosition(founderId, founder, position);
        world.AppendEvent("founder_moved", $"{founderId}:{founder.Position.X},{founder.Position.Y}->{position.X},{position.Y}");
        return true;
    }

    internal int UndoLastFounder(ITownWorld world, string founderId)
    {
        if (founderSetup is not { Started: false } setup || !world.Society.IsPaused ||
            world.WorldTick != 0 || setup.FounderIds.Count == 0)
            throw new InvalidOperationException("Founder placement can only be undone during initial paused setup.");
        if (setup.FounderIds[^1] != founderId)
            throw new ArgumentException("Only the most recently placed founder can be undone.", nameof(founderId));
        world.ApplySocietyTransition(checkpoint => SocietyRules.UndoFounderPlacement(checkpoint, founderId));
        world.PhysicalAgents.RemovePhysicalPresence(founderId);
        founderSetup = setup with { FounderIds = setup.FounderIds.Take(setup.FounderIds.Count - 1).ToArray() };
        RemoveTownResident(world, founderId);
        world.AppendEvent("founder_placement_undone", founderId);
        return founderSetup.FounderIds.Count;
    }

    internal void ValidateFounderPlacementUnsafe(ITownWorld world, string founderId, GridPoint position)
    {
        if (founderSetup is not { Started: false } setup || !world.Society.IsPaused ||
            world.WorldTick != 0 || setup.FounderIds.Count >= WorldRuntime.RequiredFounders)
            throw new InvalidOperationException("Founders can only be placed during the initial paused setup.");
        if (world.HasGeneratedGeography && setup.FounderIds.Count == 0 &&
            world.ContentState.Packages.Any(package =>
                package.Manifest.PackageId == StarterContent.PackageId && package.ActivationTick == 0) &&
            !towns.Any(item => item.Id == TownBorderRules.FirstTownId && item.OriginSite is not null))
            throw new InvalidOperationException("Choose the first Town site before placing founders.");
        if (founderId is null || !founderId.StartsWith("founder:", StringComparison.Ordinal) ||
            !Guid.TryParseExact(founderId["founder:".Length..], "N", out _) ||
            world.Agents.ContainsKey(founderId))
            throw new ArgumentException("The founder ID is invalid or already used.", nameof(founderId));
        if (!world.Map.IsBuildable(position) || world.Map.MapObjects.Any(item => item.Position == position) ||
            world.Map.Resources.Any(item => item.Position == position) ||
            world.Agents.Values.Any(person => person.Position == position))
            throw new ArgumentException("Choose an empty passable tile for this founder.", nameof(position));
    }

    internal string? AddAgentUnsafe(ITownWorld world, string agentId, GridPoint position, AgentPlacementResolution membership)
    {
        var town = membership.TownId is null ? null : towns.Single(item => item.Id == membership.TownId);
        var householdId = membership.HouseholdIdFor(agentId);
        world.ApplySocietyTransition(checkpoint => SocietyRules.AddAdult(checkpoint, agentId, householdId));
        world.PhysicalAgents.PlaceFounder(agentId, position);
        if (town is not null) AddTownResident(world, town.Id, agentId, "agent_joined");
        else world.AppendEvent("town_membership_evaluated", $"{agentId}:unaffiliated");
        world.AppendEvent("agent_added", agentId);
        return householdId;
    }

    internal AgentPlacementResolution ValidateAgentPlacementUnsafe(ITownWorld world, string agentId, GridPoint position)
    {
        if (founderSetup is not { Started: true })
            throw new InvalidOperationException("Start the world with four founders before adding more agents.");
        if (agentId is null || !agentId.StartsWith("agent:", StringComparison.Ordinal) ||
            !Guid.TryParseExact(agentId["agent:".Length..], "N", out _) ||
            world.Society.Agents.Any(person => person.Id == agentId))
            throw new ArgumentException("The agent ID is invalid or already used.", nameof(agentId));
        if (!world.Map.IsBuildable(position) || world.Map.MapObjects.Any(item => item.Position == position) ||
            world.Map.Resources.Any(item => item.Position == position) ||
            (world.Agents.Values.Any(person => person.Position == position) && !IsHouseAt(world, position)))
            throw new ArgumentException("Choose an empty passable tile for this agent.", nameof(position));

        var definitions = world.WorldContent.BuildingDefinitions;
        var buildingOwners = world.WorldSimulation.Buildings.Where(building => building.HouseholdId is not null &&
                definitions.TryGetValue(building.DefinitionId, out var definition) &&
                WorldContentSimulationRules.Footprint(definition, building).Contains(position))
            .Select(building => building.HouseholdId)
            .ToArray();
        var householdOwners = buildingOwners
            .Concat(world.Fields.Where(field => field.Position == position).Select(field => (string?)field.HouseholdId))
            .Concat(HouseholdLandClaimantsAt(world, position, buildingOwners.Length > 0));
        var townIds = towns.Where(item => item.BorderTiles.Contains(position) ||
                TownLandRightsRules.IsCoveredByTownTitle(position, item.Id, townLandTitles))
            .Select(item => item.Id);
        var membership = AgentPlacementRules.Resolve(householdOwners, townIds);
        // Only a household that holds a House has resident places to fill; one
        // with only a Farmhouse or field can still take a new member.
        if (membership.HouseholdPropertyOwnerId is { } householdId && world.HouseForHousehold(householdId) is not null)
        {
            var proposed = SocietyRules.CreateFounder(agentId, "New agent", config: world.Society.Config) with
            {
                HouseholdId = householdId,
            };
            if (!world.CanFitHouseResident(householdId, proposed))
                throw new InvalidOperationException("The household's House has no free resident place. Complete an expansion before placing another resident.");
        }
        return membership;
    }

    internal IEnumerable<string?> HouseholdLandClaimantsAt(ITownWorld world, GridPoint position, bool householdBuildingStands)
    {
        var rights = householdLandUseRights.Where(right => right.Tiles.Contains(position)).ToArray();
        var requests = householdLandUseRequests.Where(request => request.Tiles.Contains(position)).ToArray();
        if (TownLandRightsRules.IsDisputed(position, rights, requests))
            return TownLandRightsRules.ClaimantsAt(position, rights, requests);
        // A household building's current owner comes before another household's
        // undisputed use right, so a reassigned building does not block placement.
        // A field does not: tilling land never overrides a recorded right.
        if (householdBuildingStands)
            return [];
        return rights.Select(right => right.HouseholdId);
    }

    internal static void EnsurePlacementIsUnambiguous(AgentPlacementResolution membership)
    {
        if (membership.IsAmbiguous) throw new InvalidOperationException(membership.AmbiguityExplanation());
    }

    internal static void EnsureExpectedPlacement(string agentId, AgentPlacementResolution membership,
            string? expectedHouseholdId, string? expectedTownId)
    {
        if (membership.IsAmbiguous)
            throw new AgentPlacementChangedException(membership.AmbiguityExplanation());
        if (!membership.Matches(agentId, expectedHouseholdId, expectedTownId))
            throw new AgentPlacementChangedException();
    }

    internal static bool IsHouseAt(ITownWorld world, GridPoint position)
    {
        var definitions = world.WorldContent.BuildingDefinitions;
        return world.WorldSimulation.Buildings.Any(building =>
            definitions.TryGetValue(building.DefinitionId, out var definition) &&
            definition.Tags.Contains("house", StringComparer.Ordinal) &&
            WorldContentSimulationRules.Footprint(definition, building).Contains(position));
    }

    internal void StartWorld(ITownWorld world, bool resume)
    {
        if (founderSetup is not { Started: false } setup || setup.FounderIds.Count != WorldRuntime.RequiredFounders ||
            !world.Society.IsPaused || world.WorldTick != 0)
            throw new InvalidOperationException("Place and configure all four founders before starting time.");
        founderSetup = setup with { Started = true };
        if (towns.SingleOrDefault(item => item.Id == TownBorderRules.FirstTownId) is { } firstTown)
        {
            SetTown(world, firstTown with { FoundingState = "founded" });
            world.AppendEvent("town_founded", $"{firstTown.Id}:residents:{firstTown.ResidentIds.Count}");
        }
        AdvanceTownCouncil(world);
        SettleTownAdmissions(world);
        if (resume) world.ResumeSociety();
        world.AppendEvent("world_started", "four_founders_ready");
    }

    internal void AddTownResident(ITownWorld world, string townId, string residentId, string reason)
    {
        var town = towns.SingleOrDefault(item => item.Id == townId)
            ?? throw new InvalidOperationException("The resident's Town does not exist.");
        if (town.ResidentIds.Contains(residentId, StringComparer.Ordinal)) return;
        SetTown(world, town with
        {
            ResidentIds = town.ResidentIds.Append(residentId).Order(StringComparer.Ordinal).ToArray(),
        });
        AdvanceTownCouncil(world);
        var updated = towns.Single(item => item.Id == townId);
        world.AppendEvent("town_resident_joined", $"{updated.Id}:{residentId}:{reason}:residents:{updated.ResidentIds.Count}");
        SettleTownAdmissions(world);
    }

    internal void RemoveTownResident(ITownWorld world, string residentId)
    {
        var town = towns.SingleOrDefault(item => item.ResidentIds.Contains(residentId, StringComparer.Ordinal));
        if (town is null) return;
        var residents = town.ResidentIds.Where(id => id != residentId).ToArray();
        SetTown(world, town with { ResidentIds = residents });
        AdvanceTownCouncil(world);
        world.AppendEvent("town_resident_left", $"{town.Id}:{residentId}:residents:{residents.Length}");
        SettleTownAdmissions(world);
    }

    internal string? TownForResident(ITownWorld world, string residentId) => towns
            .SingleOrDefault(item => item.ResidentIds.Contains(residentId, StringComparer.Ordinal))?.Id;

    internal TownRuntimeState? TownForOwnerPlacement(ITownWorld world, GridPoint position, BuildingDefinition definition) => towns
            .SingleOrDefault(item => TownBorderRules.IsWithinOrAdjacent(item, position, definition.Width, definition.Height));

    internal void AssignBuildingToTown(ITownWorld world, PlacedBuilding building, BuildingDefinition definition)
    {
        var town = towns.SingleOrDefault(item => item.Id == building.TownId)
            ?? throw new InvalidOperationException("The assigned Town does not exist.");
        if (town.AssignedBuildingIds.Contains(building.InstanceId, StringComparer.Ordinal))
            throw new InvalidDataException("A placed building is already assigned to its Town.");
        var border = TownBorderRules.ExpandForBuilding(world.Map, town, building.Position, definition.Width, definition.Height);
        SetTown(world, town with
        {
            AssignedBuildingIds = town.AssignedBuildingIds.Append(building.InstanceId)
                .Order(StringComparer.Ordinal).ToArray(),
            BorderTiles = border,
            FirstBuildingCompletedTick = town.FirstBuildingCompletedTick ?? world.WorldTick,
        });
        var updated = towns.Single(item => item.Id == town.Id);
        world.AppendEvent("town_building_assigned", $"{town.Id}:{building.InstanceId}:buildings:{updated.AssignedBuildingIds.Count}");
        if (!town.BorderTiles.SequenceEqual(border))
            world.AppendEvent("town_border_expanded", $"{town.Id}:{building.InstanceId}:tiles:{border.Count}");
        if (town.FirstBuildingCompletedTick is null)
            world.GenerateRoadBetweenTowns(updated, building);
        var laid = world.GenerateRoadToBuilding(building, updated.BorderTiles.ToHashSet());
        if (laid.Count == 0) return;
        // Town Roads stay inside the border, so it grows around the new Road too.
        var withRoad = TownBorderRules.Expand(world.Map, updated, laid);
        if (withRoad.Count == updated.BorderTiles.Count) return;
        SetTown(world, updated with { BorderTiles = withRoad });
        world.AppendEvent("town_border_expanded", $"{town.Id}:{building.InstanceId}:tiles:{withRoad.Count}");
    }

    internal void SetTown(ICivicWorld world, TownRuntimeState updated)
    {
        var previous = towns.FirstOrDefault(item => item.Id == updated.Id);
        if (previous is null) throw new InvalidOperationException("The Town identity does not exist.");
        updated = updated with
        {
            AbandonedSinceTick = updated.IsAbandoned
                ? previous.IsAbandoned ? previous.AbandonedSinceTick : world.WorldTick
                : null,
        };
        SetTownState(updated);
        if (!previous.IsAbandoned && updated.IsAbandoned)
            world.AppendEvent("town_abandoned", updated.Id);
        else if (previous.IsAbandoned && !updated.IsAbandoned)
            world.AppendEvent("town_revived", updated.Id);
    }

}

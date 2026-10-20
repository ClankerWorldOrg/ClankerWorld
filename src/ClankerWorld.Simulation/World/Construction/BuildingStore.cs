namespace ClankerWorld.Simulation.World;

internal sealed class BuildingStore(WorldConstructionState state)
{
    internal WorldConstructionState State { get; private set; } = state;

    internal void PlaceBuildings(IEnumerable<PlacedBuilding> placements) => Commit(State with
    {
        Buildings = State.Buildings.Concat(placements).OrderBy(building => building.InstanceId, StringComparer.Ordinal).ToArray(),
    });

    internal void ReplaceFirstTownLayout(IReadOnlyList<PlacedBuilding> placements) =>
        Commit(WorldConstructionState.From(WorldContentSimulationState.Empty with { Buildings = placements }));

    internal void AssignFoundingTown(IReadOnlySet<string> assigned, string townId) => Commit(State with
    {
        Buildings = State.Buildings.Select(building => assigned.Contains(building.InstanceId)
            ? building with { TownId = townId } : building).ToArray(),
    });

    internal void RemoveBuilding(string instanceId, string definitionId) => Commit(State with
    {
        Buildings = State.Buildings.Where(building => building.InstanceId != instanceId).ToArray(),
        Expansions = (State.Expansions ?? []).Select(job => job.BuildingInstanceId == instanceId && job.DefinitionId is null
            ? job with { DefinitionId = definitionId } : job).ToArray(),
        Guests = (State.Guests ?? []).Where(invitation => invitation.HouseInstanceId != instanceId).ToArray(),
    });

    internal void ReassignOwner(string instanceId, string? townId, string? householdId, bool removeInvitations) => Commit(State with
    {
        Buildings = State.Buildings.Select(building => building.InstanceId == instanceId
            ? building with { TownId = townId, HouseholdId = householdId } : building).ToArray(),
        Guests = removeInvitations ? (State.Guests ?? []).Where(invitation => invitation.HouseInstanceId != instanceId).ToArray() : State.Guests,
    });

    internal void TransferProperty(PlacedBuilding replacement) => Commit(State with
    {
        Buildings = State.Buildings.Select(building => building.InstanceId == replacement.InstanceId ? replacement : building).ToArray(),
        Guests = (State.Guests ?? []).Where(invitation => invitation.HouseInstanceId != replacement.InstanceId).ToArray(),
    });

    internal void RecordEntrance(string instanceId, GridPoint entrance) => Commit(State with
    {
        Buildings = State.Buildings.Select(building => building.InstanceId == instanceId ? building with { Entrance = entrance } : building).ToArray(),
    });

    internal void RecordGuestInvitation(HouseGuestInvitation invitation) => Commit(State with
    {
        Guests = (State.Guests ?? []).Where(item => item.HouseInstanceId != invitation.HouseInstanceId || item.GuestId != invitation.GuestId)
            .Append(invitation).OrderBy(item => item.HouseInstanceId, StringComparer.Ordinal).ThenBy(item => item.GuestId, StringComparer.Ordinal).ToArray(),
    });

    internal void RecordConstructionReceipt(WorldConstructionReceipt receipt) => Commit(State with
    {
        Receipts = (State.Receipts ?? []).Append(receipt).OrderBy(item => item.InstructionId.Value, StringComparer.Ordinal).ToArray(),
    });

    internal void StartExpansion(BuildingExpansionJob job) => Commit(State with
    {
        Expansions = (State.Expansions ?? []).Append(job).OrderBy(item => item.JobId, StringComparer.Ordinal).ToArray(),
    });

    internal void ApplyExpandedFootprint(PlacedBuilding expanded) => Commit(State with
    {
        Buildings = State.Buildings.Select(building => building.InstanceId == expanded.InstanceId ? expanded : building).ToArray(),
    });

    internal void FinishExpansion(string id, string? failure) => Commit(State with
    {
        Expansions = (State.Expansions ?? []).Select(job => job.JobId == id
            ? job with { State = failure is null ? WorldProductionJobState.Completed : WorldProductionJobState.Cancelled, Failure = failure } : job).ToArray(),
    });

    internal void PauseExpansion(string id, long tick) => Commit(State with
    {
        Expansions = State.Expansions!.Select(job => job.JobId == id
            ? job with { State = WorldProductionJobState.Paused, PausedAtTick = tick } : job).ToArray(),
    });

    internal void ResumeExpansion(string id, long deadline, string? worker = null) => Commit(State with
    {
        Expansions = State.Expansions?.Select(job => job.JobId == id
            ? job with { WorkerId = worker ?? job.WorkerId, State = WorldProductionJobState.Running, CompletionTick = deadline, PausedAtTick = null } : job).ToArray(),
    });

    internal void PauseHouseholdExpansions(string actor, string household, long tick) => Commit(State with
    {
        Expansions = State.Expansions?.Select(job => job.WorkerId == actor && job.OwnerId == household && job.State == WorldProductionJobState.Running
            ? job with { State = WorldProductionJobState.Paused, PausedAtTick = tick } : job).ToArray(),
    });

    internal void CancelPausedExpansions(IReadOnlySet<string> ids, string failure) => Commit(State with
    {
        Expansions = State.Expansions?.Select(job => ids.Contains(job.JobId)
            ? job with { State = WorldProductionJobState.Cancelled, Failure = failure } : job).ToArray(),
    });

    internal void RetireOrderLinks(IReadOnlySet<string> retired) => Commit(State with
    {
        Expansions = State.Expansions?.Select(job => retired.Contains(job.OrderInstructionId ?? "")
            ? job with { OrderInstructionId = null } : job).ToArray(),
        Receipts = State.Receipts?.Where(receipt => !retired.Contains(receipt.InstructionId)).ToArray(),
    });

    private void Commit(WorldConstructionState next)
    {
        if (SystemInvariantChecks.Enabled) ValidateLocalState(next);
        State = next;
    }

    internal static void ValidateLocalState(WorldConstructionState state)
    {
        ValidateBuildings(state.Buildings);
        var expansions = state.Expansions ?? [];
        if (expansions.Any(job => job is null) || !expansions.Select(job => job.JobId).SequenceEqual(expansions.Select(job => job.JobId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)))
            throw new InvalidDataException("Construction expansions have duplicate identities or are not in canonical order.");
        foreach (var job in expansions)
        {
            ClankerWorld.Simulation.Content.ContentPackageRules.ValidateLocalId(job.JobId);
            if (!Enum.IsDefined(job.State) || string.IsNullOrWhiteSpace(job.OwnerId) || job.StartedTick < 0 || job.CompletionTick <= job.StartedTick ||
                job.TargetFootprint is null || job.TargetFootprint.Width <= 0 || job.TargetFootprint.Height <= 0 || job.TargetFootprint.Revision <= 0 ||
                job.ExpectedRevision != job.TargetFootprint.Revision - 1 || job.InputReservationIds is null || job.InputReservationIds.Count == 0 ||
                job.InputReservationIds.Count != job.InputReservationIds.Distinct().Count() ||
                job.State == WorldProductionJobState.Paused && (job.PausedAtTick is not { } paused || paused < job.StartedTick || paused >= job.CompletionTick) ||
                job.State is not (WorldProductionJobState.Paused or WorldProductionJobState.Cancelled) && job.PausedAtTick is not null)
                throw new InvalidDataException("Construction expansion has an invalid local state.");
        }
        var guests = state.Guests ?? [];
        if (guests.Any(item => item is null || !ValidConstructionIdentity(item.HouseInstanceId) || !ValidConstructionIdentity(item.GuestId) ||
                !ValidConstructionIdentity(item.InvitedById) || item.ChangedTick < 0) ||
            guests.GroupBy(item => (item.HouseInstanceId, item.GuestId)).Any(group => group.Count() > 1) ||
            !guests.SequenceEqual(guests.OrderBy(item => item.HouseInstanceId, StringComparer.Ordinal).ThenBy(item => item.GuestId, StringComparer.Ordinal)))
            throw new InvalidDataException("Building guest access has invalid or duplicated records.");
        ValidateReceipts(state.Receipts ?? []);
    }

    internal static void ValidateBuildings(IReadOnlyList<PlacedBuilding> buildings)
    {
        if (buildings.Any(building => building is null) || !buildings.Select(building => building.InstanceId).SequenceEqual(
                buildings.Select(building => building.InstanceId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)))
            throw new InvalidDataException("Building records have duplicate identities or are not in canonical order.");
        foreach (var building in buildings)
        {
            ClankerWorld.Simulation.Content.ContentPackageRules.ValidateLocalId(building.InstanceId);
            if (building.PlacedTick < 0 || building.HouseholdId is { } household && !ValidConstructionIdentity(household) ||
                building.Footprint is { } footprint && (footprint.Width <= 0 || footprint.Height <= 0 || footprint.Revision <= 0))
                throw new InvalidDataException("Building placement or local ownership is invalid.");
        }
    }

    internal static bool ValidConstructionIdentity(string? value) =>
        IdentityRules.Reference(value, int.MaxValue, requireTrimmed: true);

    internal static void ValidateReceipts(IReadOnlyList<WorldConstructionReceipt> receipts)
    {
        if (receipts.Any(item => item is null || !ValidConstructionIdentity(item.InstructionId) || !ValidConstructionIdentity(item.ActorId) ||
                !ValidConstructionIdentity(item.BuildingInstanceId) || !ValidConstructionIdentity(item.DefinitionId) ||
                !ValidConstructionIdentity(item.OwnerId) || !ValidConstructionIdentity(item.MaterialOwnerId) ||
                item.StartedTick < 0 || item.CompletedTick < item.StartedTick || item.InputReservationIds is null ||
                item.InputReservationIds.Any(id => !ValidConstructionIdentity(id)) ||
                item.InputReservationIds.Distinct().Count() != item.InputReservationIds.Count) ||
            !receipts.Select(item => item.InstructionId.Value).SequenceEqual(receipts.Select(item => item.InstructionId.Value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)) ||
            receipts.Select(item => item.BuildingInstanceId).Distinct(StringComparer.Ordinal).Count() != receipts.Count)
            throw new InvalidDataException("Construction receipts have invalid or duplicated local records.");
    }
}

namespace ClankerWorld.Simulation.World;

internal sealed record ProductionState(
    IReadOnlyList<WorldProductionJob> Jobs,
    long NextSequence,
    IReadOnlyList<WorldProductionJob>? CropBuilds,
    IReadOnlyList<ToolMakingRequestState> Requests);

internal sealed partial class ProductionSystem
{
    private readonly Dictionary<string, (long StartedTick, string? InstructionId, RecipeProjectActionPlan Plan)> recipePlans = new(StringComparer.Ordinal);
    private WorldConstructionState? constructionBasis;
    private WorldContentSimulationState projection;

    internal ProductionSystem(WorldContentSimulationState snapshot, IReadOnlyList<ToolMakingRequestState> requests)
    {
        State = new(snapshot.ProductionJobs, snapshot.NextProductionJobSequence, snapshot.CropBuilds, requests.ToArray());
        projection = snapshot;
    }

    internal ProductionState State { get; private set; }

    internal WorldContentSimulationState Snapshot(WorldConstructionState construction)
    {
        if (constructionBasis is null && ReferenceEquals(projection.Buildings, construction.Buildings) &&
            ReferenceEquals(projection.BuildingExpansions, construction.Expansions) && ReferenceEquals(projection.GuestInvitations, construction.Guests) &&
            ReferenceEquals(projection.ConstructionReceipts, construction.Receipts))
            constructionBasis = construction;
        if (ReferenceEquals(constructionBasis, construction) && ReferenceEquals(projection.ProductionJobs, State.Jobs) &&
            ReferenceEquals(projection.CropBuilds, State.CropBuilds) && projection.NextProductionJobSequence == State.NextSequence)
            return projection;
        projection = new(construction.Buildings, State.Jobs, State.NextSequence, State.CropBuilds,
            construction.Expansions, construction.Guests, construction.Receipts);
        constructionBasis = construction;
        return projection;
    }

    internal void StartJob(WorldProductionJob job) => Commit(State with
    {
        Jobs = State.Jobs.Append(job).OrderBy(item => item.JobId, StringComparer.Ordinal).ToArray(),
        NextSequence = checked(State.NextSequence + 1),
    });

    internal void AdvanceExpansionSequence() => Commit(State with { NextSequence = State.NextSequence + 1 });

    internal void FinishJob(string id, bool completed) => Commit(State with
    {
        Jobs = State.Jobs.Select(job => job.JobId == id
            ? job with { State = completed ? WorldProductionJobState.Completed : WorldProductionJobState.Cancelled } : job)
            .OrderBy(job => job.JobId, StringComparer.Ordinal).ToArray(),
    });

    internal void CancelJobs(IReadOnlySet<string> ids, bool includeCropBuilds = false) => Commit(State with
    {
        Jobs = State.Jobs.Select(job => ids.Contains(job.JobId) ? job with { State = WorldProductionJobState.Cancelled } : job).ToArray(),
        CropBuilds = includeCropBuilds ? State.CropBuilds?.Select(job => ids.Contains(job.JobId)
            ? job with { State = WorldProductionJobState.Cancelled } : job).ToArray() : State.CropBuilds,
    });

    internal void PauseJob(string id, long tick) => Commit(State with
    {
        Jobs = State.Jobs.Select(job => job.JobId == id
            ? job with { State = WorldProductionJobState.Paused, PausedAtTick = tick } : job).ToArray(),
    });

    internal void ResumeJob(string id, long deadline, string? knifeId, string? worker = null) => Commit(State with
    {
        Jobs = State.Jobs.Select(job => job.JobId == id
            ? job with
            {
                WorkerId = worker ?? job.WorkerId,
                State = WorldProductionJobState.Running,
                CompletionTick = deadline,
                PausedAtTick = null,
                ToolLotId = knifeId
            } : job).ToArray(),
    });

    internal void LeaveHousehold(string actor, IReadOnlySet<string> buildings, IReadOnlySet<string> personalJobs, long tick) => Commit(State with
    {
        Jobs = State.Jobs.Select(job => personalJobs.Contains(job.JobId)
            ? job with { State = WorldProductionJobState.Cancelled }
            : job.WorkerId == actor && buildings.Contains(job.BuildingInstanceId) && job.State == WorldProductionJobState.Running
            ? job with { State = WorldProductionJobState.Paused, PausedAtTick = tick } : job).ToArray(),
    });

    internal void PlaceRequest(ToolMakingRequestState request) => Commit(State with { Requests = State.Requests.Append(request).ToArray() });

    internal bool TransitionRequest(ToolMakingRequestState request, long tick)
    {
        if (request.Blocker is { Length: > 160 } blocker) request = request with { Blocker = blocker[..160].TrimEnd() };
        var index = State.Requests.ToList().FindIndex(item => item.Id == request.Id);
        if (index < 0 || State.Requests[index] == request) return false;
        var requests = State.Requests.ToArray();
        requests[index] = request with { LastTransitionTick = tick };
        Commit(State with { Requests = requests });
        return true;
    }

    internal void RetireRequests(IReadOnlySet<string> ids) => Commit(State with
    {
        Requests = State.Requests.Where(request => !ids.Contains(request.Id)).ToArray(),
        Jobs = State.Jobs.Select(job => job.ToolMakingRequestId is { } id && ids.Contains(id)
            ? job with { ToolMakingRequestId = null } : job).ToArray(),
    });

    internal void RememberRecipePlan(string actor, long startedTick, string? instructionId, RecipeProjectActionPlan plan) =>
        recipePlans[actor] = (startedTick, instructionId, plan);

    internal void ForgetRecipePlan(string actor) => recipePlans.Remove(actor);

    internal RecipeProjectActionPlan? RecipePlan(string actor, AgentProject project, string? instructionId) =>
        recipePlans.TryGetValue(actor, out var retained) && retained.StartedTick == project.StartedTick &&
        retained.Plan.CandidateId == project.CandidateId && retained.InstructionId == instructionId ? retained.Plan : null;

    internal void CopyRecipePlansFrom(ProductionSystem source,
        Func<string, long, string?, RecipeProjectActionPlan, bool> retain)
    {
        var selected = source.recipePlans.Where(item => retain(item.Key, item.Value.StartedTick,
            item.Value.InstructionId, item.Value.Plan)).ToArray();
        recipePlans.Clear();
        foreach (var (actor, plan) in selected) recipePlans.Add(actor, plan);
    }

    private void Commit(ProductionState next)
    {
        if (SystemInvariantChecks.Enabled) ValidateLocalState(next);
        State = next;
    }

    internal static void ValidateLocalState(ProductionState state)
    {
        ValidateJobs(state.Jobs, state.NextSequence, state.CropBuilds);
        if (state.Requests.Select(request => request.Id).Distinct(StringComparer.Ordinal).Count() != state.Requests.Count)
            throw new InvalidDataException("Production requests have duplicate identities.");
        foreach (var request in state.Requests)
            if (!Enum.IsDefined(request.Status) || request.RequestedTick < 0 || request.LastTransitionTick < request.RequestedTick ||
                (request.AcceptedTick is not null) != (request.WorkerId is not null) ||
                request.AcceptedTick is { } accepted && (accepted < request.RequestedTick || accepted > request.LastTransitionTick))
                throw new InvalidDataException("Production request has an invalid local state.");
    }
    internal static void ValidateJobs(IReadOnlyList<WorldProductionJob> jobs, long nextSequence, IReadOnlyList<WorldProductionJob>? cropBuilds)
    {
        if (nextSequence <= 0)
            throw new InvalidDataException("The next production job sequence must be positive.");
        if (cropBuilds is { Count: > 0 })
            throw new InvalidDataException("Crop work must belong to a tilled field.");
        if (jobs.Any(job => job is null) || !jobs.Select(job => job.JobId).SequenceEqual(
                jobs.Select(job => job.JobId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)))
            throw new InvalidDataException("Production jobs have duplicate identities or are not in canonical order.");
        foreach (var job in jobs)
        {
            ClankerWorld.Simulation.Content.ContentPackageRules.ValidateLocalId(job.JobId);
            ClankerWorld.Simulation.Content.ContentPackageRules.ValidateLocalId(job.BuildingInstanceId);
            if (!Enum.IsDefined(job.State) || string.IsNullOrWhiteSpace(job.WorkerId) || job.StartedTick < 0 ||
                job.OwnerId is { } owner && (string.IsNullOrWhiteSpace(owner) || owner != owner.Trim()) ||
                job.CompletionTick <= job.StartedTick || job.InputReservationIds is null ||
                job.InputReservationIds.Count != job.InputReservationIds.Distinct(StringComparer.Ordinal).Count() ||
                job.ToolLotId is { } tool && (string.IsNullOrWhiteSpace(tool) || tool.Any(char.IsControl)) ||
                job.State == WorldProductionJobState.Paused && (job.PausedAtTick is not { } paused || paused < job.StartedTick || paused >= job.CompletionTick) ||
                job.State is not (WorldProductionJobState.Paused or WorldProductionJobState.Cancelled) && job.PausedAtTick is not null)
                throw new InvalidDataException("Production job has an invalid local state.");
        }
    }
}

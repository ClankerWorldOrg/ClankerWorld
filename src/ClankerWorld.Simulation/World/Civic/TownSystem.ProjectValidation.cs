namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal static void ValidateLocalProjects(IReadOnlyList<TownProject>? projects)
    {
        if (projects is null) throw new InvalidDataException("A Town is missing its construction ledger.");
        var identities = new HashSet<string>(StringComparer.Ordinal);
        var deliveries = new HashSet<string>(StringComparer.Ordinal);
        foreach (var project in projects)
        {
            if (project is null || !identities.Add(project.Id)) throw new InvalidDataException("Town project identities must be unique.");
            TownProjectRules.ValidatePayload(project.Plan);
            var requiredWork = TownProjectRules.RequiredWork(project.Plan);
            if (project.ApprovedTick < 0 || project.LastTransitionTick < project.ApprovedTick ||
                project.Stage is not ("supplying" or "working" or "blocked" or "completed" or "cancelled") ||
                project.WorkDone < 0 || project.WorkDone > requiredWork || project.Deliveries is null ||
                project.Blocker is { Length: > 512 } ||
                project.Stage == "blocked" && string.IsNullOrWhiteSpace(project.Blocker) ||
                project.Stage is "supplying" or "working" or "completed" && project.Blocker is not null)
                throw new InvalidDataException("A Town project disagrees with its Council approval or construction stage.");
            var ordinal = 0;
            var liveQuantities = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var delivery in project.Deliveries)
            {
                var cost = delivery is null ? default : project.Plan.Budget.SingleOrDefault(item => item.ResourceId == delivery.ItemKind);
                if (delivery is null || delivery.Id != TownProjectRules.DeliveryId(project.Id, delivery.ContributorId,
                        delivery.SourceLotId, delivery.PickedUpTick, ordinal) || !deliveries.Add(delivery.Id) ||
                    string.IsNullOrWhiteSpace(delivery.SourceLotId) || string.IsNullOrWhiteSpace(delivery.LotId) ||
                    cost.Amount <= 0 || delivery.Quantity <= 0 || delivery.Quantity > cost.Amount || delivery.PickedUpTick < project.ApprovedTick ||
                    (delivery.DeliveredTick is null) != (delivery.ReservationId is null) ||
                    delivery.DeliveredTick is { } deliveredAt && deliveredAt < delivery.PickedUpTick ||
                    delivery.ReleasedTick is { } releasedAt && releasedAt < (delivery.DeliveredTick ?? delivery.PickedUpTick) ||
                    (delivery.ReleasedTick is null) != (delivery.ReleaseReason is null) ||
                    delivery.ReleaseReason is { Length: > 512 } || delivery.ReleaseReason is { } reason && string.IsNullOrWhiteSpace(reason))
                    throw new InvalidDataException("A Town project's retained material delivery is invalid.");
                ordinal++;
                if (delivery.ReservationId is { } receiptId && receiptId != TownRecordIds.DeliveryInput(delivery.Id))
                    throw new InvalidDataException("A Town project's exact material receipt is missing or changed.");
                if (delivery.ReleasedTick is not null) continue;
                if (project.Stage is "blocked" or "cancelled")
                    throw new InvalidDataException("A blocked or cancelled Town project must release unspent claims and incoming bindings.");
                liveQuantities[delivery.ItemKind] = liveQuantities.GetValueOrDefault(delivery.ItemKind) + delivery.Quantity;
                if (liveQuantities[delivery.ItemKind] > cost.Amount)
                    throw new InvalidDataException("Town project inputs exceed the approved material budget.");
                if (project.Stage == "completed" && delivery.DeliveredTick is null)
                    throw new InvalidDataException("A completed Town project cannot retain undelivered cargo.");
            }
            if (project.Stage != "completed" && (project.CompletedBuildingId is not null || project.CompletedBoatId is not null || project.RemovedTick is not null))
                throw new InvalidDataException("An unfinished Town project cannot own a completed building.");
            if (project.Stage == "completed" && (project.WorkDone != requiredWork ||
                (project.Plan.BoatPortId is not null ? project.CompletedBoatId != TownProjectRules.BuildingId(project.Id) || project.CompletedBuildingId is not null
                    : project.CompletedBuildingId != TownProjectRules.BuildingId(project.Id) || project.CompletedBoatId is not null)))
                throw new InvalidDataException("A working or completed Town project lacks its exact delivered material budget.");
            if (project.RemovedTick is { } removed && removed < project.LastTransitionTick)
                throw new InvalidDataException("A completed Town building disagrees with its paid approval or retained removal.");
        }
    }
}

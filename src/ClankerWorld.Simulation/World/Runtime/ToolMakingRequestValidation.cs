using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static void ValidateToolMakingRequests(IReadOnlyList<ToolMakingRequestState>? requests,
        WorldContentSimulationState simulation, DeclarativeWorldContentState content, SocietyCheckpoint societyState,
        IEnumerable<AgentState> people, IReadOnlyList<ShopTradeState> trades, long worldTick)
    {
        ProductionSystem.ValidateLocalRequests(requests, enforceRetention: true);
        if (!requests.Select(request => request.Id).SequenceEqual(requests.Select(request => request.Id).Order(StringComparer.Ordinal)))
            throw new InvalidDataException("Tool requests are missing, unbounded or duplicated.");
        var physical = people.ToDictionary(person => person.AgentId, StringComparer.Ordinal);
        foreach (var request in requests)
        {
            var terminal = ToolMakingRequestRules.IsTerminal(request.Status);
            var recipe = content.Recipes.FirstOrDefault(recipe => recipe.CanonicalId == request.RecipeId);
            var building = simulation.Buildings.FirstOrDefault(building => building.InstanceId == request.BuildingInstanceId);
            if (request.LastTransitionTick > worldTick ||
                !societyState.Agents.Any(person => person.Id == request.RequesterId) ||
                !societyState.Households.Any(household => household.Id == request.SellerHouseholdId) ||
                request.WorkerId is { } worker &&
                    !societyState.Agents.Any(person => person.Id == worker) ||
                !terminal && (building is null || building.HouseholdId != request.SellerHouseholdId ||
                    recipe is null || recipe.WorkstationBuildingId != building.DefinitionId || !ToolMakingRequestRules.IsToolRecipe(recipe) ||
                    !content.Buildings.Any(definition => definition.CanonicalId == building.DefinitionId &&
                        definition.Tags.Contains("blacksmith", StringComparer.Ordinal)) ||
                    !physical.ContainsKey(request.RequesterId) ||
                    !LifeStageRules.IsAdult(societyState.GetAgent(request.RequesterId).AgeBand)) ||
                request.Status == ToolMakingRequestStatus.Accepted &&
                    (!physical.ContainsKey(request.WorkerId!) || societyState.GetAgent(request.WorkerId!).HouseholdId != request.SellerHouseholdId ||
                     !LifeStageRules.IsAdult(societyState.GetAgent(request.WorkerId!).AgeBand)) ||
                recipe is not null && (!ToolMakingRequestRules.IsToolRecipe(recipe) || recipe.Outputs[0].ResourceId != request.ItemKind))
                throw new InvalidDataException("A tool request has invalid identity, authority, status or timing.");
            if (request.JobId is { } jobId)
            {
                var job = simulation.ProductionJobs.FirstOrDefault(job => job.JobId == jobId);
                if (job is null || job.ToolMakingRequestId != request.Id ||
                    // A housemate may resume paused work after the worker leaves; that request has already stopped.
                    !terminal && job.WorkerId != request.WorkerId || job.RecipeId != request.RecipeId ||
                    job.BuildingInstanceId != request.BuildingInstanceId || job.StartedTick < request.AcceptedTick.GetValueOrDefault() ||
                    !HasCompleteToolMakingInputs(job, recipe, societyState.Inventory) ||
                    job.InputReservationIds.Any(id => !societyState.Inventory.Reservations.Any(receipt => receipt.Id == id &&
                        receipt.OwnerId == request.SellerHouseholdId && (job.State != WorldProductionJobState.Completed ||
                            receipt.State == InventoryReservationState.Completed))) ||
                    request.Status is ToolMakingRequestStatus.Ready or ToolMakingRequestStatus.Offered or ToolMakingRequestStatus.Fulfilled &&
                        job.State != WorldProductionJobState.Completed ||
                    societyState.Inventory.Lots.Any(lot => lot.Id == ProductionOutputLotId(jobId, 0) && lot.ItemKind != request.ItemKind))
                    throw new InvalidDataException("A tool request is not bound to its actual ordinary production job.");
            }
            if (request.OfferId is { } offerId)
            {
                var offer = societyState.Inventory.Offers.FirstOrDefault(offer => offer.Id == offerId);
                var trade = trades.FirstOrDefault(trade => trade.OfferId == offerId);
                if (request.JobId is null || offer is null || trade is null ||
                    offer.FirstLotId != ProductionOutputLotId(request.JobId, 0) || offer.FirstQuantity != 1 ||
                    offer.FirstPartyId != request.SellerHouseholdId || offer.SecondPartyId != request.RequesterId ||
                    trade.BuildingInstanceId != request.BuildingInstanceId || trade.SellerHouseholdId != request.SellerHouseholdId ||
                    trade.BuyerId != request.RequesterId || trade.GoodsKind != request.ItemKind ||
                    offer.State == DirectBarterState.Settled && request.Status != ToolMakingRequestStatus.Fulfilled ||
                    request.Status == ToolMakingRequestStatus.Fulfilled && offer.State != DirectBarterState.Settled ||
                    request.Status == ToolMakingRequestStatus.Offered && offer.State != DirectBarterState.Open ||
                    ToolMakingRequestRules.IsTerminal(request.Status) && request.Status != ToolMakingRequestStatus.Fulfilled &&
                        offer.State != DirectBarterState.Cancelled)
                    throw new InvalidDataException("A tool request does not match its exact physical inventory exchange.");
            }
            else if (request.Status is ToolMakingRequestStatus.Offered or ToolMakingRequestStatus.Fulfilled)
                throw new InvalidDataException("A tool purchase must retain its authoritative inventory offer.");
            if (request.Status == ToolMakingRequestStatus.Accepted && request.JobId is null &&
                (!physical.TryGetValue(request.WorkerId!, out var person) ||
                    person.Project?.ToolMakingRequestId != request.Id || person.Project.StartedTick != request.AcceptedTick ||
                    person.Project.CandidateId != "build:recipe:" + request.RecipeId))
                throw new InvalidDataException("An accepted tool request has no actual assigned work plan.");
        }
        foreach (var person in physical.Values.Where(person => person.Project?.ToolMakingRequestId is not null))
            if (!requests.Any(request => request.Id == person.Project!.ToolMakingRequestId &&
                request.WorkerId == person.AgentId && request.AcceptedTick == person.Project.StartedTick &&
                request.Status == ToolMakingRequestStatus.Accepted && person.Project.CandidateId == "build:recipe:" + request.RecipeId))
                throw new InvalidDataException("A household project has an invalid tool request binding.");
        foreach (var job in simulation.ProductionJobs.Where(job => job.ToolMakingRequestId is not null))
            if (!requests.Any(request => request.Id == job.ToolMakingRequestId && request.JobId == job.JobId))
                throw new InvalidDataException("A production job has no matching tool request history.");
    }

    // Canonical content local names have no length cap. Keep the full identity
    // even in withdrawn history whose no-job recipe package has been rolled back.
    private static bool IsToolRequestRecipeId(string? id) => ProductionSystem.IsToolRequestRecipeId(id);

    private static bool HasCompleteToolMakingInputs(WorldProductionJob job, RecipeDefinition? recipe, InventoryCheckpoint inventory)
    {
        var purpose = job.JobId + ":input";
        var receipts = inventory.Reservations.Where(receipt => receipt.Purpose == purpose).ToArray();
        if (receipts.Length == 0 || job.InputReservationIds is not { Count: > 0 } ||
            !job.InputReservationIds.Order().SequenceEqual(receipts.Select(receipt => receipt.Id).Order()))
            return false;
        if (recipe is null) return true;
        var matched = 0;
        for (var index = 0; index < recipe.Inputs.Count; index++)
        {
            var input = recipe.Inputs[index];
            var group = receipts.Where(receipt => receipt.Id == DerivedId.Create(purpose + ":quantity:" +
                index.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":lot:" + receipt.LotId)).ToArray();
            if (group.Sum(receipt => (long)receipt.Quantity) != input.Amount || group.Any(receipt =>
                inventory.Lots.Any(lot => lot.Id == receipt.LotId && lot.ItemKind != input.ResourceId)))
                return false;
            matched += group.Length;
        }
        return matched == receipts.Length;
    }
}

using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private int AnimalDayTicks => AnimalSystem.AnimalDayTicks(animalWorldAdapter);
    private void StageAnimalContent() => AnimalSystem.StageAnimalContent(animalWorldAdapter);
    private static string AnimalKey(string value) => AnimalSystem.AnimalKey(value);
    private AnimalState? Animal(string id) => animalSystem.Animal(id);
    private void SetAnimal(AnimalState animal) => animalSystem.SetAnimal(animal);
    private PlacedBuilding? AnimalYard(string? household) => AnimalSystem.AnimalYard(animalWorldAdapter, household);
    private int AnimalYardCapacity(PlacedBuilding yard) => AnimalSystem.AnimalYardCapacity(animalWorldAdapter, yard);
    private bool HasAnimalSpace(string household, PlacedBuilding yard) => animalSystem.HasAnimalSpace(animalWorldAdapter, household, yard);
    private GridPoint? FreeAnimalYardTile(PlacedBuilding yard, string? animalId = null, GridPoint? near = null) => animalSystem.FreeAnimalYardTile(animalWorldAdapter, yard, animalId, near);
    private void SeedWildAnimals() => animalSystem.SeedWildAnimals(animalWorldAdapter);
    private void AdvanceAnimals(long tick) => animalSystem.AdvanceAnimals(animalWorldAdapter, tick);
    private void ReconcileAnimalCustody() => animalSystem.ReconcileAnimalCustody(animalWorldAdapter);
    private IEnumerable<AnimalSystem.AnimalChoice> AnimalChoices(string actor) => animalSystem.AnimalChoices(animalWorldAdapter, actor);
    private bool MayLeadAnimal(string actor, AnimalState animal) => animalSystem.MayLeadAnimal(animalWorldAdapter, actor, animal);
    private void AddAnimalCandidates(List<CognitionCandidate> candidates, string actor) => animalSystem.AddAnimalCandidates(animalWorldAdapter, candidates, actor);
    private bool ApplyAnimalCandidate(string actor, string candidateId, ActionPlan? plan = null) => animalSystem.ApplyAnimalCandidate(animalWorldAdapter, actor, candidateId, plan);
    private void AddMilkCandidate(List<CognitionCandidate> candidates, string actor) => animalSystem.AddMilkCandidate(animalWorldAdapter, candidates, actor);
    private List<(InventoryLot Lot, int Quantity)>? AnimalCareInputs(string actor, AnimalState animal, int feed, int water) => AnimalSystem.AnimalCareInputs(animalWorldAdapter, actor, animal, feed, water);
    private bool ApplyAnimalConsentDecision(SocietyCognitionDispatchResult decision)
    {
        if (!animalSystem.TryPlanAnimalConsentDecision(animalWorldAdapter, decision, out var plan)) return false;
        if (plan is not null)
            TryRegisteredAction(ActionPhase.FreshAdmission, new(plan.Actor, agents[plan.Actor], plan.CandidateId, false, plan));
        return true;
    }
    private static bool IsAnimalOrder(string action) => AnimalSystem.IsAnimalOrder(action);
    private OrderTask? ParseAnimalOrder(string text, string actor) => animalSystem.ParseAnimalOrder(animalWorldAdapter, text, actor);
    private CognitionCandidate? AnimalOrderCandidate(OwnerQueuedInstruction instruction) => animalSystem.AnimalOrderCandidate(animalWorldAdapter, instruction);
    private string AnimalOrderBlockedReason(OwnerQueuedInstruction instruction) => animalSystem.AnimalOrderBlockedReason(animalWorldAdapter, instruction);
    private void ExecuteAnimalOrder(OwnerQueuedInstruction instruction) => animalSystem.ExecuteAnimalOrder(animalWorldAdapter, instruction);
    private void CancelAnimalSupplyForOrder(OwnerQueuedInstruction instruction) => animalSystem.CancelAnimalSupplyForOrder(animalWorldAdapter, instruction);
    private bool MaySupplyAnimalYard(string actor, PlacedBuilding yard) => animalSystem.MaySupplyAnimalYard(animalWorldAdapter, actor, yard);
    private AnimalState? RidingAnimal(string actor) => animalSystem.RidingAnimal(animalWorldAdapter, actor);
    private int HorseCargoCapacity(string actor) => animalSystem.HorseCargoCapacity(animalWorldAdapter, actor);
    private void MoveAttachedAnimal(string actor, GridPoint from, GridPoint to) => animalSystem.MoveAttachedAnimal(animalWorldAdapter, actor, from, to);
    private AnimalState AdvanceWildAnimalCare(AnimalState animal, long tick) => animalSystem.AdvanceWildAnimalCare(animalWorldAdapter, animal, tick);
    internal static IOrderedEnumerable<MapResource> WildAnimalForageSources(WorldMap map, EcologyState ecology, AnimalState animal) => AnimalSystem.WildAnimalForageSources(map, ecology, animal);

    private static ActionDefinition<AnimalActionRequest, AnimalWorkActionPlan> AnimalWorkDefinition => AnimalActions.Work;
    private static ActionDefinition<AnimalActionRequest, AnimalProductActionPlan> AnimalProductDefinition => AnimalActions.Product;
    private static ActionDefinition<AnimalActionRequest, AnimalTravelActionPlan> AnimalTravelDefinition => AnimalActions.Travel;
    private static ActionDefinition<AnimalActionRequest, AnimalSupplyActionPlan> AnimalSupplyDefinition => AnimalActions.Supply;
    private static ActionDefinition<AnimalActionRequest, AnimalTripActionPlan> AnimalTripDefinition => AnimalActions.Trip;
    private static ActionDefinition<AnimalActionRequest, AnimalAgreementActionPlan> AnimalAgreementDefinition => AnimalActions.Agreement;

    private static class AnimalActions
    {
        private static ActionDefinition<AnimalActionRequest, TPlan> Define<TPlan>(AnimalActionFamily family,
            Func<WorldRuntime, TPlan, ActorCapabilityPolicy, ActionCheck> check,
            Func<WorldRuntime, TPlan, bool> apply, Func<TPlan, ActorCapabilityPolicy>? capabilities = null,
            Func<WorldRuntime, ActionContext, bool>? noPlan = null) where TPlan : ActionPlan => new(
                DecodeReferenceChoice(candidate => AnimalActionRequest.Decode(candidate) is { } request && request.Family == family ? request : null),
                static request => request.Encode(), AnimalSystem.ActionCapabilities,
                static (world, actor, request, _) => world.animalSystem.PlanAnimalRequest<TPlan>(world.animalWorldAdapter, actor, request),
                check, (world, plan, _, _) => apply(world, plan), PlanCapabilities: capabilities, NoPlanFallback: noPlan);

        internal static readonly ActionDefinition<AnimalActionRequest, AnimalWorkActionPlan> Work = Define<AnimalWorkActionPlan>(
            AnimalActionFamily.Work, static (world, plan, policy) => world.animalSystem.CheckAnimalWorkPlan(world.animalWorldAdapter, plan, policy),
            static (world, plan) => { world.animalSystem.ApplyCheckedAnimalWork(world.animalWorldAdapter, plan); return true; });

        internal static readonly ActionDefinition<AnimalActionRequest, AnimalProductActionPlan> Product = Define<AnimalProductActionPlan>(
            AnimalActionFamily.Product, static (world, plan, policy) => world.animalSystem.CheckAnimalProductPlan(world.animalWorldAdapter, plan, policy),
            static (world, plan) => { world.animalSystem.ApplyCheckedAnimalProduct(world.animalWorldAdapter, plan); return true; },
            capabilities: static plan => AnimalSystem.ProductCapabilities(plan.Choice.Kind));

        internal static readonly ActionDefinition<AnimalActionRequest, AnimalTravelActionPlan> Travel = Define<AnimalTravelActionPlan>(
            AnimalActionFamily.Travel, static (world, plan, policy) => world.animalSystem.CheckAnimalTravelPlan(world.animalWorldAdapter, plan, policy),
            static (world, plan) => { world.animalSystem.ApplyCheckedAnimalTravel(world.animalWorldAdapter, plan); return true; });

        internal static readonly ActionDefinition<AnimalActionRequest, AnimalSupplyActionPlan> Supply = Define<AnimalSupplyActionPlan>(
            AnimalActionFamily.Supply, static (world, plan, policy) => world.animalSystem.CheckAnimalSupplyPlan(world.animalWorldAdapter, plan, policy),
            static (world, plan) => { world.animalSystem.ApplyCheckedAnimalSupply(world.animalWorldAdapter, plan); return true; });

        internal static readonly ActionDefinition<AnimalActionRequest, AnimalTripActionPlan> Trip = Define<AnimalTripActionPlan>(
            AnimalActionFamily.Trip, static (world, plan, policy) => world.animalSystem.CheckAnimalTripPlan(world.animalWorldAdapter, plan, policy),
            static (world, plan) => { world.animalSystem.ApplyCheckedAnimalTrip(world.animalWorldAdapter, plan); return true; },
            noPlan: static (world, action) => world.animalSystem.FinishMissingAnimalTrip(world.animalWorldAdapter, action.AgentId));

        internal static readonly ActionDefinition<AnimalActionRequest, AnimalAgreementActionPlan> Agreement = Define<AnimalAgreementActionPlan>(
            AnimalActionFamily.Agreement, static (world, plan, policy) => world.animalSystem.CheckAnimalAgreementPlan(world.animalWorldAdapter, plan, policy),
            static (world, plan) => { world.animalSystem.CompleteAnimalAgreement(world.animalWorldAdapter, plan); return true; });
    }

    private sealed class AnimalWorldAdapter(WorldRuntime runtime) : IAnimalWorld
    {
        public AgentStore PhysicalAgents => runtime.agentStore;
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public ActorCapabilityState? CapabilitiesFor(string actor) => runtime.CapabilitiesFor(actor);
        public System.Collections.ObjectModel.ReadOnlyCollection<GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming) => runtime.Reach(actor, target, purpose, range, load, swimming);
        public void RefuseChoice(ActionPlan plan, ActionRefusal refusal) => runtime.RefuseChoice(plan, refusal);
        public void AppendEvent(WorldEventPayload payload, GridPoint? eventPosition = null) => runtime.AppendEvent(payload, eventPosition);
        public void RunInventoryCommand(InventorySystem.Command command) => runtime.RunInventoryCommand(command);
        public InventoryLot? AttachedHandcart(string actor) => TransportSystem.AttachedHandcart(runtime.HandcartWorld, actor);
        public int AvailableLotQuantity(InventoryLot lot) => runtime.AvailableLotQuantity(lot);
        public int AnimalYardFeedStock(string yardId) => runtime.AnimalYardFeedStock(yardId);
        public bool CanRemoveWorkstationStock(InventoryCheckpoint inventory, InventoryLot root, int quantity) => runtime.CanRemoveWorkstationStock(inventory, root, quantity);
        public void CreditOrderEffect(OwnerQueuedInstruction instruction, Identity effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public int DestinationRoom(string buildingId, InventoryLot? movingRoot = null) => runtime.DestinationRoom(buildingId, movingRoot);
        public GoodsAnswer FindGoods(GoodsRequest request) => runtime.FindGoods(request);
        public IReadOnlyList<GoodsMatch> DismountCargo(string actor) => runtime.DismountCargo(actor);
        public IReadOnlyList<GoodsMatch> AnimalPayments(string actor) => runtime.AnimalPayments(actor);
        public int FreeCarryCapacity(string actor) => runtime.FreeCarryCapacity(actor);
        public GridPoint[] FreshWaterShorePositions() => runtime.FreshWaterShorePositions();
        public long HoldingsTotal(IEnumerable<string> owners, GoodsKinds kinds) => runtime.HoldingsTotal(owners, kinds);
        public PlacedBuilding? HouseholdBuildingWithTag(string householdId, string tag) => runtime.HouseholdBuildingWithTag(householdId, tag);
        public string HouseholdFor(string actor) => runtime.HouseholdFor(actor);
        public GridPoint HouseholdStockPosition(InventoryLot lot) => runtime.HouseholdStockPosition(lot);
        public bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int interactionRange) => runtime.IsWithinInteractionRange(origin, destination, interactionRange);
        public void MoveToward(string agentId, AgentState state, GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100) => runtime.MoveToward(agentId, state, destination, reason, interactionRange, horseMovementBudget);
        public BoatState? PassengerBoat(string actor) => TransportSystem.PassengerBoat(runtime.BoatWorld, actor);
        public OwnerQueuedInstruction? PendingInstructionFor(string agentId) => runtime.PendingInstructionFor(agentId);
        public int PhysicalUnreservedQuantity(InventoryLot lot) => runtime.PhysicalUnreservedQuantity(lot);
        public int PickupCarryCapacity(string actor, ClankerWorld.Simulation.Kernel.InventoryLot stock, GridPoint destination, int destinationRange = 0) => runtime.PickupCarryCapacity(actor, stock, destination, destinationRange);
        public GoodsMatch? RecheckGoods(GoodsRequest request, string lotId) => runtime.RecheckGoods(request, lotId);
        public void SetOrderStatus(OwnerQueuedInstruction instruction, OrderStatus status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public List<GridPoint> SharedUnoccupiedRoute(GridPoint origin, HashSet<GridPoint> occupied, GridPoint destination, int interactionRange, bool swimming = false) => runtime.SharedUnoccupiedRoute(origin, occupied, destination, interactionRange, swimming);
        public void StageBuiltInContent(string packageId, string requiredActivePackageId, Func<ContentPackageManifest> create, string eventKind) => runtime.StageBuiltInContent(packageId, requiredActivePackageId, create, eventKind);
        public void SyncEcologyResourceStates() => runtime.SyncEcologyResourceStates();
        public bool VesselFits(InventoryLot lot, int room) => runtime.VesselFits(lot, room);
        public long WorldTick => runtime.WorldTick;
        public WorldMap Map => runtime.map;
        public WorldSystemsState Environment => runtime.worldSystems;
        public WorldContentSimulationState Simulation => runtime.worldSimulation;
        public DeclarativeWorldContentState Content => runtime.worldContent;
        public IReadOnlyDictionary<string, AgentState> Agents => runtime.agents;
        public string Seed => runtime.worldSeed;
        public GeographyOptions? Geography => runtime.geographyOptions;
        public long NextEventId => runtime.nextEventId;
        public GroundOccupancy Ground => runtime.Ground;
        public SocietyCheckpoint Society => runtime.society.Checkpoint;
        public IReadOnlyList<ContentPackageRecord> Packages => runtime.contentRegistry.ExportState().Packages;
        public GoodsKinds EdibleFoodGoods => WorldRuntime.EdibleFoodGoods;
        public void ReplaceEcologyResource(string id, EcologyResource resource) => runtime.environmentSystem.ReplaceEcologyResource(id, resource);
    }

    internal static class AnimalSharedRules
    {
        internal static bool ActiveParenthood(ParenthoodPlan? plan) => FamilySystem.ActiveParenthood(plan);
        internal static string AnimalActionId(string action, string animal, string? other) => WorldRuntime.AnimalActionId(action, animal, other);
        internal static string AnimalAgreementId(string action, string animal, string other, string? lot) => WorldRuntime.AnimalAgreementId(action, animal, other, lot);
        internal static string AnimalBornId(string mother, long startedTick) => WorldRuntime.AnimalBornId(mother, startedTick);
        internal static string AnimalCargoDismountId(string rider, long tick, long eventId, string lot) => WorldRuntime.AnimalCargoDismountId(rider, tick, eventId, lot);
        internal static string AnimalHerdId(string household) => WorldRuntime.AnimalHerdId(household);
        internal static string AnimalHideId(string animal) => WorldRuntime.AnimalHideId(animal);
        internal static string AnimalHideTransferId(string actor, string animal, long tick, bool carry) => WorldRuntime.AnimalHideTransferId(actor, animal, tick, carry);
        internal static string AnimalInputReservationId(string actor, long tick, long eventId, string purpose, int index) => WorldRuntime.AnimalInputReservationId(actor, tick, eventId, purpose, index);
        internal static string AnimalOfferId(string actor, string animal, long tick, long eventId) => WorldRuntime.AnimalOfferId(actor, animal, tick, eventId);
        internal static string AnimalOrderReceiptId(string instruction, string animal, string? completion) => WorldRuntime.AnimalOrderReceiptId(instruction, animal, completion);
        internal static string AnimalPickupId(string actor, long tick, long eventId) => WorldRuntime.AnimalPickupId(actor, tick, eventId);
        internal static string AnimalProductId(string animal, long tick) => WorldRuntime.AnimalProductId(animal, tick);
        internal static string AnimalProductReservationId(string product) => WorldRuntime.AnimalProductReservationId(product);
        internal static string AnimalSaddleOperationId(string animal, long tick, long eventId) => WorldRuntime.AnimalSaddleOperationId(animal, tick, eventId);
        internal static string AnimalSaddleReservationId(string operation) => WorldRuntime.AnimalSaddleReservationId(operation);
        internal static string AnimalSupplyId(string lot, string yard, string? animal, string? action) => WorldRuntime.AnimalSupplyId(lot, yard, animal, action);
        internal static string AnimalSupplyTripId(string actor) => WorldRuntime.AnimalSupplyTripId(actor);
        internal static bool HasActiveContainerReservation(InventoryCheckpoint inventory, string containerId) => WorldRuntime.HasActiveContainerReservation(inventory, containerId);
        internal static int HouseholdStockInteractionRange(InventoryLot lot) => WorldRuntime.HouseholdStockInteractionRange(lot);
        internal static bool IsEdibleFood(string kind) => WorldRuntime.IsEdibleFood(kind);
        internal const int ResourceInteractionRange = WorldRuntime.ResourceInteractionRange;
    }

    private static ActionRegistration RegisterAnimalActions() => new(ActionPhase.Ordinary, "ApplyAnimalCandidate", [], [], static (runtime, action) =>
    {
        return runtime.ApplyAnimalCandidate(action.AgentId, action.CandidateId, action.Plan);
    });
    private static ChoiceRegistration RegisterAnimalChoices() => new(ChoiceGroup.Prelude, "animals", static (runtime, context) => runtime.AddAnimalCandidates(context.Candidates, context.AgentId), static (runtime, context) => runtime.IsAdult(context.AgentId));

    private static void ValidateAnimalState(WorldRuntimeState state)
    {
        var world = state.AnimalWorld;
        AnimalSystem.ValidateLocalState(world);
        var society = state.Society.Society;
        var people = society.Agents.Select(person => person.Id).ToHashSet(StringComparer.Ordinal);
        void KnownAgents(IEnumerable<string> ids)
        {
            if (ids.Any(id => !people.Contains(id)))
                throw new InvalidDataException("Animal agent references must name unique, known people.");
        }
        KnownAgents(world.SupplyTrips.Select(trip => trip.ActorId));
        KnownAgents(world.Animals.Select(animal => animal.RiderId).OfType<string>());
        KnownAgents(world.Animals.Select(animal => animal.LeaderId).OfType<string>());
        var inventory = society.Inventory;
        var day = state.WorldSystems!.Config.TicksPerDay;
        var map = TravelMap(state);
        foreach (var animal in world.Animals)
        {
            var definition = AnimalRules.Definition(animal.Species);
            if (animal.BornTick > society.WorldTick || animal.BornTick < -(long)definition.AdultDays * day || !map.IsPassable(animal.Position) ||
                animal.CareUntilTick > society.WorldTick + day || animal.ProductProgressTicks >= Math.Max(1, definition.ProductDays * day) ||
                animal.DiedTick is { } death && death > society.WorldTick)
                throw new InvalidDataException("An animal has invalid age, care, product progress or physical state.");
            KnownAgents(animal.CarePermissions); KnownAgents(animal.RidingPermissions);
            if (animal.HouseholdId is { } home && (!society.Households.Any(household => household.Id == home) ||
                animal.DiedTick is null && !state.WorldSimulation!.Buildings.Any(building => building.InstanceId == animal.YardId &&
                    building.HouseholdId == home && state.WorldContent!.Buildings.Any(buildingDefinition => buildingDefinition.CanonicalId == building.DefinitionId &&
                        buildingDefinition.Tags.Contains(AnimalContent.YardTag)))))
                throw new InvalidDataException("An owned animal needs its household's actual animal yard.");
            if (animal.Pregnancy is { } pregnancy && (!AnimalRules.IsAdult(animal, society.WorldTick, day) ||
                pregnancy.StartedTick > society.WorldTick || pregnancy.ProgressTicks >= definition.GestationDays * day))
                throw new InvalidDataException("An animal pregnancy has invalid parents or progress.");
            if (animal.TamingWork is { } tame && !state.Agents.Any(person => person.AgentId == tame.ActorId))
                throw new InvalidDataException("An animal taming task has invalid work or actor.");
            if (animal.LeadDestination is { } destination && !map.IsPassable(destination))
                throw new InvalidDataException("An animal cannot be ridden and led together and needs a legal lead destination.");
            foreach (var actor in new[] { animal.RiderId, animal.LeaderId }.OfType<string>())
                if (!state.Agents.Any(person => person.AgentId == actor && person.Position == animal.Position) ||
                    state.HandcartHitches!.Any(hitch => hitch.PullerId == actor) ||
                    state.BoatTransport.Boats.Any(boat => boat.Journey?.PassengerId == actor) ||
                    world.Animals.Any(other => other.Id != animal.Id && (other.RiderId == actor || other.LeaderId == actor)))
                    throw new InvalidDataException("Animal riders and leaders must share the animal's physical position without another attachment.");
            if (animal.RiderId is not null && !AnimalRules.IsAdult(animal, society.WorldTick, day))
                throw new InvalidDataException("A ridden adult horse needs a fitted saddle.");
            if (animal.ReadyProductLotId is { } productId &&
                (!inventory.Lots.Any(lot => lot.Id == productId && lot.OwnerId == animal.HouseholdId &&
                    lot.ItemKind == definition.Product && lot.Quantity == definition.ProductQuantity &&
                    lot.GroundPosition == new InventoryGroundPosition(animal.Position.X, animal.Position.Y)) ||
                 !inventory.Reservations.Any(held => held.Id == animal.ReadyProductReservationId && held.LotId == productId &&
                    held.OwnerId == animal.HouseholdId && held.Quantity == definition.ProductQuantity && held.IsExclusive &&
                    held.State == InventoryReservationState.Reserved && held.Purpose == "animal-product:" + animal.Id)))
                throw new InvalidDataException("A ready animal product requires its real, held household lot.");
            if (animal.SaddleLotId is { } saddleId &&
                (!inventory.Lots.Any(lot => lot.Id == saddleId && lot.ItemKind == "saddle" && lot.OwnerId == animal.HouseholdId && lot.Quantity == 1 &&
                    lot.GroundPosition == new InventoryGroundPosition(animal.Position.X, animal.Position.Y)) ||
                 !inventory.Reservations.Any(held => held.Id == animal.SaddleReservationId && held.LotId == saddleId && held.OwnerId == animal.HouseholdId &&
                    held.Quantity == 1 && held.IsExclusive && held.State == InventoryReservationState.Reserved && held.Purpose == "animal-saddle:" + animal.Id)))
                throw new InvalidDataException("A fitted saddle requires its real, held household lot.");
        }
        foreach (var household in society.Households)
        {
            int Used(string? yard = null) => world.Animals.Where(animal => animal.DiedTick is null && animal.HouseholdId == household.Id &&
                (yard is null || animal.YardId == yard)).Sum(animal => 1 + (animal.Pregnancy is null ? 0 : 1)) +
                world.Offers.Where(offer => offer.ReceivingHouseholdId == household.Id && (yard is null || offer.ReceivingYardId == yard))
                    .Sum(offer => 1 + (world.Animals.FirstOrDefault(animal => animal.Id == offer.AnimalId)?.Pregnancy is null ? 0 : 1));
            if (Used() > AnimalRules.PopulationCap) throw new InvalidDataException("A household has more than eight animals and reserved offspring places.");
            foreach (var yard in state.WorldSimulation!.Buildings.Where(building => building.HouseholdId == household.Id &&
                         state.WorldContent!.Buildings.Any(definition => definition.CanonicalId == building.DefinitionId && definition.Tags.Contains(AnimalContent.YardTag))))
            {
                var definition = BuildingStorageRules.EffectiveDefinition(state.WorldContent!.Buildings.Single(definition => definition.CanonicalId == yard.DefinitionId), yard);
                if (Used(yard.InstanceId) > Math.Min(8, definition.Width * definition.Height))
                    throw new InvalidDataException("The animal yard has no room for its animals and reserved offspring places.");
            }
        }
        foreach (var offer in world.Offers)
            if (!world.Animals.Any(animal => animal.Id == offer.AnimalId && animal.DiedTick is null && animal.HouseholdId != offer.ReceivingHouseholdId) ||
                !society.Agents.Any(person => person.Id == offer.SellerId) || !society.Agents.Any(person => person.Id == offer.BuyerId &&
                    person.HouseholdId == offer.ReceivingHouseholdId) || !state.WorldSimulation!.Buildings.Any(yard =>
                    yard.InstanceId == offer.ReceivingYardId && yard.HouseholdId == offer.ReceivingHouseholdId) ||
                offer.OfferedTick > society.WorldTick)
                throw new InvalidDataException("An animal trade offer has invalid parties, receiving space or exact payment.");
        foreach (var trip in world.SupplyTrips)
            if (!state.Agents.Any(person => person.AgentId == trip.ActorId) ||
                !inventory.Lots.Any(lot => lot.Id == trip.LotId && PersonalEquipmentRules.IsCarried(lot, trip.ActorId)) ||
                !state.WorldSimulation!.Buildings.Any(yard => yard.InstanceId == trip.YardId && (yard.HouseholdId ==
                    society.Agents.Single(person => person.Id == trip.ActorId).HouseholdId || world.Animals.Any(animal =>
                        animal.YardId == yard.InstanceId && animal.CarePermissions.Contains(trip.ActorId)))) ||
                trip.AnimalId is not null && !world.Animals.Any(animal => animal.Id == trip.AnimalId))
                throw new InvalidDataException("An animal supply trip needs its real carried goods and household yard.");
        if ((state.Instructions ?? []).Any(instruction => instruction.Task?.TargetAnimalId is { } id &&
                !world.Animals.Any(animal => animal.Id == id)))
            throw new InvalidDataException("An animal order must retain its exact known animal identity.");
    }
}

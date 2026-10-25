using System.Text.Json;
using DeliveryOrderPlan = ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan;
using DeliveryNativeEffect = ClankerWorld.Simulation.World.OrderSystem.DeliveryNativeEffect;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static bool ExpansionJobMatchesOrder(BuildingExpansionJob job, OwnerQueuedInstruction instruction) =>
        instruction.Task is { Action: "expand_building", ExpansionBinding: { } binding } &&
        job.OrderInstructionId == instruction.InstructionId && job.WorkerId == instruction.TargetAgentId &&
        job.BuildingInstanceId == binding.BuildingInstanceId && job.DefinitionId == binding.DefinitionId &&
        job.OwnerId == binding.OwnerId && job.ExpectedPosition == binding.ExpectedPosition &&
        job.ExpectedRevision == binding.ExpectedRevision && job.TargetPosition == binding.TargetPosition &&
        job.TargetFootprint == binding.TargetFootprint && job.StartedTick >= instruction.SubmittedTick;

    private static string ExpansionOrderReceipt(string jobId) =>
        DerivedId.Hash(jobId, "expand:job:");

    private static void ValidateExpansionOrderBindings(WorldContentSimulationState? simulation,
        DeclarativeWorldContentState content, SocietyCheckpoint society, IReadOnlyList<TownRuntimeState>? savedTowns,
        IEnumerable<OwnerQueuedInstruction> instructions, long worldTick)
    {
        var expansionInstructions = instructions.Where(item => item.Task?.Action == "expand_building").ToArray();
        if (expansionInstructions.Select(item => item.InstructionId).Distinct(StringComparer.Ordinal).Count() != expansionInstructions.Length)
            throw new InvalidDataException("Expansion order identities are not unique.");
        var orders = expansionInstructions.ToDictionary(item => item.InstructionId, StringComparer.Ordinal);
        var jobs = simulation?.BuildingExpansions ?? [];
        foreach (var job in jobs.Where(item => item.OrderInstructionId is not null))
        {
            if (!orders.TryGetValue(job.OrderInstructionId!, out var instruction) || !ExpansionJobMatchesOrder(job, instruction) ||
                job.State is WorldProductionJobState.Running or WorldProductionJobState.Paused &&
                    (!IsActiveOrder(instruction.Task!.Status) || instruction.Task.Status == "queued" ||
                     instruction.Task.ExpansionBinding!.JobId != job.JobId) ||
                job.State == WorldProductionJobState.Completed && job.CompletionTick > worldTick)
                throw new InvalidDataException("An expansion job does not belong to its recorded owner order.");
        }
        foreach (var instruction in orders.Values)
        {
            var order = instruction.Task!;
            if (order.ExpansionBinding is { } binding)
            {
                var entry = WorldBuildingOrderCatalog.Find(content, binding.DefinitionId);
                if (simulation is null || !ValidExpansionBindingId(binding.BuildingInstanceId) || !ValidExpansionBindingId(binding.OwnerId) ||
                    entry is null || entry.BuildingKind != order.TargetBuildingKind ||
                    !(order.TargetBuildingKind is "house" or "animal-yard" ? society.Households.Any(home => home.Id == binding.OwnerId) :
                        savedTowns?.Any(town => town.Id == binding.OwnerId) == true) ||
                    !ValidExpansionBindingPosition(binding.ExpectedPosition) || !ValidExpansionBindingPosition(binding.TargetPosition) ||
                    order.TargetPosition is { } requested && requested != binding.ExpectedPosition ||
                    binding.TargetFootprint is null || !BuildingStorageRules.IsSupported(entry.Definition, binding.TargetFootprint) ||
                    binding.ExpectedRevision != binding.TargetFootprint.Revision - 1 ||
                    !PlausibleExpansionAnchors(binding) ||
                    binding.JobId is { } id && (!ValidExpansionBindingId(id) ||
                        !jobs.Any(job => job.JobId == id && ExpansionJobMatchesOrder(job, instruction))))
                    throw new InvalidDataException("An owner expansion binding is malformed or references unrelated work.");
            }
            var completed = jobs.Where(job => job.OrderInstructionId == instruction.InstructionId &&
                job.State == WorldProductionJobState.Completed).ToArray();
            if (completed.Length > 1 || order.CompletedUnits != completed.Length ||
                (completed.Length == 0 ? order.LastEffectId is not null :
                    order.LastEffectId != ExpansionOrderReceipt(completed[0].JobId) ||
                    order.ExpansionBinding?.JobId != completed[0].JobId))
                throw new InvalidDataException("Expansion order progress has no matching completed expansion.");
        }
    }

    private static bool ValidExpansionBindingId(string value) => !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 512 && value == value.Trim() && !value.Any(char.IsControl);

    private static bool ValidExpansionBindingPosition(GridPoint position) =>
        position.X is >= -10_000_000 and <= 10_000_000 && position.Y is >= -10_000_000 and <= 10_000_000;

    private static bool PlausibleExpansionAnchors(OwnerBuildingExpansionBinding binding)
    {
        var dx = binding.ExpectedPosition.X - binding.TargetPosition.X;
        var dy = binding.ExpectedPosition.Y - binding.TargetPosition.Y;
        // Yards add two rows or columns; House/Warehouse stages add one.
        var shift = (binding.TargetFootprint.Width, binding.TargetFootprint.Height) is (2, 4) or (4, 2) ? 2 : 1;
        return dx >= 0 && dy >= 0 && dx <= shift && dy <= shift && (dx == 0 || dy == 0) &&
            (binding.TargetFootprint.Width > 1 || dx == 0) && (binding.TargetFootprint.Height > 1 || dy == 0) &&
            (binding.TargetFootprint.Width != 2 || binding.TargetFootprint.Height != 3 || dx == 0) &&
            (binding.TargetFootprint.Width != 3 || binding.TargetFootprint.Height != 2 || dy == 0);
    }

    // Unpaid ordinary work may yield. Keep bound work and live paid jobs intact;
    // completed/cancelled jobs already retain their own payment and output history.
    private bool OrdinaryProjectCanYieldToOrder(AgentProject project) =>
        OrderSystem.OrdinaryProjectCanYieldToOrder(OrderSelectionWorld, project);

    private CognitionCandidate? OrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.OrderCandidateFor(OrderSelectionWorld, instruction, person);

    private static string FoodArrivalReceipt(string resourceId, GridPoint position) =>
        DerivedId.Create($"arrival:{resourceId}:{position.X}:{position.Y}");

    private bool MatchesFoodArrivalReceipt(string? receipt, MapResource resource) =>
        OrderSystem.MatchesFoodArrivalReceipt(OrderSelectionWorld, receipt, resource);

    private MapResource? KnownFoodSourceForOrder(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.KnownFoodSourceForOrder(OrderSelectionWorld, instruction, person);

    private bool KnownTargetIsUnavailable(OwnerQueuedInstruction instruction) =>
        OrderSystem.KnownTargetIsUnavailable(OrderSelectionWorld, instruction);

    private bool CanExploreForOrder(AgentState person) =>
        OrderSystem.CanExploreForOrder(OrderSelectionWorld, person);

    private bool ShouldInterruptOrder(
        AgentState person,
        OwnerQueuedInstruction instruction,
        CognitionCandidate? orderCandidate,
        CognitionCandidate? urgentCandidate) =>
        OrderSystem.ShouldInterruptOrder(OrderSurvivalWorld, person, instruction, orderCandidate, urgentCandidate);

    private CognitionCandidate? SelectOrderSurvivalCandidate(
        IEnumerable<CognitionCandidate> candidates, AgentState person, OwnerQueuedInstruction? instruction) =>
        OrderSystem.SelectOrderSurvivalCandidate(OrderSurvivalWorld, candidates, person, instruction);

    private bool IsSurvivalCandidate(string actor, string candidateId) =>
        OrderSystem.IsSurvivalCandidate(OrderSurvivalWorld, actor, candidateId);

    private void CreditOrderEffect(OwnerQueuedInstruction instruction, Identity effectId, int units) =>
        OrderSystem.CreditOrderEffect(OrderLifecycleWorld, instruction, effectId, units);

    private void SetOrderStatus(
        OwnerQueuedInstruction instruction,
        OrderStatus status,
        string? reason,
        bool waitForDecision = false) =>
        OrderSystem.SetOrderStatus(OrderLifecycleWorld, instruction, status, reason, waitForDecision);

    private string OrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.OrderBlockedReason(OrderBlockersWorld, instruction, person);

    internal static string MovementOrderEffectId(GridPoint destination) =>
        FormattableString.Invariant($"arrival:tile:{destination.X}:{destination.Y}");

    private bool MovementOrderNeedsHouseInvitation(string actor, GridPoint destination) =>
        OrderSystem.MovementOrderNeedsHouseInvitation(OrderBlockersWorld, actor, destination);

    private CognitionCandidate? CollectionOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.CollectionOrderCandidateFor(CollectionOrdersWorld, instruction, person);

    private void ExecuteCollectionOrderStep(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.ExecuteCollectionOrderStep(CollectionOrdersWorld, instruction, person);

    private static string CollectionOrderGoodsName(OrderTask order) =>
        order.Action switch
        {
            "collect_food" => "food",
            "collect_equipment" => "equipment",
            "collect_goods" => "goods",
            _ => "material",
        };

    private string CollectionOrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.CollectionOrderBlockedReason(CollectionOrdersWorld, instruction, person);

    private void InterruptOrdinaryFieldWorkForCustody(string actor, string lotId) =>
        OrderSystem.InterruptOrdinaryFieldWorkForCustody(CustodyOrdersWorld, actor, lotId);

    private CognitionCandidate? ReturnOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.ReturnOrderCandidateFor(CustodyOrdersWorld, instruction, person);

    private void ExecuteReturnOrderStep(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.ExecuteReturnOrderStep(CustodyOrdersWorld, instruction, person);

    private string ReturnOrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.ReturnOrderBlockedReason(CustodyOrdersWorld, instruction, person);

    private static void ValidateCustodyOrderBindings(SocietyCheckpoint society,
        IEnumerable<OwnerQueuedInstruction> instructions)
    {
        // Removal, reassignment and expansion are legitimate world changes. They
        // block the saved destination at execution time rather than corrupting it.
        foreach (var instruction in instructions)
            if (instruction.Task is
                {
                    Action: "store_material" or "store_equipment" or "store_goods" or "return_borrowed",
                    TargetStorageOwnerId: { } owner
                } &&
                !society.Households.Any(household => household.Id == owner))
                throw new InvalidDataException("A custody order references an unknown owning household.");
    }

    private int DeliveryResourceQuantity(InventoryLot carrier, string kind) =>
        OrderSystem.DeliveryResourceQuantity(DeliveryOrderSelectionWorld, carrier, kind);

    private bool DeliveryDestinationMatches(OrderTask order, PlacedBuilding building) =>
        OrderSystem.DeliveryDestinationMatches(DeliveryOrderSelectionWorld, order, building);

    private static int DeliveryOrderRemaining(OrderTask order) =>
        order.QuantityIsExplicit && !order.RepeatUntilCancelled ? order.RequestedUnits - order.CompletedUnits : int.MaxValue;

    private string? DeliveryRouteForCarried(OrderTask order, PlacedBuilding destination) =>
        OrderSystem.DeliveryRouteForCarried(DeliveryOrderExecutionWorld, order, destination);

    private DeliveryOrderPlan? DeliveryPlanFor(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.DeliveryPlanFor(DeliveryOrderSelectionWorld, instruction, person);

    private (PlacedBuilding Building, string Route, MapResource Source)? DeliveryGatherFor(
        OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.DeliveryGatherFor(DeliveryOrderSelectionWorld, instruction, person);

    private CognitionCandidate? DeliveryOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.DeliveryOrderCandidateFor(DeliveryOrderExecutionWorld, instruction, person);

    private OwnerQueuedInstruction BindDeliveryOrder(OwnerQueuedInstruction instruction, PlacedBuilding destination,
        string ownerId, string route, string? lotId = null, int? quantity = null) =>
        OrderSystem.BindDeliveryOrder(DeliveryOrderExecutionWorld, instruction, destination, ownerId, route, lotId, quantity);

    private DeliveryNativeEffect? ApplyDeliveryOrderPlan(string actor, AgentState person,
        DeliveryOrderPlan plan) =>
        OrderSystem.ApplyDeliveryOrderPlan(DeliveryOrderExecutionWorld, actor, person, plan);

    private void ExecuteDeliveryOrderStep(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.ExecuteDeliveryOrderStep(DeliveryOrderExecutionWorld, instruction, person);

    private string DeliveryOrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.DeliveryOrderBlockedReason(DeliveryOrderExecutionWorld, instruction, person);

    private static void ValidateDeliveryOrderBindings(WorldContentSimulationState? simulation, SocietyCheckpoint society,
        IReadOnlyList<TownRuntimeState>? savedTowns, IEnumerable<OwnerQueuedInstruction> instructions)
    {
        foreach (var instruction in instructions.Where(instruction => instruction.Task?.Action == "deliver_stock"))
        {
            var order = instruction.Task!;
            if (order.TargetStorageOwnerId is { } owner &&
                !(order.DeliveryPurpose == "town_surplus" ? savedTowns?.Any(town => town.Id == owner) == true :
                    society.Households.Any(household => household.Id == owner)))
                throw new InvalidDataException("A delivery order references an unknown destination owner.");
            // A removed building or changed physical shipment is a loadable blocker,
            // including after departure or death. Never substitute a new destination.
            if (simulation is null && order.TargetStorageBuildingId is not null)
                throw new InvalidDataException("A delivery order requires a saved settlement.");
        }
    }

    private static FarmWorkKind FieldOrderKind(string action) => action switch
    {
        "till_field" => FarmWorkKind.Till,
        "plant_field" => FarmWorkKind.Plant,
        "tend_field" => FarmWorkKind.Tend,
        "harvest_field" => FarmWorkKind.Harvest,
        _ => throw new ArgumentOutOfRangeException(nameof(action)),
    };

    private GridPoint? FieldOrderSite(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.FieldOrderSite(FieldOrdersWorld, instruction, person);

    private CognitionCandidate? FieldOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.FieldOrderCandidateFor(FieldOrdersWorld, instruction, person);

    private void ExecuteFieldOrderStep(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.ExecuteFieldOrderStep(FieldOrdersWorld, instruction, person);

    private string FieldOrderBlockedReason(OwnerQueuedInstruction instruction) =>
        OrderSystem.FieldOrderBlockedReason(FieldOrdersWorld, instruction);

    private static void ValidateFieldOrderBindings(IEnumerable<FarmFieldState> savedFields,
        IEnumerable<OwnerQueuedInstruction> instructions)
    {
        foreach (var field in savedFields)
        {
            if (field.Work is not { OrderInstructionId: { } orderId } work) continue;
            var instruction = FirstActiveOrderFor(instructions, work.WorkerId);
            if (instruction is null || instruction.InstructionId != orderId || instruction.Task is not { } task ||
                !OrderSystem.IsFieldOrder(task.Action) || task.Status == "queued" || FieldOrderKind(task.Action) != work.Kind ||
                work.LastWorkedTick < instruction.SubmittedTick ||
                task.TargetPosition is { } target && target != field.Position ||
                task.TargetCropKind is { } crop && crop != (work.Kind == FarmWorkKind.Plant ? work.Crop : field.Crop))
                throw new InvalidDataException("The saved field work does not belong to its active order.");
        }
    }

    private static bool IsProductionOrderProject(OwnerQueuedInstruction instruction, AgentProject? project) =>
        instruction.Task is { Action: "produce_item", ProductionProjectStartedTick: { } started } order &&
        project is not null && project.StartedTick == started &&
        project.CandidateId == "build:recipe:" + order.TargetRecipeId &&
        (order.ProductionJobId is null || project.JobId == order.ProductionJobId);

    // Called before the inventory clock advances: pause and hold inputs before
    // expiry or a due completion can outrun an already urgent survival need.
    private void MaintainProductionOrdersBeforeTick() =>
        OrderSystem.MaintainProductionOrdersBeforeTick(ProductionOrdersWorld);

    private void PauseProductionOrderJob(OwnerQueuedInstruction instruction, WorldProductionJob job) =>
        OrderSystem.PauseProductionOrderJob(ProductionOrdersWorld, instruction, job);

    private void CancelProductionForOrder(OwnerQueuedInstruction instruction) =>
        OrderSystem.CancelProductionForOrder(ProductionOrdersWorld, instruction);

    private static string ProductionOrderReceipt(string jobId) =>
        DerivedId.Hash(jobId, "produce:job:");

    private static void ValidateProductionOrderBindings(WorldContentSimulationState simulation,
        DeclarativeWorldContentState content, IEnumerable<AgentState> physical,
        IEnumerable<OwnerQueuedInstruction> instructions)
    {
        var productionInstructions = instructions.Where(instruction => instruction.Task?.Action == "produce_item").ToArray();
        if (productionInstructions.Select(instruction => instruction.InstructionId).Distinct(StringComparer.Ordinal).Count() !=
            productionInstructions.Length)
            throw new InvalidDataException("Production order identities are not unique.");
        var orders = productionInstructions.ToDictionary(instruction => instruction.InstructionId, StringComparer.Ordinal);
        var people = physical.ToDictionary(person => person.AgentId, StringComparer.Ordinal);
        var jobs = simulation.ProductionJobs.ToDictionary(job => job.JobId, StringComparer.Ordinal);
        foreach (var job in simulation.ProductionJobs.Where(job => job.OrderInstructionId is not null))
        {
            if (!orders.TryGetValue(job.OrderInstructionId!, out var instruction) ||
                job.WorkerId != instruction.TargetAgentId || job.RecipeId != instruction.Task!.TargetRecipeId ||
                job.BuildingInstanceId != instruction.Task.ProductionBuildingId || job.StartedTick < instruction.SubmittedTick)
                throw new InvalidDataException("A production job does not belong to its recorded order.");
            if (job.State is WorldProductionJobState.Running or WorldProductionJobState.Paused &&
                (!IsActiveOrder(instruction.Task.Status) || instruction.Task.ProductionJobId != job.JobId ||
                 !people.TryGetValue(job.WorkerId, out var person) || !IsProductionOrderProject(instruction, person.Project) ||
                 person.Project!.WorkDone != global::ClankerWorld.Simulation.World.ProductionSystem.ProjectWorkTicks || person.Project.Stage is not ("waiting" or "paused") ||
                 person.Project.RequiresFreshChoice))
                throw new InvalidDataException("Active ordered production has no matching worker project and order.");
        }
        foreach (var instruction in orders.Values)
        {
            var order = instruction.Task!;
            var recipe = WorldProductionOrderCatalog.Find(content, order.TargetRecipeId) ??
                throw new InvalidDataException("A production order references an unsupported recipe.");
            if (order.ProductionProjectStartedTick is { } started && started < instruction.SubmittedTick)
                throw new InvalidDataException("An ordered production project predates its instruction.");
            if (order.ProductionBuildingId is { } buildingId &&
                simulation.Buildings.FirstOrDefault(building => building.InstanceId == buildingId) is { } building &&
                building.DefinitionId != recipe.Recipe.WorkstationBuildingId)
                throw new InvalidDataException("An order is bound to a different recipe workstation.");
            if (order.ProductionJobId is { } jobId &&
                (!jobs.TryGetValue(jobId, out var bound) || bound.OrderInstructionId != instruction.InstructionId ||
                 bound.StartedTick < order.ProductionProjectStartedTick))
                throw new InvalidDataException("An order is bound to an unrelated production job.");
            var completed = simulation.ProductionJobs.Where(job => job.OrderInstructionId == instruction.InstructionId &&
                job.State == WorldProductionJobState.Completed).ToArray();
            var measuredUnits = Math.Min(1_000_000L, (long)completed.Length *
                (order.ProgressUnit == "production_batches" ? 1 : recipe.OutputQuantity));
            var latest = completed.OrderBy(job => job.CompletionTick).ThenBy(job => job.JobId, StringComparer.Ordinal).LastOrDefault();
            if (order.CompletedUnits != measuredUnits || order.CompletedUnits > 0 &&
                ProductionOrderReceipt(latest!.JobId) != order.LastEffectId)
                throw new InvalidDataException("Production order progress has no matching completed jobs.");
        }
    }

    private CognitionCandidate? RepairOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.RepairOrderCandidateFor(RepairOrdersWorld, instruction, person);

    private void ExecuteRepairOrderStep(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.ExecuteRepairOrderStep(RepairOrdersWorld, instruction, person);

    private string RepairOrderBlockedReason(OwnerQueuedInstruction instruction) =>
        OrderSystem.RepairOrderBlockedReason(RepairOrdersWorld, instruction);

    private static void ValidateRepairOrderBindings(IEnumerable<AgentState> people,
        InventoryCheckpoint inventory, IEnumerable<OwnerQueuedInstruction> instructions)
    {
        foreach (var person in people)
        {
            if (person.Equipment?.Repair is not { OrderInstructionId: { } orderId } repair) continue;
            var instruction = FirstActiveOrderFor(instructions, person.AgentId);
            var target = inventory.Lots.FirstOrDefault(lot => lot.Id == repair.LotId);
            if (target?.OwnerId != person.AgentId || instruction is null || instruction.InstructionId != orderId || instruction.Task is not { Action: "repair_equipment" } task ||
                task.Status == "queued" || repair.StartedTick < instruction.SubmittedTick ||
                target.ItemKind != task.TargetEquipmentKind)
                throw new InvalidDataException("The saved equipment repair does not belong to its active order.");
        }
    }

    private static bool IsShelterOrder(string action) => OrderSystem.IsShelterOrder(action);

    private CognitionCandidate? ShelterOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.ShelterOrderCandidateFor(ShelterOrdersWorld, instruction, person);

    private void ExecuteShelterOrderStep(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.ExecuteShelterOrderStep(ShelterOrdersWorld, instruction, person);

    private string ShelterOrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.ShelterOrderBlockedReason(ShelterOrdersWorld, instruction, person);

    private static string ShelterOrderReceipt(string instructionId, OrderTask order) =>
        DerivedId.Hash(JsonSerializer.Serialize(
            new { instructionId, order.Action, order.ShelterBinding, order.ShelterCompletion }),
            order.Action == "tend_fire" ? "shelter:fire:" : "shelter:arrival:");

    private static void ValidateShelterOrderBindings(
        WorldContentSimulationState? simulation, DeclarativeWorldContentState content, SocietyCheckpoint society,
        IEnumerable<OwnerQueuedInstruction> instructions, long worldTick)
    {
        var fuelReceipts = new HashSet<string>(StringComparer.Ordinal);
        var householdIds = society.Households.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var instruction in instructions.Where(item => item.Task is { } order && IsShelterOrder(order.Action)))
        {
            var order = instruction.Task!;
            if (!IsValidShelterOrderShape(order, instruction, worldTick))
                throw new InvalidDataException("A shelter order has an invalid saved shape.");
            if (order.ShelterBinding is { } binding)
            {
                if (binding.Kind is not ("natural" or "building") || binding.Position.X < 0 || binding.Position.Y < 0 ||
                    order.TargetPosition is { } requested && requested != binding.Position)
                    throw new InvalidDataException("A shelter order has an invalid destination binding.");
                if (binding.Kind == "natural")
                {
                    if (order.Action != "seek_shelter" || order.TargetBuildingKind is not null || binding.BuildingInstanceId is not null ||
                        binding.DefinitionId is not null || binding.OwnerId is not null || binding.BuildingPosition is not null || binding.BuildingPlacedTick is not null)
                        throw new InvalidDataException("Natural cover cannot carry a building or fire binding.");
                }
                else
                {
                    var definition = content.Buildings.FirstOrDefault(item => item.CanonicalId == binding.DefinitionId);
                    if (simulation is null || !IdentityRules.Reference(binding.BuildingInstanceId, int.MaxValue, requireTrimmed: true) || definition is null ||
                        binding.BuildingPosition is not { X: >= 0, Y: >= 0 } || binding.BuildingPlacedTick is not { } placed || placed < 0 || placed > worldTick ||
                        binding.OwnerId is { } owner && !householdIds.Contains(owner) ||
                        order.TargetBuildingKind == "house" && (!definition.Tags.Contains("house", StringComparer.Ordinal) || binding.OwnerId is null) ||
                        !(order.Action == "seek_shelter" ? definition.Tags.Any(tag => tag is "shelter" or TownHallContent.HallTag) :
                            definition.Tags.Any(tag => tag is "cooking" or "warmth")))
                        throw new InvalidDataException("A shelter order has an invalid building binding.");
                }
            }
            if (order.ShelterCompletion is not { } completion)
            {
                if (order.CompletedUnits != 0 || order.LastEffectId is not null)
                    throw new InvalidDataException("Shelter progress requires an actual completion receipt.");
                continue;
            }
            if (order.ShelterBinding is not { } completedBinding || order.Status != "finished" || order.CompletedUnits != 1 ||
                completion.WorldTick < instruction.SubmittedTick || completion.WorldTick > worldTick ||
                completedBinding.BuildingPlacedTick > completion.WorldTick || completion.Position != completedBinding.Position ||
                order.LastEffectId != ShelterOrderReceipt(instruction.InstructionId, order))
                throw new InvalidDataException("A shelter completion does not match its bound owner order.");
            if (order.Action == "seek_shelter")
            {
                if (completion.FuelReservationId is not null)
                    throw new InvalidDataException("A shelter arrival cannot claim a fire payment.");
                continue;
            }
            var reservation = society.Inventory.Reservations.FirstOrDefault(item => item.Id == completion.FuelReservationId);
            if (reservation is null || !fuelReceipts.Add(reservation.Id) || reservation.OwnerId != instruction.TargetAgentId ||
                reservation.Quantity != 1 || reservation.Purpose != "heating_fuel" || reservation.State != InventoryReservationState.Completed ||
                reservation.ExpiryTick != completion.WorldTick ||
                society.Inventory.Lots.FirstOrDefault(item => item.Id == reservation.LotId) is { ItemKind: not "wood" })
                throw new InvalidDataException("A fire order lacks its unique completed wood payment.");
        }
    }

    private CognitionCandidate? StorageOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.StorageOrderCandidateFor(StorageOrdersWorld, instruction, person);

    private void ExecuteStorageOrderStep(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.ExecuteStorageOrderStep(StorageOrdersWorld, instruction, person);

    private string StorageOrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.StorageOrderBlockedReason(StorageOrdersWorld, instruction, person);

    private static string? StorageOrderItemKind(OrderTask order) =>
        order.TargetItemKind ?? order.TargetEquipmentKind ?? order.TargetMaterialKind;

    private static string StorageOrderSubject(OrderTask order) =>
        order.Action == "store_equipment" ? "equipment" : order.Action == "store_goods" ? "goods" : "materials";

    private bool MayFollowOwnerOrder(string actor, OrderTask order) =>
        OrderSystem.MayFollowOwnerOrder(OrderCapabilitiesWorld, actor, order);

    private void PruneClosedOrderHistory()
    {
        var retiredInstructions = orderStore.PruneClosedOrders();
        if (retiredInstructions.Length != 0)
        {
            var retired = retiredInstructions.Select(item => item.InstructionId).ToHashSet(StringComparer.Ordinal);

            // Retire order-only links and construction evidence. Consumed
            // reservations, native work outcomes and physical state stay intact.
            productionSystem.RetireOrderLinks(retired);
            constructionSystem.Buildings.RetireOrderLinks(retired);
            RetireClosedOrderProjectReferences(retired);
            this.agentStore.RetireClosedOrderExplorationGoals(retired);
            transportSystem.RetireOrderLinks(retired);
            communitySystem.RetireOrderLinks(retired);
        }
        orderStore.PruneCancellationRetries();

    }
}

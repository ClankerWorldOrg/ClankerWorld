using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private const string KnowledgeWritePrefix = "knowledge_write:";
    private const string KnowledgeCopyPrefix = "knowledge_copy:";
    private const string KnowledgeReadPrefix = "knowledge_read:";

    private AgentKnowledgeWritingProject? KnowledgeWritingFor(string actor) =>
        knowledge.WritingProjects.SingleOrDefault(project => project.ActorId == actor);

    private bool MayMakeKnowledgeArtifact(string actor) => agents.ContainsKey(actor) && IsAdult(actor) &&
        knowledge.Artifacts.Count < AgentKnowledgeRules.MaximumArtifactsInWorld &&
        knowledge.Artifacts.Count(item => item.CreatorId == actor) < AgentKnowledgeRules.MaximumArtifactsPerCreator;

    private AgentKnowledgeFact[] FactsToWrite(string actor, string kind, AgentKnowledgeArtifact? source = null)
    {
        var learned = knowledge.Facts.Where(fact => fact.OwnerId == actor)
            .OrderByDescending(fact => fact.LearnedTick).ThenBy(fact => fact.Id, StringComparer.Ordinal).ToArray();
        if (source is not null)
        {
            var recorded = learned.Concat(knowledge.EarlierFacts.Where(fact => fact.OwnerId == actor)).ToArray();
            // A reader may already know a site firsthand or through another
            // discoverer. Copy the held source's account without replacing that
            // personal ledger entry or inventing knowledge of an unknown site.
            var copied = source.Facts.Select(fact =>
            {
                var known = recorded.FirstOrDefault(item => item.Position == fact.Position);
                return known is null ? null : fact with
                {
                    Id = known.Id,
                    OwnerId = actor,
                    LearnedTick = WorldTick,
                    Acquisition = "read",
                    SourceAgentId = source.CreatorId,
                    SourceArtifactId = source.Id,
                };
            }).ToArray();
            return copied.Any(fact => fact is null) ? [] : copied.Cast<AgentKnowledgeFact>().ToArray();
        }
        return learned.Take(kind == "field_record" ? 1 : AgentKnowledgeRules.MaximumFactsPerArtifact).ToArray();
    }

    private bool AlreadyWrote(string actor, string kind, AgentKnowledgeFact[] facts, AgentRecipeKnowledge[] recipes) =>
        knowledge.Artifacts.Any(artifact => artifact.CreatorId == actor && artifact.Kind == kind &&
            artifact.Facts.Count == facts.Length && artifact.Facts.All(fact => facts.Any(other => AgentKnowledgeRules.SameDiscovery(fact, other))) &&
            artifact.Recipes.Count == recipes.Length && artifact.Recipes.All(recipe => recipes.Any(other => AgentKnowledgeRules.SameRecipe(recipe, other))));

    private IEnumerable<InventoryLot> CarriedWritingMaterials(string actor, string kind) => society.Checkpoint.Inventory.Lots
        .Where(lot => lot.OwnerId == actor && lot.ItemKind == kind && PersonalEquipmentRules.IsCarried(lot, actor) &&
            lot.ContainerLotId is null && lot.DeliveryBuildingId is null && lot.ConditionBasisPoints > 0 &&
            lot.FreshnessBasisPoints > 0 && AvailableLotQuantity(lot) > 0)
        .OrderBy(lot => lot.Id, StringComparer.Ordinal);

    private List<InputCollectionStep>? WritingCollectionPlan(string actor, string kind)
    {
        var inputs = new List<InputCollectionStep>();
        foreach (var input in AgentKnowledgeRules.WritingMaterials(kind))
        {
            var missing = Math.Max(0, input.Quantity - CarriedWritingMaterials(actor, input.Kind).Sum(AvailableLotQuantity));
            if (missing == 0) continue;
            foreach (var stock in SharedStockItems(input.Kind, actor, GoodsUse.Collect))
            {
                var quantity = Math.Min(missing, AvailableLotQuantity(stock));
                if (quantity <= 0) continue;
                inputs.Add(StockCollectionStep(stock, quantity));
                missing -= quantity;
                if (missing == 0) break;
            }
            if (missing > 0) return null;
        }
        return PlanInputCollection(actor, inputs);
    }

    private bool CanSupplyWriting(string actor, string kind) => WritingCollectionPlan(actor, kind) is not null;

    private KnowledgeWritingActionPlan? PlanKnowledgeWriting(string actor, string candidate,
        AgentKnowledgeFact[]? selectedFacts = null, AgentRecipeKnowledge[]? selectedRecipes = null, AgentKnowledgeArtifact? selectedSource = null)
    {
        if (KnowledgeWritingFor(actor) is { } existing)
            return candidate == "knowledge_continue" || candidate == existing.CandidateId
                ? new(actor, candidate, existing.Kind, null, existing.Facts, existing.Recipes, [], null, existing) : null;
        var source = selectedSource ?? (candidate.StartsWith(KnowledgeCopyPrefix, StringComparison.Ordinal)
            ? HeldKnowledgeArtifacts(actor).SingleOrDefault(item => item.Id == candidate[KnowledgeCopyPrefix.Length..]) : null);
        var kind = source?.Kind ?? (candidate.StartsWith(KnowledgeWritePrefix, StringComparison.Ordinal) ? candidate[KnowledgeWritePrefix.Length..] : null);
        if (kind is null || !AgentKnowledgeRules.IsArtifactKind(kind)) return null;
        var facts = selectedFacts ?? FactsToWrite(actor, kind, source);
        var recipes = selectedRecipes ?? RecipesToWrite(actor, kind, source);
        var collection = WritingCollectionPlan(actor, kind);
        if (collection is null) return null;
        PlannedWritingPickup? pickup = collection.Count == 0 ? null : new(collection[0].Stock!.Id, collection[0].Stock!.OwnerId,
            collection[0].Kind, collection[0].CarryUnits, collection[0].Position, collection[0].Range);
        var materials = new List<PlannedWritingMaterial>();
        if (pickup is null)
            foreach (var input in AgentKnowledgeRules.WritingMaterials(kind))
            {
                var remaining = input.Quantity;
                foreach (var lot in CarriedWritingMaterials(actor, input.Kind))
                {
                    var quantity = Math.Min(remaining, AvailableLotQuantity(lot));
                    if (quantity > 0) materials.Add(new(lot.Id, input.Kind, quantity));
                    remaining -= quantity;
                    if (remaining == 0) break;
                }
                if (remaining != 0) return null;
            }
        return new(actor, candidate, kind, source, facts, recipes, materials, pickup, null);
    }

    private void OfferKnowledgeWriting(List<CognitionCandidate> candidates, KnowledgeWritingActionPlan? plan, string text, int priority)
    {
        if (plan is not null && CheckActionPlan(plan).Allowed) candidates.Add(new(plan.CandidateId, text, priority) { ActionPlan = plan });
    }

    private void AddKnowledgeWritingCandidates(List<CognitionCandidate> candidates, string actor, AgentState person)
    {
        if (!IsAdult(actor) || NeedsUrgentFood(person) || NeedsUrgentWarmth(person)) return;
        if (KnowledgeWritingFor(actor) is { } project)
        {
            var label = $"Continue writing your {project.Kind.Replace('_', ' ')}: {project.WorkDone}/{project.WorkRequired} work, with real materials reserved.";
            OfferKnowledgeWriting(candidates, PlanKnowledgeWriting(actor, "knowledge_continue"), label, 54);
            OfferKnowledgeWriting(candidates, PlanKnowledgeWriting(actor, project.CandidateId), label, 54);
            return;
        }
        foreach (var artifact in HeldKnowledgeArtifacts(actor))
        {
            if (HasUnknownArtifactContents(actor, artifact))
            {
                var reading = new KnowledgeReadingActionPlan(actor, artifact, actor, false);
                if (CheckActionPlan(reading).Allowed) candidates.Add(new(reading.CandidateId,
                    $"Read {artifact.Title} and learn only the sites and recipes actually written in it.", 53)
                { ActionPlan = reading });
            }
            if (!MayMakeKnowledgeArtifact(actor)) continue;
            var facts = FactsToWrite(actor, artifact.Kind, artifact);
            var recipes = RecipesToWrite(actor, artifact.Kind, artifact);
            if (facts.Length + recipes.Length > 0 && HasCompleteCopyContents(actor, artifact, facts, recipes) &&
                !AlreadyWrote(actor, artifact.Kind, facts, recipes))
                OfferKnowledgeWriting(candidates, PlanKnowledgeWriting(actor, IdentityRules.PrefixedReference(KnowledgeCopyPrefix, artifact.Id), facts, recipes, artifact),
                    $"Copy {artifact.Title} using real paper{(artifact.Kind == "book" ? " and a cloth cover" : "")}; keep its original discovery sources.", 58);
        }
        if (!MayMakeKnowledgeArtifact(actor)) return;
        foreach (var kind in new[] { "field_record", "field_map", "book" })
        {
            var facts = FactsToWrite(actor, kind);
            var recipes = RecipesToWrite(actor, kind);
            if (facts.Length + recipes.Length == 0 || AlreadyWrote(actor, kind, facts, recipes)) continue;
            var materials = string.Join(" and ", AgentKnowledgeRules.WritingMaterials(kind).Select(input => $"{input.Quantity} {input.Kind}"));
            OfferKnowledgeWriting(candidates, PlanKnowledgeWriting(actor, IdentityRules.PrefixedReference(KnowledgeWritePrefix, kind), facts, recipes),
                $"Write a {kind.Replace('_', ' ')} containing {facts.Length} personally learned site(s) and {recipes.Length} recipe(s), using {materials} and {AgentKnowledgeRules.WritingWork(kind)} work (trial amounts).", 56);
        }
    }

    private void ApplyKnowledgeWritingCandidate(string actor, AgentState person, string candidate, ActionPlan? originalPlan = null)
    {
        if (!IsAdult(actor) || NeedsUrgentFood(person) || NeedsUrgentWarmth(person)) return;
        if (candidate.StartsWith(KnowledgeReadPrefix, StringComparison.Ordinal))
        {
            var reading = originalPlan as KnowledgeReadingActionPlan;
            if (reading is null && HeldKnowledgeArtifacts(actor).FirstOrDefault(item => item.Id == candidate[KnowledgeReadPrefix.Length..]) is { } selected)
                reading = new(actor, selected, actor, false);
            if (reading is not null) ApplyKnowledgeReading(reading);
            return;
        }
        var plan = originalPlan as KnowledgeWritingActionPlan ?? PlanKnowledgeWriting(actor, candidate);
        if (plan is null) return;
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        ApplyCheckedKnowledgeWriting(plan, person);
    }

    private void ApplyCheckedKnowledgeWriting(KnowledgeWritingActionPlan plan, AgentState person)
    {
        var actor = plan.Actor;
        var candidate = plan.CandidateId;
        if (plan.Project is not null) { ContinueKnowledgeWriting(KnowledgeWritingFor(actor)!); return; }
        if (plan.Pickup is { } pickup)
        {
            CollectKnowledgeMaterial(actor, person, pickup.ItemKind, pickup.Quantity, pickup.LotId);
            return;
        }
        var kind = plan.Kind;
        var source = plan.Source;
        var facts = plan.Facts.Select(fact => source is null ? fact : fact with { LearnedTick = WorldTick }).ToArray();
        var recipes = plan.Recipes.Select(recipe => source is null ? recipe : recipe with { LearnedTick = WorldTick }).ToArray();
        var id = WritingProjectId(actor, WorldTick, society.Checkpoint.Inventory.Reservations.Count);
        var materials = new List<AgentKnowledgeMaterial>();
        var inventory = society.Checkpoint.Inventory;
        foreach (var input in plan.Materials)
        {
            var reservationId = WritingMaterialReservationId(id, materials.Count);
            inventory = InventoryAuthority.Reserve(inventory, reservationId, actor, input.LotId, input.Quantity,
                AgentKnowledgeRules.MaterialPurpose(id, input.ItemKind), long.MaxValue, holdAccess: InventoryHoldRules.BorrowedBy(actor));
            materials.Add(new(Identity.Parse(reservationId), input.LotId, input.ItemKind, input.Quantity));
        }
        RunInventoryCommand(InventorySystem.ApplyCheckedKnowledgeWriting(inventory));
        communitySystem.StartWriting(new AgentKnowledgeWritingProject(id, actor, kind, candidate, source?.Id, facts,
            WorldTick, WorldTick, 0, materials)
        { Recipes = recipes });
        checkpointSchemaVersion = StateSchemaVersion;
        AppendEvent(new AgentKnowledgeWritingStartedEventPayload(actor, kind, id));
    }

    private void CollectKnowledgeMaterial(string actor, AgentState person, string kind, int missing, string plannedLotId)
    {
        var item = SharedStockItems(kind, actor, GoodsUse.Collect).FirstOrDefault(lot => lot.Id == plannedLotId);
        if (item is null || item.DeliveryBuildingId is not null || item.ConditionBasisPoints <= 0 || item.FreshnessBasisPoints <= 0) return;
        var quantity = Math.Min(missing, Math.Min(FreeCarryCapacity(actor), AvailableLotQuantity(item)));
        if (quantity <= 0) return;
        var position = HouseholdStockPosition(item);
        var range = HouseholdStockInteractionRange(item);
        if (!IsWithinInteractionRange(person.Position, position, range))
        {
            MoveToward(actor, person, position, "knowledge_materials", range);
            return;
        }
        // Resolve household/Town permission for the planned lot again at pickup time.
        RunInventoryCommand(InventorySystem.CollectKnowledgeMaterial(InventoryKnowledgeWorld, actor, kind, item, quantity));
        AppendEvent(new AgentKnowledgeMaterialCollectedEventPayload(actor, kind, quantity));
    }

    private bool HasLiveWritingInputs(AgentKnowledgeWritingProject project) => project.Materials.All(input =>
        society.Checkpoint.Inventory.Reservations.FirstOrDefault(item => item.Id == input.ReservationId) is
        { State: InventoryReservationState.Reserved } reservation && reservation.OwnerId == project.ActorId &&
        society.Checkpoint.Inventory.Lots.FirstOrDefault(item => item.Id == input.LotId) is { } lot &&
        lot.OwnerId == project.ActorId && lot.ItemKind == input.ItemKind && lot.Quantity >= input.Quantity &&
        lot.ConditionBasisPoints > 0 && lot.FreshnessBasisPoints > 0 && lot.ContainerLotId is null &&
        lot.DeliveryBuildingId is null && PersonalEquipmentRules.IsCarried(lot, project.ActorId));

    private bool HasWritingSource(AgentKnowledgeWritingProject project) => project.SourceArtifactId is null ||
        HeldKnowledgeArtifacts(project.ActorId).Any(item => item.Id == project.SourceArtifactId);

    private void ContinueKnowledgeWriting(AgentKnowledgeWritingProject project)
    {
        var actor = project.ActorId;
        if (project.OrderInstructionId is not null && !OrderSystem.IsCurrentKnowledgeOrderProject(KnowledgeOrdersWorld, project))
        {
            CancelKnowledgeWriting(project, "order_unavailable");
            return;
        }
        if (!HasLiveWritingInputs(project) || !HasWritingSource(project))
        {
            CancelKnowledgeWriting(project, "materials_or_source_unavailable");
            return;
        }
        var person = agents[actor];
        if (project.LastWorkedTick >= WorldTick || !IllnessRules.AllowsWork(actor, WorldTick, person.Survival?.IllnessBasisPoints ?? 0)) return;
        var work = project.WorkDone + 1;
        if (work < project.WorkRequired)
        {
            ReplaceKnowledgeWriting(project with { WorkDone = work, LastWorkedTick = WorldTick });
            return;
        }
        if (!MayMakeKnowledgeArtifact(actor) || FreeCarryCapacity(actor) + project.Materials.Sum(input => input.Quantity) < 1) return;
        var sequence = knowledge.Artifacts.Count + 1;
        var artifactId = $"knowledge-artifact-{sequence:D6}";
        var lotId = $"knowledge-lot-{sequence:D6}";
        var title = (project.Kind == "book" ? "Book" : project.Kind == "field_map" ? "Field map" : "Field record") +
            (project.Facts.Count == 0 ? "" : $" · {project.Facts.Count} site{(project.Facts.Count == 1 ? "" : "s")}") +
            (project.Recipes.Count == 0 ? "" : $" · {project.Recipes.Count} recipe{(project.Recipes.Count == 1 ? "" : "s")}");
        var inventory = society.Checkpoint.Inventory;
        foreach (var input in project.Materials) inventory = InventoryAuthority.ConsumeReservation(inventory, input.ReservationId);
        inventory = InventoryAuthority.AddLot(inventory, lotId, project.Kind, actor, 1, WorldTick);
        RunInventoryCommand(InventorySystem.ContinueKnowledgeWriting(inventory));
        communitySystem.CompleteWriting(project.Id, new AgentKnowledgeArtifact(
            artifactId, actor, lotId, project.Kind, title, WorldTick, project.Facts)
        {
            Recipes = project.Recipes,
            WritingProjectId = project.Id,
            Materials = project.Materials,
            SourceArtifactId = project.SourceArtifactId,
            OrderInstructionId = project.OrderInstructionId,
        });
        checkpointSchemaVersion = StateSchemaVersion;
        AppendEvent(new AgentKnowledgeArtifactCreatedEventPayload(actor, artifactId, project.Kind, project.Facts.Count));
        OrderSystem.CreditKnowledgeOrderCompletion(KnowledgeOrdersWorld, project, artifactId);
    }

    private void ReplaceKnowledgeWriting(AgentKnowledgeWritingProject project)
    {
        communitySystem.AdvanceWriting(project);
        checkpointSchemaVersion = StateSchemaVersion;
    }

    private void CancelKnowledgeWriting(AgentKnowledgeWritingProject project, string reason)
    {
        OrderSystem.ClearKnowledgeOrderBinding(KnowledgeOrdersWorld, project);
        RunInventoryCommand(InventorySystem.CancelKnowledgeWriting(project));
        communitySystem.CancelWriting(project.Id);
        checkpointSchemaVersion = StateSchemaVersion;
        AppendEvent(new AgentKnowledgeWritingCancelledEventPayload(project.ActorId, project.Kind, reason));
    }

    private void MaintainKnowledgeWriting()
    {
        foreach (var project in knowledge.WritingProjects.ToArray())
            if (project.OrderInstructionId is not null && !OrderSystem.IsCurrentKnowledgeOrderProject(KnowledgeOrdersWorld, project) || !MayMakeKnowledgeArtifact(project.ActorId) ||
                !HasLiveWritingInputs(project) || !HasWritingSource(project))
                CancelKnowledgeWriting(project, "materials_or_source_unavailable");
    }
    private ActionCheck CheckKnowledgeWriting(KnowledgeWritingActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? KnowledgeWritingCapabilities);
        if (!capability.Allowed) return capability;
        if (plan.Project is { } selected)
        {
            var current = KnowledgeWritingFor(plan.Actor);
            return current is not null && TownHearingProcedure.Digest(current with { WorkDone = 0, LastWorkedTick = 0 }) ==
                TownHearingProcedure.Digest(selected with { WorkDone = 0, LastWorkedTick = 0 }) && HasLiveWritingInputs(current) && HasWritingSource(current)
                ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the original writing project or reserved inputs have changed");
        }
        if (!MayMakeKnowledgeArtifact(plan.Actor) || KnowledgeWritingFor(plan.Actor) is not null ||
            plan.Source is { } source && !HeldKnowledgeArtifacts(plan.Actor).Any(item => TownHearingProcedure.Digest(item) == TownHearingProcedure.Digest(source)) ||
            !RetainedWritingContentsAreKnown(plan) || AlreadyWrote(plan.Actor, plan.Kind, plan.Facts.ToArray(), plan.Recipes.ToArray()))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected writing contents, source or opportunity have changed");
        if (plan.Pickup is { } pickup)
        {
            if (SharedStockItems(pickup.ItemKind, plan.Actor, GoodsUse.Collect).SingleOrDefault(lot => lot.Id == pickup.LotId) is not { } lot ||
                lot.OwnerId != pickup.OwnerId || AvailableLotQuantity(lot) < pickup.Quantity || FreeCarryCapacity(plan.Actor) < pickup.Quantity ||
                HouseholdStockPosition(lot) != pickup.Position || HouseholdStockInteractionRange(lot) != pickup.Range)
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected writing material pickup is unavailable");
            return IsWithinInteractionRange(agents[plan.Actor].Position, pickup.Position, pickup.Range) ||
                Reach(plan.Actor, pickup.Position, ReachPurpose.KnowledgeWriting, pickup.Range, 0, SwimmingPolicy.WhenCapable).Count > 0
                ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected writing material is out of reach");
        }
        return plan.Materials.All(input => CarriedWritingMaterials(plan.Actor, input.ItemKind).Any(lot => lot.Id == input.LotId && AvailableLotQuantity(lot) >= input.Quantity))
            ? new() : ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected carried writing supplies are unavailable");
    }

    private bool RetainedWritingContentsAreKnown(KnowledgeWritingActionPlan plan) =>
        plan.Facts.Count + plan.Recipes.Count > 0 && plan.Facts.All(fact => plan.Source is null
            ? knowledge.Facts.Concat(knowledge.EarlierFacts).Any(known => AgentKnowledgeRules.SameLearnedFact(known, fact))
            : KnowsMapFact(plan.Actor, fact.Position)) &&
        plan.Recipes.All(recipe => KnowsRecipe(plan.Actor, recipe.RecipeId)) &&
        (plan.Source is null || HasCompleteCopyContents(plan.Actor, plan.Source, plan.Facts.ToArray(), plan.Recipes.ToArray()));
}

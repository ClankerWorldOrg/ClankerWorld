using System.Text.Json.Serialization;
using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(ConsumeFoodOrderDefinition), "consume_food")]
[JsonDerivedType(typeof(SeekFoodOrderDefinition), "seek_food")]
[JsonDerivedType(typeof(HarvestFoodOrderDefinition), "harvest_food")]
[JsonDerivedType(typeof(GatherMaterialOrderDefinition), "gather_material")]
[JsonDerivedType(typeof(MoveToOrderDefinition), "move_to")]
[JsonDerivedType(typeof(AcceptGuardianshipOrderDefinition), "accept_guardianship")]
[JsonDerivedType(typeof(TalkToOrderDefinition), "talk_to")]
[JsonDerivedType(typeof(ProposeMarriageOrderDefinition), "propose_marriage")]
[JsonDerivedType(typeof(ReadKnowledgeOrderDefinition), "read_knowledge")]
[JsonDerivedType(typeof(WriteKnowledgeOrderDefinition), "write_knowledge")]
[JsonDerivedType(typeof(CopyKnowledgeOrderDefinition), "copy_knowledge")]
[JsonDerivedType(typeof(AttachHandcartOrderDefinition), "attach_handcart")]
[JsonDerivedType(typeof(ParkHandcartOrderDefinition), "park_handcart")]
[JsonDerivedType(typeof(RepairHandcartOrderDefinition), "repair_handcart")]
[JsonDerivedType(typeof(LoadHandcartOrderDefinition), "load_handcart")]
[JsonDerivedType(typeof(UnloadHandcartOrderDefinition), "unload_handcart")]
[JsonDerivedType(typeof(UnloadHandcartGroundOrderDefinition), "unload_handcart_ground")]
[JsonDerivedType(typeof(AnimalCareOrderDefinition), "animal_care")]
[JsonDerivedType(typeof(AnimalCollectOrderDefinition), "animal_collect")]
[JsonDerivedType(typeof(AnimalTameOrderDefinition), "animal_tame")]
[JsonDerivedType(typeof(AnimalLeadHomeOrderDefinition), "animal_lead_home")]
[JsonDerivedType(typeof(AnimalSaddleOrderDefinition), "animal_saddle")]
[JsonDerivedType(typeof(AnimalMountOrderDefinition), "animal_mount")]
[JsonDerivedType(typeof(AnimalDismountOrderDefinition), "animal_dismount")]
[JsonDerivedType(typeof(SeekShelterOrderDefinition), "seek_shelter")]
[JsonDerivedType(typeof(TendFireOrderDefinition), "tend_fire")]
[JsonDerivedType(typeof(ConstructBuildingOrderDefinition), "construct_building")]
[JsonDerivedType(typeof(ExpandBuildingOrderDefinition), "expand_building")]
[JsonDerivedType(typeof(DeliverStockOrderDefinition), "deliver_stock")]
[JsonDerivedType(typeof(CollectGoodsOrderDefinition), "collect_goods")]
[JsonDerivedType(typeof(StoreGoodsOrderDefinition), "store_goods")]
[JsonDerivedType(typeof(ReturnBorrowedOrderDefinition), "return_borrowed")]
[JsonDerivedType(typeof(ProduceItemOrderDefinition), "produce_item")]
[JsonDerivedType(typeof(PlantTreeOrderDefinition), "plant_tree")]
[JsonDerivedType(typeof(PlantBroadleafOrderDefinition), "plant_broadleaf")]
[JsonDerivedType(typeof(PlantConiferOrderDefinition), "plant_conifer")]
[JsonDerivedType(typeof(PlantOrchardOrderDefinition), "plant_orchard")]
[JsonDerivedType(typeof(TillFieldOrderDefinition), "till_field")]
[JsonDerivedType(typeof(PlantFieldOrderDefinition), "plant_field")]
[JsonDerivedType(typeof(TendFieldOrderDefinition), "tend_field")]
[JsonDerivedType(typeof(HarvestFieldOrderDefinition), "harvest_field")]
[JsonDerivedType(typeof(RepairEquipmentOrderDefinition), "repair_equipment")]
[JsonDerivedType(typeof(RepairToolOrderDefinition), "repair_tool")]
[JsonDerivedType(typeof(CollectMaterialOrderDefinition), "collect_material")]
[JsonDerivedType(typeof(CollectEquipmentOrderDefinition), "collect_equipment")]
[JsonDerivedType(typeof(CollectFoodOrderDefinition), "collect_food")]
[JsonDerivedType(typeof(StoreMaterialOrderDefinition), "store_material")]
[JsonDerivedType(typeof(StoreEquipmentOrderDefinition), "store_equipment")]
[JsonDerivedType(typeof(TravelByBoatOrderDefinition), "travel_by_boat")]
[JsonDerivedType(typeof(UnknownOrderDefinition), "unknown")]
public abstract record OrderTaskDefinition
{
    [JsonIgnore] public abstract string Action { get; }
    internal abstract bool IsValid(OrderTask order, OwnerQueuedInstruction instruction, HashSet<string> people,
        long worldTick, DeclarativeWorldContentState worldContent);
}

public abstract record FoodOrderFamilyDefinition : OrderTaskDefinition
{
    internal override bool IsValid(OrderTask order, OwnerQueuedInstruction instruction, HashSet<string> people,
        long worldTick, DeclarativeWorldContentState worldContent)
    {
        if (order.TargetAgentId is not null || order.Action is not ("consume_food" or "seek_food" or "harvest_food") ||
                    order.RequestedUnits is < 1 or > 1000 || order.CompletedUnits is < 0 or > 1_000_000 ||
                    order.Status == "finished" && (order.RepeatUntilCancelled || order.CompletedUnits < order.RequestedUnits) ||
                    order.Action == "consume_food" && order.ProgressUnit != "food_items" ||
                    order.Action == "seek_food" && order.ProgressUnit != "arrivals" ||
                    order.Action == "harvest_food" && order.ProgressUnit is not ("harvests" or "food_items") ||
                    order.Action == "harvest_food" && order.QuantityIsExplicit != (order.ProgressUnit == "food_items"))
            return false;
        return true;
    }
}

public abstract record MaterialOrderFamilyDefinition : OrderTaskDefinition
{
    internal override bool IsValid(OrderTask order, OwnerQueuedInstruction instruction, HashSet<string> people,
        long worldTick, DeclarativeWorldContentState worldContent)
    {
        return OrderTaskParser.IsMaterialKind(order.TargetMaterialKind) &&
                        order.TargetAgentId is null && order.TargetFoodKind is null && !(order.TargetResourceId is not null && order.TargetPosition is not null) &&
                        order.RequestedUnits is >= 1 and <= 1000 && order.CompletedUnits is >= 0 and <= 1_000_000 &&
                        order.Status != "not_understood" &&
                        (order.Status == "finished") == (!order.RepeatUntilCancelled && order.CompletedUnits >= order.RequestedUnits) &&
                        (order.QuantityIsExplicit ? order.ProgressUnit == "material_items" : order.ProgressUnit == "harvests" && order.RequestedUnits == 1) &&
                        (order.CompletedUnits == 0 ? order.LastEffectId is null : order.LastEffectId?.StartsWith("gather:material:", StringComparison.Ordinal) == true);
    }
}

public abstract record MoveOrderFamilyDefinition : OrderTaskDefinition
{
    internal override bool IsValid(OrderTask order, OwnerQueuedInstruction instruction, HashSet<string> people,
        long worldTick, DeclarativeWorldContentState worldContent)
    {
        return order.TargetPosition is { } destination && order.TargetFoodKind is null && order.TargetResourceId is null &&
                        order.TargetAgentId is null &&
                        order.RequestedUnits == 1 && order.CompletedUnits is 0 or 1 &&
                        (order.Status == "finished") == (order.CompletedUnits == 1) && order.Status != "not_understood" &&
                        order.ProgressUnit == "arrivals" && !order.RepeatUntilCancelled && !order.QuantityIsExplicit &&
                        order.LastEffectId == (order.CompletedUnits == 1 ? WorldRuntime.MovementOrderEffectId(destination) : null);
    }
}

public abstract record GuardianOrderFamilyDefinition : OrderTaskDefinition
{
    internal override bool IsValid(OrderTask order, OwnerQueuedInstruction instruction, HashSet<string> people,
        long worldTick, DeclarativeWorldContentState worldContent)
    {
        return order.TargetAgentId is { } child && people.Contains(child) && child != instruction.TargetAgentId &&
                        order.RequestedUnits == 1 && order.CompletedUnits is 0 or 1 &&
                        (order.Status == "finished") == (order.CompletedUnits == 1) && order.Status != "not_understood" &&
                        order.ProgressUnit == "guardianships" && !order.RepeatUntilCancelled && !order.QuantityIsExplicit &&
                        order.TargetFoodKind is null && order.TargetResourceId is null && order.TargetPosition is null &&
                        order.LastEffectId == (order.CompletedUnits == 1 ? WorldRuntime.GuardianOrderEffectId(instruction.TargetAgentId, child) : null);
    }
}

public abstract record TalkOrderFamilyDefinition : OrderTaskDefinition
{
    internal override bool IsValid(OrderTask order, OwnerQueuedInstruction instruction, HashSet<string> people,
        long worldTick, DeclarativeWorldContentState worldContent)
    {
        return order.TargetAgentId is { } target && people.Contains(target) && target != instruction.TargetAgentId &&
                        order.RequestedUnits == 1 && order.CompletedUnits is >= 0 and <= 1 && !order.RepeatUntilCancelled && !order.QuantityIsExplicit &&
                        order.ProgressUnit == "conversations" && order.TargetFoodKind is null && order.TargetResourceId is null && order.TargetPosition is null &&
                        order.Status != "not_understood" && (order.Status == "finished") == (order.CompletedUnits == 1) &&
                        (order.TalkConversationId is null || !string.IsNullOrWhiteSpace(order.TalkConversationId) && order.TalkConversationId.Length <= DerivedId.MaximumLength &&
                            order.TalkConversationId == order.TalkConversationId.Trim() && !order.TalkConversationId.Any(char.IsControl)) &&
                        (order.CompletedUnits == 0 ? order.TalkOutcome is null && order.LastEffectId is null :
                            order.TalkConversationId is not null && WorldRuntime.IsTalkOutcome(order.TalkOutcome) && order.LastEffectId == WorldRuntime.TalkOrderEffectId(instruction));
    }
}

public abstract record MarriageOrderFamilyDefinition : OrderTaskDefinition
{
    internal override bool IsValid(OrderTask order, OwnerQueuedInstruction instruction, HashSet<string> people,
        long worldTick, DeclarativeWorldContentState worldContent)
    {
        return WorldRuntime.IsValidMarriageOrder(instruction, people);
    }
}

public abstract record KnowledgeReadOrderFamilyDefinition : OrderTaskDefinition
{
    internal override bool IsValid(OrderTask order, OwnerQueuedInstruction instruction, HashSet<string> people,
        long worldTick, DeclarativeWorldContentState worldContent)
    {
        return WorldRuntime.IsValidKnowledgeReadOrderShape(order, instruction, worldTick);
    }
}

public abstract record KnowledgeOrderFamilyDefinition : OrderTaskDefinition
{
    internal override bool IsValid(OrderTask order, OwnerQueuedInstruction instruction, HashSet<string> people,
        long worldTick, DeclarativeWorldContentState worldContent)
    {
        var terminal = order.IsTerminal;
        return order.TargetKnowledgeKind is { } kind && AgentKnowledgeRules.IsArtifactKind(kind) &&
                        order.TargetAgentId is null && order.TargetFoodKind is null && order.TargetResourceId is null && order.TargetPosition is null &&
                        order.RequestedUnits is >= 1 and <= 1000 && order.CompletedUnits is >= 0 and <= 1_000_000 &&
                        (order.QuantityIsExplicit || order.RequestedUnits == 1) && order.ProgressUnit == (order.Action == "copy_knowledge" ? "copies" : "artifacts") &&
                        (order.KnowledgeCopySourceArtifactId is null || order.KnowledgeCopySourceArtifactId.Length <= DerivedId.MaximumLength &&
                            WorldRuntime.IsValidProductionBindingId(order.KnowledgeCopySourceArtifactId)) &&
                        (order.RepeatUntilCancelled || order.CompletedUnits <= order.RequestedUnits) &&
                        order.Status != "not_understood" &&
                        (order.Status == "finished") == (!order.RepeatUntilCancelled && order.CompletedUnits >= order.RequestedUnits) &&
                        (order.KnowledgeWritingProjectId is null || !terminal && order.Status != "queued" &&
                            WorldRuntime.IsValidProductionBindingId(order.KnowledgeWritingProjectId)) &&
                        (order.CompletedUnits == 0 ? order.LastEffectId is null :
                            order.LastEffectId is { } receipt && receipt.StartsWith("writing:artifact:", StringComparison.Ordinal) &&
                            receipt.Length <= DerivedId.MaximumLength && WorldRuntime.IsValidProductionBindingId(receipt["writing:artifact:".Length..]));
    }
}

public abstract record CartOrderFamilyDefinition : OrderTaskDefinition
{
    internal override bool IsValid(OrderTask order, OwnerQueuedInstruction instruction, HashSet<string> people,
        long worldTick, DeclarativeWorldContentState worldContent)
    {
        return (order.TargetCartLotId is null || WorldRuntime.IsValidInstructionIdentifier(order.TargetCartLotId) && order.TargetCartLotId == order.TargetCartLotId.Trim()) &&
                        order.RequestedUnits == 1 && order.CompletedUnits is >= 0 and <= 1 && !order.RepeatUntilCancelled && !order.QuantityIsExplicit &&
                        order.ProgressUnit == "cart_tasks" && order.TargetFoodKind is null && order.TargetAgentId is null && order.TargetResourceId is null && order.TargetPosition is null &&
                        order.Status != "not_understood" && (order.Status == "finished") == (order.CompletedUnits == 1) &&
                        (order.CompletedUnits == 0 ? order.LastEffectId is null : order.TargetCartLotId is not null &&
                            (order.Action == "repair_handcart" ? WorldRuntime.CartRepairOrderReceiptTick(instruction, worldTick) is not null :
                                order.LastEffectId == WorldRuntime.CartOrderEffectId(instruction)));
    }
}

public abstract record CartCargoOrderFamilyDefinition : OrderTaskDefinition
{
    internal override bool IsValid(OrderTask order, OwnerQueuedInstruction instruction, HashSet<string> people,
        long worldTick, DeclarativeWorldContentState worldContent)
    {
        return WorldRuntime.IsValidCartCargoOrder(instruction);
    }
}

public abstract record AnimalOrderFamilyDefinition : OrderTaskDefinition
{
    internal override bool IsValid(OrderTask order, OwnerQueuedInstruction instruction, HashSet<string> people,
        long worldTick, DeclarativeWorldContentState worldContent)
    {
        return !string.IsNullOrWhiteSpace(order.TargetAnimalId) && order.TargetAnimalId.Length <= DerivedId.MaximumLength &&
                        !order.TargetAnimalId.Any(char.IsControl) && order.RequestedUnits == 1 && order.CompletedUnits >= 0 &&
                        (order.RepeatUntilCancelled || order.CompletedUnits <= 1) && order.ProgressUnit == "animal_tasks" &&
                        order.TargetResourceId is null && order.TargetAgentId is null && order.TargetPosition is null &&
                        order.Status != "not_understood" &&
                        (order.Status == "finished") == (!order.RepeatUntilCancelled && order.CompletedUnits == 1) &&
                        (order.CompletedUnits == 0 ? order.LastEffectId is null : order.LastEffectId?.StartsWith("animal-order:", StringComparison.Ordinal) == true);
    }
}

public abstract record ShelterOrderFamilyDefinition : OrderTaskDefinition
{
    internal override bool IsValid(OrderTask order, OwnerQueuedInstruction instruction, HashSet<string> people,
        long worldTick, DeclarativeWorldContentState worldContent)
    {
        return WorldRuntime.IsValidShelterOrderShape(order, instruction, worldTick);
    }
}

public abstract record BuildingOrderFamilyDefinition : OrderTaskDefinition
{
    internal override bool IsValid(OrderTask order, OwnerQueuedInstruction instruction, HashSet<string> people,
        long worldTick, DeclarativeWorldContentState worldContent)
    {
        var construction = order.Action == "construct_building";
        if (!WorldBuildingOrderCatalog.Supports(order.Action, order.TargetBuildingKind) ||
            !WorldBuildingOrderCatalog.Available(worldContent).Any(item => item.BuildingKind == order.TargetBuildingKind) ||
            order.RequestedUnits != 1 || order.CompletedUnits is < 0 or > 1 || order.RepeatUntilCancelled ||
            order.ProgressUnit != (construction ? "buildings" : "expansions") ||
            order.Status == "not_understood" || (order.Status == "finished") != (order.CompletedUnits == 1) ||
            order.TargetAgentId is not null || order.TargetFoodKind is not null || order.TargetResourceId is not null ||
            (order.CompletedUnits == 0 ? order.LastEffectId is not null :
                !WorldRuntime.IsValidCustodyReceipt(order.LastEffectId, construction ? "construction:building:" : "expand:job:")))
            return false;
        if (!construction) return order.ExpansionBinding is not null || order.CompletedUnits == 0;
        return WorldBuildingOrderCatalog.Find(worldContent, order.TargetDefinitionId)?.BuildingKind == order.TargetBuildingKind &&
            (order.ConstructionInstanceId is null
                ? order.ConstructionOwnerId is null && order.ConstructionPosition is null &&
                    order.ConstructionStartedTick is null && order.CompletedUnits == 0
                : WorldRuntime.IsValidProductionBindingId(order.ConstructionInstanceId) &&
                    order.ConstructionOwnerId is { } owner && WorldRuntime.IsValidProductionBindingId(owner) &&
                    order.ConstructionPosition is { X: >= -10_000_000 and <= 10_000_000, Y: >= -10_000_000 and <= 10_000_000 } site &&
                    (order.TargetPosition is null || order.TargetPosition == site) &&
                    order.ConstructionStartedTick >= instruction.SubmittedTick && order.ConstructionStartedTick <= worldTick);
    }
}

public abstract record DeliveryOrderFamilyDefinition : OrderTaskDefinition
{
    internal override bool IsValid(OrderTask order, OwnerQueuedInstruction instruction, HashSet<string> people,
        long worldTick, DeclarativeWorldContentState worldContent)
    {
        return WorldDeliveryOrderCatalog.IsValidTarget(order.DeliveryPurpose, order.TargetItemKind,
                            order.TargetBuildingKind, WorldDeliveryOrderCatalog.AvailableInputs(worldContent)) &&
                        order.TargetAgentId is null && order.TargetFoodKind is null && order.TargetResourceId is null &&
                        order.RequestedUnits is >= 1 and <= 1000 && order.CompletedUnits is >= 0 and <= 1_000_000 &&
                        (order.RepeatUntilCancelled || order.CompletedUnits <= order.RequestedUnits) &&
                        order.Status != "not_understood" &&
                        (order.Status == "finished") == (!order.RepeatUntilCancelled && order.CompletedUnits >= order.RequestedUnits) &&
                        (order.QuantityIsExplicit ? order.ProgressUnit == "goods_items"
                            : order.ProgressUnit == "delivery_loads" && order.RequestedUnits == 1) &&
                        (order.Status != "finished" || order.DeliveryLotId is null) &&
                        (order.TargetStorageBuildingId is null
                            ? order.DeliveryRoute is null && order.DeliveryLotId is null && order.DeliveryQuantity is null
                            : WorldDeliveryOrderCatalog.IsValidRoute(order.DeliveryPurpose, order.DeliveryRoute,
                                order.TargetItemKind, order.TargetBuildingKind)) &&
                        (order.DeliveryLotId is null ? order.DeliveryQuantity is null :
                            !string.IsNullOrWhiteSpace(order.DeliveryLotId) && order.DeliveryLotId == order.DeliveryLotId.Trim() &&
                            !order.DeliveryLotId.Any(char.IsControl) && order.DeliveryQuantity is > 0 and <= 1_000_000 &&
                            (!order.QuantityIsExplicit || order.RepeatUntilCancelled ||
                                order.DeliveryQuantity <= order.RequestedUnits - order.CompletedUnits)) &&
                        (order.CompletedUnits == 0 ? order.LastEffectId is null : WorldRuntime.IsValidCustodyReceipt(order.LastEffectId, "delivery:stock:"));
    }
}

public abstract record GoodsOrderFamilyDefinition : OrderTaskDefinition
{
    internal override bool IsValid(OrderTask order, OwnerQueuedInstruction instruction, HashSet<string> people,
        long worldTick, DeclarativeWorldContentState worldContent)
    {
        var defaultProgress = order.Action switch
        {
            "collect_goods" => "collection_loads",
            "store_goods" => "storage_loads",
            _ => "return_loads",
        };
        var receiptPrefix = order.Action switch
        {
            "collect_goods" => "collect:personal:",
            "store_goods" => "store:personal:",
            _ => "return:borrowed:",
        };
        return (order.Action == "return_borrowed"
                ? WorldCustodyOrderCatalog.IsReturnKind(order.TargetItemKind)
                : WorldCustodyOrderCatalog.IsGoodsKind(order.TargetItemKind)) &&
            order.TargetAgentId is null && order.TargetFoodKind is null && order.TargetResourceId is null &&
            order.RequestedUnits is >= 1 and <= 1000 && order.CompletedUnits is >= 0 and <= 1_000_000 &&
            (order.RepeatUntilCancelled || order.CompletedUnits <= order.RequestedUnits) &&
            order.Status != "not_understood" &&
            (order.Status == "finished") == (!order.RepeatUntilCancelled && order.CompletedUnits >= order.RequestedUnits) &&
            (order.QuantityIsExplicit ? order.ProgressUnit == "goods_items"
                : order.ProgressUnit == defaultProgress && order.RequestedUnits == 1) &&
            (order.CompletedUnits == 0 ? order.LastEffectId is null : WorldRuntime.IsValidCustodyReceipt(order.LastEffectId, receiptPrefix));
    }
}

public abstract record ProductionOrderFamilyDefinition : OrderTaskDefinition
{
    internal override bool IsValid(OrderTask order, OwnerQueuedInstruction instruction, HashSet<string> people,
        long worldTick, DeclarativeWorldContentState worldContent)
    {
        var recipe = WorldProductionOrderCatalog.Find(worldContent, order.TargetRecipeId);
        return recipe is not null && order.TargetOutputKind == recipe.OutputKind &&
            order.TargetAgentId is null && order.TargetFoodKind is null && order.TargetResourceId is null &&
            order.RequestedUnits is >= 1 and <= 1000 && order.CompletedUnits is >= 0 and <= 1_000_000 &&
            (order.RepeatUntilCancelled || order.CompletedUnits <= order.RequestedUnits) &&
            order.Status != "not_understood" &&
            (order.Status == "finished") == (!order.RepeatUntilCancelled && order.CompletedUnits >= order.RequestedUnits) &&
            (order.ProgressUnit == "output_items"
                ? order.QuantityIsExplicit && order.RequestedUnits % recipe.OutputQuantity == 0 &&
                    order.CompletedUnits % recipe.OutputQuantity == 0
                : order.ProgressUnit == "production_batches" && (order.QuantityIsExplicit || order.RequestedUnits == 1)) &&
            (order.ProductionBuildingId is null ? order.ProductionProjectStartedTick is null && order.ProductionJobId is null :
                WorldRuntime.IsValidProductionBindingId(order.ProductionBuildingId) && order.ProductionProjectStartedTick is >= 0 &&
                order.ProductionProjectStartedTick <= worldTick &&
                (order.ProductionJobId is null || WorldRuntime.IsValidProductionBindingId(order.ProductionJobId))) &&
            (order.CompletedUnits == 0 ? order.LastEffectId is null : WorldRuntime.IsValidProductionReceipt(order.LastEffectId));
    }
}

public abstract record TreeOrderFamilyDefinition : OrderTaskDefinition
{
    internal override bool IsValid(OrderTask order, OwnerQueuedInstruction instruction, HashSet<string> people,
        long worldTick, DeclarativeWorldContentState worldContent)
    {
        return order.TargetAgentId is null && order.TargetFoodKind is null && order.TargetResourceId is null &&
                        order.RequestedUnits is >= 1 and <= 1000 && order.CompletedUnits is >= 0 and <= 1_000_000 &&
                        (order.QuantityIsExplicit || order.RequestedUnits == 1) && order.ProgressUnit == "trees" &&
                        (order.RepeatUntilCancelled || order.CompletedUnits <= order.RequestedUnits) &&
                        order.Status != "not_understood" &&
                        (order.Status == "finished") == (!order.RepeatUntilCancelled && order.CompletedUnits >= order.RequestedUnits) &&
                        (order.CompletedUnits == 0 ? order.LastEffectId is null :
                            WorldRuntime.IsValidTreePlantingReceipt(order.LastEffectId, order.TargetPosition));
    }
}

public abstract record FieldOrderFamilyDefinition : OrderTaskDefinition
{
    internal override bool IsValid(OrderTask order, OwnerQueuedInstruction instruction, HashSet<string> people,
        long worldTick, DeclarativeWorldContentState worldContent)
    {
        return (order.Action == "till_field" ? order.TargetCropKind is null :
                            order.TargetCropKind is null ? order.Action != "plant_field" : FarmFieldRules.IsCrop(order.TargetCropKind)) &&
                        order.TargetAgentId is null && order.TargetFoodKind is null && order.TargetResourceId is null &&
                        order.RequestedUnits is >= 1 and <= 1000 && order.CompletedUnits is >= 0 and <= 1_000_000 &&
                        (order.QuantityIsExplicit || order.RequestedUnits == 1) && order.ProgressUnit == "fields" &&
                        (order.RepeatUntilCancelled || order.CompletedUnits <= order.RequestedUnits) &&
                        order.Status != "not_understood" &&
                        (order.Status == "finished") == (!order.RepeatUntilCancelled && order.CompletedUnits >= order.RequestedUnits) &&
                        (order.CompletedUnits == 0 ? order.LastEffectId is null : order.LastEffectId?.StartsWith("field:work:", StringComparison.Ordinal) == true);
    }
}

public abstract record RepairOrderFamilyDefinition : OrderTaskDefinition
{
    internal override bool IsValid(OrderTask order, OwnerQueuedInstruction instruction, HashSet<string> people,
        long worldTick, DeclarativeWorldContentState worldContent)
    {
        return (order.Action == "repair_tool" ? OrderTaskParser.IsToolKind(order.TargetEquipmentKind) :
                            OrderTaskParser.IsEquipmentKind(order.TargetEquipmentKind)) &&
                        order.TargetAgentId is null && order.TargetFoodKind is null && order.TargetResourceId is null && order.TargetPosition is null &&
                        order.RequestedUnits is >= 1 and <= 1000 && order.CompletedUnits is >= 0 and <= 1_000_000 &&
                        (order.QuantityIsExplicit || order.RequestedUnits == 1) && order.ProgressUnit == "repairs" &&
                        (order.RepeatUntilCancelled || order.CompletedUnits <= order.RequestedUnits) &&
                        order.Status != "not_understood" &&
                        (order.Status == "finished") == (!order.RepeatUntilCancelled && order.CompletedUnits >= order.RequestedUnits) &&
                        (order.CompletedUnits == 0 ? order.LastEffectId is null : order.LastEffectId?.StartsWith(order.Action == "repair_tool" ? "repair:tool:" : "repair:equipment:", StringComparison.Ordinal) == true);
    }
}

public abstract record CollectionOrderFamilyDefinition : OrderTaskDefinition
{
    internal override bool IsValid(OrderTask order, OwnerQueuedInstruction instruction, HashSet<string> people,
        long worldTick, DeclarativeWorldContentState worldContent)
    {
        return (order.Action switch
        {
            "collect_material" => OrderTaskParser.IsMaterialKind(order.TargetMaterialKind) && order.TargetFoodKind is null,
            "collect_equipment" => (OrderTaskParser.IsEquipmentKind(order.TargetEquipmentKind) ||
                OrderTaskParser.IsToolKind(order.TargetEquipmentKind)) && order.TargetFoodKind is null,
            _ => order.TargetMaterialKind is null,
        }) && order.TargetAgentId is null && order.TargetResourceId is null &&
                        order.RequestedUnits is >= 1 and <= 1000 && order.CompletedUnits is >= 0 and <= 1_000_000 &&
                        (order.RepeatUntilCancelled || order.CompletedUnits <= order.RequestedUnits) &&
                        order.Status != "not_understood" &&
                        (order.Status == "finished") == (!order.RepeatUntilCancelled && order.CompletedUnits >= order.RequestedUnits) &&
                        (order.QuantityIsExplicit ? order.ProgressUnit == (order.Action switch
                        {
                            "collect_food" => "food_items",
                            "collect_equipment" => "equipment_items",
                            _ => "material_items",
                        }) : order.ProgressUnit == "collection_loads" && order.RequestedUnits == 1) &&
                        (order.CompletedUnits == 0 ? order.LastEffectId is null : order.LastEffectId?.StartsWith("collect:personal:", StringComparison.Ordinal) == true);
    }
}

public abstract record StorageOrderFamilyDefinition : OrderTaskDefinition
{
    internal override bool IsValid(OrderTask order, OwnerQueuedInstruction instruction, HashSet<string> people,
        long worldTick, DeclarativeWorldContentState worldContent)
    {
        return (order.Action == "store_equipment"
                        ? OrderTaskParser.IsEquipmentKind(order.TargetEquipmentKind) ||
                            OrderTaskParser.IsToolKind(order.TargetEquipmentKind)
                        : OrderTaskParser.IsMaterialKind(order.TargetMaterialKind)) &&
                        order.TargetAgentId is null && order.TargetFoodKind is null && order.TargetResourceId is null &&
                        order.RequestedUnits is >= 1 and <= 1000 && order.CompletedUnits is >= 0 and <= 1_000_000 &&
                        (order.RepeatUntilCancelled || order.CompletedUnits <= order.RequestedUnits) &&
                        order.Status != "not_understood" &&
                        (order.Status == "finished") == (!order.RepeatUntilCancelled && order.CompletedUnits >= order.RequestedUnits) &&
                        (order.QuantityIsExplicit ? order.ProgressUnit == (order.Action == "store_equipment" ? "equipment_items" : "material_items")
                            : order.ProgressUnit == "storage_loads" && order.RequestedUnits == 1) &&
                        (order.CompletedUnits == 0 ? order.LastEffectId is null : order.LastEffectId?.StartsWith("store:personal:", StringComparison.Ordinal) == true);
    }
}

public abstract record BoatOrderFamilyDefinition : OrderTaskDefinition
{
    internal override bool IsValid(OrderTask order, OwnerQueuedInstruction instruction, HashSet<string> people,
        long worldTick, DeclarativeWorldContentState worldContent)
    {
        return WorldRuntime.IsValidBoatOrderShape(order);
    }
}

public abstract record UnknownOrderFamilyDefinition : OrderTaskDefinition
{
    internal override bool IsValid(OrderTask order, OwnerQueuedInstruction instruction, HashSet<string> people,
        long worldTick, DeclarativeWorldContentState worldContent)
    {
        return order.Status == "not_understood" && order.RequestedUnits == 0 && order.CompletedUnits == 0 &&
                        order.ProgressUnit == "none" && !order.RepeatUntilCancelled && order.TargetFoodKind is null &&
                        order.TargetResourceId is null && order.TargetPosition is null && order.LastEffectId is null &&
                        order.TargetAgentId is null;
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ConsumeFoodOrderDefinition([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetFoodKind = null) : FoodOrderFamilyDefinition
{
    public override string Action => "consume_food";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SeekFoodOrderDefinition([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetFoodKind = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetResourceId = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GridPoint? TargetPosition = null) : FoodOrderFamilyDefinition
{
    public override string Action => "seek_food";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HarvestFoodOrderDefinition([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetFoodKind = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetResourceId = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GridPoint? TargetPosition = null) : FoodOrderFamilyDefinition
{
    public override string Action => "harvest_food";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record GatherMaterialOrderDefinition(string TargetMaterialKind, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetResourceId = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GridPoint? TargetPosition = null) : MaterialOrderFamilyDefinition
{
    public override string Action => "gather_material";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MoveToOrderDefinition(GridPoint TargetPosition) : MoveOrderFamilyDefinition
{
    public override string Action => "move_to";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AcceptGuardianshipOrderDefinition(string TargetAgentId) : GuardianOrderFamilyDefinition
{
    public override string Action => "accept_guardianship";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TalkToOrderDefinition(string TargetAgentId, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TalkConversationId = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TalkOutcome = null) : TalkOrderFamilyDefinition
{
    public override string Action => "talk_to";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProposeMarriageOrderDefinition([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetAgentId = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TalkConversationId = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TalkOutcome = null) : MarriageOrderFamilyDefinition
{
    public override string Action => "propose_marriage";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReadKnowledgeOrderDefinition([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetItemKind = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetKnowledgeArtifactId = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] OwnerKnowledgeReadCompletion? KnowledgeReadCompletion = null) : KnowledgeReadOrderFamilyDefinition
{
    public override string Action => "read_knowledge";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WriteKnowledgeOrderDefinition(string TargetKnowledgeKind, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? KnowledgeWritingProjectId = null) : KnowledgeOrderFamilyDefinition
{
    public override string Action => "write_knowledge";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CopyKnowledgeOrderDefinition(string TargetKnowledgeKind, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? KnowledgeWritingProjectId = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? KnowledgeCopySourceArtifactId = null) : KnowledgeOrderFamilyDefinition
{
    public override string Action => "copy_knowledge";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AttachHandcartOrderDefinition([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetCartLotId = null) : CartOrderFamilyDefinition
{
    public override string Action => "attach_handcart";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ParkHandcartOrderDefinition([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetCartLotId = null) : CartOrderFamilyDefinition
{
    public override string Action => "park_handcart";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RepairHandcartOrderDefinition([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetCartLotId = null) : CartOrderFamilyDefinition
{
    public override string Action => "repair_handcart";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LoadHandcartOrderDefinition([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetItemKind = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetCartLotId = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetLotId = null) : CartCargoOrderFamilyDefinition
{
    public override string Action => "load_handcart";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UnloadHandcartOrderDefinition([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetItemKind = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetCartLotId = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetLotId = null) : CartCargoOrderFamilyDefinition
{
    public override string Action => "unload_handcart";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UnloadHandcartGroundOrderDefinition([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetItemKind = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetCartLotId = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetLotId = null) : CartCargoOrderFamilyDefinition
{
    public override string Action => "unload_handcart_ground";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AnimalCareOrderDefinition(string TargetAnimalId) : AnimalOrderFamilyDefinition
{
    public override string Action => "animal_care";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AnimalCollectOrderDefinition(string TargetAnimalId) : AnimalOrderFamilyDefinition
{
    public override string Action => "animal_collect";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AnimalTameOrderDefinition(string TargetAnimalId) : AnimalOrderFamilyDefinition
{
    public override string Action => "animal_tame";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AnimalLeadHomeOrderDefinition(string TargetAnimalId) : AnimalOrderFamilyDefinition
{
    public override string Action => "animal_lead_home";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AnimalSaddleOrderDefinition(string TargetAnimalId) : AnimalOrderFamilyDefinition
{
    public override string Action => "animal_saddle";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AnimalMountOrderDefinition(string TargetAnimalId) : AnimalOrderFamilyDefinition
{
    public override string Action => "animal_mount";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AnimalDismountOrderDefinition(string TargetAnimalId) : AnimalOrderFamilyDefinition
{
    public override string Action => "animal_dismount";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SeekShelterOrderDefinition([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GridPoint? TargetPosition = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetBuildingKind = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] OwnerShelterBinding? ShelterBinding = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] OwnerShelterCompletion? ShelterCompletion = null) : ShelterOrderFamilyDefinition
{
    public override string Action => "seek_shelter";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TendFireOrderDefinition([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GridPoint? TargetPosition = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetBuildingKind = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] OwnerShelterBinding? ShelterBinding = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] OwnerShelterCompletion? ShelterCompletion = null) : ShelterOrderFamilyDefinition
{
    public override string Action => "tend_fire";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ConstructBuildingOrderDefinition(string TargetDefinitionId, string TargetBuildingKind, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GridPoint? TargetPosition = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ConstructionOwnerId = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GridPoint? ConstructionPosition = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? ConstructionStartedTick = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ConstructionInstanceId = null) : BuildingOrderFamilyDefinition
{
    public override string Action => "construct_building";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ExpandBuildingOrderDefinition(string TargetBuildingKind, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GridPoint? TargetPosition = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] OwnerBuildingExpansionBinding? ExpansionBinding = null) : BuildingOrderFamilyDefinition
{
    public override string Action => "expand_building";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DeliverStockOrderDefinition(string TargetItemKind, string DeliveryPurpose, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GridPoint? TargetPosition = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetStorageBuildingId = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetStorageOwnerId = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GridPoint? TargetStoragePosition = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetBuildingKind = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DeliveryRoute = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DeliveryLotId = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? DeliveryQuantity = null) : DeliveryOrderFamilyDefinition
{
    public override string Action => "deliver_stock";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CollectGoodsOrderDefinition([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GridPoint? TargetPosition = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetItemKind = null) : GoodsOrderFamilyDefinition
{
    public override string Action => "collect_goods";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StoreGoodsOrderDefinition([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GridPoint? TargetPosition = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetItemKind = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetStorageBuildingId = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetStorageOwnerId = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GridPoint? TargetStoragePosition = null) : GoodsOrderFamilyDefinition
{
    public override string Action => "store_goods";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReturnBorrowedOrderDefinition([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GridPoint? TargetPosition = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetItemKind = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetStorageBuildingId = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetStorageOwnerId = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GridPoint? TargetStoragePosition = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetLotId = null) : GoodsOrderFamilyDefinition
{
    public override string Action => "return_borrowed";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProduceItemOrderDefinition(string TargetRecipeId, string TargetOutputKind, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GridPoint? TargetPosition = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ProductionBuildingId = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ProductionJobId = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? ProductionProjectStartedTick = null) : ProductionOrderFamilyDefinition
{
    public override string Action => "produce_item";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PlantTreeOrderDefinition([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GridPoint? TargetPosition = null) : TreeOrderFamilyDefinition
{
    public override string Action => "plant_tree";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PlantBroadleafOrderDefinition([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GridPoint? TargetPosition = null) : TreeOrderFamilyDefinition
{
    public override string Action => "plant_broadleaf";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PlantConiferOrderDefinition([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GridPoint? TargetPosition = null) : TreeOrderFamilyDefinition
{
    public override string Action => "plant_conifer";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PlantOrchardOrderDefinition([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GridPoint? TargetPosition = null) : TreeOrderFamilyDefinition
{
    public override string Action => "plant_orchard";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TillFieldOrderDefinition([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GridPoint? TargetPosition = null) : FieldOrderFamilyDefinition
{
    public override string Action => "till_field";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PlantFieldOrderDefinition(string TargetCropKind, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GridPoint? TargetPosition = null) : FieldOrderFamilyDefinition
{
    public override string Action => "plant_field";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TendFieldOrderDefinition([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GridPoint? TargetPosition = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetCropKind = null) : FieldOrderFamilyDefinition
{
    public override string Action => "tend_field";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HarvestFieldOrderDefinition([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GridPoint? TargetPosition = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetCropKind = null) : FieldOrderFamilyDefinition
{
    public override string Action => "harvest_field";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RepairEquipmentOrderDefinition(string TargetEquipmentKind) : RepairOrderFamilyDefinition
{
    public override string Action => "repair_equipment";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RepairToolOrderDefinition(string TargetEquipmentKind) : RepairOrderFamilyDefinition
{
    public override string Action => "repair_tool";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CollectMaterialOrderDefinition(string TargetMaterialKind, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GridPoint? TargetPosition = null) : CollectionOrderFamilyDefinition
{
    public override string Action => "collect_material";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CollectEquipmentOrderDefinition(string TargetEquipmentKind, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GridPoint? TargetPosition = null) : CollectionOrderFamilyDefinition
{
    public override string Action => "collect_equipment";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CollectFoodOrderDefinition([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetFoodKind = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GridPoint? TargetPosition = null) : CollectionOrderFamilyDefinition
{
    public override string Action => "collect_food";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StoreMaterialOrderDefinition(string TargetMaterialKind, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GridPoint? TargetPosition = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetStorageBuildingId = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetStorageOwnerId = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GridPoint? TargetStoragePosition = null) : StorageOrderFamilyDefinition
{
    public override string Action => "store_material";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StoreEquipmentOrderDefinition(string TargetEquipmentKind, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GridPoint? TargetPosition = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetStorageBuildingId = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetStorageOwnerId = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GridPoint? TargetStoragePosition = null) : StorageOrderFamilyDefinition
{
    public override string Action => "store_equipment";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TravelByBoatOrderDefinition(OwnerBoatTravelBinding BoatTravel, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GridPoint? TargetPosition = null) : BoatOrderFamilyDefinition
{
    public override string Action => "travel_by_boat";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UnknownOrderDefinition : UnknownOrderFamilyDefinition
{
    public override string Action => "unknown";
}

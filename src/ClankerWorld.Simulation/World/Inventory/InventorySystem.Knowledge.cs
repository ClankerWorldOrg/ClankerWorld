using System.Collections.ObjectModel;
using System.Globalization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal static partial class InventorySystem
{
    internal static Command ApplyCheckedKnowledgeWriting(InventoryCheckpoint inventory) => new PreparedCommand(_ => inventory);

    internal static Command CollectKnowledgeMaterial(IInventoryKnowledgeWorld commandContext, string actor, string kind, InventoryLot item, int quantity) => new PreparedCommand(inventory => InventoryAuthority.Transfer(inventory, commandContext.WritingMaterialTransferId(commandContext.WorldTick, actor, kind), item.OwnerId, actor, item.Id, quantity, "knowledge_material_collected", holdAccess: InventoryHoldRules.BorrowedBy(actor)));

    internal static Command ContinueKnowledgeWriting(InventoryCheckpoint inventory) => new PreparedCommand(_ => inventory);

    internal static Command CancelKnowledgeWriting(AgentKnowledgeWritingProject project) => new PreparedCommand(inventory => project.Materials.Aggregate(inventory, (current, input) => current.Reservations.Any(item => item.Id == input.ReservationId) ? InventoryAuthority.ReleaseReservation(current, input.ReservationId, "knowledge_writing_cancelled") : current));
}

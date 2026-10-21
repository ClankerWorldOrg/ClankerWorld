namespace ClankerWorld.Simulation.World;

internal sealed partial class OrderStore
{
    internal void CloseUnrecognizedOrder(string key, OwnerQueuedInstruction instruction) => Commit(key, instruction);
    internal void RecordBoatOrder(string key, OwnerQueuedInstruction instruction) => Commit(key, instruction);
    internal void RecordCancellation(string key, OwnerQueuedInstruction instruction) => Commit(key, instruction);
    internal void RecordCartCargoOrderStep(string key, OwnerQueuedInstruction instruction) => Commit(key, instruction);
    internal void RecordCartOrderStep(string key, OwnerQueuedInstruction instruction) => Commit(key, instruction);
    internal void RecordClearKnowledgeOrderBinding(string key, OwnerQueuedInstruction instruction) => Commit(key, instruction);
    internal void RecordCompleteClosedTalkOrders(string key, OwnerQueuedInstruction instruction) => Commit(key, instruction);
    internal void RecordConstructionOrderStep(string key, OwnerQueuedInstruction instruction) => Commit(key, instruction);
    internal void RecordCreditKnowledgeOrderCompletion(string key, OwnerQueuedInstruction instruction) => Commit(key, instruction);
    internal void RecordCustodyOrder(string key, OwnerQueuedInstruction instruction) => Commit(key, instruction);
    internal void RecordDeliveryOrder(string key, OwnerQueuedInstruction instruction) => Commit(key, instruction);
    internal void RecordDeliveryOrderStep(string key, OwnerQueuedInstruction instruction) => Commit(key, instruction);
    internal void RecordExpansionOrderStep(string key, OwnerQueuedInstruction instruction) => Commit(key, instruction);
    internal void RecordGuidancePrompt(string key, OwnerQueuedInstruction instruction) => Commit(key, instruction);
    internal void RecordKnowledgeOrderStep(string key, OwnerQueuedInstruction instruction) => Commit(key, instruction);
    internal void RecordKnowledgeReadOrder(string key, OwnerQueuedInstruction instruction) => Commit(key, instruction);
    internal void RecordObserverReply(string key, OwnerQueuedInstruction instruction) => Commit(key, instruction);
    internal void RecordProductionOrderStep(string key, OwnerQueuedInstruction instruction) => Commit(key, instruction);
    internal void RecordProgress(string key, OwnerQueuedInstruction instruction) => Commit(key, instruction);
    internal void RecordReturnOrderStep(string key, OwnerQueuedInstruction instruction) => Commit(key, instruction);
    internal void RecordShelterOrderStep(string key, OwnerQueuedInstruction instruction) => Commit(key, instruction);
    internal void RecordStartedExpansionOrder(string key, OwnerQueuedInstruction instruction) => Commit(key, instruction);
    internal void RecordStatus(string key, OwnerQueuedInstruction instruction) => Commit(key, instruction);
    internal void RecordTalkOrderStep(string key, OwnerQueuedInstruction instruction) => Commit(key, instruction);
}

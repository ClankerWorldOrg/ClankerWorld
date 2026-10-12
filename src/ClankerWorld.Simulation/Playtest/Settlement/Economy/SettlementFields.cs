using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Simulation.Playtest;

public sealed partial class PrivateWorldRuntime
{
    public IReadOnlyList<FarmFieldState> Fields => fields.ToArray();

    public FarmWorkResult StartFieldWork(string workerId, GridPoint position, FarmWorkKind kind,
        string? crop = null, string? seedLotId = null)
    {
        gate.Wait();
        try { return StartFieldWorkCore(workerId, position, kind, crop, seedLotId); }
        finally { gate.Release(); }
    }

    private FarmWorkResult StartFieldWorkCore(string workerId, GridPoint position, FarmWorkKind kind,
        string? crop = null, string? seedLotId = null, string? orderInstructionId = null) =>
        farmingSystem.StartFieldWorkCore(farmingWorld, workerId, position, kind, crop, seedLotId, orderInstructionId);

    private FarmFieldState? FarmWorkFor(string workerId) =>
        farmingSystem.FarmWorkFor(farmingWorld, workerId);

    private string IsolateFieldToolForWork(string workerId, GridPoint position, string lotId) =>
        FarmingSystem.IsolateFieldToolForWork(farmingWorld, workerId, position, lotId);

    private bool FarmableFreeTile(GridPoint position) =>
        farmingSystem.FarmableFreeTile(farmingWorld, position);

    private void SetFarmField(FarmFieldState field) =>
        farmingSystem.SetFarmField(farmingWorld, field);

    private void MaintainFarmFields() =>
        farmingSystem.MaintainFarmFields(farmingWorld);

    private void ReturnFieldToGrass(FarmFieldState field) =>
        farmingSystem.ReturnFieldToGrass(farmingWorld, field);

    private bool ActiveFarmReservation(string id) =>
        FarmingSystem.ActiveFarmReservation(farmingWorld, id);

    private void CancelFarmWork(FarmFieldState field) =>
        farmingSystem.CancelFarmWork(farmingWorld, field);

    private void CancelFieldWorkForUnavailableWorkers() =>
        farmingSystem.CancelFieldWorkForUnavailableWorkers(farmingWorld);

    private bool ContinueFarmWork(string workerId) =>
        farmingSystem.ContinueFarmWork(farmingWorld, workerId);

    private bool ContinueFarmWork(string workerId, out FarmFieldState? completed) =>
        farmingSystem.ContinueFarmWork(farmingWorld, workerId, out completed);

    private bool FieldWorkToolsAvailable(string workerId, FarmFieldWork work) =>
        FarmingSystem.FieldWorkToolsAvailable(farmingWorld, workerId, work);

    private List<ToolWorkPlan> FieldWorkToolPlans(string workerId, FarmFieldWork work) =>
        FarmingSystem.FieldWorkToolPlans(farmingWorld, workerId, work);

    private void CompleteFieldHarvest(FarmFieldState field, IReadOnlyList<ToolWorkPlan> toolPlans) =>
        farmingSystem.CompleteFieldHarvest(farmingWorld, field, toolPlans);

}

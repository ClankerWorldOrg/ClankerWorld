
namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
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

    private void MaintainFarmFields() =>
        farmingSystem.MaintainFarmFields(farmingWorld);

    private void CancelFarmWork(FarmFieldState field) =>
        farmingSystem.CancelFarmWork(farmingWorld, field);

    private void CancelFieldWorkForUnavailableWorkers() =>
        farmingSystem.CancelFieldWorkForUnavailableWorkers(farmingWorld);

    private bool ContinueFarmWork(string workerId) =>
        farmingSystem.ContinueFarmWork(farmingWorld, workerId);

    private bool ContinueFarmWork(string workerId, out FarmFieldState? completed) =>
        farmingSystem.ContinueFarmWork(farmingWorld, workerId, out completed);

}

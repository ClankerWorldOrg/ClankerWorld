
namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    public IReadOnlyList<FarmFieldState> Fields => fields.ToArray();

    public FarmWorkResult StartFieldWork(string workerId, GridPoint position, FarmWorkKind kind,
        string? crop = null, string? seedLotId = null)
    {
        gate.Wait();
        try { return this.farmingSystem.StartFieldWorkCore(this.farmingWorld, workerId, position, kind, crop, seedLotId, null); }
        finally { gate.Release(); }
    }

}

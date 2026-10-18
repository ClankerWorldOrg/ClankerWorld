namespace ClankerWorld.Contracts;

public sealed record HostStatus(int ConnectedDevices, bool SaveFailed, bool RecoveryPending,
    string Kind, bool StopWhenGameQuits);

using System.Collections.ObjectModel;

namespace ClankerWorld.Simulation.Playtest;

internal sealed partial class CivicSystem(CivicState state)
{
    internal CivicState State { get; private set; } = state;

    private SettlementCouncil? council
    {
        get => State.Council;
        set => RestoreCouncil(value);
    }

    internal void RestoreCouncil(SettlementCouncil? value)
    {
        if (!ReferenceEquals(State.Council, value)) State = State with { Council = value };
    }

    internal void RestoreTowns(IEnumerable<TownRuntimeState> towns) => State = State with
    {
        Towns = new(towns.ToDictionary(town => town.Id, TownCivicState.From, StringComparer.Ordinal)),
    };

    // The coordinator's full Town record is the existing checkpoint view.
    // Civic fields remain immutable snapshots owned by this system; legacy
    // Town writes import their civic portion through the central SetTown path.
    internal void SetTownState(TownRuntimeState town)
    {
        if (State.Towns.TryGetValue(town.Id, out var previous) && previous.Matches(town)) return;
        var updated = new Dictionary<string, TownCivicState>(State.Towns, StringComparer.Ordinal)
        {
            [town.Id] = TownCivicState.From(town),
        };
        State = State with { Towns = new ReadOnlyDictionary<string, TownCivicState>(updated) };
    }

    private void SetTown(ICivicTownWorld world, TownRuntimeState town)
    {
        SetTownState(town);
        world.SetTown(town);
    }

    internal TownRuntimeState CheckpointTown(TownRuntimeState town) => State.Towns.TryGetValue(town.Id, out var owned)
        ? owned.ApplyTo(town) : town;
}

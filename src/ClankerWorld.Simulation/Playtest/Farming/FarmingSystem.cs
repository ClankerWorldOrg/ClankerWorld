using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Simulation.Playtest;

internal sealed partial class FarmingSystem
{
    private readonly Derived<SeededMap, LandFertility> fertility;

    internal FarmingSystem(FarmingState state, string seed)
        : this(state, new Derived<SeededMap, LandFertility>(snapshot => new LandFertility(snapshot, seed), (left, right) => left.HasSameValues(right)))
    { }

    private FarmingSystem(FarmingState state, Derived<SeededMap, LandFertility> fertility)
    {
        State = state;
        this.fertility = fertility;
    }

    internal FarmingState State { get; private set; }
    private IReadOnlyList<FarmFieldState> fields => State.Fields;

    internal FarmingSystem Fork() => new(State, fertility);
    internal LandFertility Fertility(SeededMap map) => fertility.Get(map);

    private void RemoveField(GridPoint position) => State = new(fields.Where(field => field.Position != position));
}

using System.Collections.ObjectModel;

namespace ClankerWorld.Simulation.World;

public enum ReachPurpose { ChildSocial, ChildHelping, HouseholdReturn, TownWarehouse }
public enum SwimmingPolicy { WhenCapable, Never }

public sealed partial class WorldRuntime
{
    // The first leg always starts at the current actor position. A return leg
    // names its prospective origin separately instead of using a camp origin.
    private ReadOnlyCollection<GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose,
        int range, int load, SwimmingPolicy swimming) =>
        ReachLeg(actor, agents[actor].Position, target, purpose, range, load, swimming);

    private ReadOnlyCollection<GridPoint> ReachLeg(string actor, GridPoint origin, GridPoint target,
        ReachPurpose purpose, int range, int load, SwimmingPolicy swimming) =>
        FindUnoccupiedRoute(actor, origin, target, range,
            allowSwimming: swimming == SwimmingPolicy.WhenCapable, additionalCarriedUnits: load).AsReadOnly();
}

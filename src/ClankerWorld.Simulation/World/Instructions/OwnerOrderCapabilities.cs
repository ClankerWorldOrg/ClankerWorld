namespace ClankerWorld.Simulation.World;

internal static class OwnerOrderCapabilities
{
    // Command names are the order contract, including the Action projection on
    // typed orders. Native choice spelling does not determine an order's policy.
    internal static ActorCapabilityPolicy For(string action) => new(action switch
    {
        "consume_food" or "seek_food" or "harvest_food" => ActionLifeStages.NonInfants,
        "move_to" or "talk_to" or "seek_shelter" => ActionLifeStages.NonInfants,
        _ => ActionLifeStages.Adults,
    }, AllowBoat: true, AllowMounted: true, AllowUrgentFood: true,
        AllowUrgentWarmth: true, AllowActiveOrder: true);
}

namespace ClankerWorld.Simulation.World;

internal sealed partial class OrderSystem
{
    internal static bool IsFieldOrder(string action) => action is "till_field" or "plant_field" or "tend_field" or "harvest_field";
    internal static bool IsKnowledgeOrder(string action) => action is "write_knowledge" or "copy_knowledge";
    internal static bool IsShelterOrder(string action) => action is "seek_shelter" or "tend_fire";
    internal static bool IsConversationOrder(string action) => action is "talk_to" or "propose_marriage";
    internal static bool IsTreePlantingOrder(string action) =>
        action is "plant_tree" or "plant_broadleaf" or "plant_conifer" or "plant_orchard";
}

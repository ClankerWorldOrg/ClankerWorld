using System.Collections;
using System.Reflection;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class AgentRegistrationOrderTests
{
    // These are main's pre-registration lists, including repeated Market calls
    // and the routes that run before age/repair/farm-work checks.
    [Fact]
    public void ChoiceSourcesKeepTheOriginalPhaseAndEnumerationOrder()
    {
        string[] expected =
        [
            "Prelude|food",
            "Prelude|handcarts",
            "Prelude|animals",
            "Prelude|survival",
            "Prelude|town_salvage",
            "Prelude|dependent_care",
            "Prelude|guardian_placement",
            "Prelude|medical",
            "Adult|AddKnowledgeCandidates",
            "Adult|AddFamilyCandidates",
            "Adult|AddHousingCandidates",
            "Adult|AddParenthoodCandidates",
            "Adult|AddRecoverHouseholdDeliveryCandidate",
            "Adult|AddUrgentFoodPotCandidate",
            "Adult|AddShopCandidates",
            "Adult|AddToolMakingRequestCandidates",
            "Child|AddChildCandidates",
            "Child|AddChildNonLandCandidates",
            "UnhurriedAdult|AddBuildCandidates",
            "UnhurriedAdult|AddBuildingExpansionCandidates",
            "UnhurriedAdult|AddHouseGuestCandidates",
            "UnhurriedAdult|AddHouseHaulCandidate",
            "UnhurriedAdult|AddWarehouseStockCandidate",
            "UnhurriedAdult|AddFarmGrainCandidate",
            "UnhurriedAdult|AddFarmFlourCandidate",
            "UnhurriedAdult|AddFieldCandidates",
            "UnhurriedAdult|AddBlacksmithStockCandidate",
            "UnhurriedAdult|AddBlacksmithOreCandidates",
            "UnhurriedAdult|AddWorkstationSupplyCandidate",
            "UnhurriedAdult|AddContainerCandidates",
            "UnhurriedAdult|AddCraftToolCandidates",
            "UnhurriedAdult|AddRareMiningCandidates",
            "UnhurriedAdult|AddProjectAssistanceCandidates",
            "UnhurriedAdult|AddTreePlantingCandidates",
            "UnhurriedAdult|AddTradeCandidates",
            "UnhurriedAdult|AddFoodPolicyCandidates",
            "UnhurriedAdult|AddTownCivicCandidates",
            "UnhurriedAdult|AddTownProjectDonationCandidates",
            "UnhurriedAdult|AddMarketCandidates",
            "UnhurriedAdult|AddBoatCandidates",
            "UnhurriedAdult|AddLearningCandidates",
            "UnhurriedAdult|AddExplorationCandidate",
            "UrgentAdult|urgent_market",
            "Final|town_projects",
            "Final|conversations",
            "Final|safe_idle",
        ];
        Assert.Equal(expected, Registrations("ChoiceRegistrations").Select(item =>
            Property(item, "Group") + "|" + Property(item, "Name")));
    }

    [Fact]
    public void ActionRoutesKeepTheOriginalPhaseOrderAndOrdinalPrefixesAndExactIds()
    {
        string[] expected =
        [
            "BeforeAge|safe_idle_age_outcome||safe_idle",
            "BeforeAge|IsOrnamentCandidate||",
            "BeforeAge|ToolRequestPrefix|tool_request_|",
            "BeforeAge|talk:|talk:,conversation_|",
            "BeforeAge|TryApplyGuardianPlacementCandidate||",
            "BeforeAge|guardian_end:|guardian_end:|",
            "Ordinary|safe_idle||safe_idle",
            "Ordinary|medical_|medical_|",
            "Ordinary|farm:|farm:|",
            "Ordinary|child_|child_|",
            "Ordinary|guardian_|guardian_|",
            "Ordinary|parent_|parent_,care:|",
            "Ordinary|partner_|partner_|",
            "Ordinary|household_|household_|",
            "Ordinary|learn:|learn:,lesson_|",
            "Ordinary|civic||civic||",
            "Ordinary|IsTownProjectDonationCandidate||",
            "Ordinary|ApplyTownProjectCandidate||",
            "Ordinary|IsMarketCandidate||",
            "Ordinary|food_policy_|food_policy_|",
            "Ordinary|trade_|trade_|",
            "Ordinary|shop_stock_store||shop_stock_store",
            "Ordinary|shop_|shop_|",
            "Ordinary|haul_household_stock||haul_household_stock",
            "Ordinary|recover_household_delivery||recover_household_delivery",
            "Ordinary|store_household_food||store_household_food",
            "Ordinary|make_room_for_food||make_room_for_food",
            "Ordinary|store_town_resources||store_town_resources",
            "Ordinary|TownSalvagePrefix|town_salvage:|",
            "Ordinary|haul_farm_grain||haul_farm_grain",
            "Ordinary|haul_farm_flour||haul_farm_flour",
            "Ordinary|haul_smith_input||haul_smith_input",
            "Ordinary|gather_smith_ore||gather_smith_ore",
            "Ordinary|GatherBlacksmithInputPrefix|gather_smith_input:|",
            "Ordinary|deliver_smith_ore||deliver_smith_ore",
            "Ordinary|collect_wooden_axe||collect_wooden_axe,collect_wooden_pickaxe,collect_wooden_hoe",
            "Ordinary|CollectToolPrefix|collect_tool:|",
            "Ordinary|ApplyBoatCandidate||",
            "Ordinary|ApplyAnimalCandidate||",
            "Ordinary|ApplyHandcartCandidate||",
            "Ordinary|RepairToolPrefix|repair_tool:|",
            "Ordinary|RareMiningPrefix|gather_rare_material:|",
            "Ordinary|KnowledgeWritePrefix|knowledge_write:,knowledge_copy:|knowledge_continue",
            "Ordinary|KnowledgeReadPrefix|knowledge_read:|",
            "Ordinary|ApplyKnowledgeStorageCandidate||",
            "Ordinary|KnowledgeSharePrefix|knowledge_share:|",
            "Ordinary|SupplyWorkstationPrefix|supply_workstation:|",
            "Ordinary|ReturnEmptyVesselPrefix|return_empty_vessel:|",
            "Ordinary|collect_water_jug||collect_water_jug",
            "Ordinary|return_water_jug||return_water_jug",
            "Ordinary|store_food_in_pot||store_food_in_pot",
            "Ordinary|take_food_from_pot||take_food_from_pot",
            "Ordinary|FillWaterJugPrefix|fill_water_jug:|",
            "Ordinary|GatherBuildingMaterialPrefix|gather_building_material:|",
            "Ordinary|ExpandBuildingPrefix|expand_building:|",
            "Ordinary|InviteHouseGuestPrefix|invite_house_guest:,revoke_house_guest:|",
            "Ordinary|recipe||",
            "Ordinary|build:|build:|",
            "Ordinary|assist:|assist:|",
            "Ordinary|replant_tree||replant_tree",
            "Ordinary|plant_tree||plant_tree,plant_orchard",
            "Ordinary|ExplorationGoalPrefix|explore_for:|",
            "Ordinary|explore||explore",
            "Ordinary|explore_return||explore_return",
            "Ordinary|wear_clothing||wear_clothing",
            "Ordinary|equip_carry_aid||equip_carry_aid",
            "Ordinary|repair_equipment||repair_equipment",
            "Ordinary|tend_fire||tend_fire",
            "Ordinary|seek_warmth||seek_warmth",
            "Ordinary|seek_food||seek_food",
            "Ordinary|harvest_food||harvest_food",
            "Ordinary|consume_food||consume_food",
            "Ordinary|collect_shared_food||collect_shared_food",
        ];
        Assert.Equal(expected, Registrations("ActionRegistrations").Select(item =>
            Property(item, "Phase") + "|" + Property(item, "Name") + "|" +
            string.Join(",", Assert.IsType<string[]>(Property(item, "Prefixes"))) + "|" +
            string.Join(",", Assert.IsType<string[]>(Property(item, "ExactIds")))));
    }

    private static IEnumerable<object> Registrations(string field) =>
        Assert.IsAssignableFrom<IEnumerable>(typeof(WorldRuntime)
            .GetField(field, BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)).Cast<object>();

    private static object Property(object registration, string property) =>
        registration.GetType().GetProperty(property)!.GetValue(registration)!;
}

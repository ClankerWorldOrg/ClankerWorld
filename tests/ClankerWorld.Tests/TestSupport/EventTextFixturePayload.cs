using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ClankerWorld.GodotClient.Protocol;
using ClankerWorld.Server.Observation;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

// Converts the pre-refactor golden examples into the new contract. This is
// fixture input only; saves and live observations never use this parser.
internal static class EventTextFixturePayload
{
    private static readonly Dictionary<string, string> Templates = new(StringComparer.Ordinal)
    {
        ["tool_repaired"] = "{ActorId}:{RepairId}:{BlacksmithInstanceId}",
        ["milk_drunk"] = "{ActorId}",
        ["animal_tamed"] = "{ActorId}:{AnimalId}",
        ["animal_cared"] = "{ActorId}:{AnimalId}",
        ["animal_hide_collected"] = "{ActorId}:{AnimalId}",
        ["horse_mounted"] = "{ActorId}:{AnimalId}",
        ["animal_product_collected"] = "{ActorId}:{AnimalId}:{ItemKind}:{Quantity}",
        ["animal_permission_changed"] = "{ActorId}:{AnimalId}:{Action}:{OtherId}",
        ["animal_trade_offered"] = "{OfferId}:{AnimalId}:{PaymentId}",
        ["animal_transferred"] = "{OfferId}:{AnimalId}:{ReceivingHouseholdId}",
        ["animal_yard_supplied"] = "{ActorId}:{YardInstanceId}",
        ["animal_saddled"] = "{ActorId}:{AnimalId}",
        ["horse_dismounted"] = "{ActorId}:{AnimalId}:{Reason}",
        ["animals_arrived"] = "wild:{AnimalCount}",
        ["animal_died"] = "{AnimalId}:old_age",
        ["animal_product_ready"] = "{AnimalId}:{Product}",
        ["animal_born"] = "{ChildId}:{AnimalId}",
        ["animal_breeding_started"] = "{AnimalId}:{MaleId}",
        ["town_civic_action"] = "{TownId}|{ActorId}|{Action}",
        ["town_civic_action_rejected"] = "{TownId}|{ActorId}|{Action}",
        ["town_founding_started"] = "{FirstTownId}",
        ["food_steward_changed"] = "{Successor}",
        ["food_policy_adopted"] = "{Policy}",
        ["food_policy_rejected"] = "{Policy}",
        ["food_policy_proposed"] = "{Policy}",
        ["food_policy_vote_recorded"] = "{ActorId}",
        ["land_use_granted"] = "{RequestId}:{TownId}:{HouseholdId}:{TilesCount}",
        ["voluntary_goods_returned"] = "{ActorId}:{BeneficiaryId}:{LotId}:{Remaining}",
        ["town_resources_stored"] = "{ActorId}:{LotId}:{Quantity}:{WarehouseInstanceId}",
        ["law_case_remedy_effect"] = "{TownId}|{AgreementId}|{ActorId}",
        ["town_resettled"] = "{TownId}|{ActorId}|{PreviousTownId}|{MemberCount}",
        ["town_stock_salvaged"] = "{TownId}|{ActorId}|{ItemKind}|{Quantity}",
        ["town_land_claimed"] = "{TownId}|{ProposalId}|{Tiles}",
        ["land_use_requested"] = "{RequestId}:{TownId}:{HouseholdId}:{TilesLength}",
        ["town_admission_approved"] = "{TownId}|{SubjectId}|{ProposalId}",
        ["town_admission_accepted"] = "{TownId}|{SubjectId}|{PreviousTownId}|{MemberCount}",
        ["town_admission_lapsed"] = "{TownId}|{SubjectId}|{ProposalId}|{Reason}",
        ["founder_placed"] = "{ActorId}:{HouseholdId}",
        ["founder_moved"] = "{ActorId}:{PositionX},{PositionY}->{PositionXValue},{PositionYValue}",
        ["founder_placement_undone"] = "{ActorId}",
        ["town_membership_evaluated"] = "{ActorId}:unaffiliated",
        ["agent_added"] = "{ActorId}",
        ["world_started"] = "four_founders_ready",
        ["town_resident_joined"] = "{TownId}:{ResidentId}:{Reason}:residents:{ResidentIdsCount}",
        ["town_resident_left"] = "{TownId}:{ResidentId}:residents:{ResidentsLength}",
        ["town_building_assigned"] = "{TownId}:{BuildingInstanceId}:buildings:{AssignedBuildingIdsCount}",
        ["town_border_expanded"] = "{TownId}:{BuildingInstanceId}:tiles:{BorderCount}",
        ["town_abandoned"] = "{TownId}",
        ["town_revived"] = "{TownId}",
        ["borrowed_goods_returned"] = "{ActorId}|{ItemKind}|{Quantity}|{OwnerId}",
        ["household_left"] = "{ActorId}|{HouseholdId}|{Cause}|{Allowance}",
        ["household_work_resumed"] = "{ActorId}|{JobId}|{HouseholdId}",
        ["household_founded"] = "{ActorId}|{HouseholdId}",
        ["replacement_care_accepted"] = "{ActorId}|{ChildId}",
        ["housing_request_made"] = "{ActorId}:{HouseholdId}",
        ["housing_answer_recorded"] = "{ActorId}:{ApplicantId}",
        ["housing_blocked"] = "{ActorId}:{Blocker}",
        ["household_joined"] = "{ActorId}:{HouseholdId}",
        ["personal_goods_collected"] = "{ActorId}|{ItemKind}|{Quantity}|{OwnerId}",
        ["personal_goods_stored"] = "{ActorId}|{ItemKind}|{Quantity}|{ActorId}",
        ["work_practice_earned"] = "{ActorId}",
        ["relocation_notice"] = "{ActorId}|{HouseholdId}|{Reason}|{Deadline}",
        ["relocation_cancelled"] = "{ActorId}|{HouseholdId}|{Reason}",
        ["skill_learned"] = "{ActorId}|{Skill}|{TeacherId}",
        ["social_standing_changed"] = "{ActorId}:{SubjectId}:{Reason}",
        ["conversation_proposed"] = "{ConversationId}",
        ["conversation_accepted"] = "{ConversationId}",
        ["marriage_surname_blocked"] = "{ConversationId}",
        ["conversation_turn_admitted"] = "{ConversationId}:{TurnId}",
        ["conversation_action_rejected"] = "{ActorId}:{Action}",
        ["hearth_content_staged"] = "{PackageId}",
        ["house_content_staged"] = "{PackageId}",
        ["warehouse_content_staged"] = "{PackageId}",
        ["farm_content_staged"] = "{PackageId}",
        ["blacksmith_content_staged"] = "{PackageId}",
        ["house_cooking_content_staged"] = "{PackageId}",
        ["starting_resources_added"] = "{Resources}",
        ["project_fresh_choice_resumed"] = "{ActorId}:{CandidateId}",
        ["project_site_reselected"] = "{ActorId}:{DefinitionId}",
        ["project_chosen"] = "{ActorId}:{CandidateId}",
        ["town_layout_site_rejected"] = "{TownId}|{ActorId}|{DefinitionId}|{SelectedSiteX}|{SelectedSiteY}|site_unavailable",
        ["project_material_delivered"] = "{ActorId}:{ResourceId}",
        ["town_resource_collected"] = "{ActorId}:{CommunalId}:{Quantity}:{WarehouseInstanceId}",
        ["carrying_full"] = "{ActorId}",
        ["material_gathered"] = "{ActorId}:{ItemKind}:{Quantity}",
        ["tree_harvested"] = "{ActorId}:{SourceId}:{TreeKind}:stump",
        ["tree_seed_collected"] = "{ActorId}:{SourceId}:{TreeSeedQuantity}",
        ["project_request_fulfilled"] = "{ActorId}:{RequesterId}:{ItemKind}:{Quantity}",
        ["project_progress"] = "{ActorId}:{Stage}:{Blocker}",
        ["smith_ore_delivered"] = "{ActorId}:{OreId}:{Quantity}:{BlacksmithInstanceId}",
        ["smith_input_delivered"] = "{ActorId}:{InputId}:{PersonalQuantity}:{BlacksmithInstanceId}",
        ["smith_input_picked_up"] = "{ActorId}:{InputId}:{Quantity}:{BlacksmithInstanceId}",
        ["building_expansion_material_picked_up"] = "{ActorId}:{SharedId}:{Quantity}:{BuildingInstanceId}",
        ["building_expansion_started"] = "{BuildingId}:{JobId}",
        ["empty_vessel_picked_up"] = "{ActorId}:{VesselId}:{SourceInstanceId}:{HouseInstanceId}",
        ["water_jug_collected"] = "{ActorId}:{JugId}",
        ["water_jug_filled"] = "{ActorId}:{JugId}:{Remaining}:{ShoreX},{ShoreY}",
        ["water_jug_returned"] = "{ActorId}:{JugId}:{HouseInstanceId}",
        ["food_stored_in_pot"] = "{ActorId}:{PotId}:{FoodId}:{Quantity}",
        ["barter_offered"] = "{ActorId}:{OtherAgentId}",
        ["barter_declined"] = "{ActorId}",
        ["barter_completed"] = "{ActorId}",
        ["barter_cancelled"] = "{ActorId}",
        ["equipment_equipped"] = "{ActorId}|{EquippedId}|{ItemKind}",
        ["equipment_repair_started"] = "{ActorId}|{TargetId}|{SiteInstanceId}",
        ["equipment_repaired"] = "{ActorId}|{LotId}",
        ["equipment_repair_interrupted"] = "{ActorId}|{LotId}",
        ["handcart_moved"] = "{ActorId}:{CartId}",
        ["handcart_parked"] = "{ActorId}:{CartLotId}:{Reason}",
        ["handcart_attached"] = "{ActorId}:{CartId}",
        ["handcart_loaded"] = "{ActorId}:{CartHereId}:{ItemKind}:{Quantity}",
        ["handcart_unloaded"] = "{ActorId}:{CartHereId}:{ItemKind}:{Quantity}",
        ["handcart_repaired"] = "{ActorId}:{CartHereId}",
        ["handcart_transferred"] = "{ActorId}:{CartHereId}:{RecipientId}",
        ["household_delivery_recovered"] = "{ActorId}:{StockId}:{PhysicalQuantity}:pile",
        ["household_food_stored"] = "{ActorId}:{FoodId}:{Quantity}:{HouseInstanceId}",
        ["household_stock_delivered"] = "{ActorId}:{CarriedId}:{DeliveredQuantity}:{HouseInstanceId}",
        ["household_preparation_tool_stored"] = "{ActorId}:{ItemKind}:{StoredQuantity}:{HouseForPickupInstanceId}",
        ["household_stock_picked_up"] = "{ActorId}:{StockId}:{Quantity}:{HouseForPickupInstanceId}",
        ["spare_cargo_stored"] = "{ActorId}:{ItemKind}:{Quantity}:{StorageBuildingId}",
        ["agent_knowledge_artifact_collected"] = "{ActorId}|{ArtifactId}",
        ["agent_knowledge_artifact_stored"] = "{ActorId}|{ArtifactId}|{HouseInstanceId}",
        ["ornament_worn"] = "{ActorId}:ornament:{LotId}",
        ["ornament_removed"] = "{ActorId}:ornament:{LotId}",
        ["ornament_given"] = "{ActorId}:ornament_gift:{RecipientId}:{LotId}:{ReceivedId}",
        ["shop_trade_completed"] = "{ActorId}:{OfferId}",
        ["shop_trade_offered"] = "{ActorId}:{OfferId}",
        ["shop_trade_cancelled"] = "{BuyerId}:{OfferId}",
        ["store_stock_delivered"] = "{ActorId}:{GoodsId}:{Quantity}:{StoreInstanceId}",
        ["store_stock_collected"] = "{ActorId}:{GoodsId}:{Quantity}:{StoreInstanceId}",
        ["tool_request_placed"] = "{ActorId}:{RequestId}",
        ["workstation_supplied"] = "{ActorId}:{CarriedId}:{Quantity}:{BuildingInstanceId}",
        ["workstation_input_picked_up"] = "{ActorId}:{StockId}:{Quantity}:{BuildingInstanceId}",
        ["agent_removed"] = "{ActorId}",
        ["weather_changed"] = "{Season}:{AnchorWeather}",
        ["child_delivered_household"] = "{ActorId}:{ItemKind}:{Quantity}:{HouseInstanceId}",
        ["child_collected_household"] = "{ActorId}:{ItemKind}:{Quantity}",
        ["child_helped_household"] = "{ActorId}",
        ["continuity_plan_proceeded"] = "{ActorId}",
        ["caregiver_proposal_expired"] = "{TargetId}",
        ["guardian_needed"] = "{ActorId}",
        ["caregiver_accepted"] = "{ActorId}",
        ["caregiver_ended"] = "{ActorId}",
        ["primary_caregiver_assigned"] = "{ActorId}",
        ["caregiver_assigned"] = "{ActorId}",
        ["caregiver_proposed"] = "{ActorId}",
        ["caregiver_refused"] = "{ActorId}",
        ["guardian_assigned"] = "{ActorId}",
        ["dependent_cared_for"] = "{ActorId}",
        ["partnership_expired"] = "{ActorId}",
        ["partnership_proposed"] = "{ActorId}",
        ["partnership_accepted"] = "{ActorId}",
        ["partnership_refused"] = "{ActorId}",
        ["partnership_ended"] = "{ActorId}",
        ["guardian_placement_pending"] = "{ActorId}",
        ["guardian_placement_cancelled"] = "{ActorId}",
        ["guardian_placement_completed"] = "{ActorId}:{CaregiverId}",
        ["medical_treatment_started"] = "{ActorId}:medicine:{Caregiver}:{SupplyId}",
        ["medical_treatment_interrupted"] = "{ActorId}",
        ["medical_treatment_completed"] = "{ActorId}",
        ["child_born"] = "{ActorId}",
        ["child_cared_for"] = "{ActorId}",
        ["estate_will_started"] = "{EstateId}",
        ["estate_will_default"] = "{EstateId}:{Reason}",
        ["field_work_started"] = "{ActorId}:{FieldId}:{ItemKind}",
        ["field_ready"] = "{FieldId}",
        ["field_returned_to_grass"] = "{FieldId}:{HouseholdId}",
        ["field_work_interrupted"] = "{FieldId}",
        ["tree_replanted"] = "{ActorId}:{TreeId}:{TreeKind}",
        ["tree_planted"] = "{ActorId}:{TreeId}:{Species}",
        ["tree_planting_refused"] = "{ActorId}:{Refusal}",
        ["agent_memories_summarized"] = "{ActorId}:{MemoryCount}",
        ["agent_memories_archived"] = "{ArchivedMemoriesCount}:{ArchivedBeliefsCount}",
        ["exploration_goal_found"] = "{ActorId}:{GoalKind}:{Target}",
        ["exploration_started"] = "{ActorId}:{PositionX},{PositionY}",
        ["exploration_discovered"] = "{ActorId}:{PositionX},{PositionY}:{Terrain}:{Resources}",
        ["exploration_return_started"] = "{ActorId}",
        ["exploration_completed"] = "{ActorId}:visited={VisitedTilesCount}",
        ["agent_knowledge_learned"] = "{ActorId}|firsthand|1",
        ["agent_knowledge_shared"] = "{ActorId}|{RecipientId}|{ArtifactId}|{FactCount}",
        ["agent_knowledge_artifact_read"] = "{SourceAgentId}|{RecipientId}|{ArtifactId}|{FactCount}",
        ["agent_knowledge_writing_started"] = "{ActorId}|{ItemKind}|{ProjectId}",
        ["agent_knowledge_material_collected"] = "{ActorId}|{ItemKind}|{Quantity}",
        ["agent_knowledge_artifact_created"] = "{ActorId}|{ArtifactId}|{ProjectKind}|{FactCount}",
        ["agent_knowledge_writing_cancelled"] = "{ActorId}|{ProjectKind}|{Reason}",
        ["agent_recipe_learned"] = "{ActorId}|{RecipeId}|practice",
        ["destination_reached"] = "{ActorId}:{Reason}",
        ["agent_moved"] = "{ActorId}:{PositionX},{PositionY}->{NextX},{NextY}:{Reason}",
        ["handcart_blocked"] = "{ActorId}:{Reason}",
        ["movement_blocked"] = "{ActorId}:{Reason}:wait={WaitTicks}",
        ["food_harvested"] = "{ActorId}:{HarvestYield}",
        ["fruit_harvested"] = "{ActorId}:{SourceId}:{HarvestYield}:picked",
        ["household_food_collected"] = "{ActorId}:{LotId}:1",
        ["consumption_failed"] = "{ActorId}:no_food",
        ["food_consumed"] = "{ActorId}",
        ["fire_extinguished"] = "{InstanceId}",
        ["building_removed"] = "{InstanceId}:{DefinitionId}",
        ["building_reassigned"] = "{InstanceId}:town={NextTownId}:household={NextHouseholdId}",
        ["building_variant_content_staged"] = "{PackageId}",
        ["child_model_selected"] = "{ActorId}:{ChoiceReason}",
        ["cognition_backpressure"] = "{AgentId}",
        ["cognition_rejected"] = "{ActorId}:{Outcome}",
        ["boat_action_blocked"] = "{ActorId}:stay_aboard",
        ["instruction_order_stale_decision"] = "{ActorId}",
        ["age_action_rejected"] = "{ActorId}:{CandidateId}",
        ["agent_idle"] = "{ActorId}",
        ["content_proposed"] = "{PackageId}",
        ["starter_content_staged"] = "{PackageId}",
        ["initial_content_activated"] = "{PackageId}",
        ["content_validated"] = "{PackageId}",
        ["content_approved"] = "{PackageId}",
        ["content_staged"] = "{PackageId}",
        ["content_rolled_back"] = "{PackageId}:{Reason}",
        ["instruction_queued"] = "{InstructionId}:{InstructionKind}",
        ["aging_override_changed"] = "{Rate}",
        ["routine_helper_changed"] = "{Provider}:{Model}",
        ["paused"] = "owner_request",
        ["resumed"] = "epoch:{RunEpoch}",
        ["instruction_order_cancelled"] = "{ActorId}:{InstructionId}:{Cause}",
        ["instruction_not_understood"] = "{ActorId}:{InstructionId}",
        ["conversation_turn_started"] = "{ConversationId}:{SpeakerId}",
        ["animal_added"] = "{ActorId}:animal-{AnimalSequence}",
        ["expansion_order_resumed"] = "{InstructionId}:{JobId}",
        ["expansion_order_paused"] = "{InstructionId}:{JobId}",
        ["agent_identity_moment_started"] = "{ActorId}:{MomentKind}",
        ["model_attempt_status"] = "{ActorId}:{Status}",
        ["agent_name_unavailable"] = "{ActorId}",
        ["agent_named"] = "{ActorId}",
        ["agent_name_rejected"] = "{ActorId}",
        ["agent_name_retry_exhausted"] = "{ActorId}",
        ["agent_name_retry_requested"] = "{ActorId}",
        ["agent_name_retry_unavailable"] = "{ActorId}",
        ["agent_name_retry_unusable"] = "{ActorId}",
        ["instruction_applied"] = "{InstructionId}:{Action}",
        ["instruction_order_finished"] = "{ActorId}:{InstructionId}:{EffectId}",
        ["instruction_order_progress"] = "{ActorId}:{InstructionId}:{Completed}",
        ["instruction_order_status"] = "{ActorId}:{InstructionId}:{Status}",
        ["house_tool_made"] = "{ActorId}|{ItemKind}",
        ["production_order_resumed"] = "{InstructionId}:{JobId}",
        ["production_order_paused"] = "{InstructionId}:{JobId}",
        ["survival_activated"] = "weather_equipment_and_food",
        ["survival_condition_changed"] = "{ActorId}:warmth={Warmth}:illness={Illness}",
        ["equipment_collected"] = "{ActorId}:{ItemKind}",
        ["fire_fuelled"] = "{BuildingInstanceId}",
        ["meal_eaten"] = "{ActorId}:{ItemKind}:nutrition={Nutrition}",
        ["hosted_decision_started"] = "{ActorId}",
        ["content_activated"] = "{PackageId}",
        ["content_definitions_activated"] = "{PackageId}:buildings={BuildingsCount}:recipes={RecipesCount}",
        ["hosted_decision_completed"] = "{ActorId}:{Outcome}",
        ["hosted_decision_discarded"] = "{ActorId}",
        ["tick_advanced"] = "{TargetTick}",
        ["world_created"] = "{WorldSeed}:agents:{AgentsCount}",
        ["boat_trip_requested"] = "{ActorId}:{QueuedRequestId}:{OriginInstanceId}:{DestinationInstanceId}",
        ["boat_departed"] = "{ActorId}:{BoatId}:{OriginInstanceId}:{DestinationInstanceId}",
        ["boat_waiting"] = "{ActorId}:{BoatId}:arrival_blocked",
        ["boat_recovery_started"] = "{ActorId}:{BoatId}:{RecoveryInstanceId}",
        ["market_built"] = "{TownId}|{MarketId}|{PlacedInstanceId}|{StallsLength}",
        ["market_stall_built"] = "{TownId}|{MarketId}|{PlacedInstanceId}|{Slot}",
        ["market_stall_borrowed"] = "{TownId}|{MarketId}|{BuildingId}|{ActorId}|{OccupancyId}",
        ["market_stock_delivered"] = "{TownId}|{MarketId}|{BuildingId}|{ActorId}|{ReceiptId}",
        ["market_trade_offered"] = "{TownId}|{MarketId}|{BuildingId}|{Seller}|{Buyer}|{OfferId}",
        ["market_trade_completed"] = "{TownId}|{MarketId}|{BuildingId}|{ActorId}|{BuyerId}|{OfferId}",
        ["market_trade_cancelled"] = "{TownId}|{MarketId}|{StallBuildingId}|{SellerId}|{BuyerId}|{OfferId}",
        ["market_stall_left"] = "{TownId}|{MarketId}|{BuildingId}|{ActorId}|{OccupancyId}",
        ["communal_boat_launched"] = "{TownId}:{BoatId}:{PortInstanceId}:{ProjectName}",
        ["saved_road_footprints_repaired"] = "removed:{Removed}",
        ["town_road_unconnected"] = "{TownId}:{BuildingInstanceId}:{Reason}",
        ["town_road_generated"] = "{TownId}:{BuildingInstanceId}:tiles:{LaidCount}",
        ["town_road_extension_refused"] = "{TownId}:{BuildingInstanceId}:{Reason}",
        ["town_road_extended"] = "{TownId}:{BuildingInstanceId}:tiles:{ExtendedCount}",
        ["town_project_blocked"] = "{TownId}:{ProjectId}:{Failure}",
        ["town_project_cancelled"] = "{TownId}:{ProjectId}:{Reason}",
        ["town_project_approved"] = "{TownId}:{ProjectId}:{ProjectName}",
        ["town_project_resumed"] = "{TownId}:{ProjectId}",
        ["town_project_donated"] = "{ActorId}:{ProjectId}:{LotId}:{Quantity}:{ItemKind}",
        ["town_project_material_picked_up"] = "{ActorId}:{ProjectId}:{MovedId}:{Quantity}:{ItemKind}",
        ["town_project_material_delivered"] = "{ActorId}:{ProjectId}:{LotId}:{Quantity}:{ItemKind}",
        ["town_project_material_recovered"] = "{ActorId}:{ProjectId}:{MovedId}:{Quantity}:{ItemKind}",
        ["town_project_worked"] = "{ActorId}:{ProjectId}:{Done}",
        ["town_project_completed"] = "{ActorId}:{ProjectId}:{BuildingId}:{ProjectName}",
        ["town_road_link_unconnected"] = "{TownId}|{NearestId}|{Failure}",
        ["town_road_linked"] = "{TownId}|{NearestId}|tiles:{Added}",
        ["agent_renamed"] = "{ActorId}",
        ["conversation_closed"] = "{ConversationId}:{Reason}",
        ["conversation_interrupted"] = "{ConversationId}:{Reason}",
        ["build_rejected"] = "{ActorId}:{CandidateId}:{Reason}",
        ["exploration_aborted"] = "{ActorId}:{Reason}",
        ["harvest_failed"] = "{ActorId}:{Reason}",
        ["boat_trip_cancelled"] = "{ActorId}:{RequestId}:{Reason}",
        ["traffic_bridge_not_built"] = "{BridgeId}:{Reason}",
        ["starting_resource_blocked"] = "{ItemKind}",
        ["building_expanded"] = "{BuildingId}:{JobId}",
        ["building_expansion_material_delivered"] = "{ActorId}:{ItemKind}:{Quantity}:{BuildingId}:{Location}",
        ["food_taken_from_pot"] = "{ActorId}:{PotId}:{FoodId}:{Quantity}",
        ["town_project_material_returned"] = "{ActorId}:{ProjectId}:{LotId}:{Quantity}:{DestinationId}",
        ["continuity_rule_on"] = "eligible_couples|{EligibleCouples}|threshold|{Threshold}",
        ["continuity_rule_off"] = "eligible_couples|{EligibleCouples}|threshold|{Threshold}",
        ["crop_weather_loss"] = "{FieldId}:harvest:{Cycle}:{Weather}",
        ["crop_moisture_effect"] = "{FieldId}:harvest:{Cycle}:{Effect}:{Moisture}",
        ["model_call_warning"] = "used:{Used}:limit:{Limit}",
        ["marriage_accepted"] = "{InitiatorName} and {InviteeName} agreed to marry; their shared surname is still undecided.",
        ["marriage_surname_draw"] = "{CoupleName} completed their marriage. A draw chose {Surname} after four turns without agreement.",
        ["marriage_surname_agreed"] = "{CoupleName} completed their marriage and agreed to share the surname {Surname}.",
        ["estate_will_accepted"] = "{EstateId}:{Split}:{HeirCount}",
        ["lesson_requested"] = "{ActorId}",
        ["lesson_accepted"] = "{ActorId}",
        ["lesson_declined"] = "{ActorId}",
        ["lesson_paused"] = "{ActorId}",
        ["lesson_training"] = "{ActorId}",
        ["lesson_completed"] = "{ActorId}",
        ["lesson_cancelled"] = "{ActorId}",
        ["parenthood_requested"] = "{ActorId}",
        ["parenthood_preparing"] = "{ActorId}",
        ["parenthood_postponed"] = "{ActorId}",
        ["parenthood_cancelled"] = "{ActorId}",
        ["parenthood_completed"] = "{ActorId}",
        ["tool_request_accepted"] = "{ActorId}:{RequestId}",
        ["tool_request_refused"] = "{ActorId}:{RequestId}",
        ["tool_request_withdrawn"] = "{ActorId}:{RequestId}",
        ["tool_request_ready"] = "{ActorId}:{RequestId}",
        ["tool_request_completed"] = "{ActorId}:{RequestId}",
        ["tool_request_interrupted"] = "{ActorId}:{RequestId}",
        ["housing_request_refused"] = "{ActorId}:{HouseholdId}",
        ["housing_request_expired"] = "{ActorId}:{HouseholdId}",
        ["child_converse"] = "{ActorId}:{TargetId}",
        ["child_play"] = "{ActorId}:{TargetId}",
        ["child_learn"] = "{ActorId}:{TargetId}",
        ["farm_flour_picked_up"] = "{ActorId}:{ResourceId}:{Quantity}:{DestinationId}",
        ["farm_grain_picked_up"] = "{ActorId}:{ResourceId}:{Quantity}:{DestinationId}",
        ["production_worker_unavailable"] = "{JobId}",
        ["production_action_refused"] = "{JobId}",
        ["production_output_unsupported"] = "{JobId}",
        ["production_input_unusable"] = "{JobId}",
        ["production_tool_unusable"] = "{JobId}",
        ["restaurant_content_staged"] = "{PackageId}",
        ["pottery_content_staged"] = "{PackageId}",
        ["silo_content_staged"] = "{PackageId}",
        ["tailor_content_staged"] = "{PackageId}",
        ["town_civic_council"] = "{TownId}|{SubjectId}|{NoticeText}",
        ["town_civic_election"] = "{TownId}|{SubjectId}|{NoticeText}",
        ["town_civic_runoff"] = "{TownId}|{SubjectId}|{NoticeText}",
        ["town_civic_proposal"] = "{TownId}|{SubjectId}|{NoticeText}",
        ["town_civic_result"] = "{TownId}|{SubjectId}|{NoticeText}",
        ["town_civic_cancelled"] = "{TownId}|{SubjectId}|{NoticeText}",
        ["town_civic_law"] = "{TownId}|{SubjectId}|{NoticeText}",
        ["town_civic_government"] = "{TownId}|{SubjectId}|{NoticeText}",
        ["town_civic_mayor"] = "{TownId}|{SubjectId}|{NoticeText}",
        ["town_civic_land_hearing"] = "{TownId}|{SubjectId}|{NoticeText}",
        ["town_civic_land_transfer"] = "{TownId}|{SubjectId}|{NoticeText}",
        ["town_civic_nonviolent_hearing"] = "{TownId}|{SubjectId}|{NoticeText}",
        ["town_civic_law_case"] = "{TownId}|{SubjectId}|{NoticeText}",
        ["town_civic_remedy"] = "{TownId}|{SubjectId}|{NoticeText}",
        ["land_case_opened"] = "{TownId}|{CaseId}|{Revision}|{ActorId}|{Status}",
        ["land_case_notice"] = "{TownId}|{CaseId}|{Revision}|{ActorId}|{Status}",
        ["land_case_evidence"] = "{TownId}|{CaseId}|{Revision}|{ActorId}|{Status}",
        ["land_case_response"] = "{TownId}|{CaseId}|{Revision}|{ActorId}|{Status}",
        ["land_case_judge_consent"] = "{TownId}|{CaseId}|{Revision}|{ActorId}|{Status}",
        ["land_case_judge_election"] = "{TownId}|{CaseId}|{Revision}|{ActorId}|{Status}",
        ["land_case_judge_assigned"] = "{TownId}|{CaseId}|{Revision}|{ActorId}|{Status}",
        ["land_case_ruling"] = "{TownId}|{CaseId}|{Revision}|{ActorId}|{Status}",
        ["land_case_reopen_requested"] = "{TownId}|{CaseId}|{Revision}|{ActorId}|{Status}",
        ["land_case_reopened"] = "{TownId}|{CaseId}|{Revision}|{ActorId}|{Status}",
        ["land_case_rejected"] = "{TownId}|{CaseId}|{Revision}|{ActorId}|{Status}",
        ["land_case_inspected"] = "{TownId}|{CaseId}|{Revision}|{ActorId}|{Status}",
        ["land_case_relayed"] = "{TownId}|{CaseId}|{Revision}|{ActorId}|{Status}",
        ["land_transfer_proposed"] = "{TownId}|{RequestId}|1|{ActorId}|{Status}",
        ["land_transfer_read"] = "{TownId}|{RequestId}|1|{ActorId}|{Status}",
        ["land_transfer_consent"] = "{TownId}|{RequestId}|1|{ActorId}|{Status}",
        ["land_transfer_withdrawn"] = "{TownId}|{RequestId}|1|{ActorId}|{Status}",
        ["land_transfer_settled"] = "{TownId}|{RequestId}|1|{ActorId}|{Status}",
        ["land_transfer_blocked"] = "{TownId}|{RequestId}|1|{ActorId}|{Status}",
        ["law_case_opened"] = "{TownId}|{SubjectId}|{ActorId}",
        ["law_case_evidence"] = "{TownId}|{SubjectId}|{ActorId}",
        ["law_case_response"] = "{TownId}|{SubjectId}|{ActorId}",
        ["law_case_inspected"] = "{TownId}|{SubjectId}|{ActorId}",
        ["law_case_relayed"] = "{TownId}|{SubjectId}|{ActorId}",
        ["law_case_judge_consent"] = "{TownId}|{SubjectId}|{ActorId}",
        ["law_case_judge_election"] = "{TownId}|{SubjectId}|{ActorId}",
        ["law_case_judge_assigned"] = "{TownId}|{SubjectId}|{ActorId}",
        ["law_case_finding"] = "{TownId}|{SubjectId}|{ActorId}",
        ["law_case_reopen_requested"] = "{TownId}|{SubjectId}|{ActorId}",
        ["law_case_reopened"] = "{TownId}|{SubjectId}|{ActorId}",
        ["law_case_rejected"] = "{TownId}|{SubjectId}|{ActorId}",
        ["law_case_offer"] = "{TownId}|{SubjectId}|{ActorId}",
        ["law_case_offer_response"] = "{TownId}|{SubjectId}|{ActorId}",
        ["first_town_layout_accepted"] = "{SiteX},{SiteY}:buildings:{BuildingCount}:roads:{RoadCount}",
        ["first_town_layout_redone"] = "{SiteX},{SiteY}:buildings:{BuildingCount}:roads:{RoadCount}",
        ["conversation_resumed"] = "{ConversationId}",
        ["conversation_resume_requested"] = "{ConversationId}",
        ["conversation_agreement_accepted"] = "{ConversationId}",
        ["conversation_agreement_pending"] = "{ConversationId}",
        ["house_guest_invited"] = "{HouseId}:{ActorId}:{GuestId}",
        ["house_guest_revoked"] = "{HouseId}:{ActorId}:{GuestId}",
        ["medical_care_allowed"] = "{ActorId}:medical_caregiver:{CaregiverId}",
        ["medical_care_revoked"] = "{ActorId}:medical_caregiver:{CaregiverId}",
        ["field_prepared"] = "{ActorId}:{FieldId}:{Crop}",
        ["field_planted"] = "{ActorId}:{FieldId}:{Crop}",
        ["field_tended"] = "{ActorId}:{FieldId}:{Crop}",
        ["field_harvested"] = "{ActorId}:{FieldId}:{Crop}",
        ["owner_stock_delivered"] = "{ActorId}:{SourceLotId}:{Quantity}:{DestinationId}",
        ["owner_stock_picked_up"] = "{ActorId}:{SourceLotId}:{Quantity}:{DestinationId}",
        ["agent_identity_chosen"] = "{ActorId}",
        ["agent_identity_kept"] = "{ActorId}",
        ["agent_identity_revised"] = "{ActorId}:{MomentKind}",
        ["agent_identity_moment_kept"] = "{ActorId}:{MomentKind}",
        ["boat_returned"] = "{ActorId}:{BoatId}:{PortId}",
        ["boat_arrived"] = "{ActorId}:{BoatId}:{PortId}",
        ["market_stock_loaded"] = "{TownId}|{MarketId}|{StallId}|{ActorId}|{LotId}",
        ["market_stock_collected"] = "{TownId}|{MarketId}|{StallId}|{ActorId}|{LotId}",
    };
    private static readonly Dictionary<string, Type> Types = typeof(WorldEventPayload)
        .GetCustomAttributes(typeof(JsonDerivedTypeAttribute), false).Cast<JsonDerivedTypeAttribute>()
        .ToDictionary(item => (string)item.TypeDiscriminator!, item => item.DerivedType, StringComparer.Ordinal);

    public static WorldEventPayload? Create(string kind, string detail, OwnerWorldSnapshot? snapshot)
    {
        if (!Types.TryGetValue(kind, out var type)) return null;
        string Actor(string text) => snapshot?.Agents.OrderByDescending(person => person.Id.Length)
            .FirstOrDefault(person => text == person.Id || text.StartsWith(person.Id + ":", StringComparison.Ordinal))?.Id ?? text;
        if (kind == "action_refused")
        {
            try
            {
                var json = JsonDocument.Parse(detail).RootElement;
                return new ActionRefusedEventPayload(json.GetProperty("AgentId").GetString(), json.GetProperty("CandidateId").GetString()!, 0, 0, "fixture");
            }
            catch (JsonException) { return null; }
        }
        if (kind == "developer_edit")
        {
            try
            {
                var edit = JsonSerializer.Deserialize<OwnerDeveloperEditAction>(detail);
                return edit is null ? null : new DeveloperEditEventPayload(edit.AgentId, edit.WorldId, edit.ExpectedEventId, edit.Operation, edit.Value, edit.Amount, edit.OtherAgentId);
            }
            catch (JsonException) { return null; }
        }
        if (kind.StartsWith("marriage_", StringComparison.Ordinal))
        {
            if (kind == "marriage_accepted")
            {
                var names = detail.Replace(" agreed to marry; their shared surname is still undecided.", "", StringComparison.Ordinal).Split(" and ", 2);
                return new MarriageAcceptedEventPayload(null, "", names[0], names.ElementAtOrDefault(1) ?? "");
            }
            return null;
        }
        if (kind is "building_placed" or "build_started" or "build_completed" or "recipe_started" or "recipe_completed" or "recipe_cancelled")
        {
            var start = detail.IndexOf("sha256:", StringComparison.Ordinal);
            string definition;
            if (start >= 0)
            {
                var end = detail.IndexOf(':', start + 7);
                definition = end < 0 ? detail[start..] : detail[start..end];
                if (!Regex.IsMatch(definition, @"^sha256:[a-fA-F0-9]{64}/(building|recipe)/[a-z0-9._-]+@[^/]+$")) definition = "sha256";
            }
            else definition = detail.Split(':').ElementAtOrDefault(1) ?? "something new";
            return kind switch
            {
                "building_placed" => new BuildingPlacedEventPayload(null, detail.Split(':')[0], definition, null, null, null, null),
                "build_started" => new BuildStartedEventPayload(null, detail.Split(':')[0], definition, null, null, null, null),
                "build_completed" => new BuildCompletedEventPayload(null, detail.Split(':')[0], definition, null, null, null, null),
                "recipe_started" => new RecipeStartedEventPayload(null, "job", definition, null),
                "recipe_completed" => new RecipeCompletedEventPayload(null, "job", definition, null),
                _ => new RecipeCancelledEventPayload(null, "job", definition, null),
            };
        }
        if (kind == "town_admission_accepted" && detail.Split('|').Length != 4) return null;
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (Templates.TryGetValue(kind, out var template))
        {
            var tokens = Regex.Matches(template, @"\{(\w+)\}").Cast<Match>().ToArray();
            var pattern = ""; var at = 0;
            var known = (snapshot?.Agents.Select(person => person.Id) ?? [])
                .Concat(snapshot?.Towns.Select(town => town.Id) ?? [])
                .Concat(snapshot?.Towns.SelectMany(town => town.Projects).Select(project => project.Id) ?? [])
                .Concat(snapshot?.PlacedBuildings.Select(building => building.InstanceId) ?? [])
                .Concat(snapshot?.Stockpiles.Select(stock => stock.OwnerId) ?? [])
                .Concat(snapshot?.Animals.Select(animal => animal.Id) ?? []).Distinct()
                .OrderByDescending(id => id.Length).Select(Regex.Escape).ToArray();
            foreach (var token in tokens)
            {
                pattern += Regex.Escape(template[at..token.Index]);
                var name = token.Groups[1].Value;
                var candidate = name.EndsWith("Id", StringComparison.Ordinal) && known.Length > 0
                    ? "(?:" + string.Join('|', known) + "|.*?)" : ".*?";
                pattern += "(?<" + name + ">" + candidate + ")";
                at = token.Index + token.Length;
            }
            pattern += Regex.Escape(template[at..]);
            var match = Regex.Match(detail, "^" + pattern + "$", RegexOptions.Singleline);
            if (match.Success)
                foreach (var token in tokens) values[token.Groups[1].Value] = match.Groups[token.Groups[1].Value].Value;
        }
        if (kind.StartsWith("estate_will_", StringComparison.Ordinal))
        {
            var tail = detail.StartsWith("estate:", StringComparison.Ordinal) ? detail[7..] : "";
            values["ActorId"] = Actor(tail);
            if (kind == "estate_will_accepted")
            {
                var suffix = detail.Split(':'); values["EstateId"] = string.Join(':', suffix[..^2]);
                values["Split"] = suffix[^2]; values["HeirCount"] = suffix[^1];
            }
        }
        if (kind == "bridge_built")
        {
            var fields = detail.Split(':'); values["Trigger"] = fields[0]; values["BridgeId"] = fields.ElementAtOrDefault(1) ?? "";
            values["RoadId"] = fields.ElementAtOrDefault(2) ?? "";
        }
        if (kind == "town_founded")
        {
            var fields = detail.Split('|'); values["TownId"] = fields[0]; values["ActorId"] = fields.ElementAtOrDefault(1) ?? "";
            values["PreviousTownId"] = fields.ElementAtOrDefault(2) ?? "none"; values["MemberCount"] = fields.ElementAtOrDefault(3) ?? "0";
            values["IsFirstTown"] = fields.Length == 1 ? "true" : "false";
        }
        if (kind.StartsWith("town_project_", StringComparison.Ordinal))
        {
            var project = snapshot?.Towns.SelectMany(town => town.Projects)
                .Select(project => (Project: project, At: detail.IndexOf(":" + project.Id + ":", StringComparison.Ordinal)))
                .Where(item => item.At >= 0).OrderBy(item => item.At).ThenByDescending(item => item.Project.Id.Length).FirstOrDefault();
            if (project is { Project: not null, At: >= 0 } found)
            {
                values["ProjectId"] = found.Project.Id;
                if (Templates[kind].StartsWith("{ActorId}", StringComparison.Ordinal)) values["ActorId"] = detail[..found.At];
            }
        }
        if (!values.ContainsKey("ActorId")) values["ActorId"] = Actor(detail);
        var parameters = type.GetConstructors().Single().GetParameters();
        var arguments = parameters.Select(parameter =>
        {
            values.TryGetValue(parameter.Name!, out var value); value ??= "";
            if (parameter.ParameterType == typeof(string)) return (object?)value;
            if (parameter.ParameterType == typeof(bool)) return bool.TryParse(value, out var b) && b;
            if (parameter.ParameterType == typeof(int) || parameter.ParameterType == typeof(int?)) return (object)(int.TryParse(value, CultureInfo.InvariantCulture, out var i) ? i : 0);
            if (parameter.ParameterType == typeof(long) || parameter.ParameterType == typeof(long?)) return (object)(long.TryParse(value, CultureInfo.InvariantCulture, out var l) ? l : 0L);
            throw new InvalidOperationException("Unknown fixture field: " + parameter.Name);
        }).ToArray();
        return (WorldEventPayload)type.GetConstructors().Single().Invoke(arguments);
    }
}

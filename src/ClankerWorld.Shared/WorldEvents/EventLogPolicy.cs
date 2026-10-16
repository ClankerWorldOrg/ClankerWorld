using ClankerWorld.Simulation.World;

namespace ClankerWorld.Presentation;

public enum EventLogImportance { Unknown, Hidden, Important, Refusal, ModelStatus, OrderProblem }

/// <summary>The shared Event Log policy. Filtering never removes accepted history.</summary>
public static class EventLogPolicy
{
    // Every accepted kind is classified explicitly. Unknown diagnostics stay hidden;
    // the coverage test requires a deliberate choice when a new payload is added.
    public static EventLogImportance Importance(string kind) => kind switch
    {
        // Public milestones and decisions.
        "agent_building_proposed" or "agent_knowledge_artifact_created" or "agent_knowledge_artifact_read" or
        "agent_knowledge_shared" or "agent_knowledge_writing_started" or "agent_recipe_learned" or
        "agent_removed" or "animal_born" or "animal_died" or
        "animal_tamed" or "animal_transferred" or "animals_arrived" or
        "barter_completed" or "borrowed_goods_returned" or "bridge_built" or
        "build_completed" or "build_started" or "building_expanded" or
        "building_expansion_started" or "building_placed" or "caregiver_assigned" or
        "child_born" or "continuity_rule_off" or "continuity_rule_on" or
        "crop_moisture_effect" or "crop_weather_loss" or "developer_edit" or
        "estate_will_accepted" or "estate_will_default" or "expansion_order_paused" or
        "expansion_order_resumed" or "exploration_completed" or "exploration_return_started" or
        "exploration_started" or "field_harvested" or "field_planted" or
        "field_prepared" or "field_ready" or "field_returned_to_grass" or
        "food_policy_adopted" or "guardian_assigned" or "guardian_needed" or
        "guardian_placement_completed" or "guardian_placement_pending" or "handcart_repaired" or
        "handcart_transferred" or "house_guest_invited" or "house_guest_revoked" or
        "house_tool_made" or "household_founded" or "household_joined" or
        "household_left" or "housing_request_made" or "land_case_evidence" or
        "land_case_judge_assigned" or "land_case_judge_consent" or "land_case_judge_election" or
        "land_case_notice" or "land_case_opened" or "land_case_reopen_requested" or
        "land_case_reopened" or "land_case_response" or "land_case_ruling" or
        "land_transfer_consent" or "land_transfer_proposed" or "land_transfer_settled" or
        "land_transfer_withdrawn" or "land_use_granted" or "land_use_requested" or
        "law_case_evidence" or "law_case_finding" or "law_case_judge_assigned" or
        "law_case_judge_consent" or "law_case_judge_election" or "law_case_offer" or
        "law_case_offer_response" or "law_case_opened" or "law_case_remedy_effect" or
        "law_case_reopen_requested" or "law_case_reopened" or "law_case_response" or
        "market_built" or "market_stall_borrowed" or "market_stall_built" or
        "market_stall_left" or "market_trade_completed" or "market_trade_offered" or
        "marriage_accepted" or "marriage_ended" or "marriage_surname_agreed" or
        "marriage_surname_draw" or "medical_care_allowed" or "medical_care_revoked" or
        "medical_treatment_completed" or "medical_treatment_started" or "model_call_warning" or
        "ornament_given" or "ornament_removed" or "ornament_worn" or
        "partnership_accepted" or "partnership_ended" or "paused" or
        "recipe_completed" or "relocation_cancelled" or "relocation_notice" or
        "replacement_care_accepted" or "resumed" or "shop_trade_completed" or
        "shop_trade_offered" or "skill_learned" or "tool_request_accepted" or
        "tool_request_completed" or "tool_request_placed" or "tool_request_ready" or
        "tool_request_withdrawn" or "town_abandoned" or "town_admission_accepted" or
        "town_admission_approved" or "town_admission_lapsed" or "town_border_expanded" or
        "town_building_assigned" or "town_civic_cancelled" or "town_civic_council" or
        "town_civic_election" or "town_civic_government" or "town_civic_land_hearing" or
        "town_civic_law" or "town_civic_law_case" or "town_civic_mayor" or
        "town_civic_nonviolent_hearing" or "town_civic_proposal" or "town_civic_remedy" or
        "town_civic_result" or "town_civic_runoff" or "town_founded" or
        "town_founding_started" or "town_land_claimed" or "town_membership_evaluated" or
        "town_project_approved" or "town_project_completed" or "town_project_donated" or
        "town_project_resumed" or "town_resettled" or "town_resident_joined" or
        "town_resident_left" or "town_revived" or "town_stock_salvaged" or
        "tree_planted" or "tree_replanted" or "weather_changed" or
        "world_created" or "world_started" => EventLogImportance.Important,
        // Broken promises, player requests and consequential problems; once a day.
        "action_refused" or "agent_knowledge_writing_cancelled" or "building_expansion_cancelled" or
        "exploration_aborted" or "field_work_interrupted" or "guardian_placement_cancelled" or
        "handcart_blocked" or "housing_blocked" or "housing_request_expired" or
        "housing_request_refused" or "instruction_not_understood" or "instruction_order_cancelled" or
        "land_case_rejected" or "land_transfer_blocked" or "law_case_rejected" or
        "market_trade_cancelled" or "marriage_surname_blocked" or "medical_treatment_interrupted" or
        "shop_trade_cancelled" or "tool_request_interrupted" or
        "tool_request_refused" or "town_project_blocked" or "town_project_cancelled" => EventLogImportance.Refusal,
        // Routine work, automatic retries and diagnostics remain in accepted history.
        "age_action_rejected" or "agent_added" or "agent_identity_chosen" or
        "agent_identity_kept" or "agent_identity_moment_kept" or "agent_identity_moment_started" or
        "agent_identity_revised" or "agent_idle" or "agent_knowledge_artifact_collected" or
        "agent_knowledge_artifact_stored" or "agent_knowledge_learned" or "agent_knowledge_material_collected" or
        "agent_memories_archived" or "agent_memories_summarized" or "agent_moved" or
        "agent_name_rejected" or "agent_name_retry_exhausted" or "agent_name_retry_requested" or
        "agent_name_retry_unavailable" or "agent_name_retry_unusable" or "agent_name_unavailable" or
        "agent_named" or "agent_renamed" or "agent_slept" or
        "aging_override_changed" or "animal_added" or "animal_breeding_started" or
        "animal_cared" or "animal_content_staged" or "animal_hide_collected" or
        "animal_permission_changed" or "animal_product_collected" or "animal_product_ready" or
        "animal_saddled" or "animal_trade_offered" or "animal_yard_supplied" or
        "barter_cancelled" or "barter_declined" or "barter_offered" or
        "blacksmith_content_staged" or "boat_action_blocked" or "boat_arrived" or
        "boat_departed" or "boat_recovery_started" or "boat_returned" or
        "boat_trip_cancelled" or "boat_trip_requested" or "boat_waiting" or
        "build_rejected" or "building_expansion_material_delivered" or "building_expansion_material_picked_up" or
        "building_reassigned" or "building_removed" or "building_variant_content_staged" or
        "care_content_staged" or "caregiver_accepted" or "caregiver_ended" or
        "caregiver_proposal_expired" or "caregiver_proposed" or "caregiver_refused" or
        "carrying_full" or "child_cared_for" or "child_collected_household" or
        "child_converse" or "child_delivered_household" or "child_helped_household" or
        "child_learn" or "child_model_selected" or "child_play" or
        "choice_refused" or "cognition_backpressure" or "cognition_rejected" or
        "communal_boat_launched" or "consumption_failed" or "content_activated" or
        "content_approved" or "content_definitions_activated" or "content_proposed" or
        "content_rolled_back" or "content_staged" or "content_validated" or
        "continuity_plan_proceeded" or "conversation_accepted" or "conversation_action_rejected" or
        "conversation_agreement_accepted" or "conversation_agreement_pending" or "conversation_closed" or
        "conversation_interrupted" or "conversation_proposed" or "conversation_resume_requested" or
        "conversation_resumed" or "conversation_turn_admitted" or "conversation_turn_started" or
        "dependent_cared_for" or "destination_reached" or "empty_vessel_picked_up" or
        "equipment_collected" or "equipment_equipped" or "equipment_repair_interrupted" or
        "equipment_repair_started" or "equipment_repaired" or "estate_will_started" or
        "exploration_discovered" or "exploration_goal_found" or "farm_content_staged" or
        "farm_flour_picked_up" or "farm_grain_picked_up" or "field_tended" or
        "field_work_started" or "fire_extinguished" or "fire_fuelled" or
        "first_town_layout_accepted" or "first_town_layout_redone" or "food_consumed" or
        "food_harvested" or "food_policy_proposed" or "food_policy_rejected" or
        "food_policy_vote_recorded" or "food_steward_changed" or "food_stored_in_pot" or
        "food_taken_from_pot" or "founder_moved" or "founder_placed" or
        "founder_placement_undone" or "fruit_harvested" or "handcart_attached" or
        "handcart_loaded" or "handcart_moved" or "handcart_parked" or
        "handcart_unloaded" or "harvest_failed" or "hearth_content_staged" or
        "horse_dismounted" or "horse_mounted" or "hosted_decision_completed" or
        "hosted_decision_discarded" or "hosted_decision_started" or "house_content_staged" or
        "house_cooking_content_staged" or "house_tools_content_staged" or "household_delivery_recovered" or
        "household_food_collected" or "household_food_stored" or "household_preparation_tool_stored" or
        "household_stock_delivered" or "household_stock_picked_up" or "household_work_resumed" or
        "housing_answer_recorded" or "housing_request_blocked_capacity" or "housing_request_cancelled" or
        "initial_content_activated" or "instruction_applied" or "instruction_order_finished" or
        "instruction_order_progress" or "instruction_order_stale_decision" or "instruction_queued" or
        "knowledge_content_staged" or "land_case_inspected" or "land_case_relayed" or
        "land_transfer_read" or "law_case_inspected" or "law_case_relayed" or
        "lesson_accepted" or "lesson_cancelled" or "lesson_completed" or
        "lesson_declined" or "lesson_paused" or "lesson_requested" or
        "lesson_training" or "market_content_staged" or "market_stock_collected" or
        "market_stock_delivered" or "market_stock_loaded" or "material_gathered" or
        "meal_eaten" or "milk_drunk" or "movement_blocked" or
        "ornament_content_staged" or "owner_stock_delivered" or "owner_stock_picked_up" or
        "parenthood_cancelled" or "parenthood_completed" or "parenthood_postponed" or
        "parenthood_preparing" or "parenthood_requested" or "partnership_expired" or
        "partnership_proposed" or "partnership_refused" or "personal_goods_collected" or
        "personal_goods_stored" or "port_content_staged" or "pottery_content_staged" or
        "primary_caregiver_assigned" or "production_action_refused" or "production_input_unusable" or "production_order_paused" or
        "production_order_resumed" or "production_output_unsupported" or "production_tool_unusable" or
        "production_worker_unavailable" or "project_chosen" or "project_fresh_choice_resumed" or
        "project_material_delivered" or "project_progress" or "project_request_fulfilled" or
        "project_site_reselected" or "recipe_cancelled" or "recipe_started" or
        "restaurant_content_staged" or "routine_helper_changed" or "saved_road_footprints_repaired" or
        "silo_content_staged" or "smith_input_delivered" or "smith_input_picked_up" or
        "smith_ore_delivered" or "social_standing_changed" or "spare_cargo_stored" or
        "starter_content_staged" or "starting_resource_blocked" or "starting_resources_added" or
        "store_content_staged" or "store_stock_collected" or "store_stock_delivered" or
        "street_lantern_content_staged" or "survival_activated" or "survival_condition_changed" or
        "tailor_content_staged" or "tick_advanced" or "tool_repaired" or
        "town_civic_action" or "town_civic_action_rejected" or "town_civic_candidate" or
        "town_civic_land_transfer" or "town_civic_land_use" or "town_civic_nomination" or
        "town_hall_content_staged" or "town_layout_site_rejected" or "town_project_material_delivered" or
        "town_project_material_picked_up" or "town_project_material_recovered" or "town_project_material_returned" or
        "town_project_worked" or "town_resource_collected" or "town_resources_stored" or
        "town_road_extended" or "town_road_extension_refused" or "town_road_generated" or
        "town_road_link_unconnected" or "town_road_linked" or "town_road_unconnected" or
        "traffic_bridge_not_built" or "tree_harvested" or "tree_planting_refused" or
        "tree_seed_collected" or "voluntary_goods_returned" or "warehouse_content_staged" or
        "water_jug_collected" or "water_jug_filled" or "water_jug_returned" or
        "work_practice_earned" or "workstation_input_picked_up" or "workstation_supplied" => EventLogImportance.Hidden,
        "model_attempt_status" => EventLogImportance.ModelStatus,
        "instruction_order_status" => EventLogImportance.OrderProblem,
        _ => EventLogImportance.Unknown,
    };

    public static bool CanAppear(string kind) => Importance(kind) is
        EventLogImportance.Important or EventLogImportance.Refusal or EventLogImportance.OrderProblem;

    public static bool IsPlayerFacingModelStatus(string? status) =>
        status is "missing_key" or "usage_limit" or "unusable_reply" or "timed_out" or "model_unavailable";

    public static IEnumerable<T> Select<T>(IEnumerable<T> events, Func<T, long> eventId,
        Func<T, string> kind, Func<T, WorldEventPayload?> payload, Func<T, long> day)
    {
        var modelFailures = new Dictionary<string, string>(StringComparer.Ordinal);
        var refusals = new HashSet<(long Day, string Kind, string? Actor, string Target)>();
        foreach (var item in events.OrderBy(eventId))
        {
            var importance = Importance(kind(item));
            var data = payload(item);
            if (importance == EventLogImportance.ModelStatus)
            {
                if (data is not ModelAttemptStatusEventPayload { ActorId: { } actor } status) continue;
                if (status.Status == "ready") modelFailures.Remove(actor);
                if (!IsPlayerFacingModelStatus(status.Status)) continue;
                if (modelFailures.TryGetValue(actor, out var previous) && previous == status.Status) continue;
                modelFailures[actor] = status.Status;
                yield return item;
                continue;
            }
            if (data is InstructionOrderCancelledEventPayload { Cause: "owner" or "replaced" }) continue;
            if (importance == EventLogImportance.OrderProblem &&
                data is not InstructionOrderStatusEventPayload { Status: "blocked" }) continue;
            if (importance is EventLogImportance.Refusal or EventLogImportance.OrderProblem)
            {
                // Use complete typed references, not names, diagnostic prose or
                // a guessed colon-delimited prefix. Different reasons for the
                // same problem/target still produce only one row that day.
                var target = data is null ? string.Empty : string.Join("\0", data.SubjectIds);
                if (!refusals.Add((day(item), kind(item), data?.ActorId, target))) continue;
            }
            if (importance is EventLogImportance.Important or EventLogImportance.Refusal or EventLogImportance.OrderProblem)
                yield return item;
        }
    }
}

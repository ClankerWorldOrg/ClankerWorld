using ClankerWorld.Simulation.World;
using ClankerWorld.Presentation;
using System.Globalization;

namespace ClankerWorld.Server.Observation;

/// <summary>Player event descriptions, derived without rewriting accepted history.</summary>
public static class WorldEventFormatter
{
    public const string ContinuityRisk = "The world is at risk of dying out.";


    private static string DescribeActionRefusal(WorldEventPayload payload, WorldSnapshotView? snapshot)
    {
        if (payload is not ActionRefusedEventPayload refusal) return "An agent waited because a choice caused a game error.";
        var action = refusal.CandidateId.Split(':', 2)[0] switch
        {
            "explore" or "explore_for" => "explore",
            "consume_food" => "eat",
            "drink_milk" => "drink milk",
            "milk_sell" or "milk_sale_offer" => "sell milk",
            "produce_item" => "make goods",
            "repair_equipment" => "repair equipment",
            "gather_food" => "gather food",
            _ => "carry out this choice",
        };
        return $"{Name(snapshot, refusal.ActorId ?? "")} couldn't {action} because of a game error. They waited instead.";
    }

    private static string DescribeContinuity(bool active, WorldEventPayload payload)
    {
        var (couples, threshold) = payload switch
        {
            ContinuityRuleOnEventPayload p => (p.EligibleCouples, p.Threshold),
            ContinuityRuleOffEventPayload p => (p.EligibleCouples, p.Threshold),
            _ => (0, 0),
        };
        if (threshold > 0)
            return active
                ? $"The continuity rule is on because fewer than {threshold} couples of adults who are not close relatives can have children ({couples} now). " + "Couples may put off having a child for up to two days but cannot refuse."
                : $"The continuity rule is off because at least {threshold} couples of adults who are not close relatives can have children ({couples} now). " + "Couples may decide against having a child again.";
        return active
            ? "The continuity rule is on because fewer than eight people who are not elders are alive. Couples may put off having a child for up to two days but cannot refuse."
            : "The continuity rule is off because eight or more people who are not elders are alive. Couples may decide against having a child again.";
    }

    private static string DescribeDeveloperEdit(WorldEventPayload payload, WorldSnapshotView? snapshot)
    {
        if (payload is not DeveloperEditEventPayload edit || string.IsNullOrWhiteSpace(edit.ActorId) ||
            string.IsNullOrWhiteSpace(edit.Operation) || string.IsNullOrWhiteSpace(edit.Value)) return "Developer edit.";
        var name = Name(snapshot, edit.ActorId ?? "");
        var value = EventVocabulary.HumanizeIdentifier(edit.Value).ToLowerInvariant();
        var change = edit.Operation switch
        {
            "set_need" => $"{name}'s {value} set to {edit.Amount}%",
            "give_goods" => $"{name} received {edit.Amount} {value}",
            "remove_goods" => $"removed {edit.Amount} {value} from {name}",
            "add_skill" => $"added {value} skill to {name}",
            "remove_skill" => $"removed {value} skill from {name}",
            "add_animal" => $"added a {value.Replace(':', ' ')} to {name}'s household animal yard",
            "start_partnership" => $"started a partnership between {name} and {Name(snapshot, edit.OtherAgentId ?? "")}",
            "end_partnership" => $"ended the partnership between {name} and {Name(snapshot, edit.OtherAgentId ?? "")}",
            _ => "saved a change",
        };
        return "Developer edit: " + change + ".";
    }

    private static string? ContentName(WorldEventPayload payload, WorldSnapshotView? snapshot)
    {
        var id = DefinitionId(payload);
        if (id is null) return null;
        var fields = id.Split('/');
        if (fields.Length != 3 || fields[0].Length != 71 ||
            fields[0][7..].Any(character => !Uri.IsHexDigit(character)) ||
            fields[1] is not ("building" or "recipe")) return null;
        var version = fields[2].IndexOf('@');
        if (version <= 0 || version == fields[2].Length - 1) return null;
        var localId = fields[2][..version];
        if (localId.Any(character => !(char.IsLower(character) || char.IsDigit(character) || character is '.' or '-' or '_')))
            return null;
        if (fields[1] == "building")
        {
            var name = snapshot?.PlacedBuildings.FirstOrDefault(building => building.DefinitionId == id)?.DisplayName;
            if (!string.IsNullOrWhiteSpace(name)) return name;
            // Historical building IDs include a footprint suffix, which is not part of their name.
            var dash = localId.LastIndexOf('-');
            var footprint = dash < 0 ? [] : localId[(dash + 1)..].Split('x');
            if (footprint.Length == 2 &&
                int.TryParse(footprint[0], NumberStyles.None, CultureInfo.InvariantCulture, out var width) && width > 0 &&
                int.TryParse(footprint[1], NumberStyles.None, CultureInfo.InvariantCulture, out var height) && height > 0)
                localId = localId[..dash];
        }
        else
        {
            var name = snapshot?.ProductionJobs.FirstOrDefault(job => job.RecipeId == id)?.Recipe?.Name ??
                snapshot?.PlacedBuildings.SelectMany(building => building.AvailableRecipes ?? [])
                    .FirstOrDefault(recipe => recipe.Id == id)?.Name;
            if (!string.IsNullOrWhiteSpace(name)) return name;
        }
        return EventVocabulary.HumanizeIdentifier(localId);
    }

    public static bool OffersNewcomer(WorldSnapshotView? snapshot) =>
        snapshot is { ContinuityRuleActive: true, FounderSetup.Started: true };

    public static string Describe(EventView worldEvent, WorldSnapshotView? snapshot)
    {
        if (worldEvent.Payload is not { } payload) return worldEvent.Kind switch
        { "town_admission_accepted" => "Someone became a Town resident.", _ => $"{EventVocabulary.HumanizeIdentifier(worldEvent.Kind)}." };
        if (payload is ChoiceRefusedEventPayload refusal)
            return $"{refusal.ActorName} didn't {refusal.Activity}: {refusal.Explanation}.";
        if (payload is MarriageAcceptedEventPayload marriage)
            return $"{marriage.InitiatorName} and {marriage.InviteeName} agreed to marry. They haven't chosen a shared surname yet.";
        if (payload is MarriageSurnameAgreedEventPayload agreed)
            return $"{agreed.CoupleName} completed their marriage and agreed to share the surname {agreed.Surname}.";
        if (payload is MarriageSurnameDrawEventPayload draw)
            return $"{draw.CoupleName} completed their marriage. A draw chose {draw.Surname} after four turns without agreement.";
        if (payload is MarriageEndedEventPayload ended)
            return ended.EndedByDeath ? $"The marriage between {ended.FirstName} and {ended.SecondName} ended after a partner died."
                : $"The marriage between {ended.FirstName} and {ended.SecondName} ended by separation.";
        if (payload is MarriageSurnameBlockedEventPayload)
            return "The shared surname would make a name too long. Shorten the agent's name and resume the surname conversation; both names are unchanged.";
        string ThingAt(int index) => ContentName(payload, snapshot) ?? EventVocabulary.HumanizeIdentifier(DefinitionId(payload) ?? "something new");
        var buildingName = snapshot?.PlacedBuildings.FirstOrDefault(building => payload.SubjectIds.Contains(building.InstanceId))?.DisplayName ?? "building";
        var guestName = snapshot?.Agents.FirstOrDefault(person => payload.SubjectIds.Contains(person.Id))?.DisplayName ?? "The guest";
        var civicTownName = TownNamed(snapshot, payload, "A Town")!;
        var townProjectName = TownProjectForEvent(snapshot, payload)?.Name ?? "a Town project";
        var townProjectSubject = townProjectName == "a Town project" ? "A Town project" : townProjectName;
        var marketFields = PipeFields(payload);
        var marketSeller = marketFields is [_, _, _, _, ..] ? Name(snapshot, marketFields[3]) : "Someone";
        var marketBuyer = marketFields is [_, _, _, _, _, ..] ? Name(snapshot, marketFields[4]) : "a customer";
        var marketBuyerSubject = marketFields is [_, _, _, _, _, ..] ? marketBuyer : "A customer";
        var animalName = snapshot?.Animals.FirstOrDefault(animal => animal.Id == PrimaryAnimalId(payload))?.Name ?? "An animal";

        return worldEvent.Kind switch
        {
            "action_refused" => DescribeActionRefusal(payload, snapshot),
            "developer_edit" => DescribeDeveloperEdit(payload, snapshot),
            "animals_arrived" => "Wild animals have appeared in the area.",
            "animal_tamed" => $"{animalName} was tamed and joined {LeadingName(snapshot, payload)}'s household.",
            "animal_cared" => $"{animalName} got a day's feed and water.",
            "animal_product_ready" => $"{animalName}'s product is ready to collect.",
            "animal_product_collected" => $"{animalName}'s product was collected for its household.",
            "animal_hide_collected" => $"An adult collected {animalName}'s hide; it was a wild animal that died of old age.",
            "animal_breeding_started" => $"{animalName} is expecting a young one; a place is kept for it.",
            "animal_born" => $"A {YoungAnimal(snapshot, payload)}, {animalName}, was born.",
            "animal_died" => $"{animalName} died of old age.",
            "animal_saddled" => $"{animalName} now wears a saddle.",
            "horse_mounted" => $"An adult mounted {animalName}.",
            "horse_dismounted" => $"An adult got off {animalName}; spare goods they could not carry on foot were left where they stopped.",
            "animal_permission_changed" => $"{animalName}'s care or riding permissions changed.",
            "animal_trade_offered" => $"A household offered {animalName}; the receiving adult must agree.",
            "animal_transferred" => $"{animalName} now belongs to {PartyName(snapshot, (payload as AnimalTransferredEventPayload)?.ReceivingHouseholdId ?? string.Empty)}.",
            "animal_yard_supplied" => "Feed or water was brought to an animal yard.",
            "milk_drunk" => $"{Name(snapshot, payload.ActorId ?? "")} drank milk.",
            "world_created" => "A new world has begun.",
            "world_started" => "Time started. Your agents now live on their own.",
            "weather_changed" => $"The weather turned to {(payload as WeatherChangedEventPayload)?.AnchorWeather.ToLowerInvariant()}.",
            "building_placed" => $"A new {ThingAt(1)} was built.",
            "building_expansion_started" => $"Work began on more storage for the {buildingName}.",
            "building_expanded" => $"The {buildingName} now has more storage.",
            "building_expansion_cancelled" => $"Work on the {buildingName}'s storage stopped. Its materials are free to use again.",
            "expansion_order_paused" => "The expansion you ordered is paused while the worker sees to food or warmth. Its materials stay set aside.",
            "expansion_order_resumed" => "The expansion you ordered has started again.",
            "house_guest_invited" => $"{guestName} may shelter in the {buildingName} during storms.",
            "house_guest_revoked" => $"{guestName} can no longer shelter in the {buildingName} during storms.",
            "build_started" => $"Work began on a new {ThingAt(1)}.",
            "build_completed" => $"The new {ThingAt(1)} is finished.",
            "recipe_started" => $"Work began on {ThingAt(1).ToLowerInvariant()}.",
            "recipe_completed" => $"A batch of {ThingAt(1).ToLowerInvariant()} was made.",
            "production_output_unsupported" => "Work on fresh water stopped because it needs a water jug. Its materials are free to use again.",
            "house_tool_made" => $"{Name(snapshot, Field(payload, 0))} made a {EventVocabulary.ItemName(Field(payload, 1)).ToLowerInvariant()}.",
            "field_work_started" => $"{LeadingName(snapshot, payload)} started work on a field.",
            "field_prepared" => $"{LeadingName(snapshot, payload)} tilled a field.",
            "field_planted" => $"{LeadingName(snapshot, payload)} planted {(Crop(payload).Length == 0 ? "something new" : EventVocabulary.HumanizeIdentifier(Crop(payload)).ToLowerInvariant())}.",
            "field_tended" => $"{LeadingName(snapshot, payload)} tended {(Crop(payload).Length == 0 ? "something new" : EventVocabulary.HumanizeIdentifier(Crop(payload)).ToLowerInvariant())}.",
            "field_harvested" => $"{LeadingName(snapshot, payload)} harvested {(Crop(payload).Length == 0 ? "something new" : EventVocabulary.HumanizeIdentifier(Crop(payload)).ToLowerInvariant())}.",
            "field_ready" => "A field is ready to harvest.",
            "field_work_interrupted" => "Work on a field stopped before it was finished.",
            "field_returned_to_grass" => "A field nobody worked for a season went back to grass.",
            "crop_weather_loss" => $"{(payload is CropWeatherLossEventPayload { Weather: "snow" } ? "Snow" : "A storm")} damaged a crop, so its harvest will be smaller.",
            "crop_moisture_effect" => payload is CropMoistureEffectEventPayload { Effect: "wet" }
                ? "Damp soil gave a crop a bigger harvest."
                : "Dry soil gave a crop a smaller harvest.",
            "food_harvested" => $"{Name(snapshot, payload.ActorId ?? "")} gathered food.",
            "food_consumed" => $"{Name(snapshot, payload.ActorId ?? "")} ate.",
            "tree_planted" => $"{LeadingName(snapshot, payload)} planted a tree.",
            "tree_replanted" => $"{LeadingName(snapshot, payload)} replanted a tree.",
            "agent_slept" => $"{Name(snapshot, payload.ActorId ?? "")} slept.",
            "child_born" => DescribeBirth(snapshot, payload.ActorId ?? ""),
            "agent_removed" => $"{Name(snapshot, payload.ActorId ?? "")} died.",
            "estate_will_accepted" => $"{EstateOwner(snapshot, payload)}'s will was accepted for their estate.",
            "estate_will_default" => $"{EstateOwner(snapshot, payload)}'s estate will use default inheritance.",
            "partnership_accepted" => Partner(snapshot, payload.ActorId ?? "", worldEvent.WorldTick) is { } partner
                ? $"{Name(snapshot, payload.ActorId ?? "")} and {partner} became partners."
                : $"{Name(snapshot, payload.ActorId ?? "")} formed a partnership.",
            "partnership_ended" => $"{Name(snapshot, payload.ActorId ?? "")}'s partnership ended.",
            "continuity_rule_on" => DescribeContinuity(true, payload),
            "continuity_rule_off" => DescribeContinuity(false, payload),
            "caregiver_assigned" => Carer(snapshot, payload.ActorId ?? "", worldEvent.WorldTick + 1) is { } carer
                ? $"{carer} is now caring for {Name(snapshot, payload.ActorId ?? "")}."
                : $"{Name(snapshot, payload.ActorId ?? "")} has a new caregiver.",
            "guardian_needed" => $"{Name(snapshot, payload.ActorId ?? "")} needs a guardian. No adult has agreed to look after them yet.",
            "guardian_assigned" => Carer(snapshot, payload.ActorId ?? "", worldEvent.WorldTick) is { } guardian
                ? $"{guardian} agreed to look after {Name(snapshot, payload.ActorId ?? "")}."
                : $"An adult agreed to look after {Name(snapshot, payload.ActorId ?? "")}.",
            "guardian_placement_pending" => $"{Name(snapshot, payload.ActorId ?? "")} has a guardian and is waiting to move into their House.",
            "guardian_placement_completed" => $"{LeadingName(snapshot, payload)} moved into their guardian's House.",
            "guardian_placement_cancelled" => $"{Name(snapshot, payload.ActorId ?? "")}'s move to their guardian's House was called off.",
            "medical_care_allowed" => $"{LeadingName(snapshot, payload)} agreed to let {AfterMarker(snapshot, payload, ":medical_caregiver:", "someone")} treat them.",
            "medical_care_revoked" => $"{LeadingName(snapshot, payload)} no longer lets {AfterMarker(snapshot, payload, ":medical_caregiver:", "that caregiver")} treat them.",
            "medical_treatment_started" => $"{LeadingName(snapshot, payload)} began a course of medicine.",
            "medical_treatment_completed" => $"{Name(snapshot, payload.ActorId ?? "")} finished a course of medicine.",
            "medical_treatment_interrupted" => $"{Name(snapshot, payload.ActorId ?? "")}'s treatment stopped partway. Medicine already taken is used up.",
            "child_collected_household" => $"{LeadingName(snapshot, payload)} picked up a small household load to bring home.",
            "child_delivered_household" => $"{LeadingName(snapshot, payload)} brought a small household load to their House.",
            "empty_vessel_picked_up" => $"{LeadingName(snapshot, payload)} picked up an empty container to bring home.",
            "ornament_worn" => $"{LeadingName(snapshot, payload)} put on an ornament.",
            "ornament_removed" => $"{LeadingName(snapshot, payload)} took off an ornament.",
            "ornament_given" => $"{LeadingName(snapshot, payload)} gave an ornament to {OrnamentGiftRecipient(snapshot, payload)}.",
            "food_policy_adopted" => (payload as FoodPolicyAdoptedEventPayload)?.Policy switch
            {
                "essential_first" => "Scarce shared household food is now reserved for hungry members.",
                "open" => "Shared household food is open to everyone again.",
                _ => "The rule for shared household food changed.",
            },
            "barter_completed" => $"{Name(snapshot, payload.ActorId ?? "")} completed a trade.",
            "tool_request_placed" => $"{LeadingName(snapshot, payload)} asked the Blacksmith for a tool.",
            "tool_request_accepted" => $"{LeadingName(snapshot, payload)} agreed to make the tool.",
            "tool_request_refused" => $"{LeadingName(snapshot, payload)} turned down a request for a tool.",
            "tool_request_withdrawn" => $"{LeadingName(snapshot, payload)} no longer wants the tool they asked for.",
            "tool_request_ready" => "A requested tool is ready. It's handed over once it's paid for.",
            "tool_request_completed" => "A requested tool was paid for and handed over.",
            "tool_request_interrupted" => "Work on a requested tool stopped. Nothing was lost.",
            "shop_trade_offered" => $"{LeadingName(snapshot, payload)} offered to buy goods at a shop. The goods are held for them.",
            "shop_trade_completed" => $"{LeadingName(snapshot, payload)} bought goods at a shop.",
            "shop_trade_cancelled" => $"{LeadingName(snapshot, payload)}'s purchase at a shop was called off. Nothing changed hands.",
            "store_stock_collected" => $"{LeadingName(snapshot, payload)} is carrying goods to their Store.",
            "store_stock_delivered" => $"{LeadingName(snapshot, payload)} brought goods to their Store to sell.",
            "handcart_attached" => $"{LeadingName(snapshot, payload)} attached their handcart.",
            "handcart_parked" => $"{LeadingName(snapshot, payload)} parked their handcart.",
            "handcart_loaded" => $"{LeadingName(snapshot, payload)} loaded goods into their handcart.",
            "handcart_unloaded" => $"{LeadingName(snapshot, payload)} unloaded goods from their handcart.",
            "handcart_repaired" => $"{LeadingName(snapshot, payload)} repaired their handcart.",
            "handcart_transferred" => $"{LeadingName(snapshot, payload)} gave their handcart and its load to {LastPerson(snapshot, payload, "someone nearby")}.",
            "handcart_blocked" => $"{LeadingName(snapshot, payload)}'s handcart can't go this way. Park it or choose another route; its load is safe.",
            "owner_stock_picked_up" => $"{LeadingName(snapshot, payload)} picked up goods for the delivery you ordered.",
            "owner_stock_delivered" => $"{LeadingName(snapshot, payload)} delivered the goods you ordered.",
            "carrying_full" => $"{Name(snapshot, payload.ActorId ?? "")} cannot carry more; a load needs to be stored or set down.",
            "spare_cargo_stored" => $"{LeadingName(snapshot, payload)} set down spare supplies for their household to make room in their load.",
            "household_delivery_recovered" => $"{LeadingName(snapshot, payload)} brought back supplies they couldn't deliver and left them at their household's pile.",
            "equipment_equipped" => $"{Name(snapshot, payload.ActorId ?? "")} equipped an item.",
            "equipment_repair_started" => $"{Name(snapshot, payload.ActorId ?? "")} began repairing an item.",
            "equipment_repaired" => $"{Name(snapshot, payload.ActorId ?? "")} repaired an item.",
            "equipment_repair_interrupted" => "Repair stopped; its unused materials are available again.",
            "agent_knowledge_artifact_created" or "agent_knowledge_artifact_read" or "agent_knowledge_shared" or
                "agent_knowledge_writing_started" or "agent_knowledge_writing_cancelled" or "agent_knowledge_material_collected" or
                "agent_knowledge_artifact_collected" or "agent_knowledge_artifact_stored" =>
                DescribeWrittenKnowledge(worldEvent, snapshot),
            "skill_learned" => DescribeSkill(payload, snapshot),
            "agent_recipe_learned" => $"{Name(snapshot, payload.ActorId ?? "")} learned a recipe through practice.",
            "agent_building_proposed" => $"{LeadingName(snapshot, payload)} suggested a new building design.",
            "instruction_order_status" => $"{Name(snapshot, payload.ActorId ?? "")}'s order is blocked. See their Orders for the reason.",
            "instruction_order_cancelled" => $"{Name(snapshot, payload.ActorId ?? "")}'s order was cancelled. See their Orders for details.",
            "instruction_not_understood" => $"{Name(snapshot, payload.ActorId ?? "")} didn't understand your order. " +
                "Try one of the listed tasks, or use Suggest instead.",
            "town_civic_law" => $"{civicTownName} recorded a law decision. See the Towns page for its wording and scope.",
            "town_civic_government" => $"{civicTownName} recorded a resident government decision. See the Towns page for the vote or handover.",
            "town_civic_mayor" => $"{civicTownName} recorded a mayoral election or office change. See the Towns page for its result.",
            "market_built" => $"{civicTownName}'s Market was built. Adults can take a stall there to sell goods.",
            "market_stall_built" => $"{civicTownName}'s Market gained another stall.",
            "market_stall_borrowed" => $"{marketSeller} took a free stall at the Market.",
            "market_stock_loaded" => $"{marketSeller} is carrying household goods to the Market.",
            "market_stock_delivered" => $"{marketSeller} set out goods at their Market stall.",
            "market_stock_collected" => $"{marketSeller} took their goods back from the Market stall.",
            "market_stall_left" => $"{marketSeller} gave up their Market stall.",
            "market_trade_offered" => $"{marketBuyerSubject} offered {marketSeller} a trade at the Market.",
            "market_trade_completed" => $"{marketBuyerSubject} bought goods from {marketSeller} at the Market.",
            "market_trade_cancelled" => $"{marketSeller} and {marketBuyer}'s Market trade was called off. Nothing changed hands.",
            "town_civic_council" => $"{civicTownName}'s council changed.",
            "town_civic_election" => $"{civicTownName}'s council election opened.",
            "town_civic_runoff" => $"{civicTownName}'s council election was tied, so there will be a runoff.",
            "town_civic_proposal" => $"A proposal was submitted to {civicTownName}'s council.",
            // Rulings and household transfers post their outcome as a result notice, but no council decided them.
            "town_civic_result" when Field(payload, 1).StartsWith("land-ruling:", StringComparison.Ordinal) =>
                DescribeLandRulingResult(worldEvent, snapshot, civicTownName),
            "town_civic_result" when Field(payload, 1).StartsWith("land-transfer:", StringComparison.Ordinal) =>
                $"{civicTownName} posted the outcome of a land handover between households. See the Towns page.",
            "town_civic_result" => $"{civicTownName}'s council made a decision. See the Towns page for the result.",
            "town_civic_land_use" => $"A household land request in {civicTownName} has new information. See its plot for approval progress.",
            "land_use_granted" => $"{LandHousehold(snapshot, payload)} may now use a plot in {LandTown(snapshot, payload)}. Turn on the household use filter to see it.",
            "land_use_requested" => $"{LandHousehold(snapshot, payload)} asked to use a plot in {LandTown(snapshot, payload)}. The council must approve it first.",
            "town_civic_nonviolent_hearing" => $"{civicTownName}'s vote on who will judge a case has news. See the Towns page.",
            "town_civic_law_case" => $"{civicTownName} posted notice of a case. See the Towns page for the complaint and the deadline to answer.",
            "town_civic_remedy" => $"{civicTownName} posted an offer to settle a case. Nobody has agreed to it yet.",
            "law_case_opened" or "law_case_evidence" or "law_case_response" or "law_case_inspected" or
            "law_case_relayed" or "law_case_judge_consent" or "law_case_judge_election" or "law_case_judge_assigned" or
            "law_case_finding" or "law_case_reopen_requested" or "law_case_reopened" or "law_case_rejected" or
            "law_case_offer" or "law_case_offer_response" or "law_case_remedy_effect" => DescribeNonLandHearing(worldEvent, snapshot),
            "town_civic_land_hearing" => $"{civicTownName} posted notice of a land case. See the Towns page for the plot and the deadline.",
            "land_case_opened" or "land_case_notice" or "land_case_evidence" or "land_case_response" or
            "land_case_judge_consent" or "land_case_judge_election" or "land_case_judge_assigned" or
            "land_case_ruling" or "land_case_reopen_requested" or "land_case_reopened" or
            "land_case_inspected" or "land_case_relayed" or "land_case_rejected" => DescribeLandHearing(worldEvent, snapshot),
            "land_transfer_proposed" or "land_transfer_read" or "land_transfer_consent" or "land_transfer_withdrawn" or
            "land_transfer_settled" or "land_transfer_blocked" => DescribeLandTransfer(worldEvent, snapshot),
            "town_land_claimed" => $"{civicTownName}'s council claimed the land next to the Town. Turn on the Town title filter to see it.",
            "town_civic_cancelled" => $"An unfinished election in {civicTownName} was cancelled.",
            "town_project_approved" => $"The council approved {townProjectName}. It still needs materials and builders.",
            "town_project_blocked" => $"Work on {townProjectName} is blocked. See the Towns page for what is needed.",
            "town_project_resumed" => $"Work on {townProjectName} can continue.",
            "town_project_cancelled" => $"{townProjectSubject} can't be built where it was approved, so the Town stopped it. " +
                "Its materials stay where they are; see the Towns page for why.",
            "town_project_donated" => $"{LeadingName(snapshot, payload)} donated personal materials to {townProjectName}.",
            "town_project_material_picked_up" => $"{LeadingName(snapshot, payload)} picked up materials for {townProjectName}.",
            "town_project_material_recovered" => $"{LeadingName(snapshot, payload)} is taking unused materials from {townProjectName} back to the Town Warehouse.",
            "town_project_material_delivered" => $"{LeadingName(snapshot, payload)} brought materials to the site for {townProjectName}.",
            "town_project_material_returned" => payload is TownProjectMaterialReturnedEventPayload { DestinationId: "ground" }
                ? $"{LeadingName(snapshot, payload)} set down unused Town materials from {townProjectName}."
                : $"{LeadingName(snapshot, payload)} returned unused materials from {townProjectName} to the Town Warehouse.",
            "town_project_worked" => $"{LeadingName(snapshot, payload)} worked on {townProjectName}.",
            "town_project_completed" => $"{townProjectSubject} is finished.",
            "town_founding_started" => "Your first Town is being set up.",
            "town_resident_joined" => $"{ResidentName(snapshot, worldEvent)} joined {ResidentTownName(snapshot, worldEvent)}.",
            "town_resident_left" => $"{ResidentName(snapshot, worldEvent)} left {ResidentTownName(snapshot, worldEvent)}.",
            "town_membership_evaluated" => $"{LeadingName(snapshot, payload)} is now an adult but doesn't belong to a Town yet.",
            "town_admission_accepted" => DescribeAdmission(payload, snapshot),
            "town_abandoned" => $"{civicTownName} has no living residents and is abandoned. Its buildings, border and Roads remain.",
            "town_revived" => $"People live in {civicTownName} again. Its shared stock is theirs to use.",
            "town_resettled" => $"{Name(snapshot, Field(payload, 1))} moved into abandoned {civicTownName}. Its laws and everyone's property stay as they were.",
            "town_stock_salvaged" => $"{Name(snapshot, Field(payload, 1))} salvaged {Field(payload, 3)} {EventVocabulary.ItemName(Field(payload, 2)).ToLowerInvariant()} from abandoned {civicTownName}.",
            "town_admission_approved" => $"{civicTownName}'s council agreed to let {Name(snapshot, Field(payload, 1))} join. They have one game day to accept.",
            "town_admission_lapsed" when Field(payload, 3) == "acceptance_expired" => $"{Name(snapshot, Field(payload, 1))} didn't accept in time, so their place in {civicTownName} lapsed. They can ask the council again.",
            "town_admission_lapsed" => $"{Name(snapshot, Field(payload, 1))} didn't join {civicTownName}: the approval no longer fits their situation.",
            "town_building_assigned" => DescribeTownBuilding(snapshot, payload),
            "town_border_expanded" => $"{Sentence(TownNamed(snapshot, payload, null) ?? "the Town")}'s border grew.",
            "town_founded" when payload is TownFoundedEventPayload { IsFirstTown: false } => $"{Name(snapshot, Field(payload, 1))} founded {civicTownName}.",
            "town_founded" => TownNamed(snapshot, payload, null) is { } founded
                ? $"{founded}, your first Town, was founded." : "Your first Town was founded.",
            "bridge_built" when payload is BridgeBuiltEventPayload { Trigger: "road" } => "A bridge was built where a new Road crosses the river.",
            "bridge_built" => "Agents crossed a river here so often that a bridge was built.",
            "household_work_resumed" => $"{Name(snapshot, payload.ActorId ?? "")} picked up household work that had been left unfinished.",
            "household_left" => DescribeHouseholdDeparture(snapshot, payload),
            "household_founded" => $"{Name(snapshot, payload.ActorId ?? "")} started a new household. They'll need to build a House.",
            "personal_goods_collected" => $"{Name(snapshot, payload.ActorId ?? "")} collected their personal belongings.",
            "personal_goods_stored" => $"{Name(snapshot, payload.ActorId ?? "")} stored their belongings. They still own them.",
            "borrowed_goods_returned" => $"{Name(snapshot, payload.ActorId ?? "")} returned household goods.",
            "replacement_care_accepted" => $"{Name(snapshot, payload.ActorId ?? "")} agreed to take over caring for {Name(snapshot, Field(payload, 1))}.",
            "housing_request_made" => $"{LeadingName(snapshot, payload)} asked {HouseholdAfterAgent(snapshot, payload)} for a place to live in their House.",
            "household_joined" => $"{LeadingName(snapshot, payload)} now lives with {HouseholdAfterAgent(snapshot, payload)}.",
            "housing_request_refused" => $"{HouseholdAfterAgent(snapshot, payload)} did not agree to let {LeadingName(snapshot, payload)} move in.",
            "housing_request_expired" => $"{HouseholdAfterAgent(snapshot, payload)} did not answer {LeadingName(snapshot, payload)}'s request to move in.",
            "relocation_notice" => DescribeRelocationNotice(snapshot, payload),
            "relocation_cancelled" => DescribeRelocationCancellation(snapshot, payload),
            "housing_blocked" when payload is HousingBlockedEventPayload { Blocker: "overcrowded" } =>
                $"{LeadingName(snapshot, payload)}'s House is overcrowded: it has more residents than places.",
            "housing_blocked" when payload is HousingBlockedEventPayload { Blocker: "awaiting_answer" } =>
                $"{LeadingName(snapshot, payload)} is waiting for every adult in the other household to agree to the move.",
            "housing_blocked" => $"{LeadingName(snapshot, payload)} has no home: {HousingReason(payload)}.",
            "exploration_started" or "exploration_return_started" or "exploration_completed" or "exploration_aborted" =>
                DescribeScouting(worldEvent, snapshot),
            "paused" => "The world was paused.",
            "resumed" => "The world resumed.",
            "model_call_warning" => DescribeModelCallWarning(payload),
            "model_attempt_status" => DescribeModelFailure(payload, snapshot),
            _ => $"{EventVocabulary.HumanizeIdentifier(worldEvent.Kind)}.",
        };
    }

    private static string DescribeNonLandHearing(EventView worldEvent, WorldSnapshotView? snapshot)
    {
        var fields = PipeFields(worldEvent.Payload!);
        var town = snapshot?.Towns.FirstOrDefault(item => item.Id == fields[0])?.Name ?? "A Town";
        var actor = fields.Length == 3 ? snapshot?.Agents.FirstOrDefault(person => person.Id == fields[2])?.DisplayName : null;
        return worldEvent.Kind switch
        {
            "law_case_opened" => $"{town} opened a case about someone's conduct. Nothing has been decided yet.",
            "law_case_evidence" => $"New evidence was added to a case in {town}.",
            "law_case_response" => $"{actor ?? "Someone"} answered a case in {town}.",
            "law_case_inspected" => $"{actor ?? "Someone"} read the file for a case in {town}.",
            "law_case_relayed" => $"{actor ?? "Someone"} told someone nearby about a case.",
            "law_case_judge_consent" => $"An adult in {town} changed their willingness to judge a case.",
            "law_case_judge_election" => $"{town}'s vote on who will judge a case has news. See the Towns page.",
            "law_case_judge_assigned" => $"{town} chose a judge for a case.",
            "law_case_finding" => $"{town} reached a decision in a case. See the Towns page for the reasons.",
            "law_case_reopen_requested" => $"Someone asked {town} to hear a case again. Earlier decisions stand for now.",
            "law_case_reopened" => $"{town} reopened a case.",
            "law_case_offer" => $"Someone offered to settle a case in {town}. Everyone who would contribute must agree.",
            "law_case_offer_response" => $"{actor ?? "Someone"} answered an offer to settle a case.",
            "law_case_remedy_effect" => $"{actor ?? "Someone"} did their part of an agreed settlement in {town}.",
            _ => $"{town} turned down a request in a case. Nothing was imposed.",
        };
    }

    private static string DescribeLandRulingResult(EventView worldEvent, WorldSnapshotView? snapshot, string townName)
    {
        var town = snapshot?.Towns.FirstOrDefault(item => item.Id == Field(worldEvent.Payload!, 0));
        var rulingId = Field(worldEvent.Payload!, 1);
        var hearing = town?.LandHearings.FirstOrDefault(item => item.Rulings.Any(ruling => ruling.Id == rulingId));
        var subject = hearing?.Kind == "property" ? "property ruling" : "land ruling";
        var outcome = hearing?.Kind switch
        {
            "property" => "ownership outcome",
            "dispute" or "expiry" => "use right change",
            _ => "outcome",
        };
        return $"{townName} posted the result of a {subject}. See the Towns page for its {outcome} and reasons.";
    }

    private static string DescribeLandTransfer(EventView worldEvent, WorldSnapshotView? snapshot)
    {
        var fields = PipeFields(worldEvent.Payload!);
        var town = fields.Length == 5 ? snapshot?.Towns.FirstOrDefault(item => item.Id == fields[0]) : null;
        var transfer = fields.Length == 5 ? town?.LandTransfers.FirstOrDefault(item => item.Id == fields[1]) : null;
        var subject = "the land handover";
        if (transfer is not null) subject += " from " + string.Join(" and ", transfer.Parties.Where(party => party.Kind == "source")
            .Select(party => party.HouseholdName)) + " to " + transfer.TargetHouseholdName;
        var actor = fields.Length == 5 ? snapshot?.Agents.FirstOrDefault(item => item.Id == fields[3])?.DisplayName : null;
        return worldEvent.Kind switch
        {
            "land_transfer_proposed" => $"{actor ?? "An adult"} proposed {subject} in {town?.Name ?? "a Town"}. Every adult involved must agree.",
            "land_transfer_read" => $"{actor ?? "An adult"} read the terms of {subject}.",
            "land_transfer_consent" => $"{actor ?? "An adult"} " + (fields.Length == 5 && fields[4] == "decline"
                ? "turned down" : "agreed to") + $" {subject}.",
            "land_transfer_withdrawn" => $"{actor ?? "The proposer"} withdrew {subject}. Nothing changed.",
            "land_transfer_settled" => $"{Sentence(subject)} went ahead after everyone involved agreed.",
            _ => $"{Sentence(subject)} stopped because its terms no longer apply. Nothing changed.",
        };
    }

    private static string Sentence(string text) => char.ToUpperInvariant(text[0]) + text[1..];

    private static string DescribeLandHearing(EventView worldEvent, WorldSnapshotView? snapshot)
    {
        var fields = PipeFields(worldEvent.Payload!);
        var town = fields is [_, _, ..] ? snapshot?.Towns.FirstOrDefault(item => item.Id == fields[0]) : null;
        var caseId = fields is [_, _, ..] ? fields[1] : "";
        var number = !string.IsNullOrEmpty(caseId) ? caseId[(caseId.LastIndexOf(':') + 1)..] : "";
        var subject = !string.IsNullOrEmpty(number) ? "land case " + number : "a land case";
        var townName = town?.Name ?? "A Town";
        var actor = fields.Length == 5 ? snapshot?.Agents.FirstOrDefault(item => item.Id == fields[3])?.DisplayName : null;
        var action = fields.Length == 5 ? fields[4] : "";
        return worldEvent.Kind switch
        {
            "land_case_opened" => $"{townName} opened {subject}. Current land rights stay in place until it's decided.",
            "land_case_notice" => $"{townName} posted notice for {subject}. See the Towns page for the deadline.",
            "land_case_evidence" => $"New evidence was added to {subject}.",
            "land_case_response" => $"{actor ?? "An affected adult"} " +
                (action switch
                {
                    "waive" => "chose not to respond",
                    "property_accept" => "personally agreed to the property transfer",
                    "property_refuse" => "refused the property transfer",
                    _ => "recorded an answer"
                }) + $" in {subject}.",
            "land_case_judge_consent" => $"{actor ?? "An adult resident"} " + (action switch
            {
                "judge_withdraw" => "withdrew their offer to judge the case",
                "judge_resign" => "resigned from judging the case",
                _ => "agreed to judge the case",
            }) + $" for {subject}.",
            "land_case_judge_election" => $"{townName}'s vote for {subject} has news. See the Towns page.",
            "land_case_judge_assigned" => $"{actor ?? "A judge"} will judge {subject}.",
            "land_case_ruling" => $"{townName} ruled on {subject}. See the Towns page for what changed and why.",
            "land_case_reopen_requested" => $"Someone asked {townName} to rehear {subject}. Current rights still stand.",
            "land_case_reopened" => $"{townName} reopened {subject}.",
            "land_case_inspected" => $"{actor ?? "Someone"} read the file for {subject}.",
            "land_case_relayed" => $"{actor ?? "Someone"} told someone nearby about {subject}.",
            _ => $"{townName} turned down a request in {subject}. No property changed hands.",
        };
    }

    private static TownProjectView? TownProjectForEvent(WorldSnapshotView? snapshot, WorldEventPayload payload) =>
        snapshot?.Towns.SelectMany(town => town.Projects).FirstOrDefault(project => payload.SubjectIds.Contains(project.Id));

    private static string DescribeHouseholdDeparture(WorldSnapshotView? snapshot, WorldEventPayload payload)
    {
        var fields = PipeFields(payload);
        var actor = Name(snapshot, fields[0]);
        return fields is [_, _, _, ..] && fields[2] == "displaced"
            ? $"{actor} moved out of their overcrowded House. They can go back for their belongings."
            : $"{actor} left their household. They can go back for their belongings.";
    }

    private static string DescribeRelocationNotice(WorldSnapshotView? snapshot, WorldEventPayload payload)
    {
        var fields = PipeFields(payload);
        var actor = Name(snapshot, fields[0]);
        return (fields is [_, _, _, ..] ? fields[2] : string.Empty) switch
        {
            "volunteer" => $"{actor} volunteered to move out of their overcrowded House.",
            "latest_unrelated_arrival" => $"{actor} must move out of their overcrowded House. They're the newest arrival who isn't family.",
            "latest_arrival" => $"{actor} must move out of their overcrowded House. They're its newest arrival.",
            _ => $"{actor} has notice to move out of their overcrowded House.",
        };
    }

    private static string DescribeRelocationCancellation(WorldSnapshotView? snapshot, WorldEventPayload payload)
    {
        var fields = PipeFields(payload);
        var reason = (fields is [_, _, _, ..] ? fields[2] : string.Empty) switch
        {
            "room" => "the House now has enough places",
            "care" => "their dependent children still need their care",
            "family" => "the household's family arrangements changed",
            "replaced" => "another adult volunteered to move instead",
            "no_house" => "the household no longer holds that House",
            _ => "the household's housing needs changed",
        };
        return $"{Name(snapshot, fields[0])}'s move-out notice was cancelled: {reason}.";
    }

    /// <summary>A recorded Town admission: who joined, any Town they left and whether dependent children came too.</summary>
    private static string DescribeAdmission(WorldEventPayload payload, WorldSnapshotView? snapshot)
    {
        var fields = PipeFields(payload);
        if (fields.Length != 4) return "Someone became a Town resident.";
        string TownName(string id) => snapshot?.Towns.FirstOrDefault(town => town.Id == id)?.Name ?? "a Town";
        var text = $"{Name(snapshot, fields[1])} became a resident of {TownName(fields[0])}.";
        if (fields[2] != "none") text += $" They are no longer a resident of {TownName(fields[2])}.";
        if (int.TryParse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture, out var members) && members > 1)
            text += " Their dependent children moved with them.";
        return text;
    }

    /// <summary>A newborn agent, with the parents their record names when it names any.</summary>
    private static string DescribeBirth(WorldSnapshotView? snapshot, string childId)
    {
        var parents = snapshot?.Agents.FirstOrDefault(person => person.Id == childId)?.Relationships
            .Where(item => item.Type == "biological_parentage" && item.Direction == "child")
            .Select(item => Name(snapshot, item.OtherPartyId)).Where(name => name != "Someone").ToArray() ?? [];
        return parents.Length == 0 ? $"{Name(snapshot, childId)} was born."
            : $"{Name(snapshot, childId)} was born to {string.Join(" and ", parents)}.";
    }

    /// <summary>The partner whose accepted partnership became effective on the tick after the event, so a later partner never stands in.</summary>
    private static string? Partner(WorldSnapshotView? snapshot, string agentId, long tick) =>
        RelatedAt(snapshot, agentId, tick + 1, "partnership");

    /// <summary>The accepted adult carer at the supplied effective tick; infant care begins one tick after its event, new guardianship at the event tick.</summary>
    private static string? Carer(WorldSnapshotView? snapshot, string childId, long tick) =>
        RelatedAt(snapshot, childId, tick, "caregiver", "legal_guardian");

    private static string? RelatedAt(WorldSnapshotView? snapshot, string agentId, long tick, params string[] types)
    {
        var other = snapshot?.Agents.FirstOrDefault(person => person.Id == agentId)?.Relationships
            .FirstOrDefault(item => types.Contains(item.Type) && item.State == "accepted" && item.EffectiveTick == tick)?.OtherPartyId;
        return other is null ? null : snapshot?.Agents.FirstOrDefault(person => person.Id == other)?.DisplayName;
    }

    /// <summary>The agent whose estate an event settles. Estate IDs are <c>estate:&lt;agent&gt;:&lt;tick&gt;</c>.</summary>
    private static string EstateOwner(WorldSnapshotView? snapshot, WorldEventPayload payload) => Name(snapshot, payload.ActorId ?? "");

    /// <summary>The related agent named by a payload subject, such as a medical caregiver.</summary>
    private static string AfterMarker(WorldSnapshotView? snapshot, WorldEventPayload payload, string marker, string fallback) =>
        snapshot?.Agents.FirstOrDefault(person => payload.SubjectIds.Contains(person.Id))?.DisplayName ?? fallback;

    /// <summary>The related agent named by a payload subject, such as a handcart's new keeper.</summary>
    private static string LastPerson(WorldSnapshotView? snapshot, WorldEventPayload payload, string fallback) =>
        snapshot?.Agents.FirstOrDefault(person => payload.SubjectIds.Contains(person.Id))?.DisplayName ?? fallback;

    /// <summary>The household named inside a land-use event, by its stores name.</summary>
    private static string LandHousehold(WorldSnapshotView? snapshot, WorldEventPayload payload) =>
        snapshot?.Stockpiles.FirstOrDefault(item => payload.SubjectIds.Contains(item.OwnerId))?.Name ?? "A household";

    /// <summary>The Town named inside a land-use event.</summary>
    private static string LandTown(WorldSnapshotView? snapshot, WorldEventPayload payload) =>
        TownNamed(snapshot, payload, "the Town")!;

    /// <summary>The Town named by its typed event fields.</summary>
    private static string? TownNamed(WorldSnapshotView? snapshot, WorldEventPayload payload, string? fallback) =>
        snapshot?.Towns.FirstOrDefault(town => town.Id == EventTownId(payload))?.Name ?? fallback;

    /// <summary>The building named by a Town event payload.</summary>
    private static string DescribeTownBuilding(WorldSnapshotView? snapshot, WorldEventPayload payload)
    {
        var town = TownNamed(snapshot, payload, "the Town");
        var building = snapshot?.PlacedBuildings.FirstOrDefault(item => payload.SubjectIds.Contains(item.InstanceId))?.DisplayName;
        return building is null ? $"A building is now part of {town}." : $"The {building} is now part of {town}.";
    }

    /// <summary>The word for a young animal of the newborn's species, found by its payload subject reference.</summary>
    private static string YoungAnimal(WorldSnapshotView? snapshot, WorldEventPayload payload) =>
        snapshot?.Animals.FirstOrDefault(animal => animal.Id == PrimaryAnimalId(payload))?.Species switch
        { "cow" => "calf", "sheep" => "lamb", "horse" => "foal", "chicken" => "chick", _ => "young animal" };

    /// <summary>A scouting trip: setting out, turning back, coming home or ending early.</summary>
    private static string DescribeScouting(EventView worldEvent, WorldSnapshotView? snapshot)
    {
        var scout = LeadingName(snapshot, worldEvent.Payload!);
        return worldEvent.Kind switch
        {
            "exploration_started" => $"{scout} set out to scout.",
            "exploration_return_started" => $"{scout} is heading back from scouting.",
            "exploration_completed" => $"{scout} came back from scouting.",
            _ when worldEvent.Payload is ExplorationAbortedEventPayload { Reason: "return_blocked" } =>
                $"{scout}'s scouting trip ended before they got back: the way home was blocked.",
            _ => $"{scout}'s scouting trip was cut short.",
        };
    }

    private static string Field(WorldEventPayload payload, int index)
    {
        var fields = PipeFields(payload);
        return index < fields.Length ? fields[index] : string.Empty;
    }

    /// <summary>The installation's one warning at 80% of its model-call limit.</summary>
    private static string DescribeModelCallWarning(WorldEventPayload payload)
    {
        var count = payload is ModelCallWarningEventPayload { Limit: > 0 } warning
            ? string.Create(CultureInfo.InvariantCulture, $"{warning.Used:N0} of {warning.Limit:N0}") : "80% of the limit";
        return $"Model calls: {count} used across all worlds. Your worlds pause at the limit; raise it in Settings → Game.";
    }

    private static string DescribeSkill(WorldEventPayload payload, WorldSnapshotView? snapshot)
    {
        var fields = PipeFields(payload);
        if (fields.Length != 3 || fields[1] is not ("building" or "farming" or "crafting" or "smithing"))
            return "An agent learned a skill.";
        var source = fields[2] == "work" ? "by doing the work" : "from " + Name(snapshot, fields[2]);
        return $"{Name(snapshot, fields[0])} learned {fields[1]} {source}.";
    }

    private static string DescribeWrittenKnowledge(EventView worldEvent, WorldSnapshotView? snapshot)
    {
        var fields = PipeFields(worldEvent.Payload!);
        var author = Name(snapshot, fields[0]);
        var recipient = Name(snapshot, fields.ElementAtOrDefault(1) ?? string.Empty);
        return worldEvent.Kind switch
        {
            "agent_knowledge_artifact_read" => $"{recipient} learned from a written work.",
            "agent_knowledge_shared" => $"{author} shared written knowledge with {recipient}.",
            "agent_knowledge_artifact_collected" => $"{author} picked up a written work.",
            "agent_knowledge_artifact_stored" => $"{author} stored a written work.",
            "agent_knowledge_writing_started" => $"{author} started work on a {EventVocabulary.ItemName(fields.ElementAtOrDefault(1) ?? "record").ToLowerInvariant()}.",
            "agent_knowledge_writing_cancelled" => $"{author} stopped writing. The unused supplies are free again.",
            "agent_knowledge_material_collected" => $"{author} collected writing supplies.",
            _ => fields.ElementAtOrDefault(2) switch
            {
                "field_map" => $"{author} finished drawing a field map.",
                "field_record" => $"{author} finished writing a field record.",
                "book" => $"{author} finished writing a book.",
                _ => $"{author} finished a written work.",
            },
        };
    }

    private static string Name(WorldSnapshotView? snapshot, string id) =>
        snapshot?.Agents.FirstOrDefault(person => person.Id == id)?.DisplayName
        ?? (string.IsNullOrEmpty(id) || id.Contains(':', StringComparison.Ordinal)
            ? "Someone" : EventVocabulary.HumanizeIdentifier(id));

    private static string DescribeModelFailure(WorldEventPayload payload, WorldSnapshotView? snapshot)
    {
        if (payload is not ModelAttemptStatusEventPayload status) return "A model reply failed.";
        var reason = status.Status switch
        {
            "missing_key" => "the model key is missing",
            "usage_limit" => "the usage limit was reached",
            "unusable_reply" => "the model sent an unusable reply",
            "timed_out" => "the model reply timed out",
            _ => "the model is unavailable"
        };
        return $"{Name(snapshot, status.ActorId ?? "")} could not get a model reply: {reason}.";
    }

    private static string LeadingName(WorldSnapshotView? snapshot, WorldEventPayload payload) => Name(snapshot, payload.ActorId ?? "");

    private static string OrnamentGiftRecipient(WorldSnapshotView? snapshot, WorldEventPayload payload) =>
        payload is OrnamentGivenEventPayload gift ? snapshot?.Agents.FirstOrDefault(person => person.Id == gift.RecipientId)?.DisplayName ?? "someone" : "someone";

    /// <summary>The Town a resident joined or left, named as the player sees it; agents now move between Towns.</summary>
    private static string ResidentTownName(WorldSnapshotView? snapshot, EventView worldEvent) =>
        ResidentTown(snapshot, worldEvent)?.Name ?? "a Town";

    private static TownView? ResidentTown(WorldSnapshotView? snapshot, EventView worldEvent) =>
        snapshot?.Towns.FirstOrDefault(town => town.Id == (worldEvent.Payload is { } payload ? EventTownId(payload) : null));

    private static string ResidentName(WorldSnapshotView? snapshot, EventView worldEvent) =>
        Name(snapshot, worldEvent.Payload switch
        {
            TownResidentJoinedEventPayload p => p.ResidentId,
            TownResidentLeftEventPayload p => p.ResidentId,
            _ => ""
        });

    /// <summary>The household named after the leading agent ID, as the player sees it.</summary>
    private static string HouseholdAfterAgent(WorldSnapshotView? snapshot, WorldEventPayload payload) =>
        snapshot?.Stockpiles.FirstOrDefault(item => payload.SubjectIds.Contains(item.OwnerId))?.Name ?? "a household";

    private static string HousingReason(WorldEventPayload payload) => (payload as HousingBlockedEventPayload)?.Blocker switch
    {
        "no_household" => "they belong to no household, so no House can be planned for them",
        "no_authorized_home" => "their household holds no House yet",
        "missing_materials" => "their household lacks the materials for a House",
        "no_legal_site" => "their household has no legal site for a House",
        _ => "no House is available to them yet",
    };



    public static string PartyName(WorldSnapshotView? snapshot, string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return snapshot?.Agents.FirstOrDefault(person => person.Id == id)?.DisplayName
            ?? snapshot?.Stockpiles.FirstOrDefault(stockpile => stockpile.OwnerId == id)?.Name
            ?? snapshot?.Towns.FirstOrDefault(town => town.Id == id)?.Name
            ?? (id.StartsWith("household:", StringComparison.Ordinal) ? "a household" : EventVocabulary.HumanizeIdentifier(id));
    }



    private static string[] PipeFields(WorldEventPayload payload) => payload switch
    {
        LawCaseRemedyEffectEventPayload p => [p.TownId ?? "", p.AgreementId ?? "", p.ActorId ?? ""],
        TownResettledEventPayload p => [p.TownId ?? "", p.ActorId ?? "", p.PreviousTownId ?? "", Convert.ToString(p.MemberCount, CultureInfo.InvariantCulture) ?? ""],
        TownStockSalvagedEventPayload p => [p.TownId ?? "", p.ActorId ?? "", p.ItemKind ?? "", Convert.ToString(p.Quantity, CultureInfo.InvariantCulture) ?? ""],
        TownLandClaimedEventPayload p => [p.TownId ?? "", p.ProposalId ?? "", p.Tiles ?? ""],
        TownAdmissionApprovedEventPayload p => [p.TownId ?? "", p.SubjectId ?? "", p.ProposalId ?? ""],
        TownAdmissionAcceptedEventPayload p => [p.TownId ?? "", p.SubjectId ?? "", p.PreviousTownId ?? "", Convert.ToString(p.MemberCount, CultureInfo.InvariantCulture) ?? ""],
        TownAdmissionLapsedEventPayload p => [p.TownId ?? "", p.SubjectId ?? "", p.ProposalId ?? "", p.Reason ?? ""],
        BorrowedGoodsReturnedEventPayload p => [p.ActorId ?? "", p.ItemKind ?? "", Convert.ToString(p.Quantity, CultureInfo.InvariantCulture) ?? "", p.OwnerId ?? ""],
        HouseholdLeftEventPayload p => [p.ActorId ?? "", p.HouseholdId ?? "", p.Cause ?? "", Convert.ToString(p.Allowance, CultureInfo.InvariantCulture) ?? ""],
        HouseholdWorkResumedEventPayload p => [p.ActorId ?? "", p.JobId ?? "", p.HouseholdId ?? ""],
        HouseholdFoundedEventPayload p => [p.ActorId ?? "", p.HouseholdId ?? ""],
        ReplacementCareAcceptedEventPayload p => [p.ActorId ?? "", p.ChildId ?? ""],
        PersonalGoodsCollectedEventPayload p => [p.ActorId ?? "", p.ItemKind ?? "", Convert.ToString(p.Quantity, CultureInfo.InvariantCulture) ?? "", p.OwnerId ?? ""],
        PersonalGoodsStoredEventPayload p => [p.ActorId ?? "", p.ItemKind ?? "", Convert.ToString(p.Quantity, CultureInfo.InvariantCulture) ?? "", p.ActorId ?? ""],
        RelocationNoticeEventPayload p => [p.ActorId ?? "", p.HouseholdId ?? "", p.Reason ?? "", Convert.ToString(p.Deadline, CultureInfo.InvariantCulture) ?? ""],
        RelocationCancelledEventPayload p => [p.ActorId ?? "", p.HouseholdId ?? "", p.Reason ?? ""],
        SkillLearnedEventPayload p => [p.ActorId ?? "", p.Skill ?? "", p.TeacherId ?? ""],
        EquipmentEquippedEventPayload p => [p.ActorId ?? "", p.EquippedId ?? "", p.ItemKind ?? ""],
        EquipmentRepairStartedEventPayload p => [p.ActorId ?? "", p.TargetId ?? "", p.SiteInstanceId ?? ""],
        EquipmentRepairedEventPayload p => [p.ActorId ?? "", p.LotId ?? ""],
        EquipmentRepairInterruptedEventPayload p => [p.ActorId ?? "", p.LotId ?? ""],
        AgentKnowledgeArtifactCollectedEventPayload p => [p.ActorId ?? "", p.ArtifactId ?? ""],
        AgentKnowledgeArtifactStoredEventPayload p => [p.ActorId ?? "", p.ArtifactId ?? "", p.HouseInstanceId ?? ""],
        AgentKnowledgeSharedEventPayload p => [p.ActorId ?? "", p.RecipientId ?? "", p.ArtifactId ?? "", Convert.ToString(p.FactCount, CultureInfo.InvariantCulture) ?? ""],
        AgentKnowledgeArtifactReadEventPayload p => [p.SourceAgentId ?? "", p.RecipientId ?? "", p.ArtifactId ?? "", Convert.ToString(p.FactCount, CultureInfo.InvariantCulture) ?? ""],
        AgentKnowledgeWritingStartedEventPayload p => [p.ActorId ?? "", p.ItemKind ?? "", p.ProjectId ?? ""],
        AgentKnowledgeMaterialCollectedEventPayload p => [p.ActorId ?? "", p.ItemKind ?? "", Convert.ToString(p.Quantity, CultureInfo.InvariantCulture) ?? ""],
        AgentKnowledgeArtifactCreatedEventPayload p => [p.ActorId ?? "", p.ArtifactId ?? "", p.ProjectKind ?? "", Convert.ToString(p.FactCount, CultureInfo.InvariantCulture) ?? ""],
        AgentKnowledgeWritingCancelledEventPayload p => [p.ActorId ?? "", p.ProjectKind ?? "", p.Reason ?? ""],
        AgentRecipeLearnedEventPayload p => [p.ActorId ?? "", p.RecipeId ?? "", "practice"],
        HouseToolMadeEventPayload p => [p.ActorId ?? "", p.ItemKind ?? ""],
        MarketBuiltEventPayload p => [p.TownId ?? "", p.MarketId ?? "", p.PlacedInstanceId ?? "", Convert.ToString(p.StallsLength, CultureInfo.InvariantCulture) ?? ""],
        MarketStallBuiltEventPayload p => [p.TownId ?? "", p.MarketId ?? "", p.PlacedInstanceId ?? "", Convert.ToString(p.Slot, CultureInfo.InvariantCulture) ?? ""],
        MarketStallBorrowedEventPayload p => [p.TownId ?? "", p.MarketId ?? "", p.BuildingId ?? "", p.ActorId ?? "", p.OccupancyId ?? ""],
        MarketStockDeliveredEventPayload p => [p.TownId ?? "", p.MarketId ?? "", p.BuildingId ?? "", p.ActorId ?? "", p.ReceiptId ?? ""],
        MarketTradeOfferedEventPayload p => [p.TownId ?? "", p.MarketId ?? "", p.BuildingId ?? "", p.Seller ?? "", p.Buyer ?? "", p.OfferId ?? ""],
        MarketTradeCompletedEventPayload p => [p.TownId ?? "", p.MarketId ?? "", p.BuildingId ?? "", p.ActorId ?? "", p.BuyerId ?? "", p.OfferId ?? ""],
        MarketTradeCancelledEventPayload p => [p.TownId ?? "", p.MarketId ?? "", p.StallBuildingId ?? "", p.SellerId ?? "", p.BuyerId ?? "", p.OfferId ?? ""],
        MarketStallLeftEventPayload p => [p.TownId ?? "", p.MarketId ?? "", p.BuildingId ?? "", p.ActorId ?? "", p.OccupancyId ?? ""],
        ContinuityRuleOnEventPayload p => ["eligible_couples", Convert.ToString(p.EligibleCouples, CultureInfo.InvariantCulture) ?? "", "threshold", Convert.ToString(p.Threshold, CultureInfo.InvariantCulture) ?? ""],
        ContinuityRuleOffEventPayload p => ["eligible_couples", Convert.ToString(p.EligibleCouples, CultureInfo.InvariantCulture) ?? "", "threshold", Convert.ToString(p.Threshold, CultureInfo.InvariantCulture) ?? ""],
        TownCivicCouncilEventPayload p => [p.TownId ?? "", p.SubjectId ?? "", p.NoticeText ?? ""],
        TownCivicElectionEventPayload p => [p.TownId ?? "", p.SubjectId ?? "", p.NoticeText ?? ""],
        TownCivicRunoffEventPayload p => [p.TownId ?? "", p.SubjectId ?? "", p.NoticeText ?? ""],
        TownCivicProposalEventPayload p => [p.TownId ?? "", p.SubjectId ?? "", p.NoticeText ?? ""],
        TownCivicResultEventPayload p => [p.TownId ?? "", p.SubjectId ?? "", p.NoticeText ?? ""],
        TownCivicCancelledEventPayload p => [p.TownId ?? "", p.SubjectId ?? "", p.NoticeText ?? ""],
        TownCivicLawEventPayload p => [p.TownId ?? "", p.SubjectId ?? "", p.NoticeText ?? ""],
        TownCivicGovernmentEventPayload p => [p.TownId ?? "", p.SubjectId ?? "", p.NoticeText ?? ""],
        TownCivicMayorEventPayload p => [p.TownId ?? "", p.SubjectId ?? "", p.NoticeText ?? ""],
        TownCivicLandHearingEventPayload p => [p.TownId ?? "", p.SubjectId ?? "", p.NoticeText ?? ""],
        TownCivicNonviolentHearingEventPayload p => [p.TownId ?? "", p.SubjectId ?? "", p.NoticeText ?? ""],
        TownCivicLawCaseEventPayload p => [p.TownId ?? "", p.SubjectId ?? "", p.NoticeText ?? ""],
        TownCivicRemedyEventPayload p => [p.TownId ?? "", p.SubjectId ?? "", p.NoticeText ?? ""],
        LandCaseOpenedEventPayload p => [p.TownId ?? "", p.CaseId ?? "", Convert.ToString(p.Revision, CultureInfo.InvariantCulture) ?? "", p.ActorId ?? "", p.Status ?? ""],
        LandCaseNoticeEventPayload p => [p.TownId ?? "", p.CaseId ?? "", Convert.ToString(p.Revision, CultureInfo.InvariantCulture) ?? "", p.ActorId ?? "", p.Status ?? ""],
        LandCaseEvidenceEventPayload p => [p.TownId ?? "", p.CaseId ?? "", Convert.ToString(p.Revision, CultureInfo.InvariantCulture) ?? "", p.ActorId ?? "", p.Status ?? ""],
        LandCaseResponseEventPayload p => [p.TownId ?? "", p.CaseId ?? "", Convert.ToString(p.Revision, CultureInfo.InvariantCulture) ?? "", p.ActorId ?? "", p.Status ?? ""],
        LandCaseJudgeConsentEventPayload p => [p.TownId ?? "", p.CaseId ?? "", Convert.ToString(p.Revision, CultureInfo.InvariantCulture) ?? "", p.ActorId ?? "", p.Status ?? ""],
        LandCaseJudgeElectionEventPayload p => [p.TownId ?? "", p.CaseId ?? "", Convert.ToString(p.Revision, CultureInfo.InvariantCulture) ?? "", p.ActorId ?? "", p.Status ?? ""],
        LandCaseJudgeAssignedEventPayload p => [p.TownId ?? "", p.CaseId ?? "", Convert.ToString(p.Revision, CultureInfo.InvariantCulture) ?? "", p.ActorId ?? "", p.Status ?? ""],
        LandCaseRulingEventPayload p => [p.TownId ?? "", p.CaseId ?? "", Convert.ToString(p.Revision, CultureInfo.InvariantCulture) ?? "", p.ActorId ?? "", p.Status ?? ""],
        LandCaseReopenRequestedEventPayload p => [p.TownId ?? "", p.CaseId ?? "", Convert.ToString(p.Revision, CultureInfo.InvariantCulture) ?? "", p.ActorId ?? "", p.Status ?? ""],
        LandCaseReopenedEventPayload p => [p.TownId ?? "", p.CaseId ?? "", Convert.ToString(p.Revision, CultureInfo.InvariantCulture) ?? "", p.ActorId ?? "", p.Status ?? ""],
        LandCaseRejectedEventPayload p => [p.TownId ?? "", p.CaseId ?? "", Convert.ToString(p.Revision, CultureInfo.InvariantCulture) ?? "", p.ActorId ?? "", p.Status ?? ""],
        LandCaseInspectedEventPayload p => [p.TownId ?? "", p.CaseId ?? "", Convert.ToString(p.Revision, CultureInfo.InvariantCulture) ?? "", p.ActorId ?? "", p.Status ?? ""],
        LandCaseRelayedEventPayload p => [p.TownId ?? "", p.CaseId ?? "", Convert.ToString(p.Revision, CultureInfo.InvariantCulture) ?? "", p.ActorId ?? "", p.Status ?? ""],
        LandTransferProposedEventPayload p => [p.TownId ?? "", p.RequestId ?? "", "1", p.ActorId ?? "", p.Status ?? ""],
        LandTransferReadEventPayload p => [p.TownId ?? "", p.RequestId ?? "", "1", p.ActorId ?? "", p.Status ?? ""],
        LandTransferConsentEventPayload p => [p.TownId ?? "", p.RequestId ?? "", "1", p.ActorId ?? "", p.Status ?? ""],
        LandTransferWithdrawnEventPayload p => [p.TownId ?? "", p.RequestId ?? "", "1", p.ActorId ?? "", p.Status ?? ""],
        LandTransferSettledEventPayload p => [p.TownId ?? "", p.RequestId ?? "", "1", p.ActorId ?? "", p.Status ?? ""],
        LandTransferBlockedEventPayload p => [p.TownId ?? "", p.RequestId ?? "", "1", p.ActorId ?? "", p.Status ?? ""],
        LawCaseOpenedEventPayload p => [p.TownId ?? "", p.SubjectId ?? "", p.ActorId ?? ""],
        LawCaseEvidenceEventPayload p => [p.TownId ?? "", p.SubjectId ?? "", p.ActorId ?? ""],
        LawCaseResponseEventPayload p => [p.TownId ?? "", p.SubjectId ?? "", p.ActorId ?? ""],
        LawCaseInspectedEventPayload p => [p.TownId ?? "", p.SubjectId ?? "", p.ActorId ?? ""],
        LawCaseRelayedEventPayload p => [p.TownId ?? "", p.SubjectId ?? "", p.ActorId ?? ""],
        LawCaseJudgeConsentEventPayload p => [p.TownId ?? "", p.SubjectId ?? "", p.ActorId ?? ""],
        LawCaseJudgeElectionEventPayload p => [p.TownId ?? "", p.SubjectId ?? "", p.ActorId ?? ""],
        LawCaseJudgeAssignedEventPayload p => [p.TownId ?? "", p.SubjectId ?? "", p.ActorId ?? ""],
        LawCaseFindingEventPayload p => [p.TownId ?? "", p.SubjectId ?? "", p.ActorId ?? ""],
        LawCaseReopenRequestedEventPayload p => [p.TownId ?? "", p.SubjectId ?? "", p.ActorId ?? ""],
        LawCaseReopenedEventPayload p => [p.TownId ?? "", p.SubjectId ?? "", p.ActorId ?? ""],
        LawCaseRejectedEventPayload p => [p.TownId ?? "", p.SubjectId ?? "", p.ActorId ?? ""],
        LawCaseOfferEventPayload p => [p.TownId ?? "", p.SubjectId ?? "", p.ActorId ?? ""],
        LawCaseOfferResponseEventPayload p => [p.TownId ?? "", p.SubjectId ?? "", p.ActorId ?? ""],
        MarketStockLoadedEventPayload p => [p.TownId ?? "", p.MarketId ?? "", p.StallId ?? "", p.ActorId ?? "", p.LotId ?? ""],
        MarketStockCollectedEventPayload p => [p.TownId ?? "", p.MarketId ?? "", p.StallId ?? "", p.ActorId ?? "", p.LotId ?? ""],
        TownFoundedEventPayload p when !p.IsFirstTown => [p.TownId, p.ActorId ?? "", p.PreviousTownId ?? "none", p.MemberCount.ToString(CultureInfo.InvariantCulture)],
        _ => [],
    };

    private static string? PrimaryAnimalId(WorldEventPayload payload) => payload switch
    {
        AnimalBornEventPayload p => p.ChildId,
        AnimalBreedingStartedEventPayload p => p.AnimalId,
        AnimalCaredEventPayload p => p.AnimalId,
        AnimalDiedEventPayload p => p.AnimalId,
        AnimalHideCollectedEventPayload p => p.AnimalId,
        AnimalPermissionChangedEventPayload p => p.AnimalId,
        AnimalProductCollectedEventPayload p => p.AnimalId,
        AnimalProductReadyEventPayload p => p.AnimalId,
        AnimalSaddledEventPayload p => p.AnimalId,
        AnimalTamedEventPayload p => p.AnimalId,
        AnimalTradeOfferedEventPayload p => p.AnimalId,
        AnimalTransferredEventPayload p => p.AnimalId,
        HorseDismountedEventPayload p => p.AnimalId,
        HorseMountedEventPayload p => p.AnimalId,
        _ => null,
    };

    private static string? EventTownId(WorldEventPayload payload) => payload switch
    {
        LandUseGrantedEventPayload p => p.TownId,
        LandUseRequestedEventPayload p => p.TownId,
        TownAbandonedEventPayload p => p.TownId,
        TownBorderExpandedEventPayload p => p.TownId,
        TownBuildingAssignedEventPayload p => p.TownId,
        TownCivicLandUseEventPayload p => p.TownId,
        TownFoundedEventPayload p => p.TownId,
        TownResettledEventPayload p => p.TownId,
        TownResidentJoinedEventPayload p => p.TownId,
        TownResidentLeftEventPayload p => p.TownId,
        TownRevivedEventPayload p => p.TownId,
        _ => PipeFields(payload).FirstOrDefault(),
    };

    private static string? DefinitionId(WorldEventPayload payload) => payload switch
    {
        BuildingPlacedEventPayload p => p.DefinitionId,
        BuildStartedEventPayload p => p.DefinitionId,
        BuildCompletedEventPayload p => p.DefinitionId,
        RecipeStartedEventPayload p => p.RecipeId,
        RecipeCompletedEventPayload p => p.RecipeId,
        RecipeCancelledEventPayload p => p.RecipeId,
        _ => null,
    };

    private static string Crop(WorldEventPayload payload) => payload switch
    {
        FieldPlantedEventPayload p => p.Crop ?? "",
        FieldTendedEventPayload p => p.Crop ?? "",
        FieldHarvestedEventPayload p => p.Crop ?? "",
        _ => "",
    };

}

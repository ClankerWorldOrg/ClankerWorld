namespace ClankerWorld.Presentation;

/// <summary>The established words for event and action identifiers.</summary>
public static class EventVocabulary
{
    public static string ItemName(string kind) => kind switch
    {
        "storage_pot" => "Storage pot",
        "handcart" => "Handcart",
        "iron_fittings" => "Iron fittings",
        "water_jug" => "Water jug",
        "fresh_water" => "Fresh water",
        "simple_meal" => "Simple meal",
        "stew" => "Vegetable stew",
        "restaurant_meal" => "Restaurant meal",
        "gold_ore" => "Gold ore",
        "gold" => "Refined gold",
        "gold_ornament" => "Gold ornament",
        "diamond_ornament" => "Diamond ornament",
        _ => HumanizeIdentifier(kind),
    };

    public static string HumanizeIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "something";
        }

        var normalized = value.Trim();
        if (KnowledgeActionPhrase(normalized, inProgress: false) is { } knowledgeAction)
            return knowledgeAction;
        if (normalized.StartsWith("return_empty_vessel:", StringComparison.Ordinal)) return "bring an empty vessel home";
        if (normalized.StartsWith("guardian_tend:", StringComparison.Ordinal)) return "look after someone who is ill";
        if (normalized.StartsWith("care:", StringComparison.Ordinal)) return "look after a child";
        if (normalized.StartsWith("guardian_offer:", StringComparison.Ordinal)) return "offer to look after someone";
        if (normalized.StartsWith("guardian_accept:", StringComparison.Ordinal)) return "accept someone's care";
        if (normalized.StartsWith("guardian_refuse:", StringComparison.Ordinal)) return "turn down an offer of care";
        if (normalized.StartsWith("guardian_end:", StringComparison.Ordinal)) return "stop looking after someone";
        if (normalized.StartsWith("guardian_relocate:", StringComparison.Ordinal)) return "bring a child home";
        if (normalized.StartsWith("guardian_follow:", StringComparison.Ordinal)) return "follow their guardian home";
        if (normalized.StartsWith("parent_", StringComparison.Ordinal))
        {
            return normalized.StartsWith("parent_propose:", StringComparison.Ordinal) ? "talk about having a child"
                : normalized.StartsWith("parent_accept:", StringComparison.Ordinal) ? "agree to have a child"
                : normalized.StartsWith("parent_postpone:", StringComparison.Ordinal) ? "put off having a child" : "decide against having a child";
        }
        if (normalized.StartsWith("partner_", StringComparison.Ordinal))
        {
            return normalized.StartsWith("partner_propose:", StringComparison.Ordinal) ? "propose a partnership"
                : normalized.StartsWith("partner_accept:", StringComparison.Ordinal) ? "accept a partnership"
                : normalized.StartsWith("partner_refuse:", StringComparison.Ordinal) ? "refuse a partnership" : "leave a partnership";
        }
        if (normalized.StartsWith("learn:", StringComparison.Ordinal))
        {
            return "ask to be taught a skill";
        }
        if (normalized.StartsWith("lesson_", StringComparison.Ordinal))
        {
            return normalized.StartsWith("lesson_decline:", StringComparison.Ordinal) || normalized == "lesson_cancel"
                ? "turn down or stop a lesson" : "take a lesson";
        }
        if (normalized.StartsWith("nonviolent_remedy:", StringComparison.Ordinal)) return "carry out an agreed voluntary remedy";
        if (normalized.StartsWith("nonviolent_relay:", StringComparison.Ordinal)) return "share a witnessed report";
        if (normalized.StartsWith("civic|", StringComparison.Ordinal))
            return normalized.Split('|').ElementAtOrDefault(2) switch
            {
                "government_extend_non_land" => "propose adding non-land judging duties",
                "government_accept_non_land" => "personally accept added non-land duties",
                "law_case_file" or "law_case_file_town" => "file a known report for a non-land hearing",
                "law_case_propose_town" => "ask the Council to approve a report",
                "law_case_inspect" => "read a non-land hearing file",
                "law_case_statement" => "record a personal hearing statement",
                "law_case_evidence" => "add evidence to a hearing",
                "law_case_answer" => "answer a non-land hearing notice",
                "law_case_waive" => "choose not to respond to a hearing",
                "law_case_judge_register" => "offer to judge a case independently",
                "law_case_judge_withdraw" => "withdraw an offer to judge a case",
                "law_case_judge_resign" => "stop judging a case",
                "law_case_judge_vote" => "vote for an independent case judge",
                "law_case_find" => "assess the hearing evidence",
                "law_case_assess_reopen" => "assess grounds for a rehearing",
                "law_case_reopen" => "request a rehearing",
                "law_case_relay" => "pass on hearing evidence they learned",
                "remedy_offer" => "offer a voluntary remedy",
                "remedy_read" => "read a voluntary remedy offer",
                "remedy_accept" => "personally accept a voluntary contribution",
                "remedy_decline" => "decline a voluntary remedy",
                "remedy_counter" or "remedy_renegotiate" => "propose different voluntary terms",
                "visit" => "visit the Town notice place",
                "read" => "read Town notices",
                "relay" => "pass on Town notices",
                "nominate" => "nominate a council candidate",
                "request_admission" => "propose letting a newcomer join the Town",
                "register" or "remainder" => "agree to stand for council",
                "withdraw_candidate" => "withdraw a candidacy",
                "propose" => "propose a Town rule",
                "admission" => "ask to join the Town",
                "yes" or "no" => "vote on a Town proposal",
                "withdraw_proposal" => "withdraw a proposal",
                "ballot" or "single" => "cast a council election ballot",
                _ => "take part in Town affairs",
            };
        if (normalized.StartsWith("food_policy_", StringComparison.Ordinal))
        {
            return normalized.StartsWith("food_policy_propose:", StringComparison.Ordinal) ? "suggest a food rule"
                : normalized == "food_policy_vote_yes" ? "vote for a food rule" : "vote against a food rule";
        }
        if (normalized.StartsWith("trade_", StringComparison.Ordinal))
        {
            return normalized.StartsWith("trade_propose:", StringComparison.Ordinal) ? "offer a trade"
                : normalized.StartsWith("trade_accept:", StringComparison.Ordinal) ? "accept a trade" : "turn down a trade";
        }
        if (normalized.StartsWith("build:recipe:", StringComparison.Ordinal) ||
            normalized.StartsWith("build:building:", StringComparison.Ordinal))
        {
            var siteMarker = normalized.LastIndexOf(":site:", StringComparison.Ordinal);
            if (siteMarker >= 0)
                normalized = normalized[..siteMarker];
            var localId = normalized[(Math.Max(normalized.LastIndexOf('/'), normalized.LastIndexOf(':')) + 1)..];
            var versionSeparator = localId.IndexOf('@');
            if (versionSeparator >= 0)
            {
                localId = localId[..versionSeparator];
            }
            if (localId == "house-paper") return "make paper";
            return $"build {HumanizeIdentifier(localId)}";
        }

        if (normalized.StartsWith("child_gather:food:", StringComparison.Ordinal)) return "gather nearby food for the household";
        if (normalized.StartsWith("child_gather:wood:", StringComparison.Ordinal)) return "pick up nearby fallen wood for the household";
        if (normalized.StartsWith("child_carry:", StringComparison.Ordinal)) return "carry a small household load home";

        var known = normalized switch
        {
            "safe_idle" => "take it easy",
            "seek_food" => "find food",
            "move_to" => "go to a tile",
            "boat_order" => "travel to the requested Port by boat",
            "eat_food" => "eat",
            "consume_food" => "eat",
            "collect_shared_food" => "collect household food",
            "harvest_food" => "gather food",
            "gather_material" => "gather materials",
            "till_field" => "till a household field",
            "plant_field" => "plant a household field",
            "tend_field" => "tend a household field",
            "harvest_field" => "harvest a household field",
            "work_field" => "work on a household field",
            "repair_tool" => "repair a personal tool",
            "repair_equipment" => "repair personal equipment",
            "collect_material" => "collect personal materials",
            "collect_food" => "collect personal food",
            "collect_equipment" => "collect personal equipment",
            "collect_goods" => "collect personal goods",
            "store_material" => "store personal materials",
            "store_equipment" => "store personal equipment",
            "store_goods" => "store personal goods",
            "return_borrowed" => "return borrowed goods",
            "deliver_stock" => "deliver goods",
            "construct_building" => "build a household building",
            "expand_building" => "expand a building",
            "seek_shelter" => "seek shelter",
            "tend_fire" => "light a fire",
            "inspect_shelter_site" => "check the requested place",
            "produce_item" => "make goods",
            "inspect_material_site" => "look for the requested material",
            "storage_pot" => "storage pot",
            "water_jug" => "water jug",
            "fresh_water" => "fresh water",
            _ => null,
        };
        if (known is not null)
        {
            return known;
        }

        var words = normalized
            .Replace(':', ' ')
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Replace('/', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return "something";
        }

        var phrase = string.Join(' ', words.Select(word => word.ToLowerInvariant()));
        return char.ToUpperInvariant(phrase[0]) + phrase[1..];
    }

    private static string? KnowledgeActionPhrase(string candidateId, bool inProgress) => candidateId switch
    {
        "knowledge_write:field_record" => inProgress ? "writing a field record" : "write a field record",
        "knowledge_write:field_map" => inProgress ? "drawing a field map" : "draw a field map",
        "knowledge_write:book" => inProgress ? "writing a book" : "write a book",
        "knowledge_continue" => inProgress ? "continuing written work" : "continue written work",
        "knowledge_materials" => inProgress ? "collecting writing supplies" : "collect writing supplies",
        _ when candidateId == "knowledge_collect" || candidateId.StartsWith("knowledge_collect:", StringComparison.Ordinal) =>
            inProgress ? "collecting a written work" : "collect a written work",
        _ when candidateId == "knowledge_store" || candidateId.StartsWith("knowledge_store:", StringComparison.Ordinal) =>
            inProgress ? "storing a written work" : "store a written work",
        _ when candidateId.StartsWith("knowledge_copy:", StringComparison.Ordinal) =>
            inProgress ? "copying a written work" : "copy a written work",
        _ when candidateId.StartsWith("knowledge_read:", StringComparison.Ordinal) =>
            inProgress ? "reading a written work" : "read a written work",
        _ when candidateId.StartsWith("knowledge_share:", StringComparison.Ordinal) =>
            inProgress ? "sharing written knowledge" : "share written knowledge",
        _ => null,
    };
}

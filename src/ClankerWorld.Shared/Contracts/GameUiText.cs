namespace ClankerWorld.Contracts;

/// <summary>
/// Converts simulation identifiers into the deliberately small, player-facing
/// vocabulary used by the game HUD. Protocol diagnostics remain available in
/// developer tools instead of leaking into ordinary play.
/// </summary>
public static class GameUiText
{
    private const int MinutesPerDay = 1_440;

    /// <summary>Selects notable events and coalesces problems using the world's calendar.</summary>
    public static IEnumerable<OwnerWorldEvent> PlayerEvents(IEnumerable<OwnerWorldEvent> events,
        OwnerWorldCalendarPace? calendarPace = null) =>
        ClankerWorld.Presentation.EventLogPolicy.Select(events, item => item.EventId,
            item => item.Kind, item => item.Payload,
            item => CalendarRules.FromTick(item.WorldTick, calendarPace?.TicksPerDay ?? MinutesPerDay,
                calendarPace?.CalendarOffsetTicks ?? 0).DayIndex);

    public static string ModelStatus(string? status) => status switch
    {
        "waiting" => "Waiting for the model",
        "canceled" => "Canceled by pause or disconnect",
        "missing_key" => "Missing key",
        "usage_limit" => "Usage limit reached",
        "unusable_reply" => "Unusable reply",
        "timed_out" => "Timed out",
        "model_unavailable" => "Model unavailable",
        _ => "Ready",
    };

    public static string ActorMapLabel(string displayName)
    {
        var words = displayName.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return "?";
        var given = words[0];
        return new System.Globalization.StringInfo(given).LengthInTextElements <= 12
            ? given : System.Globalization.StringInfo.GetNextTextElement(given) + ".";
    }

    public static string ResourceMapCaption(OwnerWorldResource resource, float tileSize)
    {
        // At overview scale the existing terrain sprite/glyph carries the site;
        // full facts stay in hover and selected-tile inspection.
        if (tileSize < 32) return string.Empty;
        var title = NaturalObjectName(resource.NaturalObjectKind) ?? resource.Kind switch
        {
            "construction" => "Wood",
            "fertile_land" => "Plot",
            _ => HumanizeIdentifier(resource.Kind),
        };
        var quantity = ResourceQuantity(resource.Kind, resource.Quantity, resource.Capacity);
        return quantity.Length == 0 ? title : title + " " + quantity;
    }

    public static string AnimalDescription(OwnerWorldAnimal animal) =>
        $"{animal.Name}: {animal.Sex} {animal.Species}, {animal.LifeStage}, {animal.AgeDays} days old. " +
        (animal.HouseholdName is null ? "Untamed. " : $"{animal.HouseholdName}. ") + animal.CareStatus + ". " +
        (animal.ProductQuantity == 0 ? "" : $"Ready: {animal.ProductQuantity} {ItemName(animal.ProductKind!)}. ") +
        (animal.BirthDaysRemaining is null ? "" : $"Birth in {animal.BirthDaysRemaining:0.#} cared-for days; one yard place reserved. ") +
        (animal.RiderName is null ? "" : $"Ridden by {animal.RiderName}. ") + (animal.Saddled ? "Saddled. " : "") +
        $"Care permission: {(animal.CarePermissions.Count == 0 ? "household adults" : string.Join(", ", animal.CarePermissions))}. " +
        (animal.Species != "horse" ? "" : $"Riding permission: {(animal.RidingPermissions.Count == 0 ? "household adults" : string.Join(", ", animal.RidingPermissions))}.");

    public static string HandcartDescription(OwnerWorldHandcart cart)
    {
        var cargo = cart.Cargo.Count == 0 ? "Empty" : string.Join(", ",
            cart.Cargo.Select(item => $"{ItemName(item.Kind)} {item.Quantity}"));
        var status = cart.ConditionPercent == 0 ? "Broken; unload here or repair with wood, iron fittings and rope" :
            cart.PullerName is { } puller ? "Pulled by " + puller : "Parked";
        return $"Handcart · {status}\nOwner: {cart.OwnerName} · Position: {cart.Position.X}, {cart.Position.Y}" +
            $"\nCondition: {cart.ConditionPercent}% · Cargo: {cart.Cargo.Sum(item => item.Quantity)}/{cart.Capacity} · {cargo}";
    }

    /// <summary>How far a building under construction has got, such as "Being built · 40% done".</summary>
    public static string ConstructionDescription(OwnerWorldConstructionSite site)
    {
        var state = site.Stage switch
        {
            "working" => "Being built",
            "blocked" => "Work stopped",
            "paused" => "Work paused",
            "travelling" => "Builder on the way",
            _ => "Waiting for materials",
        };
        var done = site.WorkRequired <= 0 ? 0 : Math.Clamp(site.WorkDone * 100 / site.WorkRequired, 0, 100);
        return $"{state} · {done}% done";
    }

    public static string BoatDescription(OwnerWorldBoat boat)
    {
        var cargo = boat.Cargo.Count == 0 ? "Empty" : string.Join(", ", boat.Cargo.Select(item => $"{ItemName(item.Kind)} {item.Quantity}"));
        var status = boat.Status switch
        {
            "moored" => "Moored",
            "waiting" => "Waiting for a safe arrival",
            "returning" => "Returning to the departure Port",
            _ => "Underway",
        };
        return $"Boat · {status}\nTown: {boat.TownName} · Position: {boat.Position.X}, {boat.Position.Y}" +
            (boat.PassengerName is { } passenger ? "\nPassenger: " + passenger : "") +
            $"\nCargo: {cargo}";
    }

    /// <summary>
    /// Text from an agent's model or the host with each ellipsis character
    /// (U+2026) spelled as three full stops. The body font draws that character
    /// at mid-height, as Chinese text does, so game text never uses it.
    /// </summary>
    public static string PlainEllipses(string text) => text.Replace(Ellipsis, "...", StringComparison.Ordinal);

    /// <summary>The ellipsis character, written as an escape so searches for it find only mistakes.</summary>
    public const string Ellipsis = "\u2026";

    public static string ToolMakingRequestStatus(string status) => status switch
    {
        "requested" => "Asked the household · no payment taken",
        "accepted" => "Household accepted · making from its own supplies",
        "ready" => "Tool ready · payment still to be agreed",
        "offered" => "Exchange offered · both traders must meet here",
        "fulfilled" => "Purchased through the agreed exchange",
        "refused" => "Household refused · nothing taken",
        "withdrawn" => "Request withdrawn · goods remain household property",
        "interrupted" => "Request stopped · goods keep their owners",
        _ => "Status unavailable",
    };

    public static string ItemName(string kind) => ClankerWorld.Presentation.EventVocabulary.ItemName(kind);

    public static string ResourceQuantity(string kind, int? quantity, int? capacity) => quantity is null ? "" :
        (kind == "fertile_land" ? "Soil " : "") + quantity + (capacity is null ? "" : "/" + capacity);

    public static string ResourceTooltip(OwnerWorldResource resource)
    {
        var title = NaturalObjectName(resource.NaturalObjectKind) ?? resource.Kind switch
        {
            "construction" => "Wild timber",
            "food" => "Wild food",
            "fertile_land" => "Growing plot",
            _ => HumanizeIdentifier(resource.Kind),
        };
        var stock = ResourceQuantity(resource.Kind, resource.Quantity, resource.Capacity);
        var renewal = !resource.IsRenewable ? "Does not grow back." :
            resource.RegenerationAmount is > 0 && resource.RegenerationIntervalDays is > 0 && resource.RegenerationSeason is not null
                ? $"Grows back: +{resource.RegenerationAmount} every {resource.RegenerationIntervalDays} {(resource.RegenerationIntervalDays == 1 ? "day" : "days")} in {HumanizeIdentifier(resource.RegenerationSeason)}."
                : "Grows back.";
        return title + (stock.Length == 0 ? "" : " · " + stock) + "\n" + HumanizeIdentifier(resource.State) + "\n" + renewal;
    }

    private static string? NaturalObjectName(string? value) => value switch
    {
        "berry_bush" => "Berry bush",
        "wild_greens" => "Wild greens",
        "fiber_plant" => "Fiber plant",
        "reeds" => "Reeds",
        "stone_outcrop" => "Stone outcrop",
        "fallen_wood" => "Fallen wood",
        "iron_outcrop" => "Iron outcrop",
        "gold_outcrop" => "Gold outcrop",
        "diamond_outcrop" => "Diamond outcrop",
        "clay_bank" => "Clay bank",
        "medicinal_herb_patch" => "Medicinal herb patch",
        "wild_seed_patch" => "Wild seed patch",
        "fertile_soil" => "Fertile soil",
        _ => null,
    };

    /// <summary>
    /// The default date style: the season and its day, such as Autumn 2, Year 1.
    /// The numeric styles are "dmy", "mdy" and "ymd".
    /// </summary>
    public const string SeasonDates = "season";

    private static readonly string[] SeasonNames = ["Spring", "Summer", "Autumn", "Winter"];

    /// <summary>
    /// Whether dates in this style name the season. That needs the world's
    /// season lengths; without them, such as from an older host, a season-style
    /// date falls back to DD-MM-YYYY.
    /// </summary>
    public static bool ShowsSeasonDates(OwnerWorldCalendarPace? calendarPace, string dateFormat) =>
        SeasonDateLengths(calendarPace, dateFormat) is not null;

    /// <summary>
    /// The one place a world tick becomes the date and time the player reads:
    /// the top bar, Event Log, saves and every other full date use it.
    /// </summary>
    public static string FormatWorldClock(long worldTick, bool useTwelveHourClock = false,
        OwnerWorldCalendarPace? calendarPace = null, string dateFormat = SeasonDates)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(worldTick);
        var ticksPerDay = calendarPace?.TicksPerDay ?? MinutesPerDay;
        var daysPerYear = calendarPace?.DaysPerYear ?? 365;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ticksPerDay);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(daysPerYear);
        var (dayIndex, tickOfDay) = CalendarRules.FromTick(worldTick, ticksPerDay,
            calendarPace?.CalendarOffsetTicks ?? 0);
        var date = SeasonDateLengths(calendarPace, dateFormat) is { } seasonLengths
            ? FormatSeasonDate(dayIndex, daysPerYear, seasonLengths)
            : FormatWorldDate(dayIndex, daysPerYear, dateFormat);
        var minuteOfDay = (int)(((long)tickOfDay * MinutesPerDay) / ticksPerDay);
        var hour = minuteOfDay / 60;
        var minute = minuteOfDay % 60;
        if (!useTwelveHourClock) return $"{date} · {hour:00}:{minute:00}";
        var twelveHour = hour % 12;
        if (twelveHour == 0) twelveHour = 12;
        return $"{date} · {twelveHour}:{minute:00} {(hour < 12 ? "AM" : "PM")}";
    }

    /// <summary>
    /// Spring, Summer, Autumn and Winter lengths in days when dates in this
    /// style name the season; null for a numeric style, or unless the
    /// calendar's season lengths fill its year exactly.
    /// </summary>
    private static int[]? SeasonDateLengths(OwnerWorldCalendarPace? pace, string dateFormat)
    {
        if (pace is null || dateFormat is "dmy" or "mdy" or "ymd") return null;
        int[] lengths = [pace.SpringDays, pace.SummerDays, pace.AutumnDays, pace.WinterDays];
        return lengths.All(days => days > 0) && lengths.Sum(days => (long)days) == pace.DaysPerYear ? lengths : null;
    }

    /// <summary>The season and its day, counted the way the world counts seasons, such as Autumn 2, Year 1.</summary>
    private static string FormatSeasonDate(long dayIndex, int daysPerYear, int[] seasonLengths)
    {
        var year = (dayIndex / daysPerYear) + 1;
        var day = (int)(dayIndex % daysPerYear);
        var (season, firstDay) = CalendarRules.SeasonAtDay(day, seasonLengths[0], seasonLengths[1], seasonLengths[2]);
        day -= firstDay;
        return $"{SeasonNames[season]} {day + 1}, Year {year}";
    }

    private static string FormatWorldDate(long dayIndex, int daysPerYear, string dateFormat)
    {
        var year = (dayIndex / daysPerYear) + 1;
        var dayOfYear = (int)(dayIndex % daysPerYear);
        int month;
        int day;
        if (daysPerYear == 40)
        {
            month = (dayOfYear / 10) + 1;
            day = (dayOfYear % 10) + 1;
        }
        else if (daysPerYear == 365)
        {
            var monthLengths = new[] { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };
            month = 1;
            while (dayOfYear >= monthLengths[month - 1])
            {
                dayOfYear -= monthLengths[month - 1];
                month++;
            }
            day = dayOfYear + 1;
        }
        else
        {
            return $"Year {year} · Day {dayOfYear + 1}";
        }

        return dateFormat switch
        {
            "mdy" => $"{month:00}-{day:00}-{year:0000}",
            "ymd" => $"{year:0000}-{month:00}-{day:00}",
            _ => $"{day:00}-{month:00}-{year:0000}",
        };
    }

    public static bool IsPlayerFacingEvent(string kind) =>
        ClankerWorld.Presentation.EventVocabulary.IsPlayerFacingEvent(kind);

    /// <summary>
    /// Names another party the way the player sees it: an agent, a household
    /// by its stores name, or a Town, instead of a raw ID such as
    /// <c>household:first</c>.
    /// </summary>
    public static string PartyName(OwnerWorldSnapshot? snapshot, string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return snapshot?.Agents.FirstOrDefault(person => person.Id == id)?.DisplayName
            ?? snapshot?.Stockpiles.FirstOrDefault(stockpile => stockpile.OwnerId == id)?.Name
            ?? snapshot?.Towns.FirstOrDefault(town => town.Id == id)?.Name
            ?? (id.StartsWith("household:", StringComparison.Ordinal) ? "a household" : HumanizeIdentifier(id));
    }

    /// <summary>
    /// Names how fed an agent is. The host reports fullness, so a low value
    /// means hungry; the bands follow the host's food-seeking thresholds.
    /// </summary>
    /// <summary>
    /// When a world or save was last written, the way a person would say it:
    /// "just now", "12 minutes ago", "yesterday", or a date once it is a week old.
    /// </summary>
    public static string SavedAgo(DateTimeOffset saved, DateTimeOffset now)
    {
        var age = now - saved;
        if (age < TimeSpan.FromMinutes(1)) return "just now";
        if (age < TimeSpan.FromHours(1)) return Plural((int)age.TotalMinutes, "minute") + " ago";
        var savedDay = saved.ToLocalTime().Date;
        var today = now.ToLocalTime().Date;
        if (savedDay == today) return Plural((int)age.TotalHours, "hour") + " ago";
        if (savedDay == today.AddDays(-1)) return "yesterday";
        if (age < TimeSpan.FromDays(7)) return Plural((int)Math.Max(2, (today - savedDay).TotalDays), "day") + " ago";
        return saved.ToLocalTime().ToString("d MMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string Plural(int count, string unit) => count == 1 ? $"1 {unit}" : $"{count} {unit}s";

    public static string FullnessState(int fullnessBasisPoints) => NeedStates.FullnessState(fullnessBasisPoints);

    public static string NeedLabel(OwnerWorldAgent agent, string key) =>
        agent.DecisionFactors.FirstOrDefault(factor => factor.Key == key)?.Detail ?? string.Empty;

    /// <summary>
    /// Short present-tense activity for the roster. The host summary already
    /// reads naturally ("looking for food"); structured candidates such as
    /// building projects fall back to the readable candidate phrase.
    /// </summary>
    public static string ActivityPhrase(string? candidateId, string? summary)
    {
        if (summary?.StartsWith("looking for ", StringComparison.Ordinal) == true && !summary.Contains(':', StringComparison.Ordinal))
            return summary.Trim();
        if (candidateId is not null && KnowledgeActionPhrase(candidateId, inProgress: true) is { } knowledgeActivity)
            return knowledgeActivity;
        if (candidateId?.StartsWith("explore_for:resource:", StringComparison.Ordinal) == true)
            return "looking for " + candidateId["explore_for:resource:".Length..].Replace('_', ' ');
        if (candidateId?.StartsWith("explore_for:terrain:", StringComparison.Ordinal) == true)
            return "looking for " + candidateId["explore_for:terrain:".Length..].ToLowerInvariant() + " terrain";
        if (candidateId?.StartsWith("tool_request_", StringComparison.Ordinal) == true)
            return candidateId.Split(':', 2)[0] switch
            {
                "tool_request_place" => "asking the Blacksmith to make a tool",
                "tool_request_visit" => "visiting the Blacksmith to discuss a tool",
                "tool_request_accept" => "agreeing to make a tool",
                "tool_request_refuse" => "turning down a tool request",
                "tool_request_withdraw" => "withdrawing a tool request",
                "tool_request_collect" => "discussing payment for a finished tool",
                "tool_request_work" => "working on a requested tool",
                _ => "considering a tool request",
            };
        if (candidateId?.StartsWith("wear_ornament:", StringComparison.Ordinal) == true) return "putting on an ornament";
        if (candidateId == "remove_ornament") return "taking off an ornament";
        if (candidateId?.StartsWith("gift_ornament:", StringComparison.Ordinal) == true) return "giving an ornament";
        if (candidateId?.StartsWith("return_empty_vessel:", StringComparison.Ordinal) == true) return "bringing an empty vessel home";
        if (candidateId?.StartsWith("medical_allow:", StringComparison.Ordinal) == true) return "allowing medical care";
        if (candidateId?.StartsWith("medical_revoke:", StringComparison.Ordinal) == true) return "withdrawing medical permission";
        if (candidateId?.StartsWith("medical_collect:", StringComparison.Ordinal) == true) return "collecting medicine";
        if (candidateId?.StartsWith("medical_treat:", StringComparison.Ordinal) == true) return "giving medicine";
        if (candidateId?.StartsWith("guardian_relocate:", StringComparison.Ordinal) == true) return "bringing a child home";
        if (candidateId?.StartsWith("guardian_follow:", StringComparison.Ordinal) == true) return "following their guardian home";
        if (!string.IsNullOrWhiteSpace(summary) && !summary.Contains(':', StringComparison.Ordinal))
            return summary.Trim();
        if (candidateId == "produce_item") return "making goods";
        if (candidateId == "animal_order") return "following an animal care or travel order";
        if (candidateId == "drink_milk") return "drinking milk";
        if (candidateId?.StartsWith("animal:", StringComparison.Ordinal) == true)
            return candidateId.Split(':')[1] switch
            {
                "care" => "caring for an animal",
                "collect" => "collecting animal products",
                "collect_hide" => "collecting a wild animal's old-age hide",
                "tame" => "taming an animal",
                "lead_home" => "leading an animal home",
                "saddle" => "fitting a horse's saddle",
                "mount" => "mounting a horse",
                "dismount" => "dismounting a horse",
                "agreement" => "discussing animal permissions or a transfer",
                _ => "carrying animal supplies",
            };
        return string.IsNullOrWhiteSpace(candidateId) ? "taking in the surroundings" : HumanizeIdentifier(candidateId);
    }

    /// <summary>
    /// The historical profile's will lines: who the agent named and what each
    /// was left, or that default inheritance applies, then any final words.
    /// </summary>
    public static IReadOnlyList<string> FinalWillLines(string? status, OwnerWorldFinalWill? will)
    {
        var lines = new List<string>();
        switch (status)
        {
            case "pending":
                lines.Add("Final will pending.");
                break;
            case "accepted" when will is { Heirs.Count: > 0 }:
                var names = will.Heirs.Select(heir => heir.Name).ToArray();
                var joined = names.Length == 1 ? names[0] : string.Join(", ", names[..^1]) + " and " + names[^1];
                lines.Add(will.Split == "items"
                    ? $"Final will: belongings left item by item to {joined}."
                    : names.Length == 1
                        ? $"Final will: all belongings to {joined}."
                        : $"Final will: belongings shared equally between {joined}.");
                foreach (var heir in will.Heirs)
                {
                    var goods = heir.Items.Count == 0
                        ? "nothing listed"
                        : string.Join(", ", heir.Items.Select(item => $"{item.Quantity} {item.Kind.Replace('_', ' ')}"));
                    lines.Add($"To {heir.Name}: {goods}");
                }
                break;
            case "default":
                lines.Add("Personal estate follows default inheritance rules.");
                break;
            default:
                lines.Add("No current thoughts or activity.");
                break;
        }
        if (will?.FinalWords is { } words && !string.IsNullOrEmpty(words))
            lines.Add($"Final words: \u201C{words}\u201D");
        return lines;
    }

    public static string HumanizeIdentifier(string value) => ClankerWorld.Presentation.EventVocabulary.HumanizeIdentifier(value);

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

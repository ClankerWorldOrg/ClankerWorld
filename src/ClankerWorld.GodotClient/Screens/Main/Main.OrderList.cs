using ClankerWorld.GodotClient.Protocol;
using ClankerWorld.GodotClient.UI;
using Godot;
using System.Globalization;

namespace ClankerWorld.GodotClient;

/// <summary>
/// The player's orders to the selected agent, read from the host's snapshot:
/// one line on the quick card and the Profile for the order they are on now,
/// and a reader beside the Profile with every order the world keeps for them.
/// The game keeps no order queue of its own, so after a reconnect or a reload
/// both show exactly what the host holds.
/// </summary>
public partial class Main
{
    private readonly Button allOrdersButton = new();
    private readonly PanelContainer ordersPanel = new();
    private readonly Label ordersReaderTitle = new();
    private readonly RichTextLabel ordersReaderText = new();
    private string? renderedOrdersReader;

    private static void ConfigureOrderLabel(Label label)
    {
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        // A label shows its tooltip only when it takes the pointer; Pass still lets clicks reach the card.
        label.MouseFilter = Control.MouseFilterEnum.Pass;
        label.TooltipText = "All orders, in the Profile, lists their current, queued and recent orders.";
        label.Hide();
    }

    private static bool IsOpenOrder(OwnerWorldInstruction instruction) =>
        instruction.Order is { Status: "queued" or "waiting" or "doing" or "interrupted" or "blocked" };

    /// <summary>
    /// Every order for the agent in three groups: the order they are on now,
    /// the orders queued after it in the order they will be done, then the
    /// closed ones the world still keeps, newest first.
    /// </summary>
    private static (OwnerWorldInstruction? Now, OwnerWorldInstruction[] Queued, OwnerWorldInstruction[] Earlier) OrdersFor(
        OwnerWorldSnapshot snapshot, string inhabitantId)
    {
        var orders = snapshot.Instructions
            .Where(item => item.TargetInhabitantId == inhabitantId && item.Kind == "must_do")
            .ToArray();
        var now = PendingOrderToCancel(snapshot, inhabitantId);
        var queued = orders.Where(item => IsOpenOrder(item) && item.InstructionId != now?.InstructionId)
            .OrderBy(item => item.SubmissionSequence).ToArray();
        var earlier = orders.Where(item => !IsOpenOrder(item) && item.InstructionId != now?.InstructionId)
            .OrderByDescending(item => item.SubmissionSequence).ToArray();
        return (now, queued, earlier);
    }

    /// <summary>
    /// The order line on the card: the order they are on now, with why it is
    /// held up and how many wait behind it; otherwise how their latest order
    /// ended. Completion shows only once the host reports the task finished.
    /// </summary>
    private static string? OrderCardLine(OwnerWorldSnapshot snapshot, string inhabitantId)
    {
        var (now, queued, earlier) = OrdersFor(snapshot, inhabitantId);
        if (now is not null)
            return $"Order: {InstructionOrderSummary(now, includeHeard: false)}" +
                (queued.Length == 0 ? string.Empty : $"\n{queued.Length} more {(queued.Length == 1 ? "order" : "orders")} queued");
        return earlier.Length == 0 ? null : $"Last order: {InstructionOrderSummary(earlier[0], includeHeard: false)}";
    }

    private void RenderOrderSummary(OwnerWorldSnapshot snapshot, OwnerWorldInhabitant inhabitant, bool isDeceased)
    {
        var line = isDeceased ? null : OrderCardLine(snapshot, inhabitant.Id);
        var heldUp = PendingOrderToCancel(snapshot, inhabitant.Id)?.Order?.Status is "blocked" or "interrupted";
        var style = heldUp ? "WarningLabel" : line?.StartsWith("Last order:", StringComparison.Ordinal) == true ? "DimLabel" : "SoftLabel";
        foreach (var label in new[] { quickCardOrderLabel, profileOrderLabel })
        {
            label.Text = GameUiText.PlainEllipses(line ?? string.Empty);
            label.ThemeTypeVariation = style;
            label.Visible = line is not null;
        }
        allOrdersButton.Visible = !isDeceased && snapshot.Instructions.Any(item =>
            item.TargetInhabitantId == inhabitant.Id && item.Kind == "must_do");
    }

    private void BuildOrdersReader()
    {
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 6);
        body.AddChild(ReaderHeading(ordersPanel, ordersReaderTitle, "Close orders (Esc)"));
        body.AddChild(new Label
        {
            Text = "What the world reports for each order. Their replies are their own words.",
            ThemeTypeVariation = "DimLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        ConfigureTextPanel(ordersReaderText, 1000);
        ordersReaderText.AddThemeConstantOverride("paragraph_separation", 4);
        body.AddChild(ordersReaderText);
        AddPanelContents(ordersPanel, body);
        ordersPanel.ZIndex = 85;
        ordersPanel.Resized += () => PlaceReaderPanel(ordersPanel);
        ordersPanel.Hide();
    }

    private void OpenOrdersReader()
    {
        if (SelectedInhabitant() is not { } inhabitant || agentCardSnapshot is not { } snapshot) return;
        memoriesPanel.Hide();
        familyTreePanel.Hide();
        thoughtsPanel.Hide();
        conversationPanel.Hide();
        agentsPanel.Hide();
        eventLogPanel.Hide();
        worldMapPanel.Hide();
        worldInfoPanel.Hide();
        ordersPanel.Show();
        renderedOrdersReader = null;
        RenderOrdersReader(snapshot, inhabitant);
        ApplyResponsiveLayout();
    }

    /// <summary>
    /// Every order the world keeps for this agent under Now, Queued and
    /// Earlier headings. Each shows its state, the player's words and any
    /// reply, which is set apart as the agent's own speech.
    /// </summary>
    private void RenderOrdersReader(OwnerWorldSnapshot snapshot, OwnerWorldInhabitant inhabitant)
    {
        // Only an open reader follows the world; opening it draws it afresh.
        if (!ordersPanel.Visible) return;
        var (now, queued, earlier) = OrdersFor(snapshot, inhabitant.Id);
        var groups = new (string Heading, OwnerWorldInstruction[] Orders)[]
        {
            ("Now", now is null ? [] : [now]),
            ("Queued", queued),
            ("Earlier", earlier),
        };
        var signature = string.Join("\n", groups.SelectMany(group => group.Orders.Select(item =>
                $"{group.Heading}|{item.InstructionId}|{InstructionOrderSummary(item)}|{item.Text}|{item.ObserverReply}"))
            .Prepend($"{inhabitant.Id}|{inhabitant.DisplayName}|{UiTheme.Current.Name}"));
        if (renderedOrdersReader == signature) return;
        renderedOrdersReader = signature;
        ordersReaderTitle.Text = ordersReaderTitle.TooltipText = $"{inhabitant.DisplayName} · orders";
        ordersReaderText.Clear();
        if (groups.All(group => group.Orders.Length == 0))
        {
            ordersReaderText.PushColor(DimText);
            ordersReaderText.AddText("No orders yet.");
            ordersReaderText.Pop();
        }
        var first = true;
        foreach (var (heading, orders) in groups.Where(group => group.Orders.Length > 0))
        {
            if (!first) ordersReaderText.Newline();
            first = false;
            ordersReaderText.PushFont(UiFonts.Headings, ordersReaderText.GetThemeFontSize("normal_font_size"));
            ordersReaderText.PushColor(HeadingText);
            ordersReaderText.AddText(heading);
            ordersReaderText.Pop();
            ordersReaderText.Pop();
            foreach (var order in orders)
            {
                ordersReaderText.Newline();
                var heldUp = order.Order?.Status is "blocked" or "interrupted";
                if (heldUp) ordersReaderText.PushColor(UiTheme.Current.Warning);
                ordersReaderText.AddText(GameUiText.PlainEllipses(InstructionOrderSummary(order)));
                if (heldUp) ordersReaderText.Pop();
                ordersReaderText.Newline();
                ordersReaderText.PushColor(DimText);
                ordersReaderText.AddText(GameUiText.PlainEllipses($"You said: “{order.Text}”"));
                ordersReaderText.Pop();
                if (order.ObserverReply is { } reply)
                {
                    ordersReaderText.Newline();
                    ordersReaderText.PushColor(UiTheme.Current.Link);
                    ordersReaderText.AddText(GameUiText.PlainEllipses($"{inhabitant.DisplayName} replied: “{reply}”"));
                    ordersReaderText.Pop();
                }
            }
        }
        FitTextPanel(ordersReaderText);
    }

    private static OwnerWorldInstruction? PendingOrderToCancel(OwnerWorldSnapshot snapshot, string? inhabitantId) =>
        snapshot.Instructions.Where(item => item.TargetInhabitantId == inhabitantId &&
                item.Order is { Status: "queued" or "waiting" or "doing" or "interrupted" or "blocked" })
            .OrderBy(item => item.SubmissionSequence).FirstOrDefault();

    /// <summary>
    /// One order's state as the host reports it, such as "Blocked · Eating food ·
    /// 0/3 food items" followed by why it is held up or was replaced.
    /// </summary>
    private static string InstructionOrderSummary(OwnerWorldInstruction instruction, bool includeHeard = true)
    {
        var order = instruction.Order;
        if (order is null)
            return instruction.State == "completed"
                ? instruction.ObservedTick is null ? "Order closed · not reported as heard" : "Heard by their personal model · order closed"
                : instruction.ObservedTick is null ? "Order pending · waiting for their personal model" : "Heard by their personal model · order pending";

        var task = order.Action switch
        {
            "consume_food" => "Eating food",
            "read_knowledge" => "Reading " + (order.TargetItemKind is { } writtenKind
                ? GameUiText.ItemName(writtenKind).ToLowerInvariant() : "a written item"),
            "harvest_food" => "Gathering food",
            "gather_material" => "Gathering " + (order.TargetMaterialKind?.Replace('_', ' ') ?? "materials"),
            "till_field" => "Tilling household fields",
            "plant_field" => "Planting " + (order.TargetCropKind?.Replace('_', ' ') ?? "crops"),
            "tend_field" => "Tending " + (order.TargetCropKind?.Replace('_', ' ') ?? "household fields"),
            "harvest_field" => "Harvesting " + (order.TargetCropKind?.Replace('_', ' ') ?? "household fields"),
            "repair_equipment" or "repair_tool" => "Repairing " + (order.TargetEquipmentKind?.Replace('_', ' ') ?? "equipment"),
            "collect_material" => "Collecting " + (order.TargetMaterialKind?.Replace('_', ' ') ?? "materials"),
            "collect_food" => "Collecting " + (order.TargetFoodKind?.Replace('_', ' ') ?? "food"),
            "collect_equipment" => "Collecting " + (order.TargetEquipmentKind?.Replace('_', ' ') ?? "equipment"),
            "collect_goods" => "Collecting " + OrderItemName(order.TargetItemKind),
            "store_material" => "Storing " + (order.TargetMaterialKind?.Replace('_', ' ') ?? "materials"),
            "store_equipment" => "Storing " + (order.TargetEquipmentKind?.Replace('_', ' ') ?? "equipment"),
            "store_goods" => "Storing " + OrderItemName(order.TargetItemKind),
            "return_borrowed" => "Returning borrowed " + OrderItemName(order.TargetItemKind),
            "deliver_stock" => "Delivering " + OrderItemName(order.TargetItemKind) + " to " + OrderBuildingName(order.TargetBuildingKind),
            "construct_building" => "Building " + OrderBuildingName(order.TargetBuildingKind),
            "expand_building" => "Expanding " + OrderBuildingName(order.TargetBuildingKind),
            "seek_shelter" => order.TargetBuildingKind == "house" ? "Seeking shelter in their House" : "Seeking shelter",
            "tend_fire" => order.TargetBuildingKind == "house" ? "Lighting a fire in their House" : "Lighting a fire",
            "produce_item" => "Making " + (order.TargetOutputKind is { } output
                ? GameUiText.ItemName(output).ToLowerInvariant() : "goods"),
            "write_knowledge" => "Writing " + OrderItemName(order.TargetKnowledgeKind),
            "copy_knowledge" => "Copying " + OrderItemName(order.TargetKnowledgeKind),
            "seek_food" => "Going to a food site",
            "move_to" => "Going to a tile",
            "travel_by_boat" => "Traveling to a Port by boat",
            "accept_guardianship" => "Becoming a guardian",
            "load_handcart" => "Loading " + OrderItemName(order.TargetItemKind),
            "unload_handcart" or "unload_handcart_ground" => "Unloading " + OrderItemName(order.TargetItemKind),
            "talk_to" => "Talking with the named person",
            "propose_marriage" => "Proposing marriage to their partner",
            "attach_handcart" => "Attaching a handcart",
            "park_handcart" => "Parking a handcart",
            "repair_handcart" => "Repairing a handcart",
            "animal_care" => "Caring for an animal",
            "animal_collect" => "Collecting animal products",
            "animal_tame" => "Taming an animal",
            "animal_lead_home" => "Leading an animal home",
            "animal_saddle" => "Fitting a horse's saddle",
            "animal_mount" => "Mounting a horse",
            "animal_dismount" => "Dismounting a horse",
            _ => "Order",
        };
        var units = order.RepeatUntilCancelled
            ? $" · {order.CompletedUnits} {ProgressUnitLabel(order.ProgressUnit)} so far, repeats until cancelled"
            : order.Status is "doing" or "interrupted" or "blocked" or "finished"
                ? $" · {Math.Min(order.CompletedUnits, order.RequestedUnits)}/{order.RequestedUnits} {ProgressUnitLabel(order.ProgressUnit)}"
                : string.Empty;
        // Blocked and interrupted orders say why; a cancelled one says if a newer order replaced it.
        var reason = order.Status is "blocked" or "interrupted" or "cancelled" && !string.IsNullOrWhiteSpace(order.BlockedReason)
            ? $" · {order.BlockedReason}"
            : string.Empty;
        var conversation = order.Action is not ("talk_to" or "propose_marriage") ? string.Empty : " · " + (order.TalkOutcome switch
        {
            "married" => "Marriage and surname completed",
            "proposal_declined" => "Marriage proposal declined",
            "not_proposed" => "Marriage not proposed",
            "agreed" => "Conversation completed",
            "refused" => "Invitation refused",
            "deadline" => "Invitation expired",
            "daily_limit" => "Conversation allowance reached",
            "disagreed" => "Conversation ended without agreement",
            "withdrawn" => "Conversation ended",
            "participant_unavailable" => "Person unavailable",
            _ => order.TalkStatus switch
            {
                "surname_ready" or "surname_awaiting_speaker" => "Marriage agreed; choosing a shared surname",
                "surname_suspended" => "Marriage agreed; surname conversation stopped",
                "surname_closed" or "surname_pending" => "Marriage agreed; surname remains unfinished",
                "proposed" => "Invitation awaiting their choice",
                "ready" or "awaiting_speaker" => "Invitation accepted; talking",
                "wrap_up" => "Waiting for both people's wrap-up choices",
                "suspended" => "Stopped; both people must choose to resume",
                _ => "Attempting an invitation",
            },
        });
        var heard = !includeHeard || instruction.ObservedTick is null ? string.Empty : " · Heard by their personal model";
        var state = order.Status switch
        {
            "queued" => "Queued",
            "waiting" => "Waiting",
            "doing" => "Doing",
            "interrupted" => "Interrupted",
            "blocked" => "Blocked",
            "finished" => "Finished",
            "cancelled" => "Cancelled",
            "not_understood" => "Not understood",
            _ => "Waiting",
        };
        // The game understood no task in an order it could not act on, so there is none to name.
        return order.Status == "not_understood" && order.Action == "unknown"
            ? $"{state}{heard}"
            : $"{state} · {task}{units}{conversation}{reason}{heard}";
    }

    private static string OrderItemName(string? kind) => kind switch
    {
        null => "goods",
        "iron" => "refined iron",
        "tool" => "workshop tool",
        _ => GameUiText.ItemName(kind).ToLowerInvariant(),
    };

    private static string ProgressUnitLabel(string unit) => unit switch
    {
        "food_items" => "food items",
        "knowledge_reads" => "items read",
        "conversations" => "talk attempts completed",
        "marriage_attempts" => "marriage attempts completed",
        "material_items" => "items",
        "equipment_items" => "equipment items",
        "goods_items" => "items",
        "output_items" => "items made",
        "production_batches" => "batches completed",
        "artifacts" => "items written",
        "copies" => "copies made",
        "repairs" => "items repaired",
        "fields" => "fields completed",
        "collection_loads" => "loads collected",
        "storage_loads" => "loads stored",
        "return_loads" => "loads returned",
        "delivery_loads" => "loads delivered",
        "buildings" => "buildings finished",
        "expansions" => "expansions finished",
        "shelters" => "shelters reached",
        "fires" => "fires lit",
        "arrivals" => "sites reached",
        "harvests" => "harvest batches",
        "guardianships" => "care assignments",
        "cart_tasks" => "cart tasks completed",
        "animal_tasks" => "animal tasks completed",
        _ => unit,
    };

    private static string OrderBuildingName(string? kind) => kind switch
    {
        "house" => "House",
        "farmhouse" => "Farmhouse",
        "silo" => "Silo",
        "blacksmith" => "Blacksmith",
        "tailor" => "Tailor Shop",
        "clinic" => "Clinic",
        "store" => "Store",
        "warehouse" => "Town Warehouse",
        _ => "the requested building",
    };
}

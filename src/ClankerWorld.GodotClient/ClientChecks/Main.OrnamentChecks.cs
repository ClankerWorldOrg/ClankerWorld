using System.Text.Json;
using ClankerWorld.GodotClient.Protocol;

namespace ClankerWorld.GodotClient;

public partial class Main
{
    private void VerifyOrnamentPresentation()
    {
        const string founderId = "founder:00000000000000000000000000000001";
        const string parentId = "agent:00000000000000000000000000000099";
        const string wearerId = "world:agent:birth:" + founderId + ":" + parentId + ":1";
        const string recipientId = wearerId + ":birth:" + parentId + ":2";
        var position = new OwnerWorldPosition(0, 0);
        OwnerWorldAgent Person(string id, string name) => new(id, name, "active", position, 8_000, [], [],
            new("idle", null, null, [], ""), new(position, [], []), false);
        var fixture = new OwnerWorldSnapshot("ornament-ui-smoke", 1, "ornament-ui-map",
            [new(0, 0, "meadow")], [], [], null, 3)
        {
            Agents = [Person(founderId, "Rowan"), Person(parentId, "Aster"),
                Person(wearerId, "Mira") with { Equipment = new(1, 8, null, null, null, null, null, 0, 8, "gold_ornament") },
                Person(recipientId, "Orin")],
        };
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var snapshot = JsonSerializer.Deserialize<OwnerWorldSnapshot>(JsonSerializer.Serialize(fixture, options), options)
            ?? throw new InvalidOperationException("The ornament snapshot must parse through the owner wire.");
        if (snapshot.Agents.Single(person => person.Id == wearerId).Equipment?.OrnamentKind != "gold_ornament")
            throw new InvalidOperationException("The owner wire must retain the worn ornament kind.");
        var oldSelection = selectedAgentId;
        var oldProfileRequested = agentProfileRequested;
        var oldCardSnapshot = agentCardSnapshot;
        try
        {
            selectedAgentId = wearerId;
            agentProfileRequested = true;
            RenderSelectedAgentCard(snapshot);
            if (!agentDetails.GetParsedText().Contains("Wearing gold ornament", StringComparison.Ordinal))
                throw new InvalidOperationException("The agent card must display the ornament parsed from the owner wire.");
            RenderSelectedAgentCard(snapshot with
            {
                Agents = snapshot.Agents.Select(person => person.Id == wearerId
                    ? person with { Equipment = person.Equipment! with { OrnamentKind = null } } : person).ToArray(),
            });
            if (agentDetails.GetParsedText().Contains("Wearing gold ornament", StringComparison.Ordinal))
                throw new InvalidOperationException("Removing an ornament must clear its worn description on the next card refresh.");

            OwnerWorldEvent[] events =
            [
                new(1, 1, "ornament_worn", wearerId + ":ornament:smith:output:ornament"),
                new(2, 1, "ornament_removed", wearerId + ":ornament:smith:output:ornament"),
                new(3, 1, "ornament_given", wearerId + ":ornament_gift:" + recipientId + ":smith:output:ornament:gift:actual-lot"),
            ];
            var handshake = new OwnerWorldHandshake(new(1, 1),
                ["owner-observation.read.v1", "agent-inspection.read.v1", "spatial-knowledge.read.v1",
                 "owner-control.request.v1", "paused-authoring.request.v1"], []);
            if (!observationSession.TryAccept(new(handshake, new(snapshot, new(1, 0, events))), 0, out var failure))
                throw new InvalidOperationException("The ornament Event Log fixture was rejected: " + failure);
            foreach (var worldEvent in events) knownEvents[worldEvent.EventId] = worldEvent;
            RenderEventLog();
            var log = eventLog.GetParsedText();
            if (!log.Contains("Mira put on an ornament.", StringComparison.Ordinal) ||
                !log.Contains("Mira took off an ornament.", StringComparison.Ordinal) ||
                !log.Contains("Mira gave an ornament to Orin.", StringComparison.Ordinal) ||
                log.Contains("world:agent", StringComparison.Ordinal) || log.Contains("smith:output", StringComparison.Ordinal))
                throw new InvalidOperationException("Ornament events must reach the player log and resolve complete giver and recipient identities without raw lot IDs.");
        }
        finally
        {
            observationSession.ResetAfterLoad();
            knownEvents.Clear();
            UpdateUnreadEvents(null);
            renderedEventLog = string.Empty;
            RenderEventLog();
            RenderSelectedAgentCard(fixture with { Agents = [] });
            selectedAgentId = oldSelection;
            agentProfileRequested = oldProfileRequested;
            agentCardSnapshot = oldCardSnapshot;
            if (oldCardSnapshot is not null) RenderSelectedAgentCard(oldCardSnapshot);
        }
    }
}

using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public enum ConversationChoiceKind { Talk, Accept, Decline, Resume, End, AcceptWrapUp, DeclineWrapUp }

public sealed record ConversationChoice(ConversationChoiceKind Kind, string TargetId)
{
    private static string Prefix(ConversationChoiceKind kind) => kind switch
    {
        ConversationChoiceKind.Talk => "talk:",
        ConversationChoiceKind.Accept => "conversation_accept:",
        ConversationChoiceKind.Decline => "conversation_decline:",
        ConversationChoiceKind.Resume => "conversation_resume:",
        ConversationChoiceKind.End => "conversation_end:",
        ConversationChoiceKind.AcceptWrapUp => "conversation_wrapup_accept:",
        ConversationChoiceKind.DeclineWrapUp => "conversation_wrapup_decline:",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
    public string Encode() => Prefix(Kind) + TargetId;
    public static ConversationChoice? Decode(string value)
    {
        foreach (var kind in Enum.GetValues<ConversationChoiceKind>())
        {
            var prefix = Prefix(kind);
            if (value.StartsWith(prefix, StringComparison.Ordinal) && value.Length > prefix.Length)
                return new(kind, value[prefix.Length..]);
        }
        return null;
    }
}

public sealed record ConversationActionPlan(string ActorId, ConversationChoice Choice, long? Revision,
    string? InitiatorId, string? InviteeId, SocietyRelationship? Partnership) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => Choice.Kind switch
    {
        ConversationChoiceKind.Talk => "invite that person to talk",
        ConversationChoiceKind.Accept => "accept the conversation invitation",
        ConversationChoiceKind.Decline => "decline the conversation invitation",
        ConversationChoiceKind.Resume => "resume the conversation",
        ConversationChoiceKind.End => "end the interrupted conversation",
        ConversationChoiceKind.AcceptWrapUp => "accept the conversation wrap-up",
        _ => "decline the conversation wrap-up",
    };
}

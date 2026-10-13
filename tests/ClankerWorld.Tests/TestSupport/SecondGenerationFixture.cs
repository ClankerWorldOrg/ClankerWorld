using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

/// <summary>Adds an accepted birth and its physical resident to an existing test world.</summary>
internal static class SecondGenerationFixture
{
    internal static (WorldRuntimeState State, string ChildId) Add(WorldRuntimeState state,
        string? firstParentId = null, string? secondParentId = null, string key = "second-generation")
    {
        var checkpoint = state.Society.Society;
        var parents = checkpoint.Households.First(home => home.MemberIds.Count(id =>
            checkpoint.GetAgent(id).AgeBand == SocietyAgeBand.Adult) >= 2).MemberIds
            .Where(id => checkpoint.GetAgent(id).AgeBand == SocietyAgeBand.Adult).Take(2).ToArray();
        var first = firstParentId ?? parents[0];
        var second = secondParentId ?? parents[1];
        foreach (var id in new[] { first, second })
        {
            var person = checkpoint.GetAgent(id);
            if (!person.HasChosenName || AgentNameRules.SurnameKey(person.Name) is null)
                checkpoint = SocietyRules.RenameAgent(checkpoint, id, person.Name +
                    (AgentNameRules.SurnameKey(person.Name) is null ? " Vale" : "")).Checkpoint;
        }
        var household = checkpoint.GetAgent(first).HouseholdId!;
        var relationship = DerivedId.Create($"test-partnership:{first}:{second}:{key}");
        if (!checkpoint.Relationships.Any(edge => edge.Type == SocietyRelationshipType.Partnership &&
            edge.State == SocietyRelationshipState.Accepted &&
            (edge.ProposerId == first && edge.TargetId == second || edge.ProposerId == second && edge.TargetId == first)))
        {
            checkpoint = SocietyRules.ProposeRelationship(checkpoint, new(relationship, 1,
                SocietyRelationshipType.Partnership, first, second, checkpoint.WorldTick)).Checkpoint;
            checkpoint = SocietyRules.AcceptRelationship(checkpoint, relationship, 1, second).Checkpoint;
        }
        var food = DerivedId.Create($"test-birth-food:{first}:{key}");
        checkpoint = checkpoint with
        {
            Inventory = InventoryAuthority.AddLot(checkpoint.Inventory, food,
            "food", household, 4)
        };
        var request = DerivedId.Create($"test-birth:{first}:{second}:{key}");
        var birth = SocietyRules.CommitBirth(checkpoint, new(request, 1, first, second, household,
            [first, second], [first, second], food, 2, checkpoint.WorldTick, PrimaryCaregiverId: first,
            ChildName: ChosenBirthNameTestFixture.ChildName(checkpoint, first, "Zoë👶" + key)));
        var child = Assert.IsType<string>(birth.CreatedId);
        checkpoint = birth.Checkpoint;
        var parent = state.Agents.Single(person => person.AgentId == first);
        var position = state.Map.Tiles.Select(tile => tile.Position)
            .Where(point => state.Map.IsBuildable(point) && !state.Agents.Any(person => person.Position == point) &&
                !state.Map.MapObjects.Any(item => item.Position == point) && !state.Map.Resources.Any(item => item.Position == point))
            .OrderBy(point => state.Map.FootDistance(parent.Position, point)).First();
        var prepared = state with
        {
            Society = state.Society with { Society = checkpoint },
            Survival = state.Survival ?? new SurvivalState(checkpoint.WorldTick, []),
            Towns = state.Towns?.Select(town => town.ResidentIds.Contains(first, StringComparer.Ordinal)
                ? town with { ResidentIds = town.ResidentIds.Append(child).Order(StringComparer.Ordinal).ToArray() }
                : town).ToArray(),
            Agents = state.Agents.Append(new AgentState(child, position, 10_000, 0, "curious", "grow with the household",
                Survival: new SurvivalCondition(WarmthBasisPoints: 10_000))).ToArray(),
        };
        // Restore reconciles cognition bindings with the accepted birth's resident.
        using var normalized = WorldRuntime.Restore(prepared);
        return (normalized.ExportState(), child);
    }
}

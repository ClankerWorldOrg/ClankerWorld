using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed partial class AnimalPipelineTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OnlyLivingAnimalsOfferNewPermissionsGiftsAndSales(bool dead)
    {
        var (state, seller, _, animalId) = AnimalAgreementFixture(dead, permissions: false, distant: false);
        var observed = new List<CognitionCandidate>();
        var requested = false;
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)),
            id => id == seller ? new AnimalChooser(select: candidate =>
            {
                requested = true;
                if (candidate.DestinationId == animalId && candidate.Id.StartsWith("animal:agreement:", StringComparison.Ordinal))
                    observed.Add(candidate);
                return false;
            }) : new AnimalChooser());
        await Until(world, () => requested);
        if (dead) Assert.Empty(observed);
        else
        {
            Assert.Contains(observed, candidate => candidate.Description.Contains("to care for Moss", StringComparison.Ordinal));
            Assert.Contains(observed, candidate => candidate.Description.Contains("to ride Moss", StringComparison.Ordinal));
            Assert.Contains(observed, candidate => candidate.Description.StartsWith("Offer the specific animal Moss as a gift", StringComparison.Ordinal));
            Assert.Contains(observed, candidate => candidate.Description.StartsWith("Offer Moss to", StringComparison.Ordinal));
        }
        Assert.Empty(world.ExportState().AnimalWorld.Offers);
        world.Validate();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeadAnimalPermissionsCanStillBeRevokedAfterReloadAndMovingAway(bool distant)
    {
        var (state, seller, buyer, animalId) = AnimalAgreementFixture(dead: true, permissions: true, distant);
        var observed = new List<CognitionCandidate>();
        var requested = false;
        using (var inspecting = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)),
            id => id == seller ? new AnimalChooser(select: candidate =>
            {
                requested = true;
                if (candidate.DestinationId == animalId && candidate.Id.StartsWith("animal:agreement:", StringComparison.Ordinal))
                    observed.Add(candidate);
                return false;
            }) : new AnimalChooser()))
        {
            await Until(inspecting, () => requested);
            Assert.Equal(2, observed.Count);
            Assert.All(observed, candidate => Assert.StartsWith("Revoke ", candidate.Description, StringComparison.Ordinal));
            var unchanged = inspecting.Animals.Single(animal => animal.Id == animalId);
            Assert.Equal([buyer], unchanged.CarePermissions);
            Assert.Equal([buyer], unchanged.RidingPermissions);
            // Give the revocation phase a fresh choice, rather than continuing
            // the inspection provider's unchanged idle decision context.
            state = inspecting.ExportState() with
            {
                Agents = inspecting.ExportState().Agents.Select(person => person.AgentId == seller
                    ? person with { LastDecisionContext = null } : person).ToArray()
            };
            state = WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state));
        }
        using var revoking = WorldRuntime.Restore(state, id => id == seller
            ? new AnimalChooser(kind: DecisionProviderKind.LargeLanguageModel, select: candidate =>
                candidate.DestinationId == animalId && candidate.Id.StartsWith("animal:agreement:", StringComparison.Ordinal) &&
                candidate.Description.StartsWith("Revoke ", StringComparison.Ordinal))
            : new AnimalChooser());
        await Until(revoking, () => revoking.Animals.Single(animal => animal.Id == animalId) is { CarePermissions.Count: 0, RidingPermissions.Count: 0 });
        Assert.Empty(revoking.ExportState().AnimalWorld.Offers);
        Assert.Equal(2, revoking.ExportState().Events.Count(item => item.Kind == "animal_permission_changed"));
        using var reloaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(revoking.ExportState())));
        var dead = reloaded.Animals.Single(animal => animal.Id == animalId);
        Assert.NotNull(dead.DiedTick);
        Assert.Empty(dead.CarePermissions);
        Assert.Empty(dead.RidingPermissions);
        reloaded.Validate();
    }

    private static (WorldRuntimeState State, string Seller, string Buyer, string AnimalId) AnimalAgreementFixture(
        bool dead, bool permissions, bool distant)
    {
        var (state, seller, home, yard) = CreateYard("animal-death-agreements");
        var buyer = state.Society.Society.Agents.First(person => person.Status == SocietyAgentStatus.Active &&
            person.AgeBand == SocietyAgeBand.Adult && person.HouseholdId != home).Id;
        var buyerHome = state.Society.Society.GetAgent(buyer).HouseholdId!;
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "receiving-yard-wood", "wood", buyerHome, 8,
            groundPosition: new(yard.Position.X, yard.Position.Y));
        inventory = InventoryAuthority.AddLot(inventory, "receiving-yard-rope", "rope", buyerHome, 2,
            groundPosition: new(yard.Position.X, yard.Position.Y));
        using var setup = WorldRuntime.Restore(state with { Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } } },
            _ => new AnimalChooser());
        var definition = setup.WorldContent.Buildings.Single(item => item.Tags.Contains("animal-yard"));
        foreach (var tile in state.Map.Tiles.OrderBy(tile => state.Map.FootDistance(tile.Position, yard.Position)))
            if (setup.PlaceBuilding("receiving-yard", definition.CanonicalId, tile.Position, buyerHome).Applied) break;
        Assert.Contains(setup.WorldSimulation.Buildings, building => building.InstanceId == "receiving-yard");
        state = setup.ExportState();
        var animal = new AnimalState("agreement-horse", "Moss", "horse", "female",
            -(long)AnimalRules.Definition("horse").AdultDays * state.WorldSystems!.Config.TicksPerDay,
            yard.Position, "household:" + home, home, yard.InstanceId)
        {
            DiedTick = dead ? 0 : null,
            CarePermissions = permissions ? [buyer] : [],
            RidingPermissions = permissions ? [buyer] : []
        };
        inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "agreement-payment", "wood", buyer, 2);
        var buyerPosition = distant ? state.Map.Tiles.Where(tile => state.Map.IsPassable(tile.Position))
            .OrderByDescending(tile => state.Map.FootDistance(tile.Position, yard.Position)).First().Position : yard.Position;
        return (At(state, seller, yard.Position, inventory, [animal]) with
        {
            JevEnabled = false,
            RoutineHelper = RoutineHelperSettings.Off,
            Agents = At(state, seller, yard.Position, inventory, [animal]).Agents.Select(person => person.AgentId == buyer
                ? person with { Position = buyerPosition } : person).ToArray()
        }, seller, buyer, animal.Id);
    }
}

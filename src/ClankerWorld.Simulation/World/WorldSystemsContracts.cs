using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

/// <summary>
/// The four bounded first-world seasons. Season boundaries are calendar data,
/// not host-clock time.
/// </summary>
public enum SeasonKind
{
    Spring,
    Summer,
    Autumn,
    Winter,
}

/// <summary>
/// The intentionally small weather vocabulary used by the richer-world
/// contract. More effects can be data additions without adding executable
/// content.
/// </summary>
public enum WeatherKind
{
    Clear,
    Cloudy,
    Rain,
    Storm,
    Snow,
}

public enum EcologyResourceState
{
    Available,
    Depleted,
    Regenerating,
    Transformed,
}

/// <summary>
/// A season's deterministic weather distribution. The weights are data, and
/// the weather rule draws from a seed-derived stream once per world day.
/// </summary>
public sealed record WeatherProfile(
    SeasonKind Season,
    int ClearWeight,
    int CloudyWeight,
    int RainWeight,
    int StormWeight,
    int SnowWeight)
{
    [JsonIgnore]
    public int TotalWeight => checked(ClearWeight + CloudyWeight + RainWeight + StormWeight + SnowWeight);

    public int WeightFor(WeatherKind weather) => weather switch
    {
        WeatherKind.Clear => ClearWeight,
        WeatherKind.Cloudy => CloudyWeight,
        WeatherKind.Rain => RainWeight,
        WeatherKind.Storm => StormWeight,
        WeatherKind.Snow => SnowWeight,
        _ => throw new ArgumentOutOfRangeException(nameof(weather)),
    };

    public void Validate()
    {
        if (!Enum.IsDefined(Season) || ClearWeight < 0 || CloudyWeight < 0 ||
            RainWeight < 0 || StormWeight < 0 || SnowWeight < 0 ||
            (long)ClearWeight + CloudyWeight + RainWeight + StormWeight + SnowWeight > int.MaxValue ||
            TotalWeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(WeatherProfile));
        }
    }
}

/// <summary>
/// Versioned bounds and calendar/weather data for the richer systems. The
/// contract deliberately has finite collection limits and no code hooks.
/// </summary>
public sealed record WorldSystemsConfig(
    int ContractVersion = 1,
    int TicksPerDay = KernelClock.TicksPerDay,
    int DaysPerYear = KernelClock.DaysPerYear,
    int SpringDays = 91,
    int SummerDays = 91,
    int AutumnDays = 91,
    int WinterDays = 92,
    int MaxChunkCount = 256,
    int MaxResourcesPerChunk = 1_024,
    IReadOnlyList<WeatherProfile>? WeatherProfiles = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int CalendarOffsetTicks = 0)
{
    public const int MaximumChunkCount = 1_024;
    public const int MaximumResourcesPerChunk = 2_048;

    private static readonly WeatherProfile[] BaselineWeatherProfiles =
    [
        new(SeasonKind.Spring, 45, 25, 25, 5, 0),
        new(SeasonKind.Summer, 55, 20, 15, 10, 0),
        new(SeasonKind.Autumn, 35, 30, 20, 5, 10),
        new(SeasonKind.Winter, 30, 30, 5, 5, 30),
    ];

    private static readonly WeatherProfile[] BuiltInWeatherProfiles = BaselineWeatherProfiles.Select(profile =>
    {
        var weights = ReduceDefaultPrecipitation([profile.ClearWeight, profile.CloudyWeight, profile.RainWeight,
            profile.StormWeight, profile.SnowWeight]);
        return new WeatherProfile(profile.Season, weights[0], weights[1], weights[2], weights[3], weights[4]);
    }).ToArray();

    public static WorldSystemsConfig Default { get; } = new();

    [JsonIgnore]
    public IReadOnlyList<WeatherProfile> EffectiveWeatherProfiles =>
        WeatherProfiles ?? BuiltInWeatherProfiles;

    public WeatherProfile GetWeatherProfile(SeasonKind season) =>
        EffectiveWeatherProfiles.Single(profile => profile.Season == season);

    internal WeatherProfile GetRegionalBaseProfile(SeasonKind season) => WeatherProfiles is null
        ? BaselineWeatherProfiles.Single(profile => profile.Season == season) : GetWeatherProfile(season);

    internal static int[] ReduceDefaultPrecipitation(int[] weights)
    {
        // Scale by four so a 25% reduction is exact even for small weights.
        // Reallocate the removed share between clear and cloudy conditions.
        var removed = weights[2] + weights[3] + weights[4];
        return [weights[0] * 4 + removed / 2, weights[1] * 4 + removed - removed / 2,
            weights[2] * 3, weights[3] * 3, weights[4] * 3];
    }

    public void Validate()
    {
        if (ContractVersion <= 0 || TicksPerDay <= 0 || DaysPerYear <= 0 ||
            CalendarOffsetTicks < 0 || CalendarOffsetTicks >= TicksPerDay ||
            SpringDays <= 0 || SummerDays <= 0 || AutumnDays <= 0 || WinterDays <= 0 ||
            SpringDays + SummerDays + AutumnDays + WinterDays != DaysPerYear ||
            MaxChunkCount <= 0 || MaxChunkCount > MaximumChunkCount ||
            MaxResourcesPerChunk <= 0 || MaxResourcesPerChunk > MaximumResourcesPerChunk)
        {
            throw new ArgumentOutOfRangeException(nameof(WorldSystemsConfig));
        }

        var profiles = EffectiveWeatherProfiles;
        if (profiles.Count != 4 || profiles.Select(profile => profile.Season).Distinct().Count() != 4)
        {
            throw new ArgumentException("World weather must declare exactly one profile for each season.", nameof(WeatherProfiles));
        }

        foreach (var profile in profiles)
        {
            profile.Validate();
        }
    }
}

public sealed record WorldCalendar(
    long WorldTick,
    long DayIndex,
    int DayOfYear,
    int DayOfSeason,
    int TickOfDay,
    SeasonKind Season);

public static class WorldCalendarRules
{
    public static WorldCalendar FromTick(long worldTick, WorldSystemsConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.Validate();
        ArgumentOutOfRangeException.ThrowIfNegative(worldTick);

        // Keep elapsed time unchanged, and carry only the day's remainder so
        // adding the morning offset cannot overflow a long-running world tick.
        var (dayIndex, tickOfDay) = ClankerWorld.Contracts.CalendarRules.FromTick(
            worldTick, config.TicksPerDay, config.CalendarOffsetTicks);
        var dayOfYear = (int)(dayIndex % config.DaysPerYear);
        var (season, firstDayOfSeason) = SeasonAtDay(dayOfYear, config);
        return new(
            worldTick,
            dayIndex,
            dayOfYear,
            dayOfYear - firstDayOfSeason,
            tickOfDay,
            season);
    }

    public static SeasonKind GetSeason(long dayOfYear, WorldSystemsConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.Validate();
        if (dayOfYear < 0 || dayOfYear >= config.DaysPerYear)
        {
            throw new ArgumentOutOfRangeException(nameof(dayOfYear));
        }

        return SeasonAtDay((int)dayOfYear, config).Season;
    }

    private static (SeasonKind Season, int FirstDay) SeasonAtDay(int dayOfYear, WorldSystemsConfig config)
    {
        var (season, firstDay) = ClankerWorld.Contracts.CalendarRules.SeasonAtDay(
            dayOfYear, config.SpringDays, config.SummerDays, config.AutumnDays);
        return ((SeasonKind)season, firstDay);
    }
}

public sealed record WorldClimate(long WorldTick, SeasonKind Season, WeatherKind Weather);

/// <summary>
/// Pure seasonal weather transitions. A transition is stable during a day and
/// chooses the next day's weather from a stream derived only from world seed,
/// stream name, and day index.
/// </summary>
public static class WeatherRules
{
    // Weather is a local world condition, not one planet-wide roll. Regions
    // are deliberately smaller than terrain chunks so even Small worlds have
    // northern/southern climate bands.
    public const int RegionSize = 32;

    public static WeatherKind At(WorldSystemsState state, GridPoint position, int mapHeight,
        ClimateZone? climate = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentOutOfRangeException.ThrowIfNegative(position.X);
        ArgumentOutOfRangeException.ThrowIfNegative(position.Y);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(mapHeight);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(position.Y, mapHeight, nameof(position));
        if (state.RegionalWeather is { } regions)
        {
            var episode = regions.Episodes.FirstOrDefault(item =>
                item.X == position.X / RegionSize && item.Y == position.Y / RegionSize);
            if (episode is not null) return episode.Weather;
        }
        var calendar = WorldCalendarRules.FromTick(state.WorldTick, state.Config);
        var weather = mapHeight <= RegionSize ? state.Climate.Weather : WeatherForRegion(state.WorldSeed,
            calendar.DayIndex, state.Climate.Season, state.Config,
            position.X / RegionSize, position.Y / RegionSize, (mapHeight + RegionSize - 1) / RegionSize,
            climate);
        // A daily roll can select a storm, but the severe event itself must
        // end within three quarters of that saved world's day. The remaining
        // quarter stays wet without continuing the storm. Round down so short
        // fixture days never exceed the same three-quarter limit.
        var severeTicks = (int)((long)state.Config.TicksPerDay * 3 / 4);
        return weather == WeatherKind.Storm && calendar.TickOfDay >= severeTicks
            ? WeatherKind.Rain
            : weather;
    }

    /// <summary>
    /// A bounded, reproducible moisture estimate from the three most recent
    /// local weather days. It needs no per-tick save churn; detailed soil and
    /// drainage state can replace this estimate when farming is expanded.
    /// </summary>
    public static int SoilMoistureAt(WorldSystemsState state, GridPoint position, int mapHeight,
        ClimateZone? climate = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentOutOfRangeException.ThrowIfNegative(position.X);
        ArgumentOutOfRangeException.ThrowIfNegative(position.Y);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(mapHeight);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(position.Y, mapHeight, nameof(position));
        var day = WorldCalendarRules.FromTick(state.WorldTick, state.Config).DayIndex;
        var rows = (mapHeight + RegionSize - 1) / RegionSize;
        var moisture = 35;
        for (var sample = Math.Max(0, day - 2); sample <= day; sample++)
        {
            var season = WorldCalendarRules.GetSeason(sample % state.Config.DaysPerYear, state.Config);
            var weather = mapHeight <= RegionSize
                ? WeatherForDay(state.WorldSeed, sample, season, state.Config)
                : WeatherForRegion(state.WorldSeed, sample, season, state.Config,
                    position.X / RegionSize, position.Y / RegionSize, rows, climate);
            moisture = Math.Clamp(moisture + (weather switch
            {
                WeatherKind.Clear => -8,
                WeatherKind.Cloudy => -3,
                WeatherKind.Rain => 22,
                WeatherKind.Storm => 28,
                WeatherKind.Snow => 6,
                _ => throw new InvalidOperationException("The regional weather value is invalid."),
            }), 0, 100);
        }
        return moisture;
    }

    public static WeatherKind WeatherForRegion(string worldSeed, long dayIndex, SeasonKind season,
        WorldSystemsConfig config, int regionX, int regionY, int regionRows,
        ClimateZone? climate = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worldSeed);
        ArgumentNullException.ThrowIfNull(config);
        config.Validate();
        ArgumentOutOfRangeException.ThrowIfNegative(dayIndex);
        ArgumentOutOfRangeException.ThrowIfNegative(regionX);
        ArgumentOutOfRangeException.ThrowIfNegative(regionY);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(regionRows);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(regionY, regionRows);
        if (climate is { } selected && !Enum.IsDefined(selected))
            throw new ArgumentOutOfRangeException(nameof(climate));

        var weights = RegionalWeights(season, config, regionY, regionRows, climate);
        var random = Pcg32XshRrV1.Create(worldSeed,
            $"weather/day:{dayIndex.ToString(CultureInfo.InvariantCulture)}/region:{regionX.ToString(CultureInfo.InvariantCulture)},{regionY.ToString(CultureInfo.InvariantCulture)}");
        var roll = (int)(random.NextUInt() % (uint)weights.Sum());
        foreach (var weather in Enum.GetValues<WeatherKind>())
        {
            roll -= weights[(int)weather];
            if (roll < 0) return weather;
        }
        throw new InvalidOperationException("The regional weather profile did not select a weather value.");
    }

    internal static int[] RegionalWeights(SeasonKind season, WorldSystemsConfig config,
        int regionY, int regionRows, ClimateZone? climate)
    {
        var profile = config.GetRegionalBaseProfile(season);
        // Snow is confined to cold latitudes. This is a coarse first climate
        // rule; long-run rainfall and individual weather events remain distinct.
        var latitude = Math.Abs(((regionY + 0.5) / regionRows) - 0.5) * 2;
        var snowWeight = latitude >= 0.65 ? profile.SnowWeight : 0;
        var rainWeight = profile.RainWeight + profile.SnowWeight - snowWeight;
        var clearWeight = profile.ClearWeight;
        var stormWeight = profile.StormWeight;
        if (climate is ClimateZone.Dry)
        {
            var removedRain = rainWeight / 2;
            var removedStorm = stormWeight / 2;
            rainWeight -= removedRain;
            stormWeight -= removedStorm;
            clearWeight += removedRain + removedStorm;
            clearWeight += snowWeight;
            snowWeight = 0;
        }
        else if (climate is ClimateZone.Tropical)
        {
            rainWeight += snowWeight;
            snowWeight = 0;
            var extraRain = clearWeight / 4;
            clearWeight -= extraRain;
            rainWeight += extraRain;
        }
        else if (climate is ClimateZone.Cold or ClimateZone.Polar)
        {
            var shifted = (int)((long)rainWeight * (climate == ClimateZone.Polar ? 4 : 2) / 5);
            rainWeight -= shifted;
            snowWeight += shifted;
        }
        int[] weights = [clearWeight, profile.CloudyWeight, rainWeight, stormWeight, snowWeight];
        return config.WeatherProfiles is null ? WorldSystemsConfig.ReduceDefaultPrecipitation(weights) : weights;
    }

    public static ClimateZone? RegionClimate(WorldMap map, GridPoint position)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (!map.Contains(position)) return null;
        var x = Math.Min(map.Width - 1, (position.X / RegionSize) * RegionSize + RegionSize / 2);
        var y = Math.Min(map.Height - 1, (position.Y / RegionSize) * RegionSize + RegionSize / 2);
        return map.ClimateAt(new GridPoint(x, y));
    }

    public static WorldClimate CreateGenesis(string worldSeed, WorldSystemsConfig config)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worldSeed);
        ArgumentNullException.ThrowIfNull(config);
        config.Validate();

        var calendar = WorldCalendarRules.FromTick(0, config);
        return new(0, calendar.Season, WeatherForDay(worldSeed, calendar.DayIndex, calendar.Season, config));
    }

    public static WorldClimate Advance(
        WorldClimate current,
        long targetTick,
        string worldSeed,
        WorldSystemsConfig config)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentException.ThrowIfNullOrWhiteSpace(worldSeed);
        ArgumentNullException.ThrowIfNull(config);
        config.Validate();
        ArgumentOutOfRangeException.ThrowIfNegative(targetTick);
        if (targetTick < current.WorldTick)
        {
            throw new ArgumentOutOfRangeException(nameof(targetTick), "Weather cannot move backward in world time.");
        }

        var targetCalendar = WorldCalendarRules.FromTick(targetTick, config);
        var currentCalendar = WorldCalendarRules.FromTick(current.WorldTick, config);
        var crossesDay = targetCalendar.DayIndex != currentCalendar.DayIndex;
        var crossesSeason = targetCalendar.Season != current.Season;
        var weather = crossesDay || crossesSeason
            ? WeatherForDay(worldSeed, targetCalendar.DayIndex, targetCalendar.Season, config)
            : current.Weather;
        return new(targetTick, targetCalendar.Season, weather);
    }

    public static WeatherKind WeatherForDay(
        string worldSeed,
        long dayIndex,
        SeasonKind season,
        WorldSystemsConfig config)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worldSeed);
        ArgumentNullException.ThrowIfNull(config);
        config.Validate();
        ArgumentOutOfRangeException.ThrowIfNegative(dayIndex);

        var profile = config.GetWeatherProfile(season);
        var random = Pcg32XshRrV1.Create(
            worldSeed,
            $"weather/day:{dayIndex.ToString(CultureInfo.InvariantCulture)}");
        var roll = (int)(random.NextUInt() % (uint)profile.TotalWeight);
        foreach (var weather in Enum.GetValues<WeatherKind>())
        {
            roll -= profile.WeightFor(weather);
            if (roll < 0)
            {
                return weather;
            }
        }

        throw new InvalidOperationException("The weather profile did not select a weather value.");
    }
}

public sealed record EcologyResource(
    string Id,
    string Kind,
    GridPoint Position,
    bool IsRenewable,
    int Quantity,
    int Capacity,
    int RegenerationAmount,
    int RegenerationIntervalDays,
    SeasonKind RegenerationSeason,
    long NextRegenerationDay,
    EcologyResourceState State,
    bool IsPlanted = false)
{
    public void Validate()
    {
        WorldSystemsRules.RequireIdentifier(Id, nameof(Id));
        WorldSystemsRules.RequireIdentifier(Kind, nameof(Kind));
        if (!Enum.IsDefined(RegenerationSeason) || Quantity < 0 || Capacity <= 0 ||
            Quantity > Capacity || NextRegenerationDay < 0 ||
            (IsRenewable && (RegenerationAmount <= 0 || RegenerationIntervalDays <= 0)) ||
            (!IsRenewable && (RegenerationAmount != 0 || RegenerationIntervalDays != 0)) ||
            !Enum.IsDefined(State) || (!IsRenewable && State == EcologyResourceState.Regenerating) ||
            (IsPlanted && (!IsRenewable || Quantity != 0 || State != EcologyResourceState.Regenerating)))
        {
            throw new ArgumentOutOfRangeException(nameof(EcologyResource));
        }
    }
}

public sealed record EcologyState(IReadOnlyList<EcologyResource> Resources)
{
    // Source scans look up thousands of IDs a tick, so a linear search per ID
    // grows with the square of the resource count. Each state gets one index;
    // `with` makes a new state, so an index never outlives its list.
    private static readonly ConditionalWeakTable<EcologyState, Dictionary<string, int>> Indexes = new();

    public EcologyResource GetResource(string id)
        => TryGetIndexedResource(id, out var resource) ? resource : RequiredResource(id);

    public bool TryGetResource(string id, [NotNullWhen(true)] out EcologyResource? resource)
    {
        if (TryGetIndexedResource(id, out resource)) return true;
        resource = OptionalResource(id);
        return resource is not null;
    }

    private bool TryGetIndexedResource(string id, [NotNullWhen(true)] out EcologyResource? resource)
    {
        var index = Indexes.GetValue(this, static state => IndexById(state.Resources));
        if (id is not null && index.TryGetValue(id, out var position) && position >= 0 &&
            position < Resources.Count && Resources[position] is { } indexed &&
            string.Equals(indexed.Id, id, StringComparison.Ordinal))
        {
            resource = indexed;
            return true;
        }

        resource = null;
        return false;
    }

    // Keep fallback predicates off the indexed path, so successful lookups
    // allocate no per-call closure. Changed caller-owned lists still work,
    // and required missing IDs or duplicate IDs keep their original errors.
    private EcologyResource RequiredResource(string id) =>
        Resources.Single(resource => string.Equals(resource.Id, id, StringComparison.Ordinal));

    private EcologyResource? OptionalResource(string id) =>
        Resources.SingleOrDefault(resource => string.Equals(resource.Id, id, StringComparison.Ordinal));

    private static Dictionary<string, int> IndexById(IReadOnlyList<EcologyResource> resources)
    {
        var index = new Dictionary<string, int>(resources.Count, StringComparer.Ordinal);
        for (var position = 0; position < resources.Count; position++)
        {
            if (resources[position]?.Id is not { } id)
                continue;
            // A duplicate ID is marked rather than indexed, so its lookup fails as before.
            index[id] = index.ContainsKey(id) ? -1 : position;
        }

        return index;
    }
}

public sealed record EcologyHarvestResult(
    bool IsValid,
    string? Failure,
    int Yield,
    EcologyResource? Resource);

/// <summary>
/// Data-only ecology rules. Regeneration is an explicit, idempotent function
/// of committed calendar state and a resource's recorded schedule.
/// </summary>
public static class EcologyRules
{
    public static void Validate(EcologyState state, WorldSystemsConfig config)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(config);
        config.Validate();
        if (state.Resources.Count > config.MaxResourcesPerChunk * config.MaxChunkCount)
        {
            throw new ArgumentOutOfRangeException(nameof(state), "The bounded ecology collection is too large.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var resource in state.Resources)
        {
            ArgumentNullException.ThrowIfNull(resource);
            resource.Validate();
            if (!ids.Add(resource.Id))
            {
                throw new ArgumentException($"Duplicate ecology resource '{resource.Id}'.", nameof(state));
            }
        }
    }

    public static EcologyResource Regenerate(
        EcologyResource resource,
        WorldCalendar calendar,
        WorldSystemsConfig config)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(config);
        config.Validate();

        return RegenerateWithValidatedConfig(resource, calendar);
    }

    private static EcologyResource RegenerateWithValidatedConfig(EcologyResource resource, WorldCalendar calendar)
    {
        resource.Validate();

        if (!resource.IsRenewable || resource.State == EcologyResourceState.Transformed)
        {
            return resource;
        }

        if (resource.IsPlanted)
        {
            if (calendar.DayIndex < resource.NextRegenerationDay)
                return resource;
            var fruitOutOfSeason = resource.Kind == "fruit" && calendar.Season != resource.RegenerationSeason;
            return resource with
            {
                Quantity = fruitOutOfSeason ? 0 : 1,
                NextRegenerationDay = fruitOutOfSeason ? calendar.DayIndex : checked(calendar.DayIndex + resource.RegenerationIntervalDays),
                State = fruitOutOfSeason ? EcologyResourceState.Regenerating : EcologyResourceState.Available,
                IsPlanted = false,
            };
        }

        // Fruit is seasonal: it ripens only in its recorded season and falls
        // once that season ends, so a tree never carries fruit out of season.
        if (resource.Kind == "fruit" && calendar.Season != resource.RegenerationSeason)
        {
            return resource with { Quantity = 0, State = EcologyResourceState.Regenerating };
        }

        if (resource.Quantity >= resource.Capacity)
        {
            return resource with { State = EcologyResourceState.Available };
        }

        if (calendar.DayIndex < resource.NextRegenerationDay ||
            calendar.Season != resource.RegenerationSeason)
        {
            return resource with
            {
                State = resource.Quantity == 0
                    ? EcologyResourceState.Regenerating
                    : EcologyResourceState.Available,
            };
        }

        var quantity = Math.Min(
            resource.Capacity,
            checked(resource.Quantity + resource.RegenerationAmount));
        return resource with
        {
            Quantity = quantity,
            NextRegenerationDay = checked(calendar.DayIndex + resource.RegenerationIntervalDays),
            State = quantity == 0
                ? EcologyResourceState.Regenerating
                : EcologyResourceState.Available,
        };
    }

    public static EcologyState Advance(EcologyState state, WorldCalendar calendar, WorldSystemsConfig config)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(config);
        Validate(state, config);

        // Validate above checks the current config. Keep per-resource checks
        // during regeneration without retaining config trust across later calls.
        var resources = state.Resources
            .OrderBy(resource => resource.Id, StringComparer.Ordinal)
            .Select(resource => RegenerateWithValidatedConfig(resource, calendar))
            .ToArray();
        return state with { Resources = resources };
    }

    public static EcologyHarvestResult Harvest(EcologyResource resource, int amount)
    {
        ArgumentNullException.ThrowIfNull(resource);
        resource.Validate();
        if (amount <= 0)
        {
            return new(false, "Harvest amount must be positive.", 0, null);
        }

        if (resource.State is EcologyResourceState.Depleted or EcologyResourceState.Transformed ||
            amount > resource.Quantity)
        {
            return new(false, "The resource cannot supply the requested harvest.", 0, null);
        }

        var remaining = resource.Quantity - amount;
        var updated = resource with
        {
            Quantity = remaining,
            State = remaining == 0
                ? EcologyResourceState.Depleted
                : EcologyResourceState.Available,
        };
        return new(true, null, amount, updated);
    }
}

public readonly record struct ChunkCoordinate(int X, int Y)
{
    public GridPoint Origin(int chunkSize = ChunkRules.DefaultChunkSize)
    {
        ChunkRules.ValidateChunkSize(chunkSize);
        return new(checked(X * chunkSize), checked(Y * chunkSize));
    }
}

public sealed record ChunkResourceMetadata(
    string ResourceId,
    string Kind,
    GridPoint LocalPosition,
    bool IsRenewable);

public sealed record ChunkManifest(
    ChunkCoordinate Coordinate,
    int ChunkSize,
    int Width,
    int Height,
    string GeneratorId,
    string GeneratorVersion,
    IReadOnlyList<ChunkResourceMetadata> Resources,
    [property: JsonPropertyOrder(1)] string ManifestDigest = "")
{
    private readonly FrozenResources resources = Freeze(Resources);

    // `with` replacements and read-only views of borrowed arrays need the
    // same ownership boundary as constructor input. Scalar metadata is immutable.
    public IReadOnlyList<ChunkResourceMetadata> Resources
    {
        get => resources;
        init => resources = Freeze(value);
    }

    private static FrozenResources Freeze(IReadOnlyList<ChunkResourceMetadata> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source is FrozenResources owned ? owned : new(source);
    }

    private sealed class FrozenResources(IReadOnlyList<ChunkResourceMetadata> source) : IReadOnlyList<ChunkResourceMetadata>
    {
        private readonly ChunkResourceMetadata[] items = source.ToArray();
        public int Count => items.Length;
        public ChunkResourceMetadata this[int index] => items[index];
        public IEnumerator<ChunkResourceMetadata> GetEnumerator() => ((IEnumerable<ChunkResourceMetadata>)items).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

/// <summary>
/// Chunk coordinates and manifests are intentionally metadata-only. The
/// manifest digest excludes itself, following the existing seeded-map pattern.
/// </summary>
public static class ChunkRules
{
    public const int DefaultChunkSize = 16;
    public const int MaximumChunkSize = 64;

    public static ChunkCoordinate ToChunkCoordinate(GridPoint point, int chunkSize = DefaultChunkSize)
    {
        ValidateChunkSize(chunkSize);
        return new(FloorDivide(point.X, chunkSize), FloorDivide(point.Y, chunkSize));
    }

    public static GridPoint ToLocalPoint(GridPoint point, int chunkSize = DefaultChunkSize)
    {
        var coordinate = ToChunkCoordinate(point, chunkSize);
        var origin = coordinate.Origin(chunkSize);
        return new(point.X - origin.X, point.Y - origin.Y);
    }

    public static void Validate(ChunkManifest manifest, int maxResourcesPerChunk = WorldSystemsConfig.MaximumResourcesPerChunk)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ValidateChunkSize(manifest.ChunkSize);
        if (manifest.Width <= 0 || manifest.Width > manifest.ChunkSize ||
            manifest.Height <= 0 || manifest.Height > manifest.ChunkSize ||
            string.IsNullOrWhiteSpace(manifest.GeneratorId) ||
            string.IsNullOrWhiteSpace(manifest.GeneratorVersion) ||
            manifest.Resources.Count > maxResourcesPerChunk ||
            manifest.Resources.Any(resource => resource is null))
        {
            throw new ArgumentOutOfRangeException(nameof(manifest));
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var resource in manifest.Resources)
        {
            WorldSystemsRules.RequireIdentifier(resource.ResourceId, nameof(resource.ResourceId));
            WorldSystemsRules.RequireIdentifier(resource.Kind, nameof(resource.Kind));
            if (resource.LocalPosition.X < 0 || resource.LocalPosition.X >= manifest.Width ||
                resource.LocalPosition.Y < 0 || resource.LocalPosition.Y >= manifest.Height ||
                !ids.Add(resource.ResourceId))
            {
                throw new ArgumentException("Chunk resource metadata is outside the chunk or duplicated.", nameof(manifest));
            }
        }
    }

    internal static void ValidateChunkSize(int chunkSize)
    {
        if (chunkSize <= 0 || chunkSize > MaximumChunkSize)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkSize));
        }
    }

    private static int FloorDivide(int value, int divisor)
    {
        var quotient = Math.DivRem((long)value, divisor, out var remainder);
        if (remainder < 0)
        {
            quotient--;
        }

        return checked((int)quotient);
    }
}

public static class ChunkManifestCodec
{
    private const string Header = "clankerworld.world-chunk-manifest/v1";

    public static byte[] Encode(ChunkManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ChunkRules.Validate(manifest);

        var builder = new StringBuilder();
        builder.Append(Header).Append('\n');
        builder.Append("coordinate=")
            .Append(manifest.Coordinate.X.ToString(CultureInfo.InvariantCulture)).Append(',')
            .Append(manifest.Coordinate.Y.ToString(CultureInfo.InvariantCulture)).Append('\n');
        builder.Append("chunk_size=").Append(manifest.ChunkSize.ToString(CultureInfo.InvariantCulture)).Append('\n');
        builder.Append("dimensions=")
            .Append(manifest.Width.ToString(CultureInfo.InvariantCulture)).Append('x')
            .Append(manifest.Height.ToString(CultureInfo.InvariantCulture)).Append('\n');
        builder.Append("generator_id=").Append(EncodeText(manifest.GeneratorId)).Append('\n');
        builder.Append("generator_version=").Append(EncodeText(manifest.GeneratorVersion)).Append('\n');
        foreach (var resource in manifest.Resources
                     .OrderBy(item => item.ResourceId, StringComparer.Ordinal)
                     .ThenBy(item => item.LocalPosition.Y)
                     .ThenBy(item => item.LocalPosition.X))
        {
            builder.Append("resource=")
                .Append(EncodeText(resource.ResourceId)).Append('|')
                .Append(EncodeText(resource.Kind)).Append('|')
                .Append(resource.LocalPosition.X.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(resource.LocalPosition.Y.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(resource.IsRenewable ? '1' : '0').Append('\n');
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    public static string Digest(ChunkManifest manifest) =>
        Convert.ToHexStringLower(SHA256.HashData(Encode(manifest)));

    public static ChunkManifest WithDigest(ChunkManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return manifest with { ManifestDigest = Digest(manifest) };
    }

    private static string EncodeText(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
}

public sealed record WorldSystemsState(
    int SchemaVersion,
    string WorldSeed,
    long WorldTick,
    WorldSystemsConfig Config,
    WorldClimate Climate,
    EcologyState Ecology,
    IReadOnlyList<ChunkManifest> Chunks,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RegionalWeatherState? RegionalWeather = null);

/// <summary>
/// Saved environment owned by the world runtime.
/// This state contains only serializable data and applies only deterministic,
/// local rules; it does not own transport, providers, mods, or multiplayer.
/// </summary>
public static class WorldSystemsRules
{
    public const int SchemaVersion = 4;
    private sealed class ValidatedChunk { }
    private static readonly ConditionalWeakTable<ChunkManifest, ValidatedChunk> ValidatedChunks = new();

    public static WorldSystemsState CreateGenesis(
        string worldSeed,
        WorldSystemsConfig? config = null,
        IReadOnlyList<EcologyResource>? resources = null,
        IReadOnlyList<ChunkManifest>? chunks = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worldSeed);
        var effectiveConfig = config ?? WorldSystemsConfig.Default;
        effectiveConfig.Validate();
        var state = new WorldSystemsState(
            SchemaVersion,
            worldSeed.Trim(),
            0,
            effectiveConfig,
            WeatherRules.CreateGenesis(worldSeed.Trim(), effectiveConfig),
            new EcologyState(resources ?? []),
            chunks ?? []);
        Validate(state);
        return state;
    }

    public static WorldSystemsState AdvanceOneTick(WorldSystemsState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        Validate(state);
        var nextTick = checked(state.WorldTick + 1);
        var calendar = WorldCalendarRules.FromTick(nextTick, state.Config);
        var next = state with
        {
            WorldTick = nextTick,
            Climate = WeatherRules.Advance(state.Climate, nextTick, state.WorldSeed, state.Config),
            RegionalWeather = RegionalWeatherRules.Advance(state, nextTick),
            Ecology = EcologyRules.Advance(state.Ecology, calendar, state.Config),
        };
        Validate(next);
        return next;
    }

    public static void Validate(WorldSystemsState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.SchemaVersion != SchemaVersion ||
            string.IsNullOrWhiteSpace(state.WorldSeed) || state.WorldTick < 0)
        {
            throw new InvalidDataException("The world-systems schema, seed, or tick is invalid.");
        }

        ArgumentNullException.ThrowIfNull(state.Config);
        state.Config.Validate();
        RegionalWeatherRules.Validate(state.RegionalWeather, state.WorldTick, state.Config);
        ArgumentNullException.ThrowIfNull(state.Climate);
        var calendar = WorldCalendarRules.FromTick(state.WorldTick, state.Config);
        if (state.Climate.WorldTick != state.WorldTick || state.Climate.Season != calendar.Season ||
            !Enum.IsDefined(state.Climate.Weather) ||
            state.Climate.Weather != WeatherRules.WeatherForDay(
                state.WorldSeed,
                calendar.DayIndex,
                calendar.Season,
                state.Config))
        {
            throw new InvalidDataException("The saved climate does not match the saved world tick.");
        }

        EcologyRules.Validate(state.Ecology, state.Config);
        if (state.Chunks.Count > state.Config.MaxChunkCount)
        {
            throw new InvalidDataException("The saved chunk set exceeds the bounded world limit.");
        }

        var chunkKeys = new HashSet<ChunkCoordinate>();
        foreach (var chunk in state.Chunks)
        {
            ArgumentNullException.ThrowIfNull(chunk);
            // Bounds belong to this world, not the cached manifest. The cache
            // is safe only because each manifest owns its immutable resources.
            if (chunk.Resources.Count > state.Config.MaxResourcesPerChunk)
                throw new ArgumentOutOfRangeException(nameof(state));
            _ = ValidatedChunks.GetValue(chunk, static candidate =>
            {
                // Digest performs the complete structural validation too.
                if (!string.Equals(candidate.ManifestDigest, ChunkManifestCodec.Digest(candidate), StringComparison.Ordinal))
                    throw new InvalidDataException("The saved chunk manifests are duplicated or have invalid digests.");
                return new();
            });
            if (!chunkKeys.Add(chunk.Coordinate))
            {
                throw new InvalidDataException("The saved chunk manifests are duplicated or have invalid digests.");
            }
        }
    }

    internal static void RequireIdentifier(string value, string parameterName)
    {
        RequireText(value, parameterName);
        if (value.Any(character =>
                !(character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_' or ':' or '.' or '/')))
        {
            throw new ArgumentException("Identifiers may contain letters, digits, '-', '_', ':', '.', and '/'.", parameterName);
        }
    }

    internal static void RequireText(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
    }
}

/// <summary>
/// Compact JSON persistence for the disjoint systems checkpoint. It is a
/// local codec only; it performs no I/O and has no external dependencies.
/// </summary>
public static class WorldSystemsCodec
{
    private const string Header = "clankerworld.world-systems/v1";
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static byte[] Encode(WorldSystemsState state)
    {
        WorldSystemsRules.Validate(state);
        return JsonSerializer.SerializeToUtf8Bytes(new Document(Header, state), Options);
    }

    public static WorldSystemsState Decode(ReadOnlyMemory<byte> bytes)
    {
        var document = JsonSerializer.Deserialize<Document>(bytes.Span, Options)
            ?? throw new InvalidDataException("The world-systems checkpoint is empty.");
        if (!string.Equals(document.Format, Header, StringComparison.Ordinal) || document.State is null)
        {
            throw new InvalidDataException("The world-systems checkpoint format is unsupported.");
        }

        WorldSystemsRules.Validate(document.State);
        return document.State;
    }

    private sealed record Document(string Format, WorldSystemsState State);
}

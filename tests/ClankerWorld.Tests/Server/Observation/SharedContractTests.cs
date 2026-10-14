using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ClankerWorld.Contracts;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class SharedContractTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    [Fact]
    public void EveryWireRecordRoundTripsThroughTheActualGameAssembly()
    {
        var path = Path.Combine(RuntimeWriterScan.RepositoryRoot, "src", "ClankerWorld.GodotClient", ".godot",
            "mono", "temp", "bin", "Release", "ClankerWorld.GodotClient.dll");
        Assert.True(File.Exists(path), "Build the Release solution before checking both sides of the wire contract.");
        var context = new AssemblyLoadContext("wire-contract-check", isCollectible: true);
        try
        {
            var game = context.LoadFromAssemblyPath(path);
            var records = typeof(OwnerWorldSnapshot).Assembly.GetTypes()
                .Where(type => type.Namespace == "ClankerWorld.Contracts" &&
                    type.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.Instance) is not null).ToArray();
            Assert.True(records.Length > 200);
            var values = new Dictionary<Type, object?>();
            var payloads = typeof(WorldEventPayload).GetCustomAttributes<JsonDerivedTypeAttribute>()
                .Select(attribute => attribute.DerivedType);
            foreach (var server in records.Concat(payloads))
            {
                var client = game.GetType(server.FullName!, throwOnError: true)!;
                var sent = JsonSerializer.Serialize(MakeValue(server, values), server, Json);
                var received = JsonSerializer.Deserialize(sent, client, Json);
                var returned = JsonSerializer.Serialize(received, client, Json);
                Assert.True(JsonNode.DeepEquals(JsonNode.Parse(sent), JsonNode.Parse(returned)), server.FullName);
            }
        }
        finally { context.Unload(); }
    }

    [Fact]
    public void AFieldMissingOnTheReceivingSideFailsTheRoundTrip()
    {
        var sent = JsonSerializer.Serialize(new AddedField("Zoë 🌲", 37), Json);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<MissingField>(sent, Json));
    }

    private static object? MakeValue(Type type, Dictionary<Type, object?> values)
    {
        if (Nullable.GetUnderlyingType(type) is { } underlying) return MakeValue(underlying, values);
        if (type.IsAbstract)
            return MakeValue(type.GetCustomAttributes<JsonDerivedTypeAttribute>().First().DerivedType, values);
        if (type == typeof(string)) return "Zoë 🌲 e\u0301";
        if (type == typeof(bool)) return true;
        if (type == typeof(DateTimeOffset)) return new DateTimeOffset(2026, 10, 13, 12, 30, 0, TimeSpan.Zero);
        if (type.IsPrimitive || type == typeof(decimal)) return Convert.ChangeType(37, type, System.Globalization.CultureInfo.InvariantCulture);
        if (values.TryGetValue(type, out var existing)) return existing;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
        {
            var element = type.GetGenericArguments()[0];
            var array = Array.CreateInstance(element, 1);
            array.SetValue(MakeValue(element, values), 0);
            return array;
        }
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>))
        {
            var arguments = type.GetGenericArguments();
            var dictionary = (System.Collections.IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(arguments))!;
            dictionary.Add(MakeValue(arguments[0], values)!, MakeValue(arguments[1], values));
            return dictionary;
        }
        values[type] = null;
        var constructor = type.GetConstructors().OrderByDescending(candidate => candidate.GetParameters().Length).First();
        var result = constructor.Invoke(constructor.GetParameters().Select(parameter => MakeValue(parameter.ParameterType, values)).ToArray());
        values[type] = result;
        foreach (var property in type.GetProperties().Where(property => property.SetMethod is not null))
            property.SetValue(result, MakeValue(property.PropertyType, values));
        return result;
    }

    private sealed record AddedField(string Name, int NewValue);
    private sealed record MissingField(string Name);
}

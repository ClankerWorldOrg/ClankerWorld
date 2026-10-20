using System.Text.Json;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed class ModelTextBoundaryTests
{
    [Theory]
    [InlineData("  E\u200bli\u202e Anaïs 😀  ", "Eli Anaïs 😀")]
    [InlineData("\u2066Ada\u2069\u034f", "Ada")]
    [InlineData("\u200b\u202e", null)]
    [InlineData("line\nbreak", null)]
    public void ModelProseDropsInvisibleFormattingAndRetainsItsExistingBounds(string input, string? expected)
    {
        Assert.Equal(expected, CognitionDecisionResponse.NormalizeChosenName(input));
        Assert.Equal(expected, CognitionDecisionResponse.NormalizePrivateThought(input));
        Assert.Equal(expected, CognitionDecisionResponse.NormalizeObserverReply(input));
        Assert.Equal(expected, CognitionDecisionResponse.NormalizeIdentityText(input));
    }

    [Fact]
    public void InvisibleCharactersCannotHideATakenFirstNameOrSurname()
    {
        Assert.Equal(AgentNameRules.FirstNameKey("Éli River"), AgentNameRules.FirstNameKey("É\u200bli\u202e Vale"));
        Assert.Equal(AgentNameRules.SurnameKey("Éli River"), AgentNameRules.SurnameKey("Ada Ri\u200bver"));
        Assert.Null(CognitionDecisionResponse.NormalizeChosenName(new string('a', CognitionDecisionResponse.MaximumChosenNameLength) + "😀"));
        Assert.Equal(new string('a', CognitionDecisionResponse.MaximumChosenNameLength),
            CognitionDecisionResponse.NormalizeChosenName(new string('a', CognitionDecisionResponse.MaximumChosenNameLength) + "\u200b"));
    }

    [Theory]
    [InlineData("gpt\u202e-6\u200b-luna", "gpt-6-luna")]
    [InlineData("model\nprivate-text", null)]
    [InlineData("\u200b", null)]
    public void ReportedModelNamesAreSafeBeforeUsageIsSerialized(string input, string? expected)
    {
        var usage = new CognitionUsage(input, 7, 3);
        Assert.Equal(expected, usage.ModelId);
        Assert.Equal((7, 3), (usage.InputTokens, usage.OutputTokens));
        Assert.Equal(expected, (usage with { ModelId = input }).ModelId);
        Assert.Equal(expected, JsonSerializer.Deserialize<CognitionUsage>(JsonSerializer.Serialize(usage))!.ModelId);
        Assert.Null(new CognitionUsage(new string('m', ModelOutputLimits.MaximumModelNameLength + 1), 7, 3).ModelId);
    }
}

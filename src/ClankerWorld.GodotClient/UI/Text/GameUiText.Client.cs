namespace ClankerWorld.GodotClient.UI;

public static class GameUiFailures
{
    public static string FriendlyFailure(Exception exception) => exception switch
    {
        ClankerWorld.GodotClient.Pairing.OwnerAgentNameTakenException =>
            "that first name belongs to another agent. Choose a different first name",
        ClankerWorld.GodotClient.Pairing.OwnerWorldGenerationException =>
            "there is no room for a first Town with these settings. Choose another seed or change the terrain settings",
        ClankerWorld.GodotClient.Pairing.OwnerWorldStartRefusedException refused => refused.Message,
        ClankerWorld.GodotClient.Pairing.OwnerActionCompatibilityException =>
            "the game and world server need matching updates before making this change. Update both; your device pairing can stay as it is",
        System.Net.Http.HttpRequestException { StatusCode: { } code } => code switch
        {
            System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden =>
                "this device is not allowed in. Try connecting it again",
            System.Net.HttpStatusCode.NotFound => "the world server doesn't know about that",
            System.Net.HttpStatusCode.Conflict => "something changed on the world server. Try again",
            System.Net.HttpStatusCode.TooManyRequests => "the world server is busy. Try again later",
            >= System.Net.HttpStatusCode.InternalServerError => "the world server had a problem. Try again",
            _ => "the world server could not accept that request",
        },
        System.Net.Http.HttpRequestException => "cannot reach the world server",
        OperationCanceledException => "the world server took too long to answer",
        System.Text.Json.JsonException or InvalidDataException => "the world server sent something unexpected",
        UnauthorizedAccessException => "the game could not access a needed file. Check its access permissions",
        System.Security.Cryptography.CryptographicException => "the device key could not be used. Try connecting this device again",
        IOException => "the game could not read or save a needed file. Check storage and try again",
        ArgumentException => "a setting could not be used. Check the entered values",
        _ => "the action could not be completed. Try again",
    };

}

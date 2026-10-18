namespace ClankerWorld.Server.Providers;

public static partial class ProviderCredentialTelemetry
{
    [LoggerMessage(EventId = 2284, Level = LogLevel.Information,
        Message = "provider_credential_storage outcome=ready protection={Protection}")]
    public static partial void Ready(ILogger logger, string protection);

    [LoggerMessage(EventId = 2327, Level = LogLevel.Information,
        Message = "provider_credential_storage outcome=temporary_cleanup removed={Removed} retained={Retained}")]
    public static partial void TemporaryFiles(ILogger logger, int removed, int retained);
}

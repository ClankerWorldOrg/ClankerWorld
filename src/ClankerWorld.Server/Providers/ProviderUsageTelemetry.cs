namespace ClankerWorld.Server.Providers;

public static partial class ProviderUsageTelemetry
{
    [LoggerMessage(EventId = 2283, Level = LogLevel.Error,
        Message = "provider_usage_unavailable outcome=blocked scope=installation recovery=restore_trusted_meter_and_restart")]
    public static partial void AccountingBlocked(ILogger logger);

    [LoggerMessage(EventId = 2281, Level = LogLevel.Warning,
        Message = "provider_usage_limit_reached outcome={Outcome} scope=installation tick={WorldTick}")]
    public static partial void LimitReached(ILogger logger, string outcome, long worldTick);

    [LoggerMessage(EventId = 2288, Level = LogLevel.Warning,
        Message = "provider_usage_warning outcome={Outcome} scope=installation attempts={Attempts} limit={AttemptLimit} tick={WorldTick}")]
    public static partial void WarningReached(ILogger logger, string outcome, long attempts, long attemptLimit, long worldTick);

    [LoggerMessage(EventId = 2282, Level = LogLevel.Information,
        Message = "provider_usage_limit_configured outcome=accepted enabled={Enabled} limit={AttemptLimit} attempts={Attempts}")]
    public static partial void LimitConfigured(ILogger logger, bool enabled, long? attemptLimit, long attempts);
}

public static partial class ProviderCredentialTelemetry
{
    [LoggerMessage(EventId = 2284, Level = LogLevel.Information,
        Message = "provider_credential_storage outcome=ready protection={Protection}")]
    public static partial void Ready(ILogger logger, string protection);

    [LoggerMessage(EventId = 2327, Level = LogLevel.Information,
        Message = "provider_credential_storage outcome=temporary_cleanup removed={Removed} retained={Retained}")]
    public static partial void TemporaryFiles(ILogger logger, int removed, int retained);
}

public static partial class OwnerCredentialSlotTelemetry
{
    [LoggerMessage(EventId = 2264, Level = LogLevel.Information,
        Message = "provider_credential_slot_delete outcome={Outcome} slot_id={SlotId}")]
    public static partial void Deleted(ILogger logger, string outcome, string slotId);
}

namespace ClankerWorld.Server.Providers;

/// <summary>Shared guidance for model listing and explicit model tests.</summary>
internal static class ProviderFailureMessages
{
    public static string? Describe(string? failure) => failure switch
    {
        "invalid_key" => "The API key is invalid or has been revoked. Replace it with an active key from the correct project.",
        "authentication_failed" => "Authentication failed. Check the key, project membership and allowed IP addresses in your provider account.",
        "access_denied" => "Access was denied. Check the key's permissions, model access and supported region in your provider account.",
        "model_not_available" => "The model or service address is unavailable to this account. Check the model name and project access, or choose another model.",
        "no_credit" => "Your provider account has no API credit left. Check its billing balance before trying again.",
        "organization_spend_limit" => "Your organization reached its spending limit. Review that limit or wait for it to reset before trying again.",
        "project_spend_limit" => "Your project reached its spending limit. Review that limit or wait for it to reset before trying again.",
        "provider_usage_limit" => "Your organization reached its API usage limit. Ask your provider for a higher limit before trying again.",
        "quota_exceeded" => "Your account's API allowance is exhausted. Check billing, credits and usage limits in your provider account before trying again.",
        "rate_limited" => "The model service is limiting requests. Wait before retrying, or reduce the number of agents using it.",
        "timed_out" => "The model service didn't answer in time. Wait and try again.",
        "service_unavailable" => "The model service is temporarily unavailable. Wait and try again; check the provider's service status if it continues.",
        _ => null,
    };
}

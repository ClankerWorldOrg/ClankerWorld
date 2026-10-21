using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Server.Providers;

internal static class ProviderHttpClients
{
    public static void Configure(IServiceCollection services)
    {
        foreach (var name in new[] { "typesafe", "model" })
            services.AddHttpClient(name, client => client.MaxResponseContentBufferSize = ProviderResponseBody.MaximumBytes)
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
    }
}

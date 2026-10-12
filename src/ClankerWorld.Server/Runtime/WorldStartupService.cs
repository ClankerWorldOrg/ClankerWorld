using ClankerWorld.Server.Pairing;
using ClankerWorld.Server.Saves;
using ClankerWorld.Server.Worlds;

namespace ClankerWorld.Server.Runtime;

/// <summary>Start simulation and catalog work only once the active checkpoint is usable.</summary>
public sealed class WorldStartupService(IServiceProvider services,
    WorldStartupRecovery recovery) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await recovery.WaitUntilReadyAsync(stoppingToken);
        _ = services.GetRequiredService<WorldCatalogStore>();
        var advancing = services.GetRequiredService<WorldRuntimeService>();
        await advancing.StartAsync(stoppingToken);
        try { await advancing.ExecuteTask!; }
        finally
        {
            await advancing.StopAsync(CancellationToken.None);
            var localServer = services.GetRequiredService<ApprovalListenerOptions>().IsLocalServer;
            if (advancing.SaveBeforeShutdown(pauseWorld: localServer) == ShutdownSaveOutcome.WriteFailed)
                Environment.ExitCode = 1;
        }
    }
}

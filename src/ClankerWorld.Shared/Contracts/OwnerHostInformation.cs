namespace ClankerWorld.Contracts;

/// <summary>Installation metadata, separate from saved worlds and pairing authority.</summary>
public sealed record OwnerHostInformation(string Kind)
{
    public const string Launcher = "launcher";
    public const string Terminal = "terminal";
    public const string TerminalStartCommand = "sudo systemctl start clankerworld-server.service";

    public string StartupHint => Kind switch
    {
        Launcher => "If it is stopped, open ClankerWorld on the hosting computer and choose Start hosting.",
        Terminal => $"If it is stopped, run {TerminalStartCommand} on the hosting computer.",
        _ => "Check the private network and ask the person hosting the world to check its server.",
    };
}

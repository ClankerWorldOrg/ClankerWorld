using Microsoft.Win32;

namespace ClankerWorld.GodotClient.Launcher;

public static class HostingStartup
{
    private const string Entry = "ClankerWorld Hosting";
    public static string Command(string executable) => "\"" + Path.GetFullPath(executable) + "\" -- --resume-hosting";

    public static void SetEnabled(string executable, bool enabled)
    {
        if (!OperatingSystem.IsWindows()) return;
        using var run = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true) ??
            throw new IOException("The sign-in setting could not be saved.");
        if (enabled) run.SetValue(Entry, Command(executable), RegistryValueKind.String);
        else run.DeleteValue(Entry, throwOnMissingValue: false);
    }
}

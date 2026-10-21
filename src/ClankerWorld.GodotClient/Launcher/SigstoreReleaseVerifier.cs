using System.Diagnostics;

namespace ClankerWorld.GodotClient.Launcher;

/// <summary>Checks the signing identity and cryptographic proof for an approval.</summary>
public interface IReleaseSignatureVerifier
{
    bool Verify(byte[] payload, string bundle);
}

/// <summary>The bundled verifier pins the Sigstore trust root and main release workflows.</summary>
public sealed class SigstoreReleaseVerifier : IReleaseSignatureVerifier
{
    public bool Verify(byte[] payload, string bundle)
    {
        var executable = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath)!, "ClankerWorld.ReleaseVerifier.exe");
        if (!OperatingSystem.IsWindows() || !File.Exists(executable)) return false;
        var temporary = Directory.CreateTempSubdirectory("clankerworld-approval-");
        try
        {
            var messagePath = Path.Combine(temporary.FullName, "payload");
            var bundlePath = Path.Combine(temporary.FullName, "bundle.json");
            File.WriteAllBytes(messagePath, payload);
            File.WriteAllText(bundlePath, bundle);
            var start = new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            start.ArgumentList.Add(messagePath);
            start.ArgumentList.Add(bundlePath);
            using var process = Process.Start(start);
            if (process is null) return false;
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(15_000))
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
                return false;
            }
            Task.WhenAll(output, error).GetAwaiter().GetResult();
            return process.ExitCode == 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
            System.ComponentModel.Win32Exception)
        {
            return false;
        }
        finally
        {
            try { temporary.Delete(recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace ClankerWorld.Tests;

// Temporary native diagnosis for #2662; removed after choosing the final fix.
internal static class LauncherHandleDiagnostics
{
    internal static string Owners(string path)
    {
        var key = new char[33];
        var error = RmStartSession(out var session, 0, key);
        if (error != 0) return $"RmStartSession={error}";
        try
        {
            error = RmRegisterResources(session, 1, [path], 0, IntPtr.Zero, 0, IntPtr.Zero);
            if (error != 0) return $"RmRegisterResources={error}";
            uint count = 0, reasons = 0;
            error = RmGetList(session, out var needed, ref count, null, ref reasons);
            if (error == 0) return "none";
            if (error != 234) return $"RmGetList(size)={error}";
            var owners = new ProcessInfo[needed];
            count = needed;
            error = RmGetList(session, out needed, ref count, owners, ref reasons);
            if (error != 0) return $"RmGetList(owners)={error}";
            return string.Join(", ", owners.Take((int)count).Select(owner =>
                $"{owner.Process.ProcessId}:{owner.ApplicationName}"));
        }
        finally { _ = RmEndSession(session); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct UniqueProcess
    {
        public uint ProcessId;
        public FILETIME StartTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessInfo
    {
        public UniqueProcess Process;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string ApplicationName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string ServiceName;
        public int ApplicationType;
        public uint Status;
        public uint SessionId;
        [MarshalAs(UnmanagedType.Bool)] public bool Restartable;
    }

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint RmStartSession(out uint session, uint flags, [Out] char[] key);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint RmRegisterResources(uint session, uint files,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr)] string[] paths,
        uint applications, IntPtr processes, uint services, IntPtr names);

    [DllImport("rstrtmgr.dll", ExactSpelling = true)]
    private static extern uint RmGetList(uint session, out uint needed, ref uint count,
        [In, Out] ProcessInfo[]? owners, ref uint reasons);

    [DllImport("rstrtmgr.dll", ExactSpelling = true)]
    private static extern uint RmEndSession(uint session);
}

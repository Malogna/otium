using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Otium.Core.Platform;

/// <summary>
/// Large memory pages for the processor miner. RandomX runs noticeably faster with them (XMRig's documentation puts the
/// gain at up to 50%; how much depends on the processor), and XMRig already asks for them, but Windows only grants
/// them to accounts that hold the "Lock pages in memory" right (SeLockMemoryPrivilege). Granting it takes
/// administrator permission once, and it applies from the user's next sign-in.
/// </summary>
public static class LargePages
{
    public const string Privilege = "SeLockMemoryPrivilege";

    /// <summary>The argument that makes Otium grant the right to an account (given by SID) and exit, run elevated.</summary>
    public const string GrantArgument = "--grant-large-pages";

    public static bool IsSupported => OperatingSystem.IsWindows();

    /// <summary>Whether this sign-in session holds the right, so the miner can use large pages now.</summary>
    public static bool InEffect() => OperatingSystem.IsWindows() && TokenHasPrivilege(Privilege);

    /// <summary>The signed-in user's SID, which the elevated helper grants the right to.</summary>
    [SupportedOSPlatform("windows")]
    public static string CurrentUserSid() => System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value;

    /// <summary>Grants the right to an account. Needs administrator rights; returns a Windows error code, 0 on success.</summary>
    [SupportedOSPlatform("windows")]
    public static int Grant(string sid)
    {
        var sidObject = new System.Security.Principal.SecurityIdentifier(sid);
        var sidBytes = new byte[sidObject.BinaryLength];
        sidObject.GetBinaryForm(sidBytes, 0);

        var attributes = new LsaObjectAttributes { Length = Marshal.SizeOf<LsaObjectAttributes>() };
        var status = LsaOpenPolicy(IntPtr.Zero, ref attributes, PolicyCreateAccount | PolicyLookupNames, out var policy);
        if (status != 0)
            return LsaNtStatusToWinError(status);
        var sidHandle = GCHandle.Alloc(sidBytes, GCHandleType.Pinned);
        var name = Marshal.StringToHGlobalUni(Privilege);
        try
        {
            var right = new LsaUnicodeString
            {
                Length = (ushort)(Privilege.Length * 2),
                MaximumLength = (ushort)((Privilege.Length + 1) * 2),
                Buffer = name,
            };
            status = LsaAddAccountRights(policy, sidHandle.AddrOfPinnedObject(), [right], 1);
            return status == 0 ? 0 : LsaNtStatusToWinError(status);
        }
        finally
        {
            Marshal.FreeHGlobal(name);
            sidHandle.Free();
            LsaClose(policy);
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool TokenHasPrivilege(string privilege)
    {
        if (!LookupPrivilegeValue(null, privilege, out var wanted))
            return false;
        if (!OpenProcessToken(GetCurrentProcess(), TokenQuery, out var token))
            return false;
        try
        {
            GetTokenInformation(token, TokenPrivilegesClass, IntPtr.Zero, 0, out var size);
            var buffer = Marshal.AllocHGlobal(size);
            try
            {
                if (!GetTokenInformation(token, TokenPrivilegesClass, buffer, size, out _))
                    return false;
                var count = Marshal.ReadInt32(buffer);
                var entrySize = Marshal.SizeOf<LuidAndAttributes>();
                for (var i = 0; i < count; i++)
                {
                    var entry = Marshal.PtrToStructure<LuidAndAttributes>(buffer + 4 + i * entrySize);
                    if (entry.Luid.LowPart == wanted.LowPart && entry.Luid.HighPart == wanted.HighPart)
                        return true; // present in the token; XMRig enables it when it starts
                }
                return false;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        finally
        {
            CloseHandle(token);
        }
    }

    private const uint PolicyCreateAccount = 0x00000010;
    private const uint PolicyLookupNames = 0x00000800;
    private const uint TokenQuery = 0x0008;
    private const int TokenPrivilegesClass = 3;

    [StructLayout(LayoutKind.Sequential)]
    private struct LsaObjectAttributes
    {
        public int Length;
        public IntPtr RootDirectory;
        public IntPtr ObjectName;
        public uint Attributes;
        public IntPtr SecurityDescriptor;
        public IntPtr SecurityQualityOfService;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LsaUnicodeString
    {
        public ushort Length;
        public ushort MaximumLength;
        public IntPtr Buffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Luid
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct LuidAndAttributes
    {
        public Luid Luid;
        public uint Attributes;
    }

    [DllImport("advapi32.dll")]
    private static extern uint LsaOpenPolicy(IntPtr systemName, ref LsaObjectAttributes attributes, uint access, out IntPtr policy);

    [DllImport("advapi32.dll")]
    private static extern uint LsaAddAccountRights(IntPtr policy, IntPtr sid, LsaUnicodeString[] rights, uint count);

    [DllImport("advapi32.dll")]
    private static extern uint LsaClose(IntPtr policy);

    [DllImport("advapi32.dll")]
    private static extern int LsaNtStatusToWinError(uint status);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LookupPrivilegeValue(string? systemName, string name, out Luid luid);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(IntPtr token, int infoClass, IntPtr info, int length, out int returnLength);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}

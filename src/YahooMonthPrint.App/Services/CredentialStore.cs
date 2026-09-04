using System.ComponentModel;
using System.Runtime.InteropServices;

namespace YahooMonthPrint.App.Services;

public interface ICredentialStore
{
    string? Read(string accountName);

    void Write(string accountName, string appPassword);

    void Delete(string accountName);
}

public sealed class WindowsCredentialStore(string targetPrefix = "YahooMonthPrint:YahooCalendar")
    : ICredentialStore
{
    private const uint CredTypeGeneric = 1;
    private const uint CredPersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;

    public string? Read(string accountName)
    {
        var target = GetTarget(accountName);
        if (!CredRead(target, CredTypeGeneric, 0, out var credentialPointer))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == ErrorNotFound)
            {
                return null;
            }

            throw new Win32Exception(error, "Windows Credential Manager could not read the Yahoo app password.");
        }

        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(credentialPointer);
            return credential.CredentialBlobSize == 0
                ? string.Empty
                : Marshal.PtrToStringUni(
                    credential.CredentialBlob,
                    checked((int)credential.CredentialBlobSize / sizeof(char)));
        }
        finally
        {
            CredFree(credentialPointer);
        }
    }

    public void Write(string accountName, string appPassword)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appPassword);
        var target = GetTarget(accountName);
        var blob = Marshal.StringToCoTaskMemUni(appPassword);
        try
        {
            var credential = new NativeCredential
            {
                Type = CredTypeGeneric,
                TargetName = target,
                CredentialBlobSize = checked((uint)(appPassword.Length * sizeof(char))),
                CredentialBlob = blob,
                Persist = CredPersistLocalMachine,
                UserName = accountName.Trim(),
            };
            if (!CredWrite(ref credential, 0))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "Windows Credential Manager could not save the Yahoo app password.");
            }
        }
        finally
        {
            ZeroMemory(blob, appPassword.Length * sizeof(char));
            Marshal.FreeCoTaskMem(blob);
        }
    }

    public void Delete(string accountName)
    {
        var target = GetTarget(accountName);
        if (!CredDelete(target, CredTypeGeneric, 0)
            && Marshal.GetLastWin32Error() is var error
            && error != ErrorNotFound)
        {
            throw new Win32Exception(error, "Windows Credential Manager could not remove the Yahoo app password.");
        }
    }

    private string GetTarget(string accountName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountName);
        return $"{targetPrefix}:{accountName.Trim().ToLowerInvariant()}";
    }

    private static void ZeroMemory(IntPtr pointer, int byteCount)
    {
        for (var index = 0; index < byteCount; index++)
        {
            Marshal.WriteByte(pointer, index, 0);
        }
    }

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(
        string target,
        uint type,
        uint flags,
        out IntPtr credential);

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref NativeCredential credential, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        public string? TargetName;
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string? UserName;
    }
}

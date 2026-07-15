using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;

namespace KelpieWebPermissionHelper;

internal sealed class LibcUnixPermissionOperations : IUnixPermissionOperations
{
    public string RealPath(string path)
    {
        EnsureUnix();

        var pointer = realpath(path, IntPtr.Zero);
        if (pointer == IntPtr.Zero)
        {
            throw CreateNativeException("failed to resolve path");
        }

        try
        {
            return Marshal.PtrToStringAnsi(pointer)
                ?? throw new InvalidOperationException("failed to decode resolved path");
        }
        finally
        {
            free(pointer);
        }
    }

    public bool DirectoryExists(string path)
    {
        EnsureUnix();
        return Directory.Exists(path);
    }

    public void CreateDirectory(string path)
    {
        EnsureUnix();
        Directory.CreateDirectory(path);
    }

    public bool FileExists(string path)
    {
        EnsureUnix();
        return File.Exists(path);
    }

    public bool IsRegularFile(string path)
    {
        EnsureUnix();
        if (stat(path, out var status) != 0)
        {
            throw CreateNativeException("failed to stat path");
        }

        return (status.Mode & 0xF000) == 0x8000;
    }

    public bool IsSymbolicLink(string path)
    {
        EnsureUnix();
        if (lstat(path, out var status) == 0)
        {
            return (status.Mode & 0xF000) == 0xA000;
        }

        if (Marshal.GetLastPInvokeError() == 2)
        {
            return false;
        }

        throw CreateNativeException("failed to inspect symbolic link status");
    }

    public IEnumerable<string> EnumerateFileSystemEntries(string path)
    {
        EnsureUnix();
        return Directory.EnumerateFileSystemEntries(path);
    }

    public (uint Uid, uint Gid) GetOwnerIds(string path)
    {
        EnsureUnix();
        if (stat(path, out var status) != 0)
        {
            throw CreateNativeException("failed to stat path");
        }

        return (status.Uid, status.Gid);
    }

    public (uint Uid, uint Gid) GetSudoUserIds()
    {
        EnsureUnix();
        var uid = ParseSudoId("SUDO_UID", "sudo uid");
        var gid = ParseSudoId("SUDO_GID", "sudo gid");
        return (uid, gid);
    }

    public void WriteAllBytes(string path, byte[] data)
    {
        EnsureUnix();
        File.WriteAllBytes(path, data);
    }

    public byte[] ReadAllBytes(string path)
    {
        EnsureUnix();
        return File.ReadAllBytes(path);
    }

    public void MoveFileOverwrite(string sourcePath, string destinationPath)
    {
        EnsureUnix();
        File.Move(sourcePath, destinationPath, overwrite: true);
    }

    public void DeleteFileIfExists(string path)
    {
        EnsureUnix();
        File.Delete(path);
    }

    public uint ResolveUserId(string owner)
    {
        EnsureUnix();

        if (uint.TryParse(owner, NumberStyles.None, CultureInfo.InvariantCulture, out var uid))
        {
            return uid;
        }

        var pointer = getpwnam(owner);
        if (pointer == IntPtr.Zero)
        {
            throw new InvalidOperationException("owner was not found: " + owner);
        }

        return Marshal.PtrToStructure<Passwd>(pointer).Uid;
    }

    public uint ResolveGroupId(string group)
    {
        EnsureUnix();

        if (uint.TryParse(group, NumberStyles.None, CultureInfo.InvariantCulture, out var gid))
        {
            return gid;
        }

        var pointer = getgrnam(group);
        if (pointer == IntPtr.Zero)
        {
            throw new InvalidOperationException("group was not found: " + group);
        }

        return Marshal.PtrToStructure<Group>(pointer).Gid;
    }

    public void ChangeOwner(string path, uint uid, uint gid)
    {
        EnsureUnix();

        if (chown(path, uid, gid) != 0)
        {
            throw CreateNativeException("failed to change owner");
        }
    }

    public void ChangeMode(string path, uint mode)
    {
        EnsureUnix();

        if (chmod(path, mode) != 0)
        {
            throw CreateNativeException("failed to change mode");
        }
    }

    public string GetMode(string path)
    {
        EnsureUnix();

        if (stat(path, out var status) != 0)
        {
            throw CreateNativeException("failed to stat path");
        }

        return Convert.ToString(status.Mode & 0x1FF, 8).PadLeft(3, '0');
    }

    private static void EnsureUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("kelpie-web-permission-helper is supported only on Unix-like systems.");
        }
    }

    private static Win32Exception CreateNativeException(string message)
    {
        return new Win32Exception(Marshal.GetLastPInvokeError(), message);
    }

    private static uint ParseSudoId(string environmentVariableName, string label)
    {
        var value = Environment.GetEnvironmentVariable(environmentVariableName);
        if (!uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id == 0)
        {
            throw new InvalidOperationException(label + " is not available");
        }

        return id;
    }

    [DllImport("libc", EntryPoint = "realpath", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern IntPtr realpath(string path, IntPtr resolvedPath);

    [DllImport("libc", EntryPoint = "free")]
    private static extern void free(IntPtr pointer);

    [DllImport("libc", EntryPoint = "getpwnam", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern IntPtr getpwnam(string name);

    [DllImport("libc", EntryPoint = "getgrnam", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern IntPtr getgrnam(string name);

    [DllImport("libc", EntryPoint = "chown", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern int chown(string path, uint owner, uint group);

    [DllImport("libc", EntryPoint = "chmod", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern int chmod(string path, uint mode);

    [DllImport("libc", EntryPoint = "stat", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern int stat(string path, out StatBuffer status);

    [DllImport("libc", EntryPoint = "lstat", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern int lstat(string path, out StatBuffer status);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct Passwd
    {
        private readonly IntPtr _name;
        private readonly IntPtr _password;

        public readonly uint Uid;

        private readonly uint _gid;
        private readonly IntPtr _gecos;
        private readonly IntPtr _directory;
        private readonly IntPtr _shell;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct Group
    {
        private readonly IntPtr _name;
        private readonly IntPtr _password;

        public readonly uint Gid;

        private readonly IntPtr _members;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StatBuffer
    {
        private ulong _device;
        private ulong _inode;
        private ulong _hardLinks;

        public uint Mode;

        public uint Uid;
        public uint Gid;
        private int _padding;
        private ulong _rdev;
        private long _size;
        private long _blockSize;
        private long _blocks;
        private long _accessTimeSeconds;
        private long _accessTimeNanoseconds;
        private long _modifyTimeSeconds;
        private long _modifyTimeNanoseconds;
        private long _changeTimeSeconds;
        private long _changeTimeNanoseconds;
        private long _unused1;
        private long _unused2;
        private long _unused3;
    }
}

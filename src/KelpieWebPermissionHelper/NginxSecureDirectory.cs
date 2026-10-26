using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace KelpieWebPermissionHelper;

/// <summary>Descriptor-relative operations inside root-owned Linux directories.</summary>
internal sealed class NginxSecureDirectory : IDisposable
{
    private const int NoFollow = 0x20000;
    private const int DirectoryFlag = 0x10000;
    private const int CloseOnExec = 0x80000;
    private readonly SafeFileHandle _handle;
    private int Descriptor => _handle.DangerousGetHandle().ToInt32();

    private NginxSecureDirectory(int descriptor) => _handle = Own(descriptor);

    internal static void RequirePlatform()
    {
        if (!OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
            throw new NginxContractException("unsupported-platform");
    }

    internal static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith('/') || path.Length > 4096
            || path.Contains('\0') || path.Contains('\\') || path.Split('/').Any(p => p is "." or ".."))
            throw new NginxContractException("invalid-path");
        if (path == "/" || path.EndsWith('/') || path.Contains("//", StringComparison.Ordinal))
            throw new NginxContractException("invalid-path");
        return path;
    }

    internal static NginxSecureDirectory Open(string path)
    {
        RequirePlatform();
        Normalize(path);
        var current = open("/", DirectoryFlag | NoFollow | CloseOnExec, 0);
        Check(current);
        try
        {
            EnsureTrusted(current, directory: true);
            foreach (var part in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                var next = openat(current, part, DirectoryFlag | NoFollow | CloseOnExec, 0);
                Check(next);
                try { EnsureTrusted(next, directory: true); }
                catch { close(next); throw; }
                close(current);
                current = next;
            }
            return new NginxSecureDirectory(current);
        }
        catch { close(current); throw; }
    }

    internal static NginxSecureDirectory OpenPrivate(string path)
    {
        var directory = Open(path);
        try
        {
            var status = EnsureTrusted(directory.Descriptor, directory: true);
            if ((status.Mode & 0x3f) != 0) throw new NginxContractException("unsafe-state-permissions");
            return directory;
        }
        catch { directory.Dispose(); throw; }
    }

    internal static (NginxSecureDirectory Directory, string Name) Parent(string path)
    {
        Normalize(path);
        return (Open(Path.GetDirectoryName(path)!), Path.GetFileName(path));
    }

    private static string Name(string name)
    {
        if (string.IsNullOrEmpty(name) || name is "." or ".." || name.IndexOfAny(['/', '\\', '\0']) >= 0)
            throw new NginxContractException("invalid-path");
        return name;
    }

    internal byte[]? Read(string name, int limit)
    {
        var descriptor = openat(Descriptor, Name(name), NoFollow | CloseOnExec | 0x800, 0);
        if (descriptor < 0 && Marshal.GetLastPInvokeError() == 2) return null;
        Check(descriptor);
        using var handle = Own(descriptor);
        EnsureTrusted(descriptor, directory: false);
        using var stream = new FileStream(handle, FileAccess.Read);
        using var output = new MemoryStream();
        var buffer = new byte[Math.Min(limit + 1, 8192)];
        int count;
        while ((count = stream.Read(buffer, 0, Math.Min(buffer.Length, limit + 1 - (int)output.Length))) > 0)
        {
            output.Write(buffer, 0, count);
            if (output.Length > limit) throw new NginxContractException("size-exceeded");
        }
        return output.ToArray();
    }

    internal void Write(string name, byte[] bytes, bool createOnly = false, uint mode = 0x180, uint uid = 0, uint gid = 0,
        Action<ulong>? beforeInstall = null)
    {
        Name(name);
        var temporary = ".kelpie-nginx-" + Guid.NewGuid().ToString("N");
        var descriptor = openat(Descriptor, temporary, 1 | 64 | 128 | NoFollow | CloseOnExec, 0x180);
        Check(descriptor);
        var installed = false;
        try
        {
            using (var handle = Own(descriptor))
            using (var stream = new FileStream(handle, FileAccess.Write))
            {
                stream.Write(bytes);
                if (fchown(descriptor, uid, gid) != 0 || fchmod(descriptor, mode) != 0) Fail();
                stream.Flush(flushToDisk: true);
                if (fstat(descriptor, out var status) != 0) Fail();
                beforeInstall?.Invoke(status.Inode);
            }
            if (createOnly)
            {
                if (linkat(Descriptor, temporary, Descriptor, name, 0) != 0) Fail();
                if (unlinkat(Descriptor, temporary, 0) != 0) Fail();
            }
            else if (renameat(Descriptor, temporary, Descriptor, name) != 0) Fail();
            installed = true;
            Flush();
        }
        finally
        {
            if (!installed && unlinkat(Descriptor, temporary, 0) != 0 && Marshal.GetLastPInvokeError() != 2)
                throw new NginxContractException("temporary-cleanup-failed");
        }
    }

    internal void Remove(string name)
    {
        if (unlinkat(Descriptor, Name(name), 0) != 0 && Marshal.GetLastPInvokeError() != 2) Fail();
        Flush();
    }

    internal string? LinkTarget(string name)
    {
        var buffer = new byte[4097];
        var count = readlinkat(Descriptor, Name(name), buffer, (nuint)buffer.Length);
        if (count < 0 && Marshal.GetLastPInvokeError() == 2) return null;
        if (count < 0 && Marshal.GetLastPInvokeError() == 22) throw new NginxContractException("not-symlink");
        if (count <= 0 || count >= buffer.Length) throw new NginxContractException("invalid-link");
        return new System.Text.UTF8Encoding(false, true).GetString(buffer, 0, (int)count);
    }

    internal void CreateLink(string name, string target)
    {
        if (target.Length is 0 or > 4096 || target.Contains('\0')) throw new NginxContractException("invalid-link");
        if (symlinkat(target, Descriptor, Name(name)) != 0) Fail();
        Flush();
    }

    internal (uint Mode, uint Uid, uint Gid, ulong Inode) Metadata(string name)
    {
        var descriptor = openat(Descriptor, Name(name), NoFollow | CloseOnExec | 0x800, 0);
        Check(descriptor);
        using var handle = Own(descriptor);
        var status = EnsureTrusted(descriptor, directory: false);
        return (status.Mode & 0xfff, status.Uid, status.Gid, status.Inode);
    }

    internal IDisposable Lock()
    {
        var descriptor = openat(Descriptor, ".lock", 2 | 64 | NoFollow | CloseOnExec, 0x180);
        Check(descriptor);
        var handle = Own(descriptor);
        try
        {
            EnsureTrusted(descriptor, directory: false);
            if (flock(descriptor, 2 | 4) != 0) throw new NginxContractException("busy");
            return handle;
        }
        catch { handle.Dispose(); throw; }
    }

    internal static string Resolve(string path)
    {
        Normalize(path);
        var pointer = realpath(path, IntPtr.Zero);
        if (pointer == IntPtr.Zero) throw new NginxContractException("path-unavailable");
        try { return Marshal.PtrToStringUTF8(pointer) ?? throw new NginxContractException("invalid-path"); }
        finally { free(pointer); }
    }

    private void Flush() { if (fsync(Descriptor) != 0) Fail(); }
    private static void Check(int descriptor) { if (descriptor < 0) Fail(); }
    private static void Fail() => throw new NginxContractException("filesystem-denied");
    private static SafeFileHandle Own(int descriptor) => new((IntPtr)descriptor, ownsHandle: true);
    private static Status EnsureTrusted(int descriptor, bool directory)
    {
        if (fstat(descriptor, out var status) != 0) Fail();
        if (status.Uid != 0 || (status.Mode & 0x12) != 0
            || (status.Mode & 0xf000) != (directory ? 0x4000 : 0x8000)
            || (!directory && status.Links != 1)) throw new NginxContractException("unsafe-ownership");
        return status;
    }

    public void Dispose() => _handle.Dispose();

    [StructLayout(LayoutKind.Sequential)]
    private struct Status
    {
        public ulong Device, Inode, Links;
        public uint Mode, Uid, Gid;
        private int _padding;
        private ulong _rdev;
        private long _size, _blockSize, _blocks, _atime, _atimeNs, _mtime, _mtimeNs, _ctime, _ctimeNs, _reserved1, _reserved2, _reserved3;
    }

    [DllImport("libc", SetLastError = true)] private static extern int open(string path, int flags, uint mode);
    [DllImport("libc", SetLastError = true)] private static extern int openat(int directory, string path, int flags, uint mode);
    [DllImport("libc", SetLastError = true)] private static extern int close(int descriptor);
    [DllImport("libc", SetLastError = true)] private static extern int fstat(int descriptor, out Status status);
    [DllImport("libc", SetLastError = true)] private static extern int fchmod(int descriptor, uint mode);
    [DllImport("libc", SetLastError = true)] private static extern int fchown(int descriptor, uint uid, uint gid);
    [DllImport("libc", SetLastError = true)] private static extern int fsync(int descriptor);
    [DllImport("libc", SetLastError = true)] private static extern int flock(int descriptor, int operation);
    [DllImport("libc", SetLastError = true)] private static extern int renameat(int fromDirectory, string from, int toDirectory, string to);
    [DllImport("libc", SetLastError = true)] private static extern int linkat(int fromDirectory, string from, int toDirectory, string to, int flags);
    [DllImport("libc", SetLastError = true)] private static extern int unlinkat(int directory, string path, int flags);
    [DllImport("libc", SetLastError = true)] private static extern long readlinkat(int directory, string path, byte[] buffer, nuint size);
    [DllImport("libc", SetLastError = true)] private static extern int symlinkat(string target, int directory, string name);
    [DllImport("libc", SetLastError = true)] private static extern IntPtr realpath(string path, IntPtr buffer);
    [DllImport("libc")] private static extern void free(IntPtr pointer);
}

internal sealed class NginxContractException(string code) : Exception(code)
{
    internal string Code { get; } = code;
}

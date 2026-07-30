namespace KelpieWebPermissionHelper;

public interface IUnixPermissionOperations
{
    string RealPath(string path);

    bool DirectoryExists(string path);

    void CreateDirectory(string path);

    bool FileExists(string path);

    bool IsRegularFile(string path);

    bool IsSymbolicLink(string path);

    IEnumerable<string> EnumerateFileSystemEntries(string path);

    (uint Uid, uint Gid) GetOwnerIds(string path);

    (uint Uid, uint Gid) GetSudoUserIds();

    void WriteAllBytes(string path, byte[] data);

    void AppendAllText(string path, string content);

    byte[] ReadAllBytes(string path);

    void MoveFileOverwrite(string sourcePath, string destinationPath);

    void DeleteFileIfExists(string path);

    uint ResolveUserId(string owner);

    uint ResolveGroupId(string group);

    void ChangeOwner(string path, uint uid, uint gid);

    void ChangeMode(string path, uint mode);

    string GetMode(string path);
}

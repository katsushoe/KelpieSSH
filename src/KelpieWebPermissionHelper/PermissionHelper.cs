using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KelpieWebPermissionHelper;

public static partial class PermissionHelper
{
    private const int Success = 0;
    private const int Failure = 1;
    private const string DefaultNewFileMode = "644";
    private const string OptionalArgumentNone = "\u001fKELPIE_NONE\u001f";
    private const string ManagedPolicyPath = "/etc/kelpie/web-permission-helper-policy.json";

    public static int Run(
        string[] args,
        IUnixPermissionOperations operations,
        TextWriter standardOutput,
        TextWriter standardError)
    {
        return Run(args, operations, Console.In, standardOutput, standardError);
    }

    public static int Run(
        string[] args,
        IUnixPermissionOperations operations,
        TextReader standardInput,
        TextWriter standardOutput,
        TextWriter standardError)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(standardInput);
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);

        try
        {
            if (args.Length == 0)
            {
                return WriteError(standardError, "ERROR: action is required");
            }

            return args[0] switch
            {
                "--version" => WriteVersion(standardOutput),
                "version" => WriteVersion(standardOutput),
                "write-file" => WriteFile(args, operations, standardInput, standardOutput, standardError),
                "rollback-file" => RollbackFile(args, operations, standardOutput, standardError),
                "commit-file" => CommitFile(args, operations, standardOutput, standardError),
                "check-managed-file" => CheckManagedFile(args, operations, standardOutput, standardError),
                "policy" => ManagedWebPolicyCommand.Run(args, operations, standardOutput, standardError),
                "change-owner" => ChangeOwner(args, operations, standardOutput, standardError),
                "change-mode" => ChangeMode(args, operations, standardOutput, standardError),
                _ => WriteError(standardError, "ERROR: unsupported action: " + args[0]),
            };
        }
        catch (Exception ex)
        {
            return WriteError(standardError, "ERROR: " + ex.Message);
        }
    }

    private static int WriteFile(
        IReadOnlyList<string> args,
        IUnixPermissionOperations operations,
        TextReader standardInput,
        TextWriter standardOutput,
        TextWriter standardError)
    {
        if (args.Count is not (8 or 11))
        {
            return WriteError(standardError, "ERROR: write-file requires siteRoot, path, content, maxBytes, createDirectories, owner, mode, and optional expectedSha256, backup, preservePermissions");
        }

        var siteRoot = DecodeBase64(args[1], "siteRoot");
        var path = DecodeBase64(args[2], "path");
        var content = DecodeContent(args[3], standardInput);
        var maxBytes = ValidateMaxBytes(args[4]);
        var createDirectories = ValidateCreateDirectories(args[5]);
        var ownerSpec = DecodeOptionalBase64(args[6], "owner").Trim();
        var modeText = DecodeOptionalBase64(args[7], "mode").Trim();
        var expectedSha256 = args.Count == 11 ? ValidateExpectedSha256(args[8]) : null;
        var createBackup = args.Count == 11 && ValidateBooleanFlag(args[9], "backup");
        var preservePermissions = args.Count == 11 && ValidateBooleanFlag(args[10], "preservePermissions");
        if (content.Length > maxBytes)
        {
            return WriteError(standardError, "ERROR: web public content exceeds maximum write size");
        }

        var ownerRequest = ParseOwnerRequest(ownerSpec);
        uint? modeRequest = string.IsNullOrWhiteSpace(modeText)
            ? null
            : ValidateMode(modeText);
        if (!ownerRequest.HasOwner && modeRequest is null && !preservePermissions)
        {
            return WriteError(standardError, "ERROR: owner or mode is required for permissioned write");
        }

        var target = ResolveWritableTargetPath(siteRoot, path, createDirectories, operations);
        var existed = operations.FileExists(target.ResolvedPath);
        if (args.Count == 11)
        {
            ValidateManagedPolicy(siteRoot, path, allowCreate: !existed, operations);
        }
        if (existed && !operations.IsRegularFile(target.ResolvedPath))
        {
            return WriteError(standardError, "ERROR: web public path is not a regular file");
        }

        if (preservePermissions && (ownerRequest.HasOwner || modeRequest is not null))
        {
            return WriteError(standardError, "ERROR: preservePermissions does not accept owner or mode overrides");
        }

        var existingContent = existed ? operations.ReadAllBytes(target.ResolvedPath) : null;
        var existingHash = existingContent is null ? null : ComputeSha256(existingContent);
        if (expectedSha256 is not null && !string.Equals(existingHash, expectedSha256, StringComparison.Ordinal))
        {
            return WriteError(standardError, "ERROR: expected SHA-256 does not match current file");
        }

        var (uid, gid) = preservePermissions && !existed
            ? (0u, 0u)
            : ResolveWriteOwnerIds(ownerRequest, existed, target.ResolvedPath, operations);
        if ((uid == 0 || gid == 0) && !preservePermissions)
        {
            return WriteError(standardError, "ERROR: root owner or group can only be preserved for an existing file");
        }

        var finalModeText = modeText.Length == 0
            ? existed ? operations.GetMode(target.ResolvedPath) : DefaultNewFileMode
            : modeText;
        var finalMode = ValidateMode(finalModeText);

        var tempPath = target.ParentPath.TrimEnd('/') + "/.kelpie-upload-" + Guid.NewGuid().ToString("N") + ".tmp";
        var backupPath = target.ResolvedPath + ".kelpiebakup";
        try
        {
            if (createBackup && existed)
            {
                if (operations.FileExists(backupPath))
                {
                    return WriteError(standardError, "ERROR: backup already exists");
                }

                var backupTempPath = target.ParentPath.TrimEnd('/') + "/.kelpie-backup-" + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    operations.WriteAllBytes(backupTempPath, existingContent!);
                    operations.ChangeOwner(backupTempPath, uid, gid);
                    operations.ChangeMode(backupTempPath, finalMode);
                    operations.MoveFileOverwrite(backupTempPath, backupPath);
                }
                catch
                {
                    operations.DeleteFileIfExists(backupTempPath);
                    throw;
                }
            }

            operations.WriteAllBytes(tempPath, content);
            operations.ChangeOwner(tempPath, uid, gid);
            operations.ChangeMode(tempPath, finalMode);
            operations.MoveFileOverwrite(tempPath, target.ResolvedPath);
        }
        catch
        {
            operations.DeleteFileIfExists(tempPath);
            throw;
        }

        WriteWriteResult(
            standardOutput,
            target.ResolvedPath,
            written: true,
            created: !existed,
            overwritten: existed,
            content.Length,
            ownerRequest.Owner,
            ownerRequest.Group,
            finalModeText,
            existingHash ?? string.Empty,
            ComputeSha256(content),
            createBackup && existed ? backupPath : string.Empty,
            preservePermissions);
        return Success;
    }

    private static int RollbackFile(
        IReadOnlyList<string> args,
        IUnixPermissionOperations operations,
        TextWriter standardOutput,
        TextWriter standardError)
    {
        if (args.Count != 4)
        {
            return WriteError(standardError, "ERROR: rollback-file requires siteRoot, path, and expectedCurrentSha256");
        }

        var siteRoot = DecodeBase64(args[1], "siteRoot");
        var path = DecodeBase64(args[2], "path");
        var expectedCurrentSha256 = ValidateExpectedSha256(args[3]);
        var target = ResolveWritableTargetPath(siteRoot, path, createDirectories: false, operations);
        ValidateManagedPolicy(siteRoot, path, allowCreate: false, operations);
        var backupPath = target.ResolvedPath + ".kelpiebakup";
        if (!operations.FileExists(target.ResolvedPath) || !operations.FileExists(backupPath))
        {
            return WriteError(standardError, "ERROR: target or backup does not exist");
        }

        if (operations.IsSymbolicLink(backupPath) || !operations.IsRegularFile(backupPath))
        {
            return WriteError(standardError, "ERROR: backup is not a regular non-symbolic-link file");
        }

        var currentHash = ComputeSha256(operations.ReadAllBytes(target.ResolvedPath));
        if (!string.Equals(currentHash, expectedCurrentSha256, StringComparison.Ordinal))
        {
            return WriteError(standardError, "ERROR: expected SHA-256 does not match current file");
        }

        var backupContent = operations.ReadAllBytes(backupPath);
        var ownerIds = operations.GetOwnerIds(backupPath);
        var modeText = operations.GetMode(backupPath);
        var tempPath = target.ParentPath.TrimEnd('/') + "/.kelpie-rollback-" + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            operations.WriteAllBytes(tempPath, backupContent);
            operations.ChangeOwner(tempPath, ownerIds.Uid, ownerIds.Gid);
            operations.ChangeMode(tempPath, ValidateMode(modeText));
            operations.MoveFileOverwrite(tempPath, target.ResolvedPath);
            operations.DeleteFileIfExists(backupPath);
        }
        catch
        {
            operations.DeleteFileIfExists(tempPath);
            throw;
        }

        WriteWriteResult(standardOutput, target.ResolvedPath, true, false, true, backupContent.Length, string.Empty, string.Empty, modeText, currentHash, ComputeSha256(backupContent), string.Empty, true);
        return Success;
    }

    private static int CommitFile(
        IReadOnlyList<string> args,
        IUnixPermissionOperations operations,
        TextWriter standardOutput,
        TextWriter standardError)
    {
        if (args.Count != 3)
        {
            return WriteError(standardError, "ERROR: commit-file requires siteRoot and path");
        }

        var target = ResolveWritableTargetPath(DecodeBase64(args[1], "siteRoot"), DecodeBase64(args[2], "path"), false, operations);
        ValidateManagedPolicy(DecodeBase64(args[1], "siteRoot"), DecodeBase64(args[2], "path"), allowCreate: false, operations);
        var backupPath = target.ResolvedPath + ".kelpiebakup";
        if (!operations.FileExists(backupPath))
        {
            return WriteError(standardError, "ERROR: backup does not exist");
        }

        if (operations.IsSymbolicLink(backupPath) || !operations.IsRegularFile(backupPath))
        {
            return WriteError(standardError, "ERROR: backup is not a regular non-symbolic-link file");
        }

        operations.DeleteFileIfExists(backupPath);
        standardOutput.WriteLine("{\"committed\":true}");
        return Success;
    }

    private static int CheckManagedFile(
        IReadOnlyList<string> args,
        IUnixPermissionOperations operations,
        TextWriter standardOutput,
        TextWriter standardError)
    {
        if (args.Count != 4)
        {
            return WriteError(standardError, "ERROR: check-managed-file requires siteRoot, path, and create");
        }

        var siteRoot = DecodeBase64(args[1], "siteRoot");
        var path = DecodeBase64(args[2], "path");
        var allowCreate = ValidateBooleanFlag(args[3], "create");
        var createAllowed = ValidateManagedPolicy(siteRoot, path, allowCreate, operations);
        standardOutput.WriteLine("{\"allowed\":true,\"createAllowed\":" + (createAllowed ? "true" : "false") + ",\"privilegedAtomicUpdate\":true,\"preservesPermissions\":true,\"backup\":true,\"rollback\":true,\"expectedSha256\":true,\"postWriteSha256\":true}");
        return Success;
    }

    private static bool ValidateManagedPolicy(
        string siteRoot,
        string path,
        bool allowCreate,
        IUnixPermissionOperations operations)
    {
        if (!operations.FileExists(ManagedPolicyPath)
            || !operations.IsRegularFile(ManagedPolicyPath)
            || operations.IsSymbolicLink(ManagedPolicyPath))
        {
            throw new InvalidOperationException("managed web permission policy is not available");
        }

        var owner = operations.GetOwnerIds(ManagedPolicyPath);
        var mode = Convert.ToUInt32(operations.GetMode(ManagedPolicyPath), 8);
        if (owner.Uid != 0 || (mode & 0x12u) != 0)
        {
            throw new InvalidOperationException("managed web permission policy must be root-owned and not group/world-writable");
        }

        using var document = JsonDocument.Parse(operations.ReadAllBytes(ManagedPolicyPath));
        if (!document.RootElement.TryGetProperty("Sites", out var sites)
            || sites.ValueKind != JsonValueKind.Object
            || !sites.TryGetProperty(siteRoot, out var site)
            || site.ValueKind != JsonValueKind.Object
            || !site.TryGetProperty("AllowedFiles", out var files)
            || files.ValueKind != JsonValueKind.Object
            || !files.TryGetProperty(path, out var access)
            || access.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("managed web file is not allowed by helper policy");
        }

        var accessValue = access.GetString();
        if (!string.Equals(accessValue, "Update", StringComparison.Ordinal)
            && !string.Equals(accessValue, "Create", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("managed web file access is invalid");
        }

        if (allowCreate && !string.Equals(accessValue, "Create", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("managed web file creation is not allowed by helper policy");
        }

        return string.Equals(accessValue, "Create", StringComparison.Ordinal);
    }

    private static int ChangeOwner(
        IReadOnlyList<string> args,
        IUnixPermissionOperations operations,
        TextWriter standardOutput,
        TextWriter standardError)
    {
        if (args.Count != 6)
        {
            return WriteError(standardError, "ERROR: change-owner requires siteRoot, path, owner, group, and recursive");
        }

        var siteRoot = DecodeBase64(args[1], "siteRoot");
        var path = DecodeBase64(args[2], "path");
        var owner = DecodeBase64(args[3], "owner").Trim();
        var group = DecodeBase64(args[4], "group").Trim();
        var recursive = ValidateBooleanFlag(args[5], "recursive");

        ValidatePrincipal(owner, "owner");
        ValidatePrincipal(group, "group");

        var target = ResolveTargetPath(siteRoot, path, operations);
        var uid = operations.ResolveUserId(owner);
        var gid = operations.ResolveGroupId(group);
        if (uid == 0)
        {
            return WriteError(standardError, "ERROR: owner must not resolve to root");
        }

        if (gid == 0)
        {
            return WriteError(standardError, "ERROR: group must not resolve to root");
        }

        if (recursive)
        {
            foreach (var changePath in EnumerateRecursiveTargets(target.RootPath, target.ResolvedPath, operations))
            {
                operations.ChangeOwner(changePath, uid, gid);
            }
        }
        else
        {
            operations.ChangeOwner(target.ResolvedPath, uid, gid);
        }

        WriteResult(standardOutput, target.ResolvedPath, changed: true, owner, group, operations.GetMode(target.ResolvedPath));
        return Success;
    }

    private static int ChangeMode(
        IReadOnlyList<string> args,
        IUnixPermissionOperations operations,
        TextWriter standardOutput,
        TextWriter standardError)
    {
        if (args.Count != 5)
        {
            return WriteError(standardError, "ERROR: change-mode requires siteRoot, path, mode, and recursive");
        }

        var siteRoot = DecodeBase64(args[1], "siteRoot");
        var path = DecodeBase64(args[2], "path");
        var modeText = args[3].Trim();
        var recursive = ValidateBooleanFlag(args[4], "recursive");
        var mode = ValidateMode(modeText);
        var target = ResolveTargetPath(siteRoot, path, operations);

        if (recursive)
        {
            foreach (var changePath in EnumerateRecursiveTargets(target.RootPath, target.ResolvedPath, operations))
            {
                operations.ChangeMode(changePath, mode);
            }
        }
        else
        {
            operations.ChangeMode(target.ResolvedPath, mode);
        }

        WriteResult(standardOutput, target.ResolvedPath, changed: true, owner: string.Empty, group: string.Empty, modeText);
        return Success;
    }

    private static ResolvedTargetPath ResolveTargetPath(
        string siteRoot,
        string path,
        IUnixPermissionOperations operations)
    {
        if (!IsSafeAbsoluteUnixPath(siteRoot))
        {
            throw new InvalidOperationException("web public root must be a safe absolute Unix path");
        }

        if (!IsSafeSiteRelativePath(path))
        {
            throw new InvalidOperationException("requested path must be an absolute site-relative path without traversal");
        }

        var rootReal = operations.RealPath(siteRoot);
        var pathParts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var candidate = pathParts.Length == 0
            ? rootReal
            : rootReal.TrimEnd('/') + "/" + string.Join('/', pathParts);
        if (pathParts.Length > 0 && operations.IsSymbolicLink(candidate))
        {
            throw new InvalidOperationException("requested path must not be a symbolic link");
        }

        var resolvedPath = operations.RealPath(candidate);

        if (!IsSameOrChildPath(rootReal, resolvedPath))
        {
            throw new InvalidOperationException("resolved path is outside web public root");
        }

        return new ResolvedTargetPath(rootReal, resolvedPath);
    }

    private static WritableTargetPath ResolveWritableTargetPath(
        string siteRoot,
        string path,
        bool createDirectories,
        IUnixPermissionOperations operations)
    {
        if (!IsSafeAbsoluteUnixPath(siteRoot))
        {
            throw new InvalidOperationException("web public root must be a safe absolute Unix path");
        }

        if (!IsSafeSiteRelativePath(path))
        {
            throw new InvalidOperationException("requested path must be an absolute site-relative path without traversal");
        }

        var rootReal = operations.RealPath(siteRoot);
        var pathParts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (pathParts.Length == 0)
        {
            throw new InvalidOperationException("requested path must point to a file");
        }

        var fileName = pathParts[^1];
        var parentPath = pathParts.Length == 1
            ? rootReal
            : rootReal.TrimEnd('/') + "/" + string.Join('/', pathParts.Take(pathParts.Length - 1));
        if (!operations.DirectoryExists(parentPath))
        {
            if (!createDirectories)
            {
                throw new InvalidOperationException("web public parent directory does not exist");
            }

            operations.CreateDirectory(parentPath);
        }

        var parentReal = operations.RealPath(parentPath);
        if (!IsSameOrChildPath(rootReal, parentReal))
        {
            throw new InvalidOperationException("resolved parent is outside web public root");
        }

        var candidate = parentReal.TrimEnd('/') + "/" + fileName;
        if (operations.IsSymbolicLink(candidate))
        {
            throw new InvalidOperationException("requested path must not be a symbolic link");
        }

        var resolvedPath = operations.FileExists(candidate)
            ? operations.RealPath(candidate)
            : candidate;
        if (!IsSameOrChildPath(rootReal, resolvedPath))
        {
            throw new InvalidOperationException("resolved path is outside web public root");
        }

        return new WritableTargetPath(parentReal, resolvedPath);
    }

    private static string DecodeBase64(string value, string label)
    {
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(value));
        }
        catch (FormatException ex)
        {
            throw new FormatException(label + " is not valid Base64", ex);
        }
    }

    private static string DecodeOptionalBase64(string value, string label)
    {
        var decoded = DecodeBase64(value, label);
        return string.Equals(decoded, OptionalArgumentNone, StringComparison.Ordinal)
            ? string.Empty
            : decoded;
    }

    private static byte[] DecodeContent(string value, TextReader standardInput)
    {
        var contentBase64 = string.Equals(value, "-", StringComparison.Ordinal)
            ? standardInput.ReadToEnd()
            : value;

        try
        {
            return Convert.FromBase64String(contentBase64);
        }
        catch (FormatException ex)
        {
            throw new FormatException("content is not valid Base64", ex);
        }
    }

    private static int ValidateMaxBytes(string maxBytes)
    {
        if (!int.TryParse(maxBytes, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value <= 0)
        {
            throw new InvalidOperationException("maxBytes must be a positive integer");
        }

        return value;
    }

    private static bool ValidateCreateDirectories(string createDirectories)
    {
        return ValidateBooleanFlag(createDirectories, "createDirectories");
    }

    private static bool ValidateBooleanFlag(string value, string label)
    {
        return value switch
        {
            "0" => false,
            "1" => true,
            _ => throw new InvalidOperationException(label + " must be 0 or 1"),
        };
    }

    private static void ValidatePrincipal(string value, string label)
    {
        if (!LinuxPrincipalRegex().IsMatch(value))
        {
            throw new InvalidOperationException(label + " must be a safe Linux user or group name");
        }

        if (string.Equals(value, "root", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "0", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(label + " must not be root");
        }
    }

    private static OwnerRequest ParseOwnerRequest(string ownerSpec)
    {
        if (string.IsNullOrWhiteSpace(ownerSpec))
        {
            return OwnerRequest.Empty;
        }

        var parts = ownerSpec.Split(':');
        if (parts.Length > 2 || string.IsNullOrWhiteSpace(parts[0]))
        {
            throw new InvalidOperationException("owner must be in owner[:group] form");
        }

        var owner = parts[0].Trim();
        var group = parts.Length == 2 ? parts[1].Trim() : string.Empty;
        ValidatePrincipal(owner, "owner");
        if (group.Length > 0)
        {
            ValidatePrincipal(group, "group");
        }

        return new OwnerRequest(true, owner, group);
    }

    private static (uint Uid, uint Gid) ResolveWriteOwnerIds(
        OwnerRequest ownerRequest,
        bool existed,
        string resolvedPath,
        IUnixPermissionOperations operations)
    {
        var fallback = existed
            ? operations.GetOwnerIds(resolvedPath)
            : operations.GetSudoUserIds();
        if (!ownerRequest.HasOwner)
        {
            return fallback;
        }

        var uid = operations.ResolveUserId(ownerRequest.Owner);
        var gid = ownerRequest.Group.Length == 0
            ? fallback.Gid
            : operations.ResolveGroupId(ownerRequest.Group);
        return (uid, gid);
    }

    private static uint ValidateMode(string mode)
    {
        if (!ModeRegex().IsMatch(mode))
        {
            throw new InvalidOperationException("mode must be a 3-digit octal value");
        }

        var otherDigit = mode[2] - '0';
        if ((otherDigit & 0b010) != 0)
        {
            throw new InvalidOperationException("mode must not be world-writable");
        }

        return Convert.ToUInt32(mode, 8);
    }

    private static string? ValidateExpectedSha256(string value)
    {
        if (string.Equals(value, "-", StringComparison.Ordinal))
        {
            return null;
        }

        if (!Sha256Regex().IsMatch(value))
        {
            throw new InvalidOperationException("expectedSha256 must be '-' or 64 lowercase hexadecimal characters");
        }

        return value;
    }

    private static string ComputeSha256(byte[] content)
    {
        return Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
    }

    private static bool IsSafeAbsoluteUnixPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("/", StringComparison.Ordinal))
        {
            return false;
        }

        return !path.Contains('\0')
            && !path.Contains('\\', StringComparison.Ordinal)
            && !path.Split('/', StringSplitOptions.RemoveEmptyEntries).Contains("..", StringComparer.Ordinal);
    }

    private static bool IsSafeSiteRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("/", StringComparison.Ordinal))
        {
            return false;
        }

        return !path.Contains('\0')
            && !path.Contains('\\', StringComparison.Ordinal)
            && !path.Split('/', StringSplitOptions.RemoveEmptyEntries).Contains("..", StringComparer.Ordinal);
    }

    private static bool IsSameOrChildPath(string rootPath, string targetPath)
    {
        var normalizedRoot = rootPath.TrimEnd('/');
        return string.Equals(normalizedRoot, targetPath.TrimEnd('/'), StringComparison.Ordinal)
            || targetPath.StartsWith(normalizedRoot + "/", StringComparison.Ordinal);
    }

    private static IEnumerable<string> EnumerateRecursiveTargets(
        string rootReal,
        string resolvedPath,
        IUnixPermissionOperations operations)
    {
        if (!operations.DirectoryExists(resolvedPath))
        {
            yield return resolvedPath;
            yield break;
        }

        foreach (var childPath in operations.EnumerateFileSystemEntries(resolvedPath))
        {
            if (operations.IsSymbolicLink(childPath))
            {
                continue;
            }

            var childResolvedPath = operations.RealPath(childPath);
            if (!IsSameOrChildPath(rootReal, childResolvedPath))
            {
                throw new InvalidOperationException("resolved child path is outside web public root");
            }

            foreach (var nestedPath in EnumerateRecursiveTargets(rootReal, childResolvedPath, operations))
            {
                yield return nestedPath;
            }
        }

        yield return resolvedPath;
    }

    private static void WriteResult(
        TextWriter standardOutput,
        string resolvedPath,
        bool changed,
        string owner,
        string group,
        string mode)
    {
        standardOutput.WriteLine(JsonSerializer.Serialize(
            new PermissionChangeOutput(resolvedPath, changed, owner, group, mode),
            PermissionChangeJsonContext.Default.PermissionChangeOutput));
    }

    private static void WriteWriteResult(
        TextWriter standardOutput,
        string resolvedPath,
        bool written,
        bool created,
        bool overwritten,
        long size,
        string owner,
        string group,
        string mode,
        string previousSha256 = "",
        string sha256 = "",
        string backupPath = "",
        bool permissionsPreserved = false)
    {
        standardOutput.WriteLine(JsonSerializer.Serialize(
            new PermissionedWriteOutput(resolvedPath, written, created, overwritten, size, owner, group, mode, previousSha256, sha256, backupPath, permissionsPreserved),
            PermissionChangeJsonContext.Default.PermissionedWriteOutput));
    }

    private static int WriteError(TextWriter standardError, string message)
    {
        standardError.WriteLine(message);
        return Failure;
    }

    private static int WriteVersion(TextWriter standardOutput)
    {
        var version = typeof(PermissionHelper).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "0.0.0.0";
        standardOutput.WriteLine("kelpie-web-permission-helper " + version);
        return Success;
    }

    [GeneratedRegex(@"^([A-Za-z_][A-Za-z0-9_-]{0,31}|[1-9][0-9]{0,9})$", RegexOptions.CultureInvariant)]
    private static partial Regex LinuxPrincipalRegex();

    [GeneratedRegex("^[0-7]{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex ModeRegex();

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Regex();

    internal sealed record PermissionChangeOutput(
        string ResolvedPath,
        bool Changed,
        string Owner,
        string Group,
        string Mode);

    internal sealed record PermissionedWriteOutput(
        string ResolvedPath,
        bool Written,
        bool Created,
        bool Overwritten,
        long Size,
        string Owner,
        string Group,
        string Mode,
        string PreviousSha256,
        string Sha256,
        string BackupPath,
        bool PermissionsPreserved);

    private sealed record WritableTargetPath(
        string ParentPath,
        string ResolvedPath);

    private sealed record ResolvedTargetPath(
        string RootPath,
        string ResolvedPath);

    private sealed record OwnerRequest(
        bool HasOwner,
        string Owner,
        string Group)
    {
        public static OwnerRequest Empty { get; } = new(false, string.Empty, string.Empty);
    }
}

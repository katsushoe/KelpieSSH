using System.Globalization;
using System.Reflection;
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

    public static int Run(
        string[] args,
        IUnixPermissionOperations operations,
        TextWriter standardOutput,
        TextWriter standardError)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(operations);
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
                "write-file" => WriteFile(args, operations, standardOutput, standardError),
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
        TextWriter standardOutput,
        TextWriter standardError)
    {
        if (args.Count != 8)
        {
            return WriteError(standardError, "ERROR: write-file requires siteRoot, path, content, maxBytes, createDirectories, owner, and mode");
        }

        var siteRoot = DecodeBase64(args[1], "siteRoot");
        var path = DecodeBase64(args[2], "path");
        var content = DecodeContent(args[3]);
        var maxBytes = ValidateMaxBytes(args[4]);
        var createDirectories = ValidateCreateDirectories(args[5]);
        var ownerSpec = DecodeOptionalBase64(args[6], "owner").Trim();
        var modeText = DecodeOptionalBase64(args[7], "mode").Trim();
        if (content.Length > maxBytes)
        {
            return WriteError(standardError, "ERROR: web public content exceeds maximum write size");
        }

        var ownerRequest = ParseOwnerRequest(ownerSpec);
        uint? modeRequest = string.IsNullOrWhiteSpace(modeText)
            ? null
            : ValidateMode(modeText);
        if (!ownerRequest.HasOwner && modeRequest is null)
        {
            return WriteError(standardError, "ERROR: owner or mode is required for permissioned write");
        }

        var target = ResolveWritableTargetPath(siteRoot, path, createDirectories, operations);
        var existed = operations.FileExists(target.ResolvedPath);
        if (existed && !operations.IsRegularFile(target.ResolvedPath))
        {
            return WriteError(standardError, "ERROR: web public path is not a regular file");
        }

        var (uid, gid) = ResolveWriteOwnerIds(ownerRequest, existed, target.ResolvedPath, operations);
        if (uid == 0)
        {
            return WriteError(standardError, "ERROR: owner must not resolve to root");
        }

        if (gid == 0)
        {
            return WriteError(standardError, "ERROR: group must not resolve to root");
        }

        var finalModeText = modeText.Length == 0
            ? existed ? operations.GetMode(target.ResolvedPath) : DefaultNewFileMode
            : modeText;
        var finalMode = ValidateMode(finalModeText);

        var tempPath = target.ParentPath.TrimEnd('/') + "/.kelpie-upload-" + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
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
            finalModeText);
        return Success;
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

    private static byte[] DecodeContent(string value)
    {
        try
        {
            return Convert.FromBase64String(value);
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
        string mode)
    {
        standardOutput.WriteLine(JsonSerializer.Serialize(
            new PermissionedWriteOutput(resolvedPath, written, created, overwritten, size, owner, group, mode),
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
        string Mode);

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

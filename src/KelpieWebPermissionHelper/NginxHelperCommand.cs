using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KelpieWebPermissionHelper;

/// <summary>Fixed nginx operations; never invokes an interpreter or a service.</summary>
internal static partial class NginxHelperCommand
{
    internal const string PolicyPath = "/etc/kelpie/nginx-config-helper-policy.json";
    internal static readonly string[] Verbs = ["nginx-plan", "nginx-check", "nginx-apply", "nginx-status", "nginx-commit", "nginx-rollback"];
    private static readonly UTF8Encoding Utf8 = new(false, true);

    internal static int Run(string[] args, Stream input, TextWriter output)
    {
        NginxRequest? request = null;
        try
        {
            if (args.SequenceEqual(["nginx-capabilities"]))
            {
                output.WriteLine("{\"protocol\":1,\"family\":\"nginx\",\"verbs\":[\"nginx-plan\",\"nginx-check\",\"nginx-apply\",\"nginx-status\",\"nginx-commit\",\"nginx-rollback\"]}");
                return 0;
            }
            if (args.Length != 2 || args[1] != "1" || !Verbs.Contains(args[0], StringComparer.Ordinal))
                throw new NginxContractException("unsupported-operation");
            request = ParseRequest(ReadBounded(input, 256 * 1024));
            NginxSecureDirectory.RequirePlatform();
            if (geteuid() != 0 || !uint.TryParse(Environment.GetEnvironmentVariable("SUDO_UID"), NumberStyles.None,
                    CultureInfo.InvariantCulture, out var caller) || caller == 0)
                throw new NginxContractException("caller-denied");
            var response = Execute(args[0], request, caller);
            output.WriteLine(JsonSerializer.Serialize(response, NginxContractJsonContext.Default.NginxResponse));
            return response.Ok ? 0 : 1;
        }
        catch (Exception ex)
        {
            var error = ex is NginxContractException contract ? contract.Code : "invalid-request-or-state";
            System.Diagnostics.Trace.TraceError("Nginx helper failure: {0}", ex.GetType().FullName);
            output.WriteLine(JsonSerializer.Serialize(new NginxResponse
            {
                RequestId = request?.RequestId ?? "", TransactionId = request?.TransactionId ?? "",
                State = args.FirstOrDefault() is "nginx-apply" or "nginx-commit" or "nginx-rollback" ? "unknown" : "rejected", Error = error,
            }, NginxContractJsonContext.Default.NginxResponse));
            return 1;
        }
    }

    internal static NginxRequest ParseRequest(byte[] bytes)
    {
        var text = Utf8.GetString(bytes);
        using var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 16 });
        RejectDuplicates(document.RootElement);
        var request = JsonSerializer.Deserialize(text, NginxContractJsonContext.Default.NginxRequest)
            ?? throw new NginxContractException("invalid-request");
        if (request.Protocol != 1 || !Guid.TryParseExact(request.RequestId, "N", out _))
            throw new NginxContractException("invalid-request");
        NginxSecureDirectory.Normalize(request.Target);
        if (!document.RootElement.TryGetProperty("transactionId", out _)) request = request with { TransactionId = "" };
        if (!document.RootElement.TryGetProperty("intentDigest", out _)) request = request with { IntentDigest = "" };
        if (request.TransactionId is null || request.IntentDigest is null)
            throw new NginxContractException("invalid-request");
        if (request.TransactionId.Length > 0 && !Guid.TryParseExact(request.TransactionId, "N", out _))
            throw new NginxContractException("invalid-transaction");
        if (request.ExpectedHash is not null && !HashPattern().IsMatch(request.ExpectedHash))
            throw new NginxContractException("invalid-hash");
        if (request.IntentDigest.Length > 0 && !HashPattern().IsMatch(request.IntentDigest))
            throw new NginxContractException("invalid-hash");
        if (request.ContentBase64 is not null) ValidateContent(request.ContentBase64);
        return request;
    }

    private static NginxResponse Execute(string verb, NginxRequest request, uint caller)
    {
        var (policyDirectory, policyName) = NginxSecureDirectory.Parent(PolicyPath);
        using (policyDirectory)
        {
            var policyBytes = policyDirectory.Read(policyName, 65536) ?? throw new NginxContractException("policy-unavailable");
            using var document = JsonDocument.Parse(policyBytes);
            RejectDuplicates(document.RootElement);
            var policy = JsonSerializer.Deserialize(policyBytes, NginxContractJsonContext.Default.NginxPolicy)
                ?? throw new NginxContractException("policy-invalid");
            if (policy.Protocol != 1 || string.IsNullOrWhiteSpace(policy.Revision) || policy.CallerUids is null
                || policy.Targets is null || policy.Directories is null || policy.SiteTargetDirectories is null
                || !policy.CallerUids.Contains(caller))
                throw new NginxContractException("policy-denied");
            Authorize(policy, request.Target);
            using var store = NginxSecureDirectory.OpenPrivate(policy.StateDirectory);
            using var stateLock = store.Lock();
            var policyHash = Hash(policyBytes);
            if (verb == "nginx-check")
            {
                var resolved = ResolveTarget(policy, request.Target);
                var (targetParent, targetName) = NginxSecureDirectory.Parent(resolved);
                using (targetParent) targetParent.Read(targetName, 65536);
                var (backupParent, backupName) = NginxSecureDirectory.Parent(request.Target + ".kelpiebakup");
                using (backupParent)
                    if (backupParent.Read(backupName, 65536) is not null) throw new NginxContractException("backup-conflict");
                return new NginxResponse { Ok = true, RequestId = request.RequestId, State = "allowed" };
            }
            if (verb == "nginx-plan") return Plan(request, policy, policyHash, caller, store);
            var transaction = Load(request, caller, store, policy.StateDirectory);
            if (transaction.PolicyHash != policyHash) throw new NginxContractException("policy-changed");
            if (verb == "nginx-status")
            {
                return InspectStatus(transaction, policy, store, request.RequestId);
            }
            if (request.IntentDigest != transaction.IntentDigest) throw new NginxContractException("intent-conflict");
            if (verb == "nginx-apply") return Apply(request, transaction, policy, store);
            Reconcile(transaction, policy, store);
            if (verb == "nginx-rollback") return Rollback(request, transaction, policy, store);
            return Commit(request, transaction, policy, store);
        }
    }

    private static NginxResponse Plan(NginxRequest request, NginxPolicy policy, string policyHash, uint caller, NginxSecureDirectory store)
    {
        // A request ID identifies one immutable intent, including after process restart.
        var existing = store.Read(request.RequestId + ".json", 1024 * 1024);
        if (existing is not null)
        {
            var old = JsonSerializer.Deserialize(existing, NginxContractJsonContext.Default.NginxTransaction)!;
            if (old.CallerUid != caller || old.Target != request.Target || old.PolicyHash != policyHash
                || old.NewHash != (request.ContentBase64 is null ? "" : Hash(ValidateContent(request.ContentBase64)))
                || old.WriteRequested != (request.ContentBase64 is not null)
                || old.DisableDefaultSites != request.DisableDefaultSites
                || request.ExpectedHash is not null && request.ExpectedHash != old.OriginalHash
                || request.ExpectedExists is not null && request.ExpectedExists != old.OriginalExists)
                throw new NginxContractException("request-id-conflict");
            return Response(old, request.RequestId);
        }
        if (Directory.EnumerateFiles(policy.StateDirectory, "*.json").Take(256).Count() >= 256)
            throw new NginxContractException("state-capacity");
        var resolved = ResolveTarget(policy, request.Target);
        var (parent, name) = NginxSecureDirectory.Parent(resolved);
        using (parent)
        {
            var before = parent.Read(name, 65536);
            if (request.ExpectedExists is not null && request.ExpectedExists != (before is not null)
                || request.ExpectedHash is not null && request.ExpectedHash != Hash(before ?? []))
                throw new NginxContractException("state-conflict");
            var (backupParent, backupName) = NginxSecureDirectory.Parent(request.Target + ".kelpiebakup");
            using (backupParent)
                if (backupParent.Read(backupName, 65536) is not null) throw new NginxContractException("backup-conflict");
            var metadata = before is null ? (Mode: 0x1a4u, Uid: 0u, Gid: 0u, Inode: 0UL) : parent.Metadata(name);
            var links = request.DisableDefaultSites ? FindLinks(policy) : [];
            var transaction = new NginxTransaction
            {
                Id = request.RequestId, RequestId = request.RequestId, CallerUid = caller, PolicyHash = policyHash,
                Target = request.Target, ResolvedPath = resolved, OriginalExists = before is not null,
                OriginalHash = Hash(before ?? []), NewHash = request.ContentBase64 is null ? "" : Hash(ValidateContent(request.ContentBase64)),
                OriginalMode = metadata.Mode, OriginalUid = metadata.Uid, OriginalGid = metadata.Gid, OriginalInode = metadata.Inode,
                WriteRequested = request.ContentBase64 is not null, Links = links, ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
                DisableDefaultSites = request.DisableDefaultSites,
                NewBytes = request.ContentBase64 is null ? 0 : ValidateContent(request.ContentBase64).Length,
            };
            transaction.IntentDigest = Hash(JsonSerializer.SerializeToUtf8Bytes(transaction, NginxContractJsonContext.Default.NginxTransaction));
            Save(store, transaction);
            return Response(transaction, request.RequestId);
        }
    }

    private static NginxResponse Apply(NginxRequest request, NginxTransaction transaction, NginxPolicy policy, NginxSecureDirectory store)
    {
        var content = request.ContentBase64 is null ? null : ValidateContent(request.ContentBase64);
        if (request.RequestId != transaction.RequestId || transaction.WriteRequested != (content is not null)
            || content is not null && Hash(content) != transaction.NewHash)
            throw new NginxContractException("intent-conflict");
        if (transaction.State != "planned")
        {
            Reconcile(transaction, policy, store);
            return Response(transaction, request.RequestId);
        }
        if (transaction.ExpiresAt < DateTimeOffset.UtcNow) throw new NginxContractException("plan-expired");
        ValidatePreState(transaction, policy);
        var (parent, name) = NginxSecureDirectory.Parent(transaction.ResolvedPath);
        using (parent)
        {
            transaction.State = "applying";
            Save(store, transaction);
            try
            {
                if (content is not null)
                {
                    transaction.BackupPending = true;
                    Save(store, transaction);
                    var (backupParent, backupName) = NginxSecureDirectory.Parent(transaction.Target + ".kelpiebakup");
                    using (backupParent) backupParent.Write(backupName, parent.Read(name, 65536) ?? [], createOnly: true,
                        beforeInstall: inode => { transaction.BackupInode = inode; Save(store, transaction); });
                    transaction.BackupCreated = true;
                    transaction.BackupPending = false;
                    transaction.FilePending = true;
                    Save(store, transaction);
                    parent.Write(name, content, mode: transaction.OriginalMode, uid: transaction.OriginalUid, gid: transaction.OriginalGid,
                        beforeInstall: inode => { transaction.AppliedInode = inode; Save(store, transaction); });
                    transaction.FileApplied = true;
                    transaction.FilePending = false;
                    Save(store, transaction);
                }
                foreach (var link in transaction.Links)
                {
                    var (directory, linkName) = NginxSecureDirectory.Parent(link.Path);
                    using (directory)
                    {
                        VerifyLink(link, directory, linkName);
                        link.Pending = true;
                        Save(store, transaction);
                        directory.Remove(linkName);
                        link.Removed = true;
                        link.Pending = false;
                        Save(store, transaction);
                    }
                }
                transaction.State = transaction.FileApplied || transaction.Links.Any(x => x.Removed) ? "applied" : "no-op";
            }
            catch (NginxContractException ex)
            {
                transaction.State = "recovery-required";
                transaction.Error = ex.Code;
            }
            Save(store, transaction);
            return Response(transaction, request.RequestId);
        }
    }

    private static NginxResponse Rollback(NginxRequest request, NginxTransaction transaction, NginxPolicy policy, NginxSecureDirectory store)
    {
        if (transaction.State == "rolled-back") return Response(transaction, request.RequestId);
        if (transaction.State is not ("applied" or "recovery-required" or "rolling-back")) throw new NginxContractException("invalid-state");
        ValidateCurrent(transaction, policy);
        ValidateBackup(transaction);
        transaction.State = "rolling-back";
        Save(store, transaction);
        foreach (var link in transaction.Links.Reverse().Where(x => x.Removed))
        {
            var (directory, name) = NginxSecureDirectory.Parent(link.Path);
            using (directory)
            {
                link.Pending = true;
                Save(store, transaction);
                directory.CreateLink(name, link.Target);
                link.Removed = false;
                link.Pending = false;
                Save(store, transaction);
            }
        }
        var (parent, file) = NginxSecureDirectory.Parent(transaction.ResolvedPath);
        using (parent)
        {
            if (transaction.FileApplied)
            {
                var (backupParent, backupName) = NginxSecureDirectory.Parent(transaction.Target + ".kelpiebakup");
                byte[] original;
                using (backupParent) original = backupParent.Read(backupName, 65536) ?? throw new NginxContractException("backup-unavailable");
                if (Hash(original) != transaction.OriginalHash) throw new NginxContractException("backup-conflict");
                transaction.FilePending = true;
                Save(store, transaction);
                if (transaction.OriginalExists) parent.Write(file, original, mode: transaction.OriginalMode, uid: transaction.OriginalUid, gid: transaction.OriginalGid,
                    beforeInstall: inode => { transaction.RestoredInode = inode; Save(store, transaction); });
                else parent.Remove(file);
                transaction.FileApplied = false;
                transaction.FilePending = false;
                Save(store, transaction);
            }
            if (transaction.BackupCreated)
            {
                ValidateBackup(transaction);
                transaction.BackupCleanupPending = true;
                Save(store, transaction);
                var (backupParent, backupName) = NginxSecureDirectory.Parent(transaction.Target + ".kelpiebakup");
                using (backupParent) backupParent.Remove(backupName);
                transaction.BackupCreated = false;
                transaction.BackupCleanupPending = false;
            }
        }
        transaction.State = "rolled-back";
        transaction.Error = "";
        Save(store, transaction);
        return Response(transaction, request.RequestId);
    }

    private static NginxResponse Commit(NginxRequest request, NginxTransaction transaction, NginxPolicy policy, NginxSecureDirectory store)
    {
        if (transaction.State == "committed") return Response(transaction, request.RequestId);
        if (transaction.State is not ("applied" or "committing")) throw new NginxContractException("invalid-state");
        ValidateCurrent(transaction, policy);
        ValidateBackup(transaction, allowMissing: transaction.State == "committing");
        transaction.State = "committing";
        Save(store, transaction);
        if (transaction.BackupCreated)
        {
            transaction.BackupCleanupPending = true;
            Save(store, transaction);
            var (parent, name) = NginxSecureDirectory.Parent(transaction.Target + ".kelpiebakup");
            using (parent) parent.Remove(name);
            transaction.BackupCreated = false;
            transaction.BackupCleanupPending = false;
        }
        transaction.State = "committed";
        Save(store, transaction);
        return Response(transaction, request.RequestId);
    }

    private static void ValidatePreState(NginxTransaction transaction, NginxPolicy policy)
    {
        if (ResolveTarget(policy, transaction.Target) != transaction.ResolvedPath) throw new NginxContractException("state-conflict");
        var (parent, name) = NginxSecureDirectory.Parent(transaction.ResolvedPath);
        using (parent)
        {
            var before = parent.Read(name, 65536);
            if ((before is not null) != transaction.OriginalExists || Hash(before ?? []) != transaction.OriginalHash
                || before is not null && parent.Metadata(name).Inode != transaction.OriginalInode) throw new NginxContractException("state-conflict");
        }
        var (backupParent, backupName) = NginxSecureDirectory.Parent(transaction.Target + ".kelpiebakup");
        using (backupParent)
            if (backupParent.Read(backupName, 65536) is not null) throw new NginxContractException("backup-conflict");
        foreach (var link in transaction.Links)
        {
            var (directory, linkName) = NginxSecureDirectory.Parent(link.Path);
            using (directory) VerifyLink(link, directory, linkName);
        }
    }

    private static NginxResponse InspectStatus(NginxTransaction transaction, NginxPolicy policy, NginxSecureDirectory store, string requestId)
    {
        try
        {
            Reconcile(transaction, policy, store);
            if (ResolveTarget(policy, transaction.Target) != transaction.ResolvedPath)
                throw new NginxContractException("state-conflict");
            if (transaction.State is "applied" or "recovery-required" or "applying" or "rolling-back" or "committing")
                ValidateCurrent(transaction, policy);
            var (parent, name) = NginxSecureDirectory.Parent(transaction.ResolvedPath);
            using (parent) return Response(transaction, requestId) with { CurrentHash = Hash(parent.Read(name, 65536) ?? []) };
        }
        catch (NginxContractException ex)
        {
            return Response(transaction, requestId) with { Ok = false, State = "conflict", Error = ex.Code, CurrentHash = "", Recoverable = true };
        }
    }

    private static void ValidateBackup(NginxTransaction transaction, bool allowMissing = false)
    {
        if (!transaction.BackupCreated) return;
        var (parent, name) = NginxSecureDirectory.Parent(transaction.Target + ".kelpiebakup");
        using (parent)
        {
            var bytes = parent.Read(name, 65536);
            if (bytes is null && allowMissing) return;
            if (bytes is null || Hash(bytes) != transaction.OriginalHash || parent.Metadata(name).Inode != transaction.BackupInode)
                throw new NginxContractException("backup-conflict");
        }
    }

    private static void ValidateCurrent(NginxTransaction transaction, NginxPolicy policy)
    {
        if (ResolveTarget(policy, transaction.Target) != transaction.ResolvedPath) throw new NginxContractException("state-conflict");
        if (transaction.BackupPending)
        {
            var (backupParent, backupName) = NginxSecureDirectory.Parent(transaction.Target + ".kelpiebakup");
            using (backupParent)
            {
                var backup = backupParent.Read(backupName, 65536);
                if (backup is not null && (Hash(backup) != transaction.OriginalHash || backupParent.Metadata(backupName).Inode != transaction.BackupInode))
                    throw new NginxContractException("backup-conflict");
                transaction.BackupCreated = backup is not null;
            }
            transaction.BackupPending = false;
        }
        var (parent, name) = NginxSecureDirectory.Parent(transaction.ResolvedPath);
        using (parent)
        {
            var current = parent.Read(name, 65536);
            if (transaction.FileApplied && (current is null || Hash(current) != transaction.NewHash
                || parent.Metadata(name).Inode != transaction.AppliedInode)) throw new NginxContractException("state-conflict");
        }
        foreach (var link in transaction.Links.Where(x => x.Removed))
        {
            var (directory, linkName) = NginxSecureDirectory.Parent(link.Path);
            using (directory)
                if (directory.LinkTarget(linkName) is not null) throw new NginxContractException("state-conflict");
        }
    }

    private static void Reconcile(NginxTransaction transaction, NginxPolicy policy, NginxSecureDirectory store)
    {
        if (transaction.BackupCleanupPending)
        {
            ValidateBackup(transaction, allowMissing: true);
            var (backupParent, backupName) = NginxSecureDirectory.Parent(transaction.Target + ".kelpiebakup");
            using (backupParent)
            {
                if (backupParent.Read(backupName, 65536) is null)
                {
                    transaction.BackupCreated = false;
                    transaction.BackupCleanupPending = false;
                    Save(store, transaction);
                }
            }
        }
        if (transaction.State is not ("applying" or "rolling-back" or "recovery-required")) return;
        if (ResolveTarget(policy, transaction.Target) != transaction.ResolvedPath) throw new NginxContractException("state-conflict");
        if (transaction.BackupPending) ValidateCurrent(transaction, policy);
        if (transaction.FilePending)
        {
            var (parent, name) = NginxSecureDirectory.Parent(transaction.ResolvedPath);
            using (parent)
            {
                var bytes = parent.Read(name, 65536);
                var hash = Hash(bytes ?? []);
                var inode = bytes is null ? 0UL : parent.Metadata(name).Inode;
                if (bytes is not null && hash == transaction.NewHash && inode == transaction.AppliedInode) transaction.FileApplied = true;
                else if ((bytes is not null) == transaction.OriginalExists && hash == transaction.OriginalHash
                    && (bytes is null || inode == transaction.OriginalInode || inode == transaction.RestoredInode)) transaction.FileApplied = false;
                else throw new NginxContractException("state-conflict");
            }
            transaction.FilePending = false;
        }
        foreach (var link in transaction.Links.Where(x => x.Pending))
        {
            var (directory, name) = NginxSecureDirectory.Parent(link.Path);
            using (directory)
            {
                var target = directory.LinkTarget(name);
                if (target is not null && target != link.Target) throw new NginxContractException("state-conflict");
                link.Removed = target is null;
                link.Pending = false;
            }
        }
        transaction.State = "recovery-required";
        Save(store, transaction);
    }

    private static NginxLink[] FindLinks(NginxPolicy policy)
    {
        if (string.IsNullOrEmpty(policy.SitesEnabledDirectory)) return [];
        NginxSecureDirectory.Normalize(policy.SitesEnabledDirectory);
        if (!Directory.Exists(policy.SitesEnabledDirectory)) return [];
        using var directory = NginxSecureDirectory.Open(policy.SitesEnabledDirectory);
        var paths = Directory.EnumerateFileSystemEntries(policy.SitesEnabledDirectory).Take(65).Order(StringComparer.Ordinal).ToArray();
        if (paths.Length > 64) throw new NginxContractException("too-many-links");
        var links = new List<NginxLink>();
        foreach (var path in paths)
        {
            string? target;
            try { target = directory.LinkTarget(Path.GetFileName(path)); }
            catch (NginxContractException ex) when (ex.Code == "not-symlink") { continue; }
            if (target is null) continue;
            var resolved = NginxSecureDirectory.Resolve(path);
            if (!policy.SiteTargetDirectories.Contains(Path.GetDirectoryName(resolved), StringComparer.Ordinal))
                throw new NginxContractException("site-target-denied");
            var (parent, name) = NginxSecureDirectory.Parent(resolved);
            using (parent)
            {
                var bytes = parent.Read(name, 65536) ?? throw new NginxContractException("site-target-unavailable");
                var text = Utf8.GetString(bytes);
                if (text.Contains('\0')) throw new NginxContractException("invalid-content");
                if (DefaultSitePattern().IsMatch(text)) links.Add(new NginxLink { Path = path, Target = target, ResolvedPath = resolved, Hash = Hash(bytes) });
            }
        }
        return links.ToArray();
    }

    private static void VerifyLink(NginxLink link, NginxSecureDirectory directory, string name)
    {
        if (directory.LinkTarget(name) != link.Target || NginxSecureDirectory.Resolve(link.Path) != link.ResolvedPath)
            throw new NginxContractException("link-conflict");
        var (parent, file) = NginxSecureDirectory.Parent(link.ResolvedPath);
        using (parent)
            if (Hash(parent.Read(file, 65536) ?? []) != link.Hash) throw new NginxContractException("link-conflict");
    }

    private static void Authorize(NginxPolicy policy, string target)
    {
        NginxSecureDirectory.Normalize(target);
        if (!policy.Targets.Contains(target, StringComparer.Ordinal) && !policy.Directories.Contains(Path.GetDirectoryName(target), StringComparer.Ordinal))
            throw new NginxContractException("target-denied");
    }

    private static string ResolveTarget(NginxPolicy policy, string target)
    {
        Authorize(policy, target);
        // Every parent is trusted and opened without following symlinks.
        using var parent = NginxSecureDirectory.Open(Path.GetDirectoryName(target)!);
        var link = false;
        try { link = parent.LinkTarget(Path.GetFileName(target)) is not null; }
        catch (NginxContractException ex) when (ex.Code == "not-symlink") { }
        var resolved = link ? NginxSecureDirectory.Resolve(target) : target;
        Authorize(policy, resolved);
        return resolved;
    }

    private static NginxTransaction Load(NginxRequest request, uint caller, NginxSecureDirectory store, string stateDirectory)
    {
        var id = request.TransactionId;
        if (id.Length == 0)
        {
            var candidates = new List<NginxTransaction>();
            foreach (var path in Directory.EnumerateFiles(stateDirectory, "*.json").Take(257))
            {
                var data = store.Read(Path.GetFileName(path), 1024 * 1024) ?? throw new NginxContractException("transaction-unavailable");
                var entry = JsonSerializer.Deserialize(data, NginxContractJsonContext.Default.NginxTransaction)
                    ?? throw new NginxContractException("invalid-state");
                if (entry.Target == request.Target && entry.CallerUid == caller
                    && entry.State is "applying" or "applied" or "recovery-required" or "rolling-back" or "committing") candidates.Add(entry);
            }
            if (candidates.Count != 1) throw new NginxContractException("transaction-unavailable-or-ambiguous");
            id = candidates[0].Id;
        }
        if (!Guid.TryParseExact(id, "N", out _)) throw new NginxContractException("invalid-transaction");
        var bytes = store.Read(id + ".json", 1024 * 1024) ?? throw new NginxContractException("transaction-unavailable");
        var transaction = JsonSerializer.Deserialize(bytes, NginxContractJsonContext.Default.NginxTransaction)
            ?? throw new NginxContractException("invalid-state");
        if (transaction.CallerUid != caller || transaction.Target != request.Target) throw new NginxContractException("transaction-denied");
        return transaction;
    }

    private static void Save(NginxSecureDirectory store, NginxTransaction transaction) =>
        store.Write(transaction.Id + ".json", JsonSerializer.SerializeToUtf8Bytes(transaction, NginxContractJsonContext.Default.NginxTransaction));

    private static NginxResponse Response(NginxTransaction transaction, string requestId) => new()
    {
        RequestId = requestId, TransactionId = transaction.Id, State = transaction.State,
        Ok = transaction.State is "planned" or "applied" or "no-op" or "committed" or "rolled-back",
        ConfigChanged = transaction.FileApplied, LinksChanged = transaction.Links.Count(x => x.Removed),
        BytesWritten = transaction.FileApplied ? transaction.NewBytes : 0, IntentDigest = transaction.IntentDigest,
        OriginalHash = transaction.OriginalHash, CurrentHash = transaction.FileApplied ? transaction.NewHash : transaction.OriginalHash,
        Recoverable = transaction.State is "applying" or "applied" or "recovery-required" or "rolling-back" or "committing",
        Error = transaction.Error,
    };

    private static byte[] ValidateContent(string content)
    {
        if (content.Length > 87384) throw new NginxContractException("size-exceeded");
        var bytes = Convert.FromBase64String(content);
        if (bytes.Length > 65536 || Utf8.GetString(bytes).Contains('\0')) throw new NginxContractException("invalid-content");
        return bytes;
    }

    private static byte[] ReadBounded(Stream stream, int limit)
    {
        using var output = new MemoryStream();
        var buffer = new byte[4096];
        int count;
        while ((count = stream.Read(buffer, 0, Math.Min(buffer.Length, limit + 1 - (int)output.Length))) > 0)
        {
            output.Write(buffer, 0, count);
            if (output.Length > limit) throw new NginxContractException("size-exceeded");
        }
        return output.ToArray();
    }

    private static void RejectDuplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new NginxContractException("duplicate-property");
                RejectDuplicates(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) RejectDuplicates(item);
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    [GeneratedRegex("^[0-9a-f]{64}$")] private static partial Regex HashPattern();
    [GeneratedRegex(@"^\s*listen\s+[^;]*(?<!\d)80(?!\d)[^;]*\bdefault_server\b[^;]*;", RegexOptions.Multiline | RegexOptions.IgnoreCase, 1000)]
    private static partial Regex DefaultSitePattern();
    [DllImport("libc")] private static extern uint geteuid();
}

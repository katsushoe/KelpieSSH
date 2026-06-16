using System.Collections.Concurrent;

namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Keeps SSH passwords in memory for the lifetime of the current process.
/// </summary>
public sealed class InMemorySshPasswordSessionStore : ISshPasswordSessionStore
{
    private readonly ConcurrentDictionary<string, string> _passwords = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SshSessionInfo> _sessions = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public void SetPassword(string secretName, string password)
    {
        SetPasswordSession(secretName, secretName, password);
    }

    /// <inheritdoc />
    public void SetPasswordSession(string profileName, string secretName, string password)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            throw new InvalidOperationException("SSH profile name is required.");
        }

        if (string.IsNullOrWhiteSpace(secretName))
        {
            throw new InvalidOperationException("SSH password secret name is required.");
        }

        if (string.IsNullOrEmpty(password))
        {
            throw new InvalidOperationException("SSH password is required.");
        }

        _passwords[secretName] = password;
        _sessions[secretName] = new SshSessionInfo(
            CreateHandle(),
            profileName,
            secretName,
            DateTimeOffset.UtcNow,
            "password");
    }

    /// <inheritdoc />
    public SshSessionInfo OpenInteractiveSession(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            throw new InvalidOperationException("SSH profile name is required.");
        }

        var handle = CreateHandle();
        var session = new SshSessionInfo(
            handle,
            profileName,
            handle,
            DateTimeOffset.UtcNow,
            "interactive");
        _sessions[handle] = session;
        return session;
    }

    /// <inheritdoc />
    public bool ClearPassword(string secretName)
    {
        if (string.IsNullOrWhiteSpace(secretName))
        {
            return false;
        }

        _sessions.TryRemove(secretName, out _);
        return _passwords.TryRemove(secretName, out _);
    }

    /// <inheritdoc />
    public bool ClearSession(string handle)
    {
        if (string.IsNullOrWhiteSpace(handle))
        {
            return false;
        }

        var session = _sessions.Values.FirstOrDefault(item =>
            string.Equals(item.Handle, handle, StringComparison.OrdinalIgnoreCase));
        if (session is null)
        {
            return false;
        }

        _sessions.TryRemove(session.SecretName, out _);
        _passwords.TryRemove(session.SecretName, out _);
        return true;
    }

    /// <inheritdoc />
    public ValueTask<string?> GetPasswordAsync(string secretName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(secretName))
        {
            return ValueTask.FromResult<string?>(null);
        }

        cancellationToken.ThrowIfCancellationRequested();
        _passwords.TryGetValue(secretName, out var password);
        return ValueTask.FromResult<string?>(password);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<SshSessionInfo> ListSessions()
    {
        return _sessions.Values
            .OrderBy(session => session.StartedAtUtc)
            .ToArray();
    }

    private static string CreateHandle()
    {
        return "ssh-" + Guid.NewGuid().ToString("N")[..12];
    }
}

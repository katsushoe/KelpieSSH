namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Refuses all SSH password lookups.
/// </summary>
public sealed class NullSshPasswordProvider : ISshPasswordProvider
{
    /// <inheritdoc />
    public ValueTask<string?> GetPasswordAsync(string secretName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<string?>(null);
    }
}

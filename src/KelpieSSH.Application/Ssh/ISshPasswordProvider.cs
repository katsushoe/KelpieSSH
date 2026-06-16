namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Resolves SSH passwords from a secret reference without storing plain text in profile files.
/// </summary>
public interface ISshPasswordProvider
{
    /// <summary>
    /// Gets a password for a secret name.
    /// </summary>
    /// <param name="secretName">The configured password secret name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The password, or <c>null</c> when it is not available.</returns>
    ValueTask<string?> GetPasswordAsync(string secretName, CancellationToken cancellationToken = default);
}

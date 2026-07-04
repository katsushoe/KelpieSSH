namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Verifies SSH host key fingerprints from profile settings.
/// </summary>
public static class SshHostKeyVerifier
{
    /// <summary>
    /// Determines whether the received host key fingerprint is trusted.
    /// </summary>
    /// <param name="expectedSha256">The expected SHA256 fingerprint, with or without the SHA256 prefix.</param>
    /// <param name="actualSha256">The received SHA256 fingerprint, without padding.</param>
    /// <returns><c>true</c> when no pin is configured or when the fingerprint matches.</returns>
    public static bool IsTrusted(string? expectedSha256, string? actualSha256)
    {
        if (string.IsNullOrWhiteSpace(expectedSha256))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(actualSha256))
        {
            return false;
        }

        return string.Equals(
            Normalize(expectedSha256),
            Normalize(actualSha256),
            StringComparison.Ordinal);
    }

    private static string Normalize(string value)
    {
        var normalized = value.Trim();
        if (normalized.StartsWith("SHA256:", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["SHA256:".Length..];
        }

        return normalized.TrimEnd('=');
    }
}

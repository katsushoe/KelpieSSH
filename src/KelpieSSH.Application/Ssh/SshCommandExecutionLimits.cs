namespace KelpieSSH.Application.Ssh;

/// <summary>Opt-in byte limits enforced during SSH reception, before string decoding.</summary>
public sealed record SshCommandExecutionLimits(int StandardOutputBytes, int StandardErrorBytes, int StandardInputBytes = 4096)
{
    /// <summary>Validates the bounded transport contract.</summary>
    public void Validate()
    {
        if (StandardOutputBytes is < 1 or > 1048576 || StandardErrorBytes is < 1 or > 65536
            || StandardInputBytes is < 0 or > 65536)
            throw new ArgumentOutOfRangeException(nameof(StandardOutputBytes), "SSH execution limits are outside the supported range.");
    }
}

/// <summary>Indicates that a bounded SSH stream exceeded its byte allowance.</summary>
public sealed class SshCommandOutputLimitException : IOException
{
    /// <summary>Creates a sanitized limit error without captured remote output.</summary>
    public SshCommandOutputLimitException() : base("SSH command output exceeded the configured byte limit.") { }
}

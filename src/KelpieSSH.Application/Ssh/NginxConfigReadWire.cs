using System.Globalization;
using System.Text;

namespace KelpieSSH.Application.Ssh;

/// <summary>Fixed, read-only standard-utility transport for the nginx configuration reader.</summary>
internal static class NginxConfigReadWire
{
    private const string Script = """
set -o pipefail
fail() { printf '%s\n' "$1" >&2; exit 2; }
for utility in base64 realpath dirname head; do command -v "$utility" >/dev/null 2>&1 || fail 'ERROR: required utility unavailable'; done
decode() { printf '%s' "$1" | base64 -d 2>/dev/null && printf '.'; }
requested=$(decode "$1") || fail 'ERROR: invalid path encoding'
requested=${requested%.}
allowed=$(decode "$2") || fail 'ERROR: invalid path encoding'
allowed=${allowed%.}
directories=$(decode "$3") || fail 'ERROR: invalid path encoding'
directories=${directories%.}
resolved=$(realpath -e -- "$requested" 2>/dev/null && printf '.') || fail 'ERROR: config path cannot be resolved'
resolved=${resolved%.}
resolved=${resolved%$'\n'}
parent=$(dirname -- "$requested" && printf '.') || fail 'ERROR: config parent cannot be resolved'
parent=${parent%.}
parent=${parent%$'\n'}
parent=$(realpath -m -- "$parent" 2>/dev/null && printf '.') || fail 'ERROR: config parent cannot be resolved'
parent=${parent%.}
parent=${parent%$'\n'}
resolved_parent=$(dirname -- "$resolved" && printf '.') || fail 'ERROR: config parent cannot be resolved'
resolved_parent=${resolved_parent%.}
resolved_parent=${resolved_parent%$'\n'}
ok=0
while IFS= read -r entry || [[ -n "$entry" ]]; do
    [[ -n "$entry" ]] || continue
    candidate=$(realpath -m -- "$entry" 2>/dev/null && printf '.') || fail 'ERROR: allowed path cannot be resolved'
    candidate=${candidate%.}
    candidate=${candidate%$'\n'}
    [[ "$resolved" == "$candidate" ]] && ok=1
done <<< "$allowed"
while IFS= read -r entry || [[ -n "$entry" ]]; do
    [[ -n "$entry" ]] || continue
    candidate=$(realpath -m -- "$entry" 2>/dev/null && printf '.') || fail 'ERROR: allowed path cannot be resolved'
    candidate=${candidate%.}
    candidate=${candidate%$'\n'}
    [[ "$parent" == "$candidate" && "$resolved_parent" == "$parent" ]] && ok=1
done <<< "$directories"
[[ "$ok" == 1 ]] || fail 'ERROR: path is not an allowed service config file'
[[ -f "$resolved" ]] || fail 'ERROR: config path is not a regular file'
if ! { exec 3< "$resolved"; } 2>/dev/null; then fail 'ERROR: config file read failed'; fi
[[ -f /proc/self/fd/3 ]] || fail 'ERROR: opened config is not a regular file'
opened=$(realpath -e -- /proc/self/fd/3 2>/dev/null && printf '.') || fail 'ERROR: opened config cannot be resolved'
opened=${opened%.}
opened=${opened%$'\n'}
[[ "$opened" == "$resolved" ]] || fail 'ERROR: config path changed while opening'
head -c "$(($4 + 1))" <&3 2>/dev/null | base64 -w 0 2>/dev/null || fail 'ERROR: config file read failed'
""";

    internal static string CommandTemplate()
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(Script));
        return "/usr/bin/env -i PATH=/usr/bin:/bin LC_ALL=C /bin/sh -c \"if test -x /usr/bin/base64 && test -x /bin/bash; then printf %s '"
            + encoded + "' | /usr/bin/base64 -d | /bin/bash -s -- {pathBase64} {allowedPathsBase64} {allowedDirsBase64} {maxBytes}; else printf '%s\\n' 'ERROR: required utility unavailable' >&2; exit 2; fi\"";
    }

    internal static SshCommandResult Decode(SshCommandResult result, int maxBytes)
    {
        if (result.TimedOut) return Failure(result, "ERROR: config file read timed out");
        if (result.ExitCode != 0) return result;
        try
        {
            if (result.StandardOutput.Length > ((maxBytes + 3) / 3 * 4)) throw new FormatException();
            var bytes = Convert.FromBase64String(result.StandardOutput);
            if (Convert.ToBase64String(bytes) != result.StandardOutput || bytes.Length > maxBytes + 1)
                throw new FormatException();
            if (Array.IndexOf(bytes, (byte)0) >= 0)
                return Failure(result, "ERROR: binary config file is not allowed");
            var truncated = bytes.Length > maxBytes;
            var text = new UTF8Encoding(false, true).GetString(bytes, 0, Math.Min(bytes.Length, maxBytes));
            var stderr = truncated ? result.StandardError + "\nKELPIE_TRUNCATED=1\n" : result.StandardError;
            return result with { StandardOutput = text, StandardError = stderr };
        }
        catch (Exception ex) when (ex is FormatException or DecoderFallbackException)
        {
            return Failure(result, "ERROR: invalid config read response");
        }
    }

    internal static async Task<SshCommandResult> ExecuteAsync(
        SshCommandService service, SshConnectionProfile profile, string commandName,
        IReadOnlyDictionary<string, string> arguments, KelpieExecutionChannel channel, CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.ExecuteAsync(profile, commandName, arguments, channel: channel, cancellationToken: cancellationToken);
            return Decode(result, int.Parse(arguments["maxBytes"], CultureInfo.InvariantCulture));
        }
        catch (SshCommandOutputLimitException)
        {
            return new SshCommandResult(commandName, string.Empty, 1, string.Empty, "ERROR: config read output limit exceeded", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, false);
        }
        catch (DecoderFallbackException)
        {
            return new SshCommandResult(commandName, string.Empty, 1, string.Empty, "ERROR: invalid config read response", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, false);
        }
    }

    private static SshCommandResult Failure(SshCommandResult result, string error) =>
        result with { ExitCode = 1, StandardOutput = string.Empty, StandardError = error };
}

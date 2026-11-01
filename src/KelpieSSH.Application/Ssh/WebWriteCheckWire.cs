using System.Text;
using System.Text.Json;
namespace KelpieSSH.Application.Ssh;
/// <summary>Adapts a bounded permission probe to the existing write-check response.</summary>
internal static class WebWriteCheckWire
{
    private const string Script = """
set -euo pipefail
PATH=/usr/bin:/bin; export PATH LC_ALL=C
fail() { printf '%s\n' 'ERROR: web file write check failed' >&2; exit 2; }
for utility in base64 realpath dirname; do command -v "$utility" >/dev/null || fail; done
decode() { printf '%s' "$1" | base64 -d 2>/dev/null && printf '.'; }
root=$(decode "$1"); root=${root%.}
relative=$(decode "$2"); relative=${relative%.}; relative=${relative//\\//}
[[ $3 == 0 || $3 == 1 ]] || fail
root=$(realpath -m -- "$root" 2>/dev/null && printf '.') || fail
root=${root%.}; root=${root%$'\n'}
target=$root; count=0; remaining=$relative
while :; do
    part=${remaining%%/*}
    [[ $part != '..' ]] || fail
    if [[ -n $part ]]; then target=$target/$part; count=$((count+1)); fi
    [[ $remaining == */* ]] || break
    remaining=${remaining#*/}
done
(( count > 0 )) || fail
parent=$(dirname -- "$target" && printf '.') || fail
parent=${parent%.}; parent=${parent%$'\n'}
parent=$(realpath -m -- "$parent" 2>/dev/null && printf '.') || fail
parent=${parent%.}; parent=${parent%$'\n'}
[[ $parent == "$root" || $root == / || $parent == "$root/"* ]] || fail
resolved=$(realpath -m -- "$target" 2>/dev/null && printf '.') || fail
resolved=${resolved%.}; resolved=${resolved%$'\n'}
exists=0; can=0; reason=NONE
if [[ -e $target || -L $target ]]; then
    exists=1
    [[ $resolved == "$root" || $root == / || $resolved == "$root/"* ]] || fail
    if [[ ! -f $target ]]; then reason=TYPE
    elif [[ -w $target ]]; then can=1
    else reason=FILE; fi
elif [[ -d $parent ]]; then
    if [[ -w $parent ]]; then can=1; else reason=PARENT; fi
else
    if [[ $3 == 1 ]]; then can=1; else reason=MISSING; fi
fi
printf 'KELPIE_WRITE_CHECK_1\n'
printf '%s' "$resolved" | base64 -w0
printf '\n%s\n%s\n%s\n' "$exists" "$can" "$reason"
""";
    internal static string CommandTemplate() => "sh -c \"printf %s '" + Convert.ToBase64String(Encoding.UTF8.GetBytes(Script))
        + "' | base64 -d | /bin/bash -s -- {siteRootBase64} {pathBase64} {createDirectories}\"";
    internal static SshCommandResult Decode(SshCommandResult result)
    {
        if (result.ExitCode != 0) return result;
        try
        {
            var fields = result.StandardOutput.Split('\n');
            if (result.StandardOutput.Length > 32768 || fields.Length != 6 || fields[0] != "KELPIE_WRITE_CHECK_1"
                || fields[5].Length != 0 || fields[2] is not ("0" or "1") || fields[3] is not ("0" or "1"))
                throw new FormatException("Invalid write-check envelope.");
            var bytes = Convert.FromBase64String(fields[1]);
            if (bytes.Length is 0 or > 12288 || bytes.Contains((byte)0)) throw new FormatException("Invalid resolved path.");
            var path = new UTF8Encoding(false, true).GetString(bytes);
            if (!path.StartsWith('/')) throw new FormatException("Invalid resolved path.");
            var reason = fields[4] switch
            {
                "NONE" => null,
                "TYPE" => "Target path is not a regular file.",
                "FILE" => "Target file is not writable by the SSH user.",
                "PARENT" => "Parent directory is not writable by the SSH user.",
                "MISSING" => "Parent directory does not exist and createDirectories is disabled.",
                _ => throw new FormatException("Invalid write-check reason."),
            };
            var exists = fields[2] == "1";
            var canWrite = fields[3] == "1";
            if (canWrite != (reason is null) || (fields[4] is "TYPE" or "FILE") && !exists
                || (fields[4] is "PARENT" or "MISSING") && exists) throw new FormatException("Inconsistent write-check result.");
            return result with { StandardOutput = JsonSerializer.Serialize(new { resolvedPath = path, exists, canWrite, reason }), StandardError = "" };
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            return result with { ExitCode = 1, StandardOutput = "", StandardError = "ERROR: invalid web file write check response" };
        }
    }
    internal static async Task<SshCommandResult> ExecuteAsync(SshCommandService service, SshConnectionProfile profile,
        string command, IReadOnlyDictionary<string, string> arguments, CancellationToken cancellationToken)
    {
        try { return Decode(await service.ExecuteAsync(profile, command, arguments, channel: KelpieExecutionChannel.Mcp, cancellationToken: cancellationToken)); }
        catch (SshCommandOutputLimitException)
        {
            return new SshCommandResult(command, "", 1, "", "ERROR: web file write check output limit exceeded", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, false);
        }
    }
}

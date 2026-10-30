using System.Globalization;
using System.Text;
using System.Text.Json;

namespace KelpieSSH.Application.Ssh;

/// <summary>Fixed byte reads followed by Python-compatible byte line selection on the host.</summary>
internal static class WebFileSliceWire
{
    private const string Script = """
set -euo pipefail
fail() { printf '%s\n' "$1" >&2; exit 2; }
[[ $3 == head || $3 == tail ]] || fail 'invalid slice mode'
(( $4 >= 1 && $4 <= 1048576 && $5 >= 0 && $5 <= 1000 )) || fail 'invalid slice bounds'
for utility in base64 realpath stat; do command -v "$utility" >/dev/null || fail 'required utility unavailable'; done
if [[ $3 == head ]]; then command -v head >/dev/null || fail 'required utility unavailable'; else command -v dd >/dev/null || fail 'required utility unavailable'; fi
decode() { printf '%s' "$1" | base64 -d 2>/dev/null && printf '.'; }
root=$(decode "$1"); root=${root%.}
relative=$(decode "$2"); relative=${relative%.}; relative=${relative//\\//}
root=$(realpath -m -- "$root" 2>/dev/null && printf '.'); root=${root%.}; root=${root%$'\n'}
target=$root
remaining=$relative
has_part=0
while :; do
    part=${remaining%%/*}
    [[ $part != '..' ]] || fail 'invalid web public path'
    if [[ -n $part ]]; then target=$target/$part; has_part=1; fi
    [[ $remaining == */* ]] || break
    remaining=${remaining#*/}
done
[[ $has_part == 1 ]] || fail 'invalid web public path'
resolved=$(realpath -m -- "$target" 2>/dev/null && printf '.'); resolved=${resolved%.}; resolved=${resolved%$'\n'}
[[ $resolved == "$root" || $root == '/' || $resolved == "$root/"* ]] || fail 'resolved path outside root'
printf '%s\n' 'KELPIE_READ_1'
printf '%s' "$resolved" | base64 -w0
printf '\n'
if [[ ! -e $resolved ]]; then printf '0\n'; exit 0; fi
[[ -f $resolved ]] || fail 'web public path is not a regular file'
size=$(stat -L --printf='%s' -- "$resolved" 2>/dev/null) || fail 'metadata read failed'
[[ $size =~ ^[0-9]+$ ]] || fail 'invalid metadata'
if ! { exec 3< "$resolved"; } 2>/dev/null; then fail 'web public file read failed'; fi
[[ -f /proc/self/fd/3 ]] || fail 'opened path is not a regular file'
opened=$(realpath -e -- /proc/self/fd/3 2>/dev/null && printf '.') || fail 'opened path cannot be resolved'
opened=${opened%.}; opened=${opened%$'\n'}
[[ $opened == "$resolved" ]] || fail 'path changed while opening'
if [[ $3 == head ]]; then
    content=$(head -c "$4" <&3 2>/dev/null | base64 -w0) || fail 'web public file read failed'
else
    offset=$((size > $4 ? size - $4 : 0))
    content=$(dd iflag=skip_bytes,count_bytes skip="$offset" count="$4" bs=65536 status=none <&3 2>/dev/null | base64 -w0) || fail 'web public file read failed'
fi
metadata=$(stat -L --printf='%Y\0%y\0' -- /proc/self/fd/3 2>/dev/null | base64 -w0) || fail 'metadata read failed'
printf '1\n%s\n%s\n' "$metadata" "$content"
""";

    internal static string CommandTemplate()
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(Script));
        return "/usr/bin/env -i PATH=/usr/bin:/bin LC_ALL=C /bin/sh -c \"if test -x /usr/bin/base64 && test -x /bin/bash; then printf %s '"
            + encoded + "' | /usr/bin/base64 -d | /bin/bash -s -- {siteRootBase64} {pathBase64} {mode} {maxBytes} {maxLines}; else printf '%s\\n' 'ERROR: required utility unavailable' >&2; exit 2; fi\"";
    }

    internal static SshCommandResult Decode(SshCommandResult result, string mode, int maxBytes, int maxLines)
    {
        if (mode is not ("head" or "tail") || maxBytes is < 1 or > 1048576 || maxLines is < 0 or > 1000)
            return Failure(result, "ERROR: invalid slice bounds");
        result = WebFileReadWire.Decode(result, maxBytes);
        if (result.ExitCode != 0) return result;
        using var doc = JsonDocument.Parse(result.StandardOutput);
        var remote = doc.RootElement;
        if (!remote.GetProperty("exists").GetBoolean()) return result;
        var bytes = Convert.FromBase64String(remote.GetProperty("contentBase64").GetString()!);
        var selected = LimitLines(bytes, mode, maxLines);
        return result with { StandardOutput = JsonSerializer.Serialize(new {
            resolvedPath = remote.GetProperty("resolvedPath").GetString(), exists = true,
            contentBase64 = Convert.ToBase64String(selected), size = selected.Length,
            lastModified = remote.GetProperty("lastModified").GetString() }) };
    }

    internal static byte[] LimitLines(byte[] data, string mode, int maxLines)
    {
        if (maxLines == 0 || data.Length == 0) return data;
        var starts = new Queue<int>();
        starts.Enqueue(0);
        var lines = 0;
        for (var i = 0; i < data.Length; i++)
        {
            if (data[i] is not (10 or 13)) continue;
            if (data[i] == 13 && i + 1 < data.Length && data[i + 1] == 10) i++;
            var end = i + 1;
            if (mode == "head" && ++lines == maxLines) return data[..end];
            if (mode == "tail" && end < data.Length)
            {
                starts.Enqueue(end);
                if (starts.Count > maxLines) starts.Dequeue();
            }
        }
        return mode == "tail" ? data[starts.Peek()..] : data;
    }

    internal static async Task<SshCommandResult> ExecuteAsync(
        SshCommandService service, SshConnectionProfile profile, string commandName,
        IReadOnlyDictionary<string, string> arguments, CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.ExecuteAsync(profile, commandName, arguments,
                channel: KelpieExecutionChannel.Mcp, cancellationToken: cancellationToken);
            return Decode(result, arguments["mode"], int.Parse(arguments["maxBytes"], CultureInfo.InvariantCulture),
                int.Parse(arguments["maxLines"], CultureInfo.InvariantCulture));
        }
        catch (SshCommandOutputLimitException)
        {
            return new SshCommandResult(commandName, "", 1, "", "ERROR: web slice output limit exceeded", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, false);
        }
        catch (DecoderFallbackException)
        {
            return new SshCommandResult(commandName, "", 1, "", "ERROR: invalid web slice response", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, false);
        }
    }

    private static SshCommandResult Failure(SshCommandResult result, string error) =>
        result with { ExitCode = 1, StandardOutput = "", StandardError = error };
}

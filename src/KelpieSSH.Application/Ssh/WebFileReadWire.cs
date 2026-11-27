using System.Text;
using System.Text.Json;

namespace KelpieSSH.Application.Ssh;

/// <summary>Fixed system-utility transport for complete web file reads.</summary>
internal static class WebFileReadWire
{
    private const string Script = """
set -euo pipefail
fail() { printf '%s\n' "$1" >&2; exit 2; }
for utility in base64 realpath stat head; do command -v "$utility" >/dev/null || fail 'required utility unavailable'; done
decode() { printf '%s' "$1" | base64 -d 2>/dev/null && printf '.'; }
root=$(decode "$1"); root=${root%.}
relative=$(decode "$2"); relative=${relative%.}; relative=${relative//\\//}
root=$(realpath -m -- "$root" 2>/dev/null && printf '.'); root=${root%.}; root=${root%$'\n'}
target=$root
remaining=$relative
while :; do
    part=${remaining%%/*}
    [[ $part != '..' ]] || fail 'invalid web public path'
    [[ -z $part ]] || target=$target/$part
    [[ $remaining == */* ]] || break
    remaining=${remaining#*/}
done
resolved=$(realpath -m -- "$target" 2>/dev/null && printf '.'); resolved=${resolved%.}; resolved=${resolved%$'\n'}
[[ $resolved == "$root" || $root == '/' || $resolved == "$root/"* ]] || fail 'resolved path outside root'
printf '%s\n' 'KELPIE_READ_1'
printf '%s' "$resolved" | base64 -w0
printf '\n'
if [[ ! -e $resolved ]]; then printf '0\n'; exit 0; fi
[[ -f $resolved ]] || fail 'web public path is not a regular file'
size=$(stat -L --printf='%s' -- "$resolved" 2>/dev/null) || fail 'metadata read failed'
[[ $size =~ ^[0-9]+$ ]] || fail 'invalid metadata'
(( size <= $3 )) || fail 'web public file exceeds maximum read size'
if ! { exec 3< "$resolved"; } 2>/dev/null; then fail 'web public file read failed'; fi
[[ -f /proc/self/fd/3 ]] || fail 'opened path is not a regular file'
opened=$(realpath -e -- /proc/self/fd/3 2>/dev/null && printf '.') || fail 'opened path cannot be resolved'
opened=${opened%.}; opened=${opened%$'\n'}
[[ $opened == "$resolved" ]] || fail 'path changed while opening'
content=$(head -c "$(($3 + 1))" <&3 2>/dev/null | base64 -w0) || fail 'web public file read failed'
metadata=$(stat -L --printf='%Y\0%y\0' -- /proc/self/fd/3 2>/dev/null | base64 -w0) || fail 'metadata read failed'
printf '1\n%s\n%s\n' "$metadata" "$content"
""";

    internal static string CommandTemplate()
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(Script));
        return "/usr/bin/env -i PATH=/usr/bin:/bin LC_ALL=C /bin/sh -c \"if test -x /usr/bin/base64 && test -x /bin/bash; then printf %s '"
            + encoded + "' | /usr/bin/base64 -d | /bin/bash -s -- {siteRootBase64} {pathBase64} {maxBytes}; else printf '%s\\n' 'ERROR: required utility unavailable' >&2; exit 2; fi\"";
    }

    internal static SshCommandResult Decode(SshCommandResult result, int maxBytes)
    {
        if (result.TimedOut) return Failure(result, "ERROR: web read timed out");
        if (result.ExitCode != 0) return result;
        try
        {
            if (maxBytes < 1 || result.StandardOutput.Length > ((long)maxBytes + 3) / 3 * 4 + 32768) throw new FormatException();
            var lines = result.StandardOutput.Split('\n');
            if (lines.Length < 4 || lines[0] != "KELPIE_READ_1") throw new FormatException();
            var path = Text(lines[1]);
            if (path.Length == 0 || path[0] != '/' || path.Contains('\0')) throw new FormatException();
            object response;
            if (lines[2] == "0" && lines.Length == 4 && lines[3] == "")
                response = new { resolvedPath = path, exists = false };
            else
            {
                if (lines.Length != 6 || lines[2] != "1" || lines[5] != "") throw new FormatException();
                var metadata = Text(lines[3]).Split('\0');
                if (metadata.Length != 3 || metadata[2] != "") throw new FormatException();
                var time = WebFileStatWire.DecodeTimestamp(metadata[0], metadata[1]);
                var bytes = Bytes(lines[4]);
                if (bytes.Length > maxBytes) return Failure(result, "ERROR: web public file exceeds maximum read size");
                response = new { resolvedPath = path, exists = true, contentBase64 = lines[4], size = bytes.Length, lastModified = time };
            }
            return result with { StandardOutput = JsonSerializer.Serialize(response) };
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentOutOfRangeException or DecoderFallbackException)
        {
            return Failure(result, "ERROR: invalid web read response");
        }
    }

    private static string Text(string value)
    {
        if (value.Length > 16384) throw new FormatException();
        return new UTF8Encoding(false, true).GetString(Bytes(value));
    }

    private static byte[] Bytes(string value)
    {
        var bytes = Convert.FromBase64String(value);
        if (Convert.ToBase64String(bytes) != value) throw new FormatException();
        return bytes;
    }

    private static SshCommandResult Failure(SshCommandResult result, string error) =>
        result with { ExitCode = 1, StandardOutput = "", StandardError = error };
}

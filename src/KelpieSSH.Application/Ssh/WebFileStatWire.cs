using System.Globalization;
using System.Text;
using System.Text.Json;

namespace KelpieSSH.Application.Ssh;

/// <summary>Decodes bounded metadata from the fixed web stat command.</summary>
internal static class WebFileStatWire
{
    private const string Script = """
#!/bin/bash
set -euo pipefail
export LC_ALL=C
fail() { printf '%s\n' "$1" >&2; exit 2; }
for utility in base64 realpath stat getent; do command -v "$utility" >/dev/null || fail 'required utility unavailable'; done
decode() { printf '%s' "$1" | base64 -d 2>/dev/null && printf '.'; }
root=$(decode "$1"); root=${root%.}
relative=$(decode "$2"); relative=${relative%.}
relative=${relative//\\//}
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
printf '%s\n' 'KELPIE_STAT_1'
printf '%s' "$resolved" | base64 -w0
printf '\n'
if [[ ! -e $target && ! -L $target ]]; then printf '0\n'; exit 0; fi
printf '1\n'
metadata=$(stat --printf='%f\0%s\0%u\0%g\0%U\0%G\0%Y\0%y\0' -- "$target" 2>/dev/null | base64 -w0) || fail 'metadata read failed'
mapfile -d '' -t fields < <(printf '%s' "$metadata" | base64 -d)
[[ ${#fields[@]} == 8 ]] || fail 'invalid metadata'
if [[ ${fields[4]} == UNKNOWN ]]; then
    if entry=$(getent passwd "${fields[2]}"); then fields[4]=${entry%%:*}; else code=$?; [[ $code == 2 ]] || fail 'owner lookup failed'; fields[4]=${fields[2]}; fi
fi
if [[ ${fields[5]} == UNKNOWN ]]; then
    if entry=$(getent group "${fields[3]}"); then fields[5]=${entry%%:*}; else code=$?; [[ $code == 2 ]] || fail 'group lookup failed'; fields[5]=${fields[3]}; fi
fi
printf '%s\0' "${fields[@]}" | base64 -w0
printf '\n'
""";

    internal static string CommandTemplate()
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(Script));
        return "/usr/bin/env -i PATH=/usr/bin:/bin LC_ALL=C /bin/sh -c \"if test -x /usr/bin/base64 && test -x /bin/bash; then printf %s '"
            + encoded + "' | /usr/bin/base64 -d | /bin/bash -s -- {siteRootBase64} {pathBase64}; else printf '%s\\n' 'ERROR: required utility unavailable' >&2; exit 2; fi\"";
    }

    internal static SshCommandResult Decode(SshCommandResult result)
    {
        if (result.TimedOut)
            return result with { ExitCode = 1, StandardOutput = "", StandardError = "ERROR: web stat timed out" };
        if (result.ExitCode != 0) return result;
        try
        {
            var lines = result.StandardOutput.Split('\n');
            if (lines.Length < 4 || lines[0] != "KELPIE_STAT_1") throw new FormatException();
            var path = Text(lines[1]);
            if (path.Length == 0 || path[0] != '/' || path.Contains('\0')) throw new FormatException();
            object response;
            if (lines[2] == "0" && lines.Length == 4 && lines[3] == "")
                response = new { resolvedPath = path, exists = false };
            else
            {
                if (lines.Length != 5 || lines[2] != "1" || lines[4] != "") throw new FormatException();
                var values = Text(lines[3]).Split('\0');
                if (values.Length != 9 || values[8] != "") throw new FormatException();
                var mode = Convert.ToInt32(values[0], 16);
                var size = long.Parse(values[1], NumberStyles.None, CultureInfo.InvariantCulture);
                var uid = ulong.Parse(values[2], NumberStyles.None, CultureInfo.InvariantCulture);
                var gid = ulong.Parse(values[3], NumberStyles.None, CultureInfo.InvariantCulture);
                if (mode is < 0 or > 65535 || uid > uint.MaxValue || gid > uint.MaxValue || string.IsNullOrEmpty(values[4]) || string.IsNullOrEmpty(values[5])) throw new FormatException();
                var time = DecodeTimestamp(values[6], values[7]);
                var type = (mode & 0xf000) switch { 0xa000 => "symlink", 0x4000 => "directory", 0x8000 => "file", _ => "other" };
                response = new { resolvedPath = path, exists = true, type, size,
                    mode = Convert.ToString(mode & 0xfff, 8).PadLeft(3, '0'),
                    owner = values[4],
                    group = values[5],
                    lastModified = time, isSymlink = type == "symlink" };
            }
            return result with { StandardOutput = JsonSerializer.Serialize(response) };
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentOutOfRangeException or DecoderFallbackException)
        {
            return result with { ExitCode = 1, StandardOutput = "", StandardError = "ERROR: invalid web stat response" };
        }
    }

    internal static string DecodeTimestamp(string secondsValue, string dateValue)
    {
        var seconds = long.Parse(secondsValue, CultureInfo.InvariantCulture);
        var fractionStart = dateValue.IndexOf('.');
        if (fractionStart < 0 || dateValue.Length < fractionStart + 10) throw new FormatException();
        var nanos = int.Parse(dateValue.Substring(fractionStart + 1, 9), NumberStyles.None, CultureInfo.InvariantCulture);
        var timestamp = seconds + nanos / 1e9;
        var whole = Math.Floor(timestamp);
        var micros = (long)Math.Round((timestamp - whole) * 1e6, MidpointRounding.ToEven);
        var modified = DateTimeOffset.FromUnixTimeSeconds((long)whole).AddTicks(micros * 10);
        return modified.ToString(modified.Ticks % TimeSpan.TicksPerSecond == 0 ? "yyyy-MM-dd'T'HH:mm:ss'Z'" : "yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture);
    }

    private static string Text(string value)
    {
        if (value.Length > 32768) throw new FormatException();
        var bytes = Convert.FromBase64String(value);
        if (Convert.ToBase64String(bytes) != value) throw new FormatException();
        return new UTF8Encoding(false, true).GetString(bytes);
    }
}

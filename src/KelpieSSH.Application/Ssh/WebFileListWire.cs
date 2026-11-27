using System.Globalization;
using System.Text;
using System.Text.Json;

namespace KelpieSSH.Application.Ssh;

/// <summary>Fixed system-utility transport for bounded web directory listings.</summary>
internal static class WebFileListWire
{
    private const string Script = """
set -euo pipefail
fail() { printf '%s\n' "$1" >&2; exit 2; }
for utility in base64 realpath stat getent find sort; do command -v "$utility" >/dev/null || fail 'required utility unavailable'; done
decode() { printf '%s' "$1" | base64 -d 2>/dev/null && printf '.'; }
canonical() { realpath -m -- "$1" 2>/dev/null && printf '.'; }
inside() { [[ $1 == "$root" || $root == '/' || $1 == "$root/"* ]]; }
root=$(decode "$1"); root=${root%.}
relative=$(decode "$2"); relative=${relative%.}; relative=${relative//\\//}
root=$(canonical "$root"); root=${root%.}; root=${root%$'\n'}
target=$root
remaining=$relative
while :; do
    part=${remaining%%/*}
    [[ $part != '..' ]] || fail 'invalid web public directory path'
    [[ -z $part ]] || target=$target/$part
    [[ $remaining == */* ]] || break
    remaining=${remaining#*/}
done
resolved=$(canonical "$target"); resolved=${resolved%.}; resolved=${resolved%$'\n'}
inside "$resolved" || fail 'resolved path outside root'
[[ $3 =~ ^[0-5]$ && $4 =~ ^[0-9]+$ ]] || fail 'invalid listing bounds'
max_depth=$3; limit=$4
(( limit >= 1 && limit <= 500 )) || fail 'invalid listing bounds'
printf 'KELPIE_LIST_1\n'
printf '%s' "$resolved" | base64 -w0; printf '\n'
if [[ ! -e $resolved ]]; then printf '0\n'; exit 0; fi
[[ -d $resolved ]] || fail 'web public path is not a directory'
printf '1\n'
count=0; truncated=0
declare -A owner_names=() group_names=()
emit() {
    local path=$1 depth=$2 rp name rel code entry
    local -a fields=("${@:3}")
    if (( count >= limit )); then truncated=1; return 0; fi
    rp=$path
    if [[ -L $path ]]; then
        rp=$(canonical "$path") || fail 'path resolution failed'
        rp=${rp%.}; rp=${rp%$'\n'}
    fi
    inside "$rp" || return 0
    [[ ${#fields[@]} == 8 ]] || fail 'invalid metadata'
    if [[ ! ${owner_names[${fields[2]}]+present} ]]; then
        if entry=$(getent passwd "${fields[2]}"); then owner_names[${fields[2]}]=${entry%%:*}; else code=$?; [[ $code == 2 ]] || fail 'owner lookup failed'; owner_names[${fields[2]}]=${fields[2]}; fi
    fi
    if [[ ! ${group_names[${fields[3]}]+present} ]]; then
        if entry=$(getent group "${fields[3]}"); then group_names[${fields[3]}]=${entry%%:*}; else code=$?; [[ $code == 2 ]] || fail 'group lookup failed'; group_names[${fields[3]}]=${fields[3]}; fi
    fi
    fields[4]=${owner_names[${fields[2]}]}; fields[5]=${group_names[${fields[3]}]}
    name=${path##*/}
    if [[ $root == '/' ]]; then rel=$path; else rel=${path#"$root"}; fi
    printf '%s\0' "$name" "$rel" "$rp" "$depth" "${fields[@]}" >&3
    count=$((count+1))
}
walk() {
    local current=$1 depth=$2 pid path rp start take index offset
    local -a paths sorted metadata dirs=() candidates=()
    rp=$(canonical "$current") || fail 'path resolution failed'
    rp=${rp%.}; rp=${rp%$'\n'}
    inside "$rp" || return 0
    mapfile -d '' -t paths < <(find -P "$current" -mindepth 1 -maxdepth 1 -print0 2>/dev/null)
    pid=$!
    if ! wait "$pid"; then
        (( max_depth != 0 )) || fail 'directory read failed'
        return 0
    fi
    for path in "${paths[@]}"; do
        if (( max_depth > 0 && depth >= max_depth )) && [[ -d $path ]]; then continue; fi
        candidates+=("$path")
    done
    if (( ${#candidates[@]} != 0 )); then
        mapfile -d '' -t sorted < <(printf '%s\0' "${candidates[@]}" | sort -z -T /dev/null)
        pid=$!
        wait "$pid" || fail 'directory sort failed'
        for ((start=0; start<${#sorted[@]}; start+=take)); do
            if (( count >= limit )); then truncated=1; return 0; fi
            take=$((${#sorted[@]}-start)); (( take <= 128 )) || take=128
            (( take <= limit-count )) || take=$((limit-count))
            mapfile -d '' -t metadata < <(stat --printf='%f\0%s\0%u\0%g\0%u\0%g\0%Y\0%y\0' -- "${sorted[@]:start:take}" 2>/dev/null)
            pid=$!
            wait "$pid" || fail 'metadata read failed'
            (( ${#metadata[@]} == take*8 )) || fail 'invalid metadata'
            for ((index=0; index<take; index++)); do
                offset=$((index*8))
                emit "${sorted[start+index]}" "$depth" "${metadata[@]:offset:8}"
                (( truncated == 0 )) || return 0
            done
        done
    fi
    (( depth < max_depth )) || return 0
    for path in "${paths[@]}"; do
        [[ ! -L $path && -d $path ]] && dirs+=("$path")
    done
    for path in "${dirs[@]}"; do
        walk "$path" "$((depth+1))"
        (( truncated == 0 )) || return 0
    done
}
exec 3> >(base64 -w0)
encoder_pid=$!
walk "$resolved" 0
exec 3>&-
wait "$encoder_pid" || fail 'metadata encoding failed'
printf '\nEND:%s\n' "$truncated"
""";

    internal static string CommandTemplate()
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(Script));
        return "/usr/bin/env -i PATH=/usr/bin:/bin LC_ALL=C /bin/sh -c \"if test -x /usr/bin/base64 && test -x /bin/bash; then printf %s '"
            + encoded + "' | /usr/bin/base64 -d | /bin/bash -s -- {siteRootBase64} {pathBase64} {maxDepth} {limit}; else printf '%s\\n' 'ERROR: required utility unavailable' >&2; exit 2; fi\"";
    }

    internal static SshCommandResult Decode(SshCommandResult result, int maxDepth, int limit)
    {
        if (result.TimedOut) return Failure(result, "ERROR: web list timed out");
        if (result.ExitCode != 0) return result;
        try
        {
            var lines = result.StandardOutput.Split('\n');
            if (lines.Length < 4 || lines[0] != "KELPIE_LIST_1" || lines[^1] != "") throw new FormatException();
            var path = Text(lines[1]);
            RequirePath(path);
            var entries = new List<WebPublicFileListEntry>();
            var exists = lines[2] == "1";
            var truncated = false;
            if (lines[2] == "0")
            {
                if (lines.Length != 4) throw new FormatException();
            }
            else
            {
                if (!exists || lines.Length != 6 || lines[^2] is not ("END:0" or "END:1")) throw new FormatException();
                truncated = lines[^2] == "END:1";
                var fields = Text(lines[3]).Split('\0');
                if (fields.Length % 12 != 1 || fields[^1] != "" || fields.Length / 12 > limit) throw new FormatException();
                for (var offset = 0; offset < fields.Length - 1; offset += 12)
                    entries.Add(Entry(fields[offset..(offset + 12)], maxDepth));
                if (truncated && entries.Count != limit) throw new FormatException();
            }
            return result with { StandardOutput = JsonSerializer.Serialize(new { resolvedPath = path, exists, entries, truncated },
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }) };
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentOutOfRangeException or DecoderFallbackException)
        {
            return Failure(result, "ERROR: invalid web list response");
        }
    }

    private static WebPublicFileListEntry Entry(string[] fields, int maxDepth)
    {
        RequirePath(fields[1]); RequirePath(fields[2]);
        var depth = int.Parse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture);
        var mode = Convert.ToInt32(fields[4], 16);
        var size = long.Parse(fields[5], NumberStyles.None, CultureInfo.InvariantCulture);
        var uid = ulong.Parse(fields[6], NumberStyles.None, CultureInfo.InvariantCulture);
        var gid = ulong.Parse(fields[7], NumberStyles.None, CultureInfo.InvariantCulture);
        if (depth < 0 || depth > maxDepth || mode is < 0 or > 65535 || uid > uint.MaxValue || gid > uint.MaxValue
            || string.IsNullOrEmpty(fields[0]) || fields[0].Contains('/') || string.IsNullOrEmpty(fields[8]) || string.IsNullOrEmpty(fields[9])) throw new FormatException();
        var type = (mode & 0xf000) switch { 0xa000 => "symlink", 0x4000 => "directory", 0x8000 => "file", _ => "other" };
        return new(fields[0], fields[1], fields[2], type, size, Convert.ToString(mode & 0xfff, 8).PadLeft(3, '0'),
            fields[8], fields[9], WebFileStatWire.DecodeTimestamp(fields[10], fields[11]), depth, type == "symlink");
    }

    private static string Text(string value)
    {
        if (value.Length > 2097152) throw new FormatException();
        var bytes = Convert.FromBase64String(value);
        if (Convert.ToBase64String(bytes) != value) throw new FormatException();
        return new UTF8Encoding(false, true).GetString(bytes);
    }

    private static void RequirePath(string path)
    {
        if (path.Length == 0 || path[0] != '/' || path.Contains('\0')) throw new FormatException();
    }

    internal static async Task<SshCommandResult> ExecuteAsync(SshCommandService service, SshConnectionProfile profile,
        string commandName, IReadOnlyDictionary<string, string> arguments, CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.ExecuteAsync(profile, commandName, arguments,
                channel: KelpieExecutionChannel.Mcp, cancellationToken: cancellationToken);
            return Decode(result, int.Parse(arguments["maxDepth"], CultureInfo.InvariantCulture), int.Parse(arguments["limit"], CultureInfo.InvariantCulture));
        }
        catch (SshCommandOutputLimitException)
        {
            return new(commandName, "", 1, "", "ERROR: web list output limit exceeded", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, false);
        }
        catch (DecoderFallbackException)
        {
            return new(commandName, "", 1, "", "ERROR: invalid web list response", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, false);
        }
    }

    private static SshCommandResult Failure(SshCommandResult result, string error) =>
        result with { ExitCode = 1, StandardOutput = "", StandardError = error };
}

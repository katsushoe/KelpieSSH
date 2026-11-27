using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace KelpieSSH.Application.Ssh;

/// <summary>Reads bounded log bytes and applies the legacy row/time filters using remote evidence.</summary>
internal static partial class NginxLogWire
{
    private const string Script = """
set -euo pipefail
PATH=/usr/bin:/bin; export PATH LC_ALL=C
fail() { printf '%s\n' 'ERROR: nginx log read failed' >&2; exit 2; }
for utility in base64 realpath stat dd date head; do command -v "$utility" >/dev/null || fail; done
decode() { printf '%s' "$1" | base64 -d 2>/dev/null && printf '.'; }
(( $3 >= 1 && $3 <= 262144 && $4 >= 1 && $4 <= 5000 && $5 >= 0 && $5 <= 1440 )) || fail
p=$(decode "$1"); p=${p%.}
allowed=$(decode "$2"); allowed=${allowed%.}
resolved=$(realpath -m -- "$p" 2>/dev/null && printf '.') || fail
resolved=${resolved%.}; resolved=${resolved%$'\n'}
matched=0
while IFS= read -r candidate || [[ -n $candidate ]]; do
    [[ -n $candidate ]] || continue
    canonical=$(realpath -m -- "$candidate" 2>/dev/null && printf '.') || fail
    canonical=${canonical%.}; canonical=${canonical%$'\n'}
    [[ $canonical != "$resolved" ]] || matched=1
done <<< "$allowed"
[[ $matched == 1 && -f $resolved ]] || fail
size=$(stat -L --printf='%s' -- "$resolved" 2>/dev/null) || fail
[[ $size =~ ^[0-9]+$ && ${#size} -le 18 ]] || fail
if ! { exec 3< "$resolved"; } 2>/dev/null; then fail; fi
[[ -f /proc/self/fd/3 ]] || fail
offset=$((size > $3 + 1 ? size - $3 - 1 : 0))
content=$(dd iflag=skip_bytes,count_bytes skip="$offset" count="$(($3+1))" bs=65536 status=none <&3 2>/dev/null | base64 -w0) || fail
now=$(date -u +%s.%6N) || fail
[[ $now =~ ^-?[0-9]+\.[0-9]{6}$ ]] || fail
kind=POSIX; timezone=$(printf UTC0 | base64 -w0)
if (( $5 > 0 )); then
    if [[ ${TZ+x} ]]; then
        zone=${TZ#:}
        if [[ -z $zone ]]; then zone=UTC0; fi
        if [[ $zone == /* ]]; then
            [[ $zone == /usr/share/zoneinfo/* || $zone == /etc/localtime ]] || fail
            file=$zone
        elif [[ $zone != *..* && -f /usr/share/zoneinfo/$zone ]]; then
            file=/usr/share/zoneinfo/$zone
        else
            [[ ${#zone} -le 1024 ]] || fail
            timezone=$(printf '%s' "$zone" | base64 -w0); file=
        fi
    else file=/etc/localtime; fi
    if [[ -n $file ]]; then
        if [[ $file != /etc/localtime ]]; then
            zone_root=$(realpath -e -- /usr/share/zoneinfo 2>/dev/null) || fail
            zone_real=$(realpath -e -- "$file" 2>/dev/null) || fail
            [[ $zone_real == "$zone_root/"* || $zone_real == /etc/localtime ]] || fail
            file=$zone_real
        fi
        [[ -f $file ]] || fail
        count=$(stat -L --printf='%s' -- "$file") || fail
        (( count > 0 && count <= 131072 )) || fail
        timezone=$(head -c 131073 -- "$file" | base64 -w0) || fail
        kind=TZIF
    fi
fi
printf 'KELPIE_LOG_1\n%s\n%s\n%s\n%s\n%s\n' "$size" "$now" "$kind" "$timezone" "$content"
""";

    internal static string CommandTemplate()
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(Script));
        return "sh -c \"printf %s '" + encoded
            + "' | base64 -d | /bin/bash -s -- {pathBase64} {allowedPathsBase64} {maxBytes} {lines} {sinceMinutes}\"";
    }

    internal static SshCommandResult Decode(SshCommandResult result, int maxBytes, int lines, int sinceMinutes)
    {
        if (result.ExitCode != 0) return result;
        try
        {
            if (maxBytes is < 1 or > 262144 || lines is < 1 or > 5000 || sinceMinutes is < 0 or > 1440 || result.StandardOutput.Length > 600000)
                throw new FormatException("Invalid log bounds.");
            var fields = result.StandardOutput.Split('\n');
            if (fields.Length != 7 || fields[0] != "KELPIE_LOG_1" || fields[6].Length != 0)
                throw new FormatException("Invalid log envelope.");
            if (!long.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var size) || size < 0)
                throw new FormatException("Invalid log size.");
            if (!decimal.TryParse(fields[2], NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var now))
                throw new FormatException("Invalid remote clock.");
            if (fields[4].Length > 174764 || fields[5].Length > ((maxBytes + 3) / 3) * 4)
                throw new FormatException("Oversized log response.");
            var bytes = Convert.FromBase64String(fields[5]);
            if (bytes.Length > maxBytes + 1 || bytes.Contains((byte)0)) throw new FormatException("Invalid log bytes.");
            var zone = RemoteLogTimeZone.Parse(fields[3], Convert.FromBase64String(fields[4]));
            var content = Encoding.UTF8.GetString(bytes.AsSpan(Math.Max(0, bytes.Length - maxBytes)));
            var rows = SplitLines(content).TakeLast(lines);
            var selected = new List<string>();
            var partial = false;
            var cutoff = now - sinceMinutes * 60m;
            foreach (var row in rows)
            {
                if (sinceMinutes == 0) { selected.Add(row); continue; }
                var seconds = Timestamp(row, zone);
                if (seconds is null) partial = true;
                else if (seconds >= cutoff) selected.Add(row);
            }
            var warnings = (size > maxBytes ? "KELPIE_TRUNCATED=1\n" : "")
                + (partial ? "KELPIE_SINCE_FILTER_PARTIAL=1\n" : "");
            return result with { StandardOutput = selected.Count == 0 ? "" : string.Join('\n', selected) + "\n", StandardError = warnings };
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException)
        {
            return result with { ExitCode = 1, StandardOutput = "", StandardError = "ERROR: invalid or unsupported nginx log response" };
        }
    }

    internal static async Task<SshCommandResult> ExecuteAsync(
        SshCommandService service, SshConnectionProfile profile, string commandName,
        IReadOnlyDictionary<string, string> arguments, CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.ExecuteAsync(profile, commandName, arguments,
                channel: KelpieExecutionChannel.Mcp, cancellationToken: cancellationToken);
            return Decode(result, int.Parse(arguments["maxBytes"], CultureInfo.InvariantCulture),
                int.Parse(arguments["lines"], CultureInfo.InvariantCulture), int.Parse(arguments["sinceMinutes"], CultureInfo.InvariantCulture));
        }
        catch (SshCommandOutputLimitException)
        {
            return new SshCommandResult(commandName, "", 1, "", "ERROR: nginx log output limit exceeded", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, false);
        }
    }

    internal static IEnumerable<string> SplitLines(string value)
    {
        var start = 0;
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] is not ('\n' or '\r' or '\v' or '\f' or '\u001c' or '\u001d' or '\u001e' or '\u0085' or '\u2028' or '\u2029')) continue;
            yield return value[start..i];
            if (value[i] == '\r' && i + 1 < value.Length && value[i + 1] == '\n') i++;
            start = i + 1;
        }
        if (start < value.Length) yield return value[start..];
    }

    private static long? Timestamp(string row, RemoteLogTimeZone zone)
    {
        var access = AccessTimestamp().Match(row);
        if (access.Success)
        {
            var months = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
            var month = Array.IndexOf(months, access.Groups[2].Value) + 1;
            var local = MakeDate(access, month == 0 ? 1 : month, 3, 1, 4);
            var offset = access.Groups[7].Value;
            var hours = int.Parse(offset.AsSpan(1, 2), CultureInfo.InvariantCulture);
            var minutes = int.Parse(offset.AsSpan(3, 2), CultureInfo.InvariantCulture);
            var total = hours * 3600 + minutes * 60;
            if (total >= 86400) throw new FormatException("Invalid timestamp offset.");
            return new DateTimeOffset(local, TimeSpan.Zero).ToUnixTimeSeconds() - (offset[0] == '-' ? -total : total);
        }
        var error = ErrorTimestamp().Match(row);
        if (!error.Success) return null;
        return zone.ToUnixSeconds(MakeDate(error, int.Parse(error.Groups[2].Value, CultureInfo.InvariantCulture), 1, 3, 4));
    }

    private static DateTime MakeDate(Match match, int month, int yearGroup, int dayGroup, int timeGroup) => new(
        int.Parse(match.Groups[yearGroup].Value, CultureInfo.InvariantCulture), month,
        int.Parse(match.Groups[dayGroup].Value, CultureInfo.InvariantCulture),
        int.Parse(match.Groups[timeGroup].Value, CultureInfo.InvariantCulture),
        int.Parse(match.Groups[timeGroup + 1].Value, CultureInfo.InvariantCulture),
        int.Parse(match.Groups[timeGroup + 2].Value, CultureInfo.InvariantCulture), DateTimeKind.Unspecified);

    [GeneratedRegex(@"\[([0-9]{2})/([A-Za-z]{3})/([0-9]{4}):([0-9]{2}):([0-9]{2}):([0-9]{2}) ([+-][0-9]{4})\]", RegexOptions.CultureInvariant)]
    private static partial Regex AccessTimestamp();

    [GeneratedRegex(@"^([0-9]{4})/([0-9]{2})/([0-9]{2}) ([0-9]{2}):([0-9]{2}):([0-9]{2})", RegexOptions.CultureInvariant)]
    private static partial Regex ErrorTimestamp();
}

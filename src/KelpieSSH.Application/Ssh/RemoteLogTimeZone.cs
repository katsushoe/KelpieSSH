using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace KelpieSSH.Application.Ssh;

/// <summary>Interprets local log timestamps using bounded timezone evidence from the SSH host.</summary>
internal sealed class RemoteLogTimeZone
{
    private readonly List<(long At, int Offset)> _transitions = [];
    private readonly HashSet<int> _offsets = [];
    private int _initial;
    private PosixRule? _future;

    internal static RemoteLogTimeZone Parse(string kind, byte[] data)
    {
        if (data.Length > 131072) throw new FormatException("Timezone data exceeds limit.");
        var zone = new RemoteLogTimeZone();
        if (kind == "POSIX")
        {
            zone._future = PosixRule.Parse(Encoding.ASCII.GetString(data));
            zone._initial = zone._future.Standard;
            zone._offsets.UnionWith([zone._future.Standard, zone._future.Daylight]);
            return zone;
        }
        if (kind != "TZIF") throw new FormatException("Unsupported timezone evidence.");
        var first = ReadHeader(data, 0);
        var start = 44;
        var width = 4;
        var header = first;
        if (data[4] is (byte)'2' or (byte)'3' or (byte)'4')
        {
            start = checked(start + BlockLength(first, 4));
            header = ReadHeader(data, start);
            start += 44;
            width = 8;
        }
        if (header.Leaps != 0) throw new FormatException("Leap-second timezone tables are unsupported.");
        var end = checked(start + BlockLength(header, width));
        if (end > data.Length) throw new FormatException("Truncated timezone data.");
        var typeStart = checked(start + header.Times * (width + 1));
        var offsets = new int[header.Types];
        for (var i = 0; i < offsets.Length; i++)
        {
            var position = typeStart + i * 6;
            offsets[i] = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(position, 4));
            if (offsets[i] is < -89999 or > 93599 || data[position + 4] > 1 || data[position + 5] >= header.Chars)
                throw new FormatException("Invalid timezone type.");
            zone._offsets.Add(offsets[i]);
        }
        zone._initial = offsets[0];
        long previous = long.MinValue;
        for (var i = 0; i < header.Times; i++)
        {
            var at = width == 8 ? BinaryPrimitives.ReadInt64BigEndian(data.AsSpan(start + i * width, width))
                : BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(start + i * width, width));
            var type = data[start + header.Times * width + i];
            if (at <= previous || type >= offsets.Length) throw new FormatException("Invalid timezone transition.");
            zone._transitions.Add((at, offsets[type]));
            previous = at;
        }
        if (width == 8)
        {
            var footer = Encoding.ASCII.GetString(data, end, data.Length - end);
            if (!footer.StartsWith('\n') || !footer.EndsWith('\n')) throw new FormatException("Invalid timezone footer.");
            var rule = footer.Trim('\n');
            if (rule.Length > 0)
            {
                zone._future = PosixRule.Parse(rule);
                zone._offsets.UnionWith([zone._future.Standard, zone._future.Daylight]);
            }
        }
        return zone;
    }

    internal long ToUnixSeconds(DateTime local)
    {
        var wall = new DateTimeOffset(DateTime.SpecifyKind(local, DateTimeKind.Utc)).ToUnixTimeSeconds();
        long? earliest = null;
        foreach (var offset in _offsets)
        {
            var candidate = checked(wall - offset);
            if (OffsetAt(candidate) == offset && (earliest is null || candidate < earliest)) earliest = candidate;
        }
        if (earliest.HasValue) return earliest.Value;
        // fold=0 uses the pre-transition offset for a nonexistent local time.
        var before = _initial;
        foreach (var transition in _transitions)
        {
            if (transition.Offset > before && wall >= transition.At + before && wall < transition.At + transition.Offset)
                return wall - before;
            before = transition.Offset;
        }
        if (_future is not null)
        {
            foreach (var transition in _future.NearbyTransitions(local.Year))
            {
                if (transition.After > transition.Before && wall >= transition.At + transition.Before && wall < transition.At + transition.After)
                    return wall - transition.Before;
            }
        }
        throw new FormatException("Local timestamp is outside supported timezone evidence.");
    }

    private int OffsetAt(long seconds)
    {
        if (_future is not null && (_transitions.Count == 0 || seconds > _transitions[^1].At)) return _future.OffsetAt(seconds);
        var result = _initial;
        foreach (var transition in _transitions)
        {
            if (transition.At > seconds) break;
            result = transition.Offset;
        }
        return result;
    }

    private sealed record Header(int Gmt, int Std, int Leaps, int Times, int Types, int Chars);

    private static Header ReadHeader(byte[] data, int start)
    {
        if (start < 0 || data.Length - start < 44 || Encoding.ASCII.GetString(data, start, 4) != "TZif")
            throw new FormatException("Invalid timezone header.");
        var values = new int[6];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(start + 20 + i * 4, 4));
            if (values[i] < 0 || values[i] > 16384) throw new FormatException("Invalid timezone count.");
        }
        if (values[4] is < 1 or > 256 || values[5] < 1) throw new FormatException("Invalid timezone types.");
        return new Header(values[0], values[1], values[2], values[3], values[4], values[5]);
    }

    private static int BlockLength(Header h, int width) => checked(h.Times * (width + 1) + h.Types * 6 + h.Chars + h.Leaps * (width + 4) + h.Std + h.Gmt);

    private sealed record PosixRule(int Standard, int Daylight, PosixRule.Change? Start, PosixRule.Change? End)
    {
        internal static PosixRule Parse(string value)
        {
            var m = Regex.Match(value, @"^(?:[A-Za-z]{3,}|<[^<>\r\n]+>)(?<std>[+-]?\d{1,3}(?::\d{1,2}(?::\d{1,2})?)?)(?:(?:[A-Za-z]{3,}|<[^<>\r\n]+>)(?<dst>[+-]?\d{1,3}(?::\d{1,2}(?::\d{1,2})?)?)?(?:,(?<start>[^,]+),(?<end>[^,]+))?)?$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            if (!m.Success) throw new FormatException("Unsupported POSIX timezone rule.");
            var standard = -ParseTime(m.Groups["std"].Value, 24);
            if (!m.Groups["start"].Success)
            {
                // A daylight abbreviation without explicit rules depends on a host default file.
                if (m.Length != Regex.Match(value, @"^(?:[A-Za-z]{3,}|<[^<>]+>)[+-]?\d{1,3}(?::\d{1,2}(?::\d{1,2})?)?", RegexOptions.CultureInvariant).Length)
                    throw new FormatException("Implicit daylight rules are unsupported.");
                return new PosixRule(standard, standard, null, null);
            }
            var daylight = m.Groups["dst"].Success ? -ParseTime(m.Groups["dst"].Value, 24) : standard + 3600;
            return new PosixRule(standard, daylight, Change.Parse(m.Groups["start"].Value), Change.Parse(m.Groups["end"].Value));
        }

        internal IEnumerable<(long At, int Before, int After)> Transitions(int year)
        {
            if (Start is null || End is null) yield break;
            yield return (Start.Seconds(year) - Standard, Standard, Daylight);
            yield return (End.Seconds(year) - Daylight, Daylight, Standard);
        }

        internal IEnumerable<(long At, int Before, int After)> NearbyTransitions(int year)
        {
            for (var ruleYear = Math.Max(1, year - 1); ruleYear <= Math.Min(9999, year + 1); ruleYear++)
                foreach (var transition in Transitions(ruleYear)) yield return transition;
        }

        internal int OffsetAt(long seconds)
        {
            if (Start is null || End is null) return Standard;
            var year = DateTimeOffset.FromUnixTimeSeconds(seconds).Year;
            var result = Standard;
            foreach (var transition in NearbyTransitions(year).OrderBy(item => item.At))
            {
                if (transition.At > seconds) break;
                result = transition.After;
            }
            return result;
        }

        private static int ParseTime(string value, int maxHours)
        {
            var sign = value.StartsWith('-') ? -1 : 1;
            var parts = value.TrimStart('+', '-').Split(':');
            var hours = int.Parse(parts[0], CultureInfo.InvariantCulture);
            var minutes = parts.Length > 1 ? int.Parse(parts[1], CultureInfo.InvariantCulture) : 0;
            var seconds = parts.Length > 2 ? int.Parse(parts[2], CultureInfo.InvariantCulture) : 0;
            if (hours > maxHours || minutes > 59 || seconds > 59) throw new FormatException("Invalid timezone offset.");
            return sign * (hours * 3600 + minutes * 60 + seconds);
        }

        internal sealed record Change(char Kind, int Day, int Month, int Week, int Weekday, int Time)
        {
            internal static Change Parse(string text)
            {
                var parts = text.Split('/');
                if (parts.Length > 2) throw new FormatException("Invalid timezone change.");
                var time = parts.Length == 2 ? ParseTime(parts[1], 167) : 7200;
                if (parts[0].StartsWith('M'))
                {
                    var fields = parts[0][1..].Split('.');
                    if (fields.Length != 3) throw new FormatException("Invalid month rule.");
                    var month = int.Parse(fields[0], CultureInfo.InvariantCulture);
                    var week = int.Parse(fields[1], CultureInfo.InvariantCulture);
                    var day = int.Parse(fields[2], CultureInfo.InvariantCulture);
                    if (month is < 1 or > 12 || week is < 1 or > 5 || day is < 0 or > 6) throw new FormatException("Invalid month rule.");
                    return new Change('M', 0, month, week, day, time);
                }
                var julian = parts[0].StartsWith('J');
                var ordinal = int.Parse(julian ? parts[0][1..] : parts[0], CultureInfo.InvariantCulture);
                if (ordinal < (julian ? 1 : 0) || ordinal > (julian ? 365 : 365)) throw new FormatException("Invalid day rule.");
                return new Change(julian ? 'J' : 'N', ordinal, 0, 0, 0, time);
            }

            internal long Seconds(int year)
            {
                var date = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                if (Kind == 'M')
                {
                    date = new DateTime(year, Month, 1, 0, 0, 0, DateTimeKind.Utc);
                    var day = 1 + (Weekday - (int)date.DayOfWeek + 7) % 7 + (Week - 1) * 7;
                    if (day > DateTime.DaysInMonth(year, Month)) day -= 7;
                    date = date.AddDays(day - 1);
                }
                else date = date.AddDays(Kind == 'N' ? Day : Day - 1 + (DateTime.IsLeapYear(year) && Day >= 60 ? 1 : 0));
                return new DateTimeOffset(date).ToUnixTimeSeconds() + Time;
            }
        }
    }
}

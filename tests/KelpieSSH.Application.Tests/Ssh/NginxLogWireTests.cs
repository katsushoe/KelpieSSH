using System.Globalization;
using System.Buffers.Binary;
using System.Text;
using FluentAssertions;
using KelpieSSH.Application.Ssh;

namespace KelpieSSH.Application.Tests.Ssh;

public sealed class NginxLogWireTests
{
    internal static string Envelope(string content, string now = "2026-06-14T10:01:00Z", long? size = null, string tz = "UTC0") =>
        EnvelopeBytes(Encoding.UTF8.GetBytes(content), DateTimeOffset.Parse(now, CultureInfo.InvariantCulture).ToUnixTimeSeconds(), size, "POSIX", Encoding.ASCII.GetBytes(tz));

    internal static string EnvelopeBytes(byte[] content, decimal now, long? size, string kind, byte[] timezone) =>
        $"KELPIE_LOG_1\n{size ?? content.Length}\n{now.ToString(CultureInfo.InvariantCulture)}\n{kind}\n{Convert.ToBase64String(timezone)}\n{Convert.ToBase64String(content)}\n";

    private static SshCommandResult Decode(string envelope, int bytes = 262144, int lines = 500, int since = 0) =>
        NginxLogWire.Decode(new SshCommandResult("service_logfile_nginx_read", "", 0, envelope, "", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, false), bytes, lines, since);

    [Theory]
    [InlineData("", "")]
    [InlineData("a\r\nb\rc\nd\vE\fF\u001cG\u001dH\u001eI\u0085J\u2028K\u2029", "a\nb\nc\nd\nE\nF\nG\nH\nI\nJ\nK\n")]
    [InlineData("a\n\n", "a\n\n")]
    [InlineData("\n", "\n")]
    public void Decode_PreservesPythonTextLines(string input, string expected) => Decode(Envelope(input)).StandardOutput.Should().Be(expected);

    [Fact]
    public void Decode_SelectsRowsBeforeFiltering()
    {
        var result = Decode(Envelope("2026/06/14 10:01:00 kept?\nunknown\n"), lines: 1, since: 10);
        result.StandardOutput.Should().BeEmpty();
        result.StandardError.Should().Contain("KELPIE_SINCE_FILTER_PARTIAL=1");
    }

    [Theory]
    [InlineData("[14/Jun/2026:10:00:00 +0000]", true)]
    [InlineData("[14/Jun/2026:09:59:59 +0000]", false)]
    [InlineData("[14/Jun/2026:15:30:00 +0530]", true)]
    [InlineData("[14/Jun/2026:07:00:00 -0300]", true)]
    [InlineData("2026/06/14 10:00:00", true)]
    [InlineData("2026/06/14 10:01:01", true)]
    [InlineData("2026/06/14 09:59:59", false)]
    public void Decode_UsesInclusiveRemoteClock(string row, bool keep)
    {
        var result = Decode(Envelope(row + "\n"), since: 1);
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be(keep ? row + "\n" : "");
    }

    [Fact]
    public void Decode_UsesMicrosecondCutoff()
    {
        var content = Encoding.UTF8.GetBytes("2026/06/14 10:00:00\n");
        var now = DateTimeOffset.Parse("2026-06-14T10:01:00Z").ToUnixTimeSeconds() + 0.000001m;
        Decode(EnvelopeBytes(content, now, null, "POSIX", Encoding.ASCII.GetBytes("UTC0")), since: 1).StandardOutput.Should().BeEmpty();
    }

    [Theory]
    [InlineData("[31/Feb/2026:10:00:00 +0000]")]
    [InlineData("2026/06/14 24:00:00")]
    [InlineData("[14/Jun/2026:10:00:00 +2400]")]
    public void Decode_RejectsRecognizedInvalidTime(string row)
    {
        var result = Decode(Envelope(row), since: 1);
        result.ExitCode.Should().Be(1);
        result.StandardOutput.Should().BeEmpty();
        Decode(Envelope(row)).ExitCode.Should().Be(0);
    }

    [Fact]
    public void Decode_UnknownAccessMonthUsesJanuary()
    {
        var row = "[14/Xxx/2026:10:00:00 +0000]";
        Decode(Envelope(row, "2026-01-14T10:01:00Z"), since: 1).StandardOutput.Should().Be(row + "\n");
    }

    [Fact]
    public void Decode_RejectsNulInDiscardedExtraByte()
    {
        Decode(EnvelopeBytes([0, 65, 66], 0, 3, "POSIX", Encoding.ASCII.GetBytes("UTC0")), bytes: 2).ExitCode.Should().Be(1);
    }

    [Fact]
    public void Decode_ReplacesPartialUtf8AndUsesSnapshotTruncation()
    {
        var result = Decode(EnvelopeBytes([0xe3, 0x81, 0x82], 0, 3, "POSIX", Encoding.ASCII.GetBytes("UTC0")), bytes: 2);
        result.StandardOutput.Should().Be("\ufffd\ufffd\n");
        result.StandardError.Should().Be("KELPIE_TRUNCATED=1\n");
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("KELPIE_LOG_1\n0\nNaN\nPOSIX\nVVRDMA==\n\n")]
    [InlineData("KELPIE_LOG_1\n0\n0\nPOSIX\n!\n\n")]
    [InlineData("KELPIE_LOG_1\n0\n0\nOTHER\nVVRDMA==\n\n")]
    public void Decode_RejectsInvalidEnvelope(string envelope)
    {
        var result = Decode(envelope);
        result.ExitCode.Should().Be(1);
        result.StandardOutput.Should().BeEmpty();
    }

    [Theory]
    [InlineData("UTC0", "2026-01-01T10:00:00", "2026-01-01T10:00:00Z")]
    [InlineData("JST-9", "2026-01-01T10:00:00", "2026-01-01T01:00:00Z")]
    [InlineData("EST5EDT,M3.2.0,M11.1.0", "2026-11-01T01:30:00", "2026-11-01T05:30:00Z")]
    [InlineData("EST5EDT,M3.2.0,M11.1.0", "2026-03-08T02:30:00", "2026-03-08T07:30:00Z")]
    [InlineData("AEST-10AEDT,M10.1.0,M4.1.0/3", "2026-01-01T10:00:00", "2025-12-31T23:00:00Z")]
    [InlineData("EST5EDT,J60,J300", "2028-03-01T10:00:00", "2028-03-01T14:00:00Z")]
    [InlineData("EST5EDT,60,300", "2028-03-01T10:00:00", "2028-03-01T14:00:00Z")]
    [InlineData("STD-14DST-15,M1.1.0/0,M6.1.0/0", "2023-01-01T01:00:00", "2022-12-31T10:00:00Z")]
    [InlineData("STD-14DST-15,M1.1.0/0,M6.1.0/0", "2023-01-01T00:30:00", "2022-12-31T10:30:00Z")]
    [InlineData("STD0DST,M1.1.0/-2,M6.1.0", "2022-12-31T22:30:00", "2022-12-31T22:30:00Z")]
    [InlineData("STD0DST,J365/26,J100", "2023-01-01T03:30:00", "2023-01-01T02:30:00Z")]
    public void TimeZone_UsesRemoteRulesAndFoldZero(string rule, string local, string expected)
    {
        var zone = RemoteLogTimeZone.Parse("POSIX", Encoding.ASCII.GetBytes(rule));
        zone.ToUnixSeconds(DateTime.Parse(local, CultureInfo.InvariantCulture)).Should().Be(DateTimeOffset.Parse(expected).ToUnixTimeSeconds());
    }

    [Theory]
    [InlineData("EST5EDT")]
    [InlineData("JST-100")]
    [InlineData("EST5EDT,M13.1.0,M11.1.0")]
    public void TimeZone_RejectsUnsupportedRule(string rule)
    {
        Action action = () => RemoteLogTimeZone.Parse("POSIX", Encoding.ASCII.GetBytes(rule));
        action.Should().Throw<FormatException>();
    }

    [Fact]
    public void TimeZone_RejectsTruncatedTzif()
    {
        Action action = () => RemoteLogTimeZone.Parse("TZIF", Encoding.ASCII.GetBytes("TZif"));
        action.Should().Throw<FormatException>();
    }

    [Theory]
    [InlineData(0, 3600, "1970-01-01T00:30:00", 1800)]
    [InlineData(3600, 0, "1970-01-01T00:30:00", -1800)]
    [InlineData(3600, 0, "1969-12-31T23:00:00", -7200)]
    [InlineData(0, 3600, "1970-01-02T00:00:00", 82800)]
    public void TimeZone_ReadsTzifTransitionsAndInitialType(int before, int after, string local, long expected)
    {
        var data = new byte[65];
        Encoding.ASCII.GetBytes("TZif").CopyTo(data,0);
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(32,4),1);
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(36,4),2);
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(40,4),4);
        data[48]=1;
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(49,4),before);
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(55,4),after);
        Encoding.ASCII.GetBytes("UTC\0").CopyTo(data,61);
        RemoteLogTimeZone.Parse("TZIF",data).ToUnixSeconds(DateTime.Parse(local,CultureInfo.InvariantCulture)).Should().Be(expected);
    }

    [Fact]
    public void Decode_RejectsOversizedPayloadAndInvalidBounds()
    {
        Decode(Envelope("abc"),bytes:1).ExitCode.Should().Be(1);
        Decode(Envelope("abc"),lines:0).ExitCode.Should().Be(1);
        Decode(Envelope("abc"),since:1441).ExitCode.Should().Be(1);
    }
}

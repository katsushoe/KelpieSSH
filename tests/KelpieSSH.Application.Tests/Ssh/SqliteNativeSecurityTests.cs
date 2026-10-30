using FluentAssertions;
using Microsoft.Data.Sqlite;

namespace KelpieSSH.Application.Tests.Ssh;

public sealed class SqliteNativeSecurityTests
{
    [Fact]
    public async Task NativeEngine_ShouldMeetAggregateMemoryCorruptionFixBaseline()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT sqlite_version()";
        var text = (string)(await command.ExecuteScalarAsync())!;
        Version.TryParse(text, out var version).Should().BeTrue();
        version.Should().BeGreaterThanOrEqualTo(new Version(3, 50, 2),
            "the loaded native engine must include the CVE-2025-6965 fix");
    }
}

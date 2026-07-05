using FluentAssertions;
using KelpieClientCommand;

namespace KelpieSSH.Application.Tests;

public sealed class ProfilePromptListReaderTests
{
    [Fact]
    public void Read_ShouldReturnDefaultsWhenFirstInputIsEmpty()
    {
        using var reader = new StringReader(Environment.NewLine);
        using var writer = new StringWriter();

        var result = ProfilePromptListReader.Read(reader, writer, "Read-only root", ["/var/log"]);

        result.Should().Equal("/var/log");
    }

    [Fact]
    public void Read_ShouldReturnEmptyWhenFirstInputIsDash()
    {
        using var reader = new StringReader("-" + Environment.NewLine);
        using var writer = new StringWriter();

        var result = ProfilePromptListReader.Read(reader, writer, "Deny pattern", ["**/.env"]);

        result.Should().BeEmpty();
    }
}

using FluentAssertions;
using Kelpie.Core;
using Microsoft.Extensions.Configuration;

namespace KelpieSSH.Application.Tests.Command;

public sealed class KelpieMcpServerOptionsTests
{
    [Fact]
    public void FromConfiguration_ShouldReadShortKelpieKeys()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Server:Port"] = "45433",
                ["Server:ControlPipeName"] = "Kelpie.Test.Control",
                ["Commands:ExecutablePath"] = "server.exe",
                ["Commands:WorkingDirectory"] = "server-work",
            })
            .Build();

        var options = KelpieMcpServerOptions.FromConfiguration(configuration);

        options.ServerPort.Should().Be(45433);
        options.ControlPipeName.Should().Be("Kelpie.Test.Control");
        options.ServerExecutablePath.Should().Be("server.exe");
        options.ServerWorkingDirectory.Should().Be("server-work");
    }

    [Fact]
    public void FromConfiguration_ShouldRejectMissingControlPipeName()
    {
        var configuration = new ConfigurationBuilder().Build();

        var action = () => KelpieMcpServerOptions.FromConfiguration(configuration);

        action.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Server:ControlPipeName is not configured.");
    }
}

using FluentAssertions;
using Kelpie.Core;
using Microsoft.Extensions.Configuration;

namespace KelpieSSH.Application.Tests.Command;

public sealed class KelpieProfileOperationsOptionsTests
{
    [Fact]
    public void FromConfiguration_ShouldUseDefaultPolicyWhenUnset()
    {
        var configuration = new ConfigurationBuilder().Build();

        var options = KelpieProfileOperationsOptions.FromConfiguration(configuration);

        options.IsAllowed("add", "CLI").Should().BeTrue();
        options.IsAllowed("reload", "CLI").Should().BeTrue();
        options.IsAllowed("revoke", "CLI").Should().BeTrue();
        options.IsAllowed("add", "MCP").Should().BeFalse();
        options.IsAllowed("reload", "MCP").Should().BeFalse();
        options.IsAllowed("revoke", "MCP").Should().BeFalse();
    }

    [Fact]
    public void FromConfiguration_ShouldReadAllowDenyStrings()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ProfileOperations:Add:CLI"] = "Deny",
                ["ProfileOperations:Reload:CLI"] = "Allow",
                ["ProfileOperations:Revoke:CLI"] = "Deny",
                ["ProfileOperations:Reload:MCP"] = "Allow",
            })
            .Build();

        var options = KelpieProfileOperationsOptions.FromConfiguration(configuration);

        options.IsAllowed("add", "CLI").Should().BeFalse();
        options.IsAllowed("reload", "CLI").Should().BeTrue();
        options.IsAllowed("revoke", "CLI").Should().BeFalse();
        options.IsAllowed("reload", "MCP").Should().BeTrue();
    }

    [Fact]
    public void FromConfiguration_ShouldReadLegacyAllowedString()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ProfileOperations:Reload:MCP"] = "Allowed",
            })
            .Build();

        var options = KelpieProfileOperationsOptions.FromConfiguration(configuration);

        options.IsAllowed("reload", "MCP").Should().BeTrue();
    }

    [Fact]
    public void FromConfiguration_ShouldReadLegacyBooleanValues()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ProfileOperations:Reload:MCP"] = "true",
                ["ProfileOperations:Add:CLI"] = "false",
            })
            .Build();

        var options = KelpieProfileOperationsOptions.FromConfiguration(configuration);

        options.IsAllowed("reload", "MCP").Should().BeTrue();
        options.IsAllowed("add", "CLI").Should().BeFalse();
    }
}

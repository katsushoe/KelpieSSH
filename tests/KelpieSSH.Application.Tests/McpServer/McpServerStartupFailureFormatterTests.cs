using System.Text.Json;
using FluentAssertions;
using KelpieMCPServer;
using Microsoft.AspNetCore.Connections;

namespace KelpieSSH.Application.Tests.McpServer;

public sealed class McpServerStartupFailureFormatterTests
{
    [Fact]
    public void Format_WhenConfigurationIsInvalid_ShouldReturnRepairGuidance()
    {
        var exception = new JsonException("secret parser detail");

        var result = McpServerStartupFailureFormatter.Format(exception);

        result.Should().Be(
            "KelpieMCPServer configuration is invalid. Run 'kelpie config check' and correct kelpiemcp.json.");
        result.Should().NotContain("secret parser detail");
    }

    [Fact]
    public void Format_WhenConfigurationTrustDoesNotMatch_ShouldReturnReloadGuidance()
    {
        var exception = new InvalidOperationException(
            "MCP server configuration hash does not match trusted baseline. secret detail");

        var result = McpServerStartupFailureFormatter.Format(exception);

        result.Should().Be(
            "KelpieMCPServer configuration is not trusted. Restart with --reload-config to accept the current configuration.");
        result.Should().NotContain("secret detail");
    }

    [Fact]
    public void Format_WhenEndpointIsInUse_ShouldReturnPortGuidance()
    {
        var exception = new IOException(
            "secret transport detail",
            new AddressInUseException("secret endpoint detail"));

        var result = McpServerStartupFailureFormatter.Format(exception);

        result.Should().Be(
            "KelpieMCPServer endpoint is already in use. Stop the existing server or select another --port.");
        result.Should().NotContain("secret");
    }

    [Fact]
    public void Format_WhenAccessIsDenied_ShouldReturnPermissionGuidance()
    {
        var exception = new IOException(
            "secret wrapper detail",
            new UnauthorizedAccessException("secret path detail"));

        var result = McpServerStartupFailureFormatter.Format(exception);

        result.Should().Be(
            "KelpieMCPServer access was denied. Verify Kelpie home permissions and that another server instance is not using the control pipe.");
        result.Should().NotContain("secret");
    }

    [Fact]
    public void Format_WhenFailureIsUnexpected_ShouldReturnLogGuidance()
    {
        var exception = new Exception("secret unexpected detail");

        var result = McpServerStartupFailureFormatter.Format(exception);

        result.Should().Be("KelpieMCPServer failed to start. Check the Kelpie logs for details.");
        result.Should().NotContain("secret unexpected detail");
    }
}

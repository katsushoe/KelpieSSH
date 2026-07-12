using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Kelpie.Core;
using KelpieSSH.Application.Ssh;
using KelpieServerCommand;

namespace KelpieSSH.Application.Tests.Command;

public sealed class KelpieServerCommandRunnerTests
{
    private static readonly UTF8Encoding ControlPipeEncoding = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    [Fact]
    public async Task StartAsync_ShouldStartWindowsServiceWhenServiceIsRegistered()
    {
        var options = CreateOptions();
        using var output = new StringWriter();
        var previousOutput = Console.Out;
        var serviceStartCalled = false;
        Console.SetOut(output);

        try
        {
            await KelpieServerCommandRunner.StartAsync(
                options,
                () => Task.FromResult(true),
                () =>
                {
                    serviceStartCalled = true;
                    return Task.FromResult(true);
                });
        }
        finally
        {
            Console.SetOut(previousOutput);
        }

        serviceStartCalled.Should().BeTrue();
        output.ToString().Should().Contain("Windows Service start requested: KelpieMCPServer");
    }

    [Fact]
    public async Task StatusAsync_ShouldPrintStoppedWhenPipeIsUnavailable()
    {
        var options = CreateOptions();
        using var output = new StringWriter();
        var previousOutput = Console.Out;
        Console.SetOut(output);

        try
        {
            await KelpieServerCommandRunner.StatusAsync(options, () => Task.FromResult(false));
        }
        finally
        {
            Console.SetOut(previousOutput);
        }

        output.ToString().Should().Contain("KelpieMCPServer: stopped");
        output.ToString().Should().Contain("Registered as Windows service: no");
    }

    [Fact]
    public async Task StatusAsync_ShouldPrintRunningWhenPipeReturnsPong()
    {
        var options = CreateOptions();
        using var output = new StringWriter();
        var previousOutput = Console.Out;
        var serverTask = RunSingleResponsePipeAsync(options.ControlPipeName, "ping", "pong;windowsService=false");
        Console.SetOut(output);

        try
        {
            await KelpieServerCommandRunner.StatusAsync(options, () => Task.FromResult(true));
        }
        finally
        {
            Console.SetOut(previousOutput);
        }

        await serverTask;
        output.ToString().Should().Contain("KelpieMCPServer: running");
        output.ToString().Should().Contain("MCP URL: http://127.0.0.1:45432/mcp");
        output.ToString().Should().Contain($"Control pipe: {options.ControlPipeName}");
        output.ToString().Should().Contain("Registered as Windows service: yes");
    }

    [Fact]
    public async Task StopAsync_ShouldSendStopToPipe()
    {
        var options = CreateOptions();
        using var output = new StringWriter();
        var previousOutput = Console.Out;
        var serverTask = RunSingleResponsePipeAsync(options.ControlPipeName, "stop", "stopping");
        Console.SetOut(output);

        try
        {
            await KelpieServerCommandRunner.StopAsync(options);
        }
        finally
        {
            Console.SetOut(previousOutput);
        }

        await serverTask;
        output.ToString().Should().Contain("KelpieMCPServer stop requested.");
    }

    [Fact]
    public async Task PasswordAsync_ShouldSendPasswordToPipe()
    {
        var options = CreateOptions();
        using var output = new StringWriter();
        var previousOutput = Console.Out;
        var serverTask = RunLoginPipeAsync(options.ControlPipeName, "vps01", "secret", "logged-in");
        Console.SetOut(output);

        try
        {
            await KelpieServerCommandRunner.PasswordAsync(options, "vps01", () => "secret");
        }
        finally
        {
            Console.SetOut(previousOutput);
        }

        await serverTask;
        output.ToString().Should().Contain("SSH password stored for this KelpieMCPServer session.");
    }

    [Fact]
    public async Task PasswordAsync_ShouldSendUtf8PasswordToPipe()
    {
        var options = CreateOptions();
        using var output = new StringWriter();
        var previousOutput = Console.Out;
        const string password = "pass-日本語-umlaut-ä";
        var serverTask = RunLoginPipeAsync(options.ControlPipeName, "vps01", password, "logged-in");
        Console.SetOut(output);

        try
        {
            await KelpieServerCommandRunner.PasswordAsync(options, "vps01", () => password);
        }
        finally
        {
            Console.SetOut(previousOutput);
        }

        await serverTask;
        output.ToString().Should().Contain("SSH password stored for this KelpieMCPServer session.");
    }

    [Fact]
    public async Task SecretPutAsync_ShouldSendSecretPayloadThroughPipeBody()
    {
        var options = CreateOptions();
        using var output = new StringWriter();
        var previousOutput = Console.Out;
        var directory = Path.Combine(Path.GetTempPath(), "kelpie-secret-put-" + Guid.NewGuid().ToString("N"));
        var secretPath = Path.Combine(directory, "secret.txt");
        var secretBytes = Encoding.UTF8.GetBytes("TOKEN=secret-value");
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(secretPath, secretBytes);
        var serverTask = RunSecretPutPipeAsync(options.ControlPipeName, "prod-web-env", 600, secretBytes);
        Console.SetOut(output);

        try
        {
            await KelpieServerCommandRunner.SecretPutAsync(
                options,
                ["--name", "prod-web-env", "--from-file", secretPath, "--ttl", "10m"]);
        }
        finally
        {
            Console.SetOut(previousOutput);
            Directory.Delete(directory, recursive: true);
        }

        await serverTask;
        output.ToString().Should().Contain("Secret stored for this KelpieMCPServer session.");
    }

    [Fact]
    public async Task SecretPutAsync_ShouldRejectOverflowTtl()
    {
        var options = CreateOptions();
        using var error = new StringWriter();
        var previousError = Console.Error;
        var previousExitCode = Environment.ExitCode;
        Console.SetError(error);
        Environment.ExitCode = 0;

        try
        {
            await KelpieServerCommandRunner.SecretPutAsync(
                options,
                ["--name", "prod-web-env", "--from-file", "secret.txt", "--ttl", "999999999999999999999999h"]);
        }
        finally
        {
            Console.SetError(previousError);
            Environment.ExitCode = previousExitCode;
        }

        error.ToString().Should().Contain("TTL must be a positive duration");
    }

    [Fact]
    public async Task EnvPutAsync_ShouldSendValuePayloadThroughPipeBody()
    {
        var options = CreateOptions();
        using var output = new StringWriter();
        var previousOutput = Console.Out;
        const string value = "token with $dollar 'single'";
        var serverTask = RunEnvPutPipeAsync(options.ControlPipeName, "vps01", "DEPLOY_TOKEN", value);
        Console.SetOut(output);

        try
        {
            await KelpieServerCommandRunner.EnvPutAsync(
                options,
                ["vps01", "DEPLOY_TOKEN", value]);
        }
        finally
        {
            Console.SetOut(previousOutput);
        }

        await serverTask;
        output.ToString().Should().Contain("Environment override stored for this KelpieMCPServer session.");
        output.ToString().Should().NotContain(value);
    }

    [Fact]
    public async Task EnvListAsync_ShouldPrintOverrideMetadata()
    {
        var options = CreateOptions();
        using var output = new StringWriter();
        var previousOutput = Console.Out;
        var now = DateTimeOffset.UtcNow;
        var response = JsonSerializer.Serialize(new[]
        {
            new KelpieEnvironmentOverrideInfo("vps01", "APP_ENV", 10, now),
        });
        var requestJson = JsonSerializer.Serialize(new EnvListRequest("vps01"));
        var serverTask = RunSingleResponsePipeAsync(options.ControlPipeName, "env-list " + requestJson, response);
        Console.SetOut(output);

        try
        {
            await KelpieServerCommandRunner.EnvListAsync(options, "vps01");
        }
        finally
        {
            Console.SetOut(previousOutput);
        }

        await serverTask;
        output.ToString().Should().Contain("Environment overrides:");
        output.ToString().Should().Contain("vps01  APP_ENV  length 10");
    }

    [Fact]
    public async Task ForgetAsync_ShouldSendLogoutToPipe()
    {
        var options = CreateOptions();
        using var output = new StringWriter();
        var previousOutput = Console.Out;
        var serverTask = RunSingleResponsePipeAsync(options.ControlPipeName, "logout vps01", "logged-out");
        Console.SetOut(output);

        try
        {
            await KelpieServerCommandRunner.ForgetAsync(options, "vps01");
        }
        finally
        {
            Console.SetOut(previousOutput);
        }

        await serverTask;
        output.ToString().Should().Contain("SSH password cleared for this KelpieMCPServer session.");
    }

    [Fact]
    public async Task SessionsAsync_ShouldPrintSessions()
    {
        var options = CreateOptions();
        using var output = new StringWriter();
        var previousOutput = Console.Out;
        var response = """[{"Handle":"ssh-abc123","ProfileName":"vps01","SecretName":"kelpie:vps01","StartedAtUtc":"2026-06-05T01:02:03+00:00","Kind":"password"}]""";
        var serverTask = RunSingleResponsePipeAsync(options.ControlPipeName, "sessions", response);
        Console.SetOut(output);

        try
        {
            await KelpieServerCommandRunner.SessionsAsync(options);
        }
        finally
        {
            Console.SetOut(previousOutput);
        }

        await serverTask;
        output.ToString().Should().Contain("Sessions:");
        output.ToString().Should().Contain("ssh-abc123  vps01  password  2026-06-05 01:02:03Z");
    }

    [Fact]
    public async Task SessionsAsync_ShouldPrintNoneWhenSessionListIsEmpty()
    {
        var options = CreateOptions();
        using var output = new StringWriter();
        var previousOutput = Console.Out;
        var serverTask = RunSingleResponsePipeAsync(options.ControlPipeName, "sessions", "[]");
        Console.SetOut(output);

        try
        {
            await KelpieServerCommandRunner.SessionsAsync(options);
        }
        finally
        {
            Console.SetOut(previousOutput);
        }

        await serverTask;
        output.ToString().Should().Contain("Sessions:");
        output.ToString().Should().Contain("(none)");
    }

    [Fact]
    public async Task KillAsync_ShouldSendKillToPipe()
    {
        var options = CreateOptions();
        using var output = new StringWriter();
        var previousOutput = Console.Out;
        var serverTask = RunSingleResponsePipeAsync(options.ControlPipeName, "kill ssh-abc123", "killed");
        Console.SetOut(output);

        try
        {
            await KelpieServerCommandRunner.KillAsync(options, "ssh-abc123");
        }
        finally
        {
            Console.SetOut(previousOutput);
        }

        await serverTask;
        output.ToString().Should().Contain("SSH session killed: ssh-abc123");
    }

    [Fact]
    public async Task KillAsync_ShouldPrintMissingSession()
    {
        var options = CreateOptions();
        using var error = new StringWriter();
        var previousError = Console.Error;
        var previousExitCode = Environment.ExitCode;
        var serverTask = RunSingleResponsePipeAsync(options.ControlPipeName, "kill ssh-missing", "session-not-found");
        Console.SetError(error);
        Environment.ExitCode = 0;

        try
        {
            await KelpieServerCommandRunner.KillAsync(options, "ssh-missing");
        }
        finally
        {
            Console.SetError(previousError);
            Environment.ExitCode = previousExitCode;
        }

        await serverTask;
        error.ToString().Should().Contain("SSH session was not found: ssh-missing");
    }

    [Fact]
    public async Task ProfileCapabilitiesAsync_ShouldApplyCliProfileOperationPolicy()
    {
        var options = WithProfileOperations(
            CreateOptions(),
            new KelpieProfileOperationsOptions(
                addCliAllowed: true,
                addMcpAllowed: false,
                reloadCliAllowed: false,
                reloadMcpAllowed: false,
                revokeCliAllowed: false,
                revokeMcpAllowed: false));
        using var output = new StringWriter();
        var previousOutput = Console.Out;
        var response = JsonSerializer.Serialize(new SshProfileTrustCapabilities(
            "vps01",
            AddAllowed: true,
            ReloadAllowed: true,
            RevokeAllowed: true,
            Reason: string.Empty));
        var serverTask = RunSingleResponsePipeAsync(options.ControlPipeName, "profile-capabilities vps01", response);
        Console.SetOut(output);

        try
        {
            await KelpieServerCommandRunner.ProfileCapabilitiesAsync(options, "vps01");
        }
        finally
        {
            Console.SetOut(previousOutput);
        }

        await serverTask;
        var capabilities = JsonSerializer.Deserialize<SshProfileTrustCapabilities>(output.ToString());
        capabilities.Should().NotBeNull();
        capabilities!.AddAllowed.Should().BeTrue();
        capabilities.ReloadAllowed.Should().BeFalse();
        capabilities.RevokeAllowed.Should().BeFalse();
    }

    [Fact]
    public async Task ProfileReloadAsync_ShouldRejectWhenCliReloadIsDenied()
    {
        var options = WithProfileOperations(
            CreateOptions(),
            new KelpieProfileOperationsOptions(
                addCliAllowed: true,
                addMcpAllowed: false,
                reloadCliAllowed: false,
                reloadMcpAllowed: false,
                revokeCliAllowed: true,
                revokeMcpAllowed: false));
        using var output = new StringWriter();
        var previousOutput = Console.Out;
        var previousExitCode = Environment.ExitCode;
        Console.SetOut(output);
        Environment.ExitCode = 0;

        try
        {
            await KelpieServerCommandRunner.ProfileReloadAsync(options, "vps01");
        }
        finally
        {
            Console.SetOut(previousOutput);
            Environment.ExitCode = previousExitCode;
        }

        var result = JsonSerializer.Deserialize<SshProfileTrustOperationResult>(output.ToString());
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Status.Should().Be("disabled-by-config");
    }

    private static KelpieMcpServerOptions CreateOptions()
    {
        return new KelpieMcpServerOptions
        {
            ControlPipeName = "KelpieTest." + Guid.NewGuid().ToString("N"),
            ServerPort = 45432,
        };
    }

    private static KelpieMcpServerOptions WithProfileOperations(
        KelpieMcpServerOptions options,
        KelpieProfileOperationsOptions profileOperations)
    {
        return new KelpieMcpServerOptions
        {
            ControlPipeName = options.ControlPipeName,
            ServerPort = options.ServerPort,
            ServerExecutablePath = options.ServerExecutablePath,
            ServerWorkingDirectory = options.ServerWorkingDirectory,
            ReloadConfig = options.ReloadConfig,
            ReloadProfileNames = options.ReloadProfileNames,
            ProfileOperations = profileOperations,
        };
    }

    private static async Task RunSingleResponsePipeAsync(string pipeName, string expectedRequest, string response)
    {
        await using var pipe = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        await pipe.WaitForConnectionAsync();
        using var reader = new StreamReader(pipe, ControlPipeEncoding);
        var writer = new StreamWriter(pipe, ControlPipeEncoding)
        {
            AutoFlush = true,
        };

        var request = await reader.ReadLineAsync();
        request.Should().Be(expectedRequest);
        await writer.WriteLineAsync(response);
        await writer.FlushAsync();
    }

    private static async Task RunLoginPipeAsync(
        string pipeName,
        string expectedProfileName,
        string expectedPassword,
        string response)
    {
        await using var pipe = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        await pipe.WaitForConnectionAsync();
        using var reader = new StreamReader(pipe, ControlPipeEncoding);
        var writer = new StreamWriter(pipe, ControlPipeEncoding)
        {
            AutoFlush = true,
        };

        var request = await reader.ReadLineAsync();
        request.Should().Be("login " + expectedProfileName);
        await writer.WriteLineAsync("password-required");
        await writer.FlushAsync();

        var password = await reader.ReadLineAsync();
        password.Should().Be(expectedPassword);
        await writer.WriteLineAsync(response);
        await writer.FlushAsync();
    }

    private static async Task RunSecretPutPipeAsync(
        string pipeName,
        string expectedSecretName,
        int expectedTtlSeconds,
        byte[] expectedSecretBytes)
    {
        await using var pipe = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        await pipe.WaitForConnectionAsync();
        using var reader = new StreamReader(pipe, ControlPipeEncoding);
        var writer = new StreamWriter(pipe, ControlPipeEncoding)
        {
            AutoFlush = true,
        };

        var requestLine = await reader.ReadLineAsync();
        requestLine.Should().NotContain(Convert.ToBase64String(expectedSecretBytes));
        requestLine.Should().StartWith("secret-put ");
        var requestJson = requestLine!["secret-put ".Length..];
        using var requestDocument = JsonDocument.Parse(requestJson);
        requestDocument.RootElement.GetProperty("Name").GetString().Should().Be(expectedSecretName);
        requestDocument.RootElement.GetProperty("TtlSeconds").GetInt32().Should().Be(expectedTtlSeconds);

        await writer.WriteLineAsync("secret-required");
        await writer.FlushAsync();

        var payloadBase64 = await reader.ReadLineAsync();
        Convert.FromBase64String(payloadBase64!).Should().Equal(expectedSecretBytes);

        var now = DateTimeOffset.UtcNow;
        await writer.WriteLineAsync(JsonSerializer.Serialize(new KelpieSecretInfo(
            expectedSecretName,
            expectedSecretBytes.Length,
            now,
            now.AddSeconds(expectedTtlSeconds))));
        await writer.FlushAsync();
    }

    private static async Task RunEnvPutPipeAsync(
        string pipeName,
        string expectedProfileName,
        string expectedKey,
        string expectedValue)
    {
        await using var pipe = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        await pipe.WaitForConnectionAsync();
        using var reader = new StreamReader(pipe, ControlPipeEncoding);
        var writer = new StreamWriter(pipe, ControlPipeEncoding)
        {
            AutoFlush = true,
        };

        var requestLine = await reader.ReadLineAsync();
        requestLine.Should().NotContain(expectedValue);
        requestLine.Should().StartWith("env-put ");
        var requestJson = requestLine!["env-put ".Length..];
        using var requestDocument = JsonDocument.Parse(requestJson);
        requestDocument.RootElement.GetProperty("ProfileName").GetString().Should().Be(expectedProfileName);
        requestDocument.RootElement.GetProperty("Key").GetString().Should().Be(expectedKey);

        await writer.WriteLineAsync("env-value-required");
        await writer.FlushAsync();

        var payloadBase64 = await reader.ReadLineAsync();
        Encoding.UTF8.GetString(Convert.FromBase64String(payloadBase64!)).Should().Be(expectedValue);

        await writer.WriteLineAsync(JsonSerializer.Serialize(new KelpieEnvironmentOverrideInfo(
            expectedProfileName,
            expectedKey,
            expectedValue.Length,
            DateTimeOffset.UtcNow)));
        await writer.FlushAsync();
    }

    private sealed record EnvListRequest(string? ProfileName);
}

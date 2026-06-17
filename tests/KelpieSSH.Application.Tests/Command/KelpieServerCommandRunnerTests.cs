using System.IO.Pipes;
using FluentAssertions;
using Kelpie.Core;
using KelpieServerCommand;

namespace KelpieSSH.Application.Tests.Command;

public sealed class KelpieServerCommandRunnerTests
{
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

    private static KelpieMcpServerOptions CreateOptions()
    {
        return new KelpieMcpServerOptions
        {
            ControlPipeName = "KelpieTest." + Guid.NewGuid().ToString("N"),
            ServerPort = 45432,
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
        using var reader = new StreamReader(pipe);
        var writer = new StreamWriter(pipe)
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
        using var reader = new StreamReader(pipe);
        var writer = new StreamWriter(pipe)
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
}

using System.IO.Pipes;
using System.Security.Principal;
using System.Text.Json;
using FluentAssertions;
using KelpieMCPServer;
using KelpieSSH.Application.Ssh;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace KelpieSSH.Application.Tests.McpServer;

public sealed class NamedPipeShutdownServiceTests
{
    [Fact]
    public async Task ExecuteAsync_ProfileReloadApproved_ShouldRequireExplicitPrivilegeExpansionApproval()
    {
        var directory = Path.Combine(Path.GetTempPath(), "kelpie-pipe-auth-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var profilePath = Path.Combine(directory, "vps01.json");
        var trustStorePath = Path.Combine(directory, "mcp_trusted_store.dat");
        File.WriteAllText(profilePath, CreateAuthorizationProfileJson("ReadOnly"));
        var catalog = new ReloadingSshConnectionProfileCatalog(directory, trustStorePath, []);
        File.WriteAllText(profilePath, CreateAuthorizationProfileJson("Expert"));
        var lifetime = new FakeHostApplicationLifetime();
        var pipeName = "KelpieTest." + Guid.NewGuid().ToString("N");
        using var service = new NamedPipeShutdownService(
            lifetime,
            NullLogger<NamedPipeShutdownService>.Instance,
            new KelpieServerControlOptions(pipeName),
            catalog,
            new InMemorySshPasswordSessionStore(),
            CreateSshCommandService());

        await service.StartAsync(CancellationToken.None);
        var blockedJson = await SendControlCommandAsync(pipeName, "profile-reload vps01");
        var approvedJson = await SendControlCommandAsync(pipeName, "profile-reload-approved vps01");
        await service.StopAsync(CancellationToken.None);

        var blocked = JsonSerializer.Deserialize<SshProfileTrustOperationResult>(blockedJson!);
        var approved = JsonSerializer.Deserialize<SshProfileTrustOperationResult>(approvedJson!);
        blocked!.Status.Should().Be("profile-privilege-expansion");
        blocked.ChangedFields.Should().Contain("Mode");
        approved!.Success.Should().BeTrue();
        approved.AuthorizationChange.Should().Be(SshProfileAuthorizationChangeKind.PrivilegeExpansion);
        catalog.TryGet("vps01", out var active).Should().BeTrue();
        active.Mode.Should().Be(KelpiePolicyMode.Expert);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRespondToPingAndStop()
    {
        var lifetime = new FakeHostApplicationLifetime();
        var pipeName = "KelpieTest." + Guid.NewGuid().ToString("N");
        using var service = new NamedPipeShutdownService(
            lifetime,
            NullLogger<NamedPipeShutdownService>.Instance,
            new KelpieServerControlOptions(pipeName),
            CreateProfileCatalog(),
            new InMemorySshPasswordSessionStore(),
            CreateSshCommandService());

        await service.StartAsync(CancellationToken.None);

        var pingResponse = await SendControlCommandAsync(pipeName, "ping");
        var stopResponse = await SendControlCommandAsync(pipeName, "stop");
        await WaitForStopRequestedAsync(lifetime);

        await service.StopAsync(CancellationToken.None);

        pingResponse.Should().Be("pong;windowsService=false");
        stopResponse.Should().Be("stopping");
        lifetime.StopRequested.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReportWindowsServiceModeInPingResponse()
    {
        var lifetime = new FakeHostApplicationLifetime();
        var pipeName = "KelpieTest." + Guid.NewGuid().ToString("N");
        using var service = new NamedPipeShutdownService(
            lifetime,
            NullLogger<NamedPipeShutdownService>.Instance,
            new KelpieServerControlOptions(pipeName),
            CreateProfileCatalog(),
            new InMemorySshPasswordSessionStore(),
            CreateSshCommandService(),
            () => true);

        await service.StartAsync(CancellationToken.None);

        var pingResponse = await SendControlCommandAsync(pipeName, "ping");

        await service.StopAsync(CancellationToken.None);

        pingResponse.Should().Be("pong;windowsService=true");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRespondUnknownForUnknownCommand()
    {
        var lifetime = new FakeHostApplicationLifetime();
        var pipeName = "KelpieTest." + Guid.NewGuid().ToString("N");
        using var service = new NamedPipeShutdownService(
            lifetime,
            NullLogger<NamedPipeShutdownService>.Instance,
            new KelpieServerControlOptions(pipeName),
            CreateProfileCatalog(),
            new InMemorySshPasswordSessionStore(),
            CreateSshCommandService());

        await service.StartAsync(CancellationToken.None);

        var response = await SendControlCommandAsync(pipeName, "wat");

        await service.StopAsync(CancellationToken.None);

        response.Should().Be("unknown");
        lifetime.StopRequested.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStorePasswordForLoginCommand()
    {
        var lifetime = new FakeHostApplicationLifetime();
        var pipeName = "KelpieTest." + Guid.NewGuid().ToString("N");
        var passwordStore = new InMemorySshPasswordSessionStore();
        using var service = new NamedPipeShutdownService(
            lifetime,
            NullLogger<NamedPipeShutdownService>.Instance,
            new KelpieServerControlOptions(pipeName),
            CreateProfileCatalog(),
            passwordStore,
            CreateSshCommandService());

        await service.StartAsync(CancellationToken.None);

        var response = await SendLoginCommandAsync(pipeName, "vps01", "secret");

        await service.StopAsync(CancellationToken.None);

        response.Should().Be("logged-in");
        var password = await passwordStore.GetPasswordAsync("kelpie:vps01");
        password.Should().Be("secret");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldClearPasswordForLogoutCommand()
    {
        var lifetime = new FakeHostApplicationLifetime();
        var pipeName = "KelpieTest." + Guid.NewGuid().ToString("N");
        var passwordStore = new InMemorySshPasswordSessionStore();
        passwordStore.SetPassword("kelpie:vps01", "secret");
        using var service = new NamedPipeShutdownService(
            lifetime,
            NullLogger<NamedPipeShutdownService>.Instance,
            new KelpieServerControlOptions(pipeName),
            CreateProfileCatalog(),
            passwordStore,
            CreateSshCommandService());

        await service.StartAsync(CancellationToken.None);

        var response = await SendControlCommandAsync(pipeName, "logout vps01");

        await service.StopAsync(CancellationToken.None);

        response.Should().Be("logged-out");
        var password = await passwordStore.GetPasswordAsync("kelpie:vps01");
        password.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStoreEnvironmentOverrideForEnvPutCommand()
    {
        var lifetime = new FakeHostApplicationLifetime();
        var pipeName = "KelpieTest." + Guid.NewGuid().ToString("N");
        var environmentStore = new InMemoryKelpieEnvironmentOverrideStore();
        using var service = new NamedPipeShutdownService(
            lifetime,
            NullLogger<NamedPipeShutdownService>.Instance,
            new KelpieServerControlOptions(pipeName),
            CreateProfileCatalog(allowEnvironmentOverride: true),
            new InMemorySshPasswordSessionStore(),
            CreateSshCommandService(),
            environmentOverrideStore: environmentStore);

        await service.StartAsync(CancellationToken.None);

        var response = await SendEnvPutCommandAsync(pipeName, "vps01", "APP_ENV", "production");

        await service.StopAsync(CancellationToken.None);

        var info = JsonSerializer.Deserialize<KelpieEnvironmentOverrideInfo>(response!);
        info.Should().NotBeNull();
        info!.ProfileName.Should().Be("vps01");
        info.Key.Should().Be("APP_ENV");
        info.ValueLength.Should().Be("production".Length);
        environmentStore.GetValues("vps01").Should().Contain("APP_ENV", "production");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRejectEnvPutWhenKeyIsNotAllowed()
    {
        var lifetime = new FakeHostApplicationLifetime();
        var pipeName = "KelpieTest." + Guid.NewGuid().ToString("N");
        using var service = new NamedPipeShutdownService(
            lifetime,
            NullLogger<NamedPipeShutdownService>.Instance,
            new KelpieServerControlOptions(pipeName),
            CreateProfileCatalog(allowEnvironmentOverride: true),
            new InMemorySshPasswordSessionStore(),
            CreateSshCommandService());

        await service.StartAsync(CancellationToken.None);

        var response = await SendEnvPutCommandAsync(pipeName, "vps01", "UNLISTED", "production");

        await service.StopAsync(CancellationToken.None);

        response.Should().Be("env-key-not-allowed");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnSessions()
    {
        var lifetime = new FakeHostApplicationLifetime();
        var pipeName = "KelpieTest." + Guid.NewGuid().ToString("N");
        var passwordStore = new InMemorySshPasswordSessionStore();
        using var service = new NamedPipeShutdownService(
            lifetime,
            NullLogger<NamedPipeShutdownService>.Instance,
            new KelpieServerControlOptions(pipeName),
            CreateProfileCatalog(),
            passwordStore,
            CreateSshCommandService());

        await service.StartAsync(CancellationToken.None);

        var loginResponse = await SendLoginCommandAsync(pipeName, "vps01", "secret");
        var response = await SendControlCommandAsync(pipeName, "sessions");

        await service.StopAsync(CancellationToken.None);

        loginResponse.Should().Be("logged-in");
        var sessions = JsonSerializer.Deserialize<SshSessionInfo[]>(response!);
        var session = sessions.Should().ContainSingle().Subject;
        session.Handle.Should().StartWith("ssh-");
        session.ProfileName.Should().Be("vps01");
        session.Kind.Should().Be("password");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldKillSessionByHandle()
    {
        var lifetime = new FakeHostApplicationLifetime();
        var pipeName = "KelpieTest." + Guid.NewGuid().ToString("N");
        var passwordStore = new InMemorySshPasswordSessionStore();
        using var service = new NamedPipeShutdownService(
            lifetime,
            NullLogger<NamedPipeShutdownService>.Instance,
            new KelpieServerControlOptions(pipeName),
            CreateProfileCatalog(),
            passwordStore,
            CreateSshCommandService());

        await service.StartAsync(CancellationToken.None);

        var loginResponse = await SendLoginCommandAsync(pipeName, "vps01", "secret");
        var sessionHandle = passwordStore.ListSessions().Should().ContainSingle().Subject.Handle;
        var killResponse = await SendControlCommandAsync(pipeName, "kill " + sessionHandle);

        await service.StopAsync(CancellationToken.None);

        loginResponse.Should().Be("logged-in");
        killResponse.Should().Be("killed");
        passwordStore.ListSessions().Should().BeEmpty();
        var password = await passwordStore.GetPasswordAsync("kelpie:vps01");
        password.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldOpenInteractiveSession()
    {
        var lifetime = new FakeHostApplicationLifetime();
        var pipeName = "KelpieTest." + Guid.NewGuid().ToString("N");
        var passwordStore = new InMemorySshPasswordSessionStore();
        using var service = new NamedPipeShutdownService(
            lifetime,
            NullLogger<NamedPipeShutdownService>.Instance,
            new KelpieServerControlOptions(pipeName),
            CreateProfileCatalog(),
            passwordStore,
            CreateSshCommandService());

        await service.StartAsync(CancellationToken.None);

        var response = await SendControlCommandAsync(pipeName, "open-session vps01");

        await service.StopAsync(CancellationToken.None);

        var session = JsonSerializer.Deserialize<SshSessionInfo>(response!);
        session.Should().NotBeNull();
        session!.Handle.Should().StartWith("ssh-");
        session.ProfileName.Should().Be("vps01");
        session.Kind.Should().Be("interactive");
        passwordStore.ListSessions().Should().ContainSingle()
            .Which.Handle.Should().Be(session.Handle);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRejectOpenInteractiveSessionForUnknownProfile()
    {
        var lifetime = new FakeHostApplicationLifetime();
        var pipeName = "KelpieTest." + Guid.NewGuid().ToString("N");
        using var service = new NamedPipeShutdownService(
            lifetime,
            NullLogger<NamedPipeShutdownService>.Instance,
            new KelpieServerControlOptions(pipeName),
            CreateProfileCatalog(),
            new InMemorySshPasswordSessionStore(),
            CreateSshCommandService());

        await service.StartAsync(CancellationToken.None);

        var response = await SendControlCommandAsync(pipeName, "open-session missing");

        await service.StopAsync(CancellationToken.None);

        response.Should().Be("profile-not-found");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCloseInteractiveSessionByHandle()
    {
        var lifetime = new FakeHostApplicationLifetime();
        var pipeName = "KelpieTest." + Guid.NewGuid().ToString("N");
        var passwordStore = new InMemorySshPasswordSessionStore();
        using var service = new NamedPipeShutdownService(
            lifetime,
            NullLogger<NamedPipeShutdownService>.Instance,
            new KelpieServerControlOptions(pipeName),
            CreateProfileCatalog(),
            passwordStore,
            CreateSshCommandService());

        await service.StartAsync(CancellationToken.None);

        var openResponse = await SendControlCommandAsync(pipeName, "open-session vps01");
        var session = JsonSerializer.Deserialize<SshSessionInfo>(openResponse!)!;
        var closeResponse = await SendControlCommandAsync(pipeName, "close-session " + session.Handle);

        await service.StopAsync(CancellationToken.None);

        closeResponse.Should().Be("killed");
        passwordStore.ListSessions().Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSendCommandToInteractiveSession()
    {
        var lifetime = new FakeHostApplicationLifetime();
        var pipeName = "KelpieTest." + Guid.NewGuid().ToString("N");
        var passwordStore = new InMemorySshPasswordSessionStore();
        using var service = new NamedPipeShutdownService(
            lifetime,
            NullLogger<NamedPipeShutdownService>.Instance,
            new KelpieServerControlOptions(pipeName),
            CreateProfileCatalog(),
            passwordStore,
            CreateSshCommandService());

        await service.StartAsync(CancellationToken.None);

        var openResponse = await SendControlCommandAsync(pipeName, "open-session vps01");
        var session = JsonSerializer.Deserialize<SshSessionInfo>(openResponse!)!;
        var response = await SendControlCommandWithBodyAsync(
            pipeName,
            "send-command " + session.Handle,
            """{"CommandName":"get_system_info","Arguments":{},"TimeoutSeconds":30}""");

        await service.StopAsync(CancellationToken.None);

        using var document = JsonDocument.Parse(response!);
        var root = document.RootElement;
        root.GetProperty("Handle").GetString().Should().Be(session.Handle);
        root.GetProperty("ProfileName").GetString().Should().Be("vps01");
        root.GetProperty("CommandName").GetString().Should().Be("get_system_info");
        root.GetProperty("CommandText").GetString().Should().NotBeNullOrWhiteSpace();
        root.GetProperty("ExitCode").GetInt32().Should().Be(0);
        root.GetProperty("StandardOutput").GetString().Should().Be("fake output");
        root.GetProperty("StandardError").GetString().Should().BeEmpty();
        root.GetProperty("TimedOut").GetBoolean().Should().BeFalse();
        root.GetProperty("PolicyError").GetString().Should().BeEmpty();
        root.GetProperty("Error").GetString().Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSendRawCommandToInteractiveSession()
    {
        var lifetime = new FakeHostApplicationLifetime();
        var pipeName = "KelpieTest." + Guid.NewGuid().ToString("N");
        var passwordStore = new InMemorySshPasswordSessionStore();
        using var service = new NamedPipeShutdownService(
            lifetime,
            NullLogger<NamedPipeShutdownService>.Instance,
            new KelpieServerControlOptions(pipeName),
            CreateProfileCatalog(),
            passwordStore,
            CreateSshCommandService());

        await service.StartAsync(CancellationToken.None);

        var openResponse = await SendControlCommandAsync(pipeName, "open-session vps01");
        var session = JsonSerializer.Deserialize<SshSessionInfo>(openResponse!)!;
        var response = await SendControlCommandWithBodyAsync(
            pipeName,
            "send-command " + session.Handle,
            """{"CommandName":"pwd","Arguments":{},"TimeoutSeconds":30}""");

        await service.StopAsync(CancellationToken.None);

        using var document = JsonDocument.Parse(response!);
        var root = document.RootElement;
        root.GetProperty("Handle").GetString().Should().Be(session.Handle);
        root.GetProperty("ProfileName").GetString().Should().Be("vps01");
        root.GetProperty("CommandName").GetString().Should().Be("raw_shell");
        root.GetProperty("CommandText").GetString().Should().Be("pwd");
        root.GetProperty("ExitCode").GetInt32().Should().Be(0);
        root.GetProperty("StandardOutput").GetString().Should().Be("fake output");
        root.GetProperty("PolicyError").GetString().Should().BeEmpty();
        root.GetProperty("Error").GetString().Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRejectSendCommandForMissingSession()
    {
        var lifetime = new FakeHostApplicationLifetime();
        var pipeName = "KelpieTest." + Guid.NewGuid().ToString("N");
        using var service = new NamedPipeShutdownService(
            lifetime,
            NullLogger<NamedPipeShutdownService>.Instance,
            new KelpieServerControlOptions(pipeName),
            CreateProfileCatalog(),
            new InMemorySshPasswordSessionStore(),
            CreateSshCommandService());

        await service.StartAsync(CancellationToken.None);

        var response = await SendControlCommandWithBodyAsync(
            pipeName,
            "send-command ssh-missing",
            """{"CommandName":"get_system_info","Arguments":{},"TimeoutSeconds":30}""");

        await service.StopAsync(CancellationToken.None);

        using var document = JsonDocument.Parse(response!);
        var root = document.RootElement;
        root.GetProperty("Handle").GetString().Should().Be("ssh-missing");
        root.GetProperty("Error").GetString().Should().Be("session-not-found");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRejectSendCommandForPasswordSession()
    {
        var lifetime = new FakeHostApplicationLifetime();
        var pipeName = "KelpieTest." + Guid.NewGuid().ToString("N");
        var passwordStore = new InMemorySshPasswordSessionStore();
        using var service = new NamedPipeShutdownService(
            lifetime,
            NullLogger<NamedPipeShutdownService>.Instance,
            new KelpieServerControlOptions(pipeName),
            CreateProfileCatalog(),
            passwordStore,
            CreateSshCommandService());

        await service.StartAsync(CancellationToken.None);

        await SendLoginCommandAsync(pipeName, "vps01", "secret");
        var session = passwordStore.ListSessions().Should().ContainSingle().Subject;
        var response = await SendControlCommandWithBodyAsync(
            pipeName,
            "send-command " + session.Handle,
            """{"CommandName":"get_system_info","Arguments":{},"TimeoutSeconds":30}""");

        await service.StopAsync(CancellationToken.None);

        using var document = JsonDocument.Parse(response!);
        var root = document.RootElement;
        root.GetProperty("Handle").GetString().Should().Be(session.Handle);
        root.GetProperty("ProfileName").GetString().Should().Be("vps01");
        root.GetProperty("Error").GetString().Should().Be("session-is-not-interactive");
    }

    [Fact]
    public async Task ExecuteAsync_ConcurrentPingClients_ShouldAllReceiveResponses()
    {
        var lifetime = new FakeHostApplicationLifetime();
        var pipeName = "KelpieTest." + Guid.NewGuid().ToString("N");
        using var service = new NamedPipeShutdownService(
            lifetime,
            NullLogger<NamedPipeShutdownService>.Instance,
            new KelpieServerControlOptions(pipeName),
            CreateProfileCatalog(),
            new InMemorySshPasswordSessionStore(),
            CreateSshCommandService());

        await service.StartAsync(CancellationToken.None);

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 8)
                .Select(_ => SendControlCommandAsync(pipeName, "ping")));

        await service.StopAsync(CancellationToken.None);

        responses.Should().OnlyContain(response => response == "pong;windowsService=false");
        lifetime.StopRequested.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ClientDisconnectsBeforeCommand_ShouldAcceptNextClient()
    {
        var lifetime = new FakeHostApplicationLifetime();
        var pipeName = "KelpieTest." + Guid.NewGuid().ToString("N");
        using var service = new NamedPipeShutdownService(
            lifetime,
            NullLogger<NamedPipeShutdownService>.Instance,
            new KelpieServerControlOptions(pipeName),
            CreateProfileCatalog(),
            new InMemorySshPasswordSessionStore(),
            CreateSshCommandService());

        await service.StartAsync(CancellationToken.None);

        await using (var abandonedClient = new NamedPipeClientStream(
                         ".",
                         pipeName,
                         PipeDirection.InOut,
                         PipeOptions.Asynchronous,
                         TokenImpersonationLevel.Identification))
        {
            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await abandonedClient.ConnectAsync(cancellationTokenSource.Token);
        }

        var response = await SendControlCommandAsync(pipeName, "ping");

        await service.StopAsync(CancellationToken.None);

        response.Should().Be("pong;windowsService=false");
        lifetime.StopRequested.Should().BeFalse();
    }

    private static async Task WaitForStopRequestedAsync(FakeHostApplicationLifetime lifetime)
    {
        for (var index = 0; index < 30; index++)
        {
            if (lifetime.StopRequested)
            {
                return;
            }

            await Task.Delay(10);
        }
    }

    private static async Task<string?> SendControlCommandAsync(string pipeName, string command)
    {
        await using var pipe = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous,
            TokenImpersonationLevel.Identification);

        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await pipe.ConnectAsync(cancellationTokenSource.Token);

        var writer = new StreamWriter(pipe)
        {
            AutoFlush = true,
        };
        await writer.WriteLineAsync(command);
        await writer.FlushAsync(cancellationTokenSource.Token);

        using var reader = new StreamReader(pipe);
        return await reader.ReadLineAsync(cancellationTokenSource.Token);
    }

    private static async Task<string?> SendControlCommandWithBodyAsync(string pipeName, string command, string body)
    {
        await using var pipe = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous,
            TokenImpersonationLevel.Identification);

        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await pipe.ConnectAsync(cancellationTokenSource.Token);

        var writer = new StreamWriter(pipe)
        {
            AutoFlush = true,
        };
        await writer.WriteLineAsync(command);
        await writer.WriteLineAsync(body);
        await writer.FlushAsync(cancellationTokenSource.Token);

        using var reader = new StreamReader(pipe);
        return await reader.ReadLineAsync(cancellationTokenSource.Token);
    }

    private static async Task<string?> SendLoginCommandAsync(string pipeName, string profileName, string password)
    {
        await using var pipe = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous,
            TokenImpersonationLevel.Identification);

        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await pipe.ConnectAsync(cancellationTokenSource.Token);

        var writer = new StreamWriter(pipe)
        {
            AutoFlush = true,
        };
        var reader = new StreamReader(pipe);

        await writer.WriteLineAsync("login " + profileName);
        await writer.FlushAsync(cancellationTokenSource.Token);
        var firstResponse = await reader.ReadLineAsync(cancellationTokenSource.Token);
        firstResponse.Should().Be("password-required");

        await writer.WriteLineAsync(password);
        await writer.FlushAsync(cancellationTokenSource.Token);
        return await reader.ReadLineAsync(cancellationTokenSource.Token);
    }

    private static async Task<string?> SendEnvPutCommandAsync(
        string pipeName,
        string profileName,
        string key,
        string value)
    {
        await using var pipe = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous,
            TokenImpersonationLevel.Identification);

        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await pipe.ConnectAsync(cancellationTokenSource.Token);

        var writer = new StreamWriter(pipe)
        {
            AutoFlush = true,
        };
        var reader = new StreamReader(pipe);

        await writer.WriteLineAsync("env-put " + JsonSerializer.Serialize(new EnvPutRequest(profileName, key)));
        await writer.FlushAsync(cancellationTokenSource.Token);
        var firstResponse = await reader.ReadLineAsync(cancellationTokenSource.Token);
        if (!string.Equals(firstResponse, "env-value-required", StringComparison.OrdinalIgnoreCase))
        {
            return firstResponse;
        }

        await writer.WriteLineAsync(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value)));
        await writer.FlushAsync(cancellationTokenSource.Token);
        return await reader.ReadLineAsync(cancellationTokenSource.Token);
    }

    private static SshConnectionProfileCatalog CreateProfileCatalog(bool allowEnvironmentOverride = false)
    {
        return new SshConnectionProfileCatalog(
        [
            new SshConnectionProfile
            {
                Name = "vps01",
                Host = "example.invalid",
                UserName = "deploy",
                AuthenticationMethod = "password",
                PasswordSecretName = "kelpie:vps01",
                OsFamily = "debian",
                PackageManager = "apt",
                Capabilities = allowEnvironmentOverride
                    ? PolicySet.FromNames([KelpiePolicyNames.AllowSetEnvironmentValues])
                    : PolicySet.Empty,
                EnvironmentValues = allowEnvironmentOverride
                    ? [new EnvironmentValueRule("APP_ENV", EnvironmentValueAccess.SetCommon)]
                    : [],
            },
        ]);
    }

    private static string CreateAuthorizationProfileJson(string mode)
    {
        return $$"""
        {
          "Host": { "Address": "example.com", "Port": 22 },
          "Auth": { "Method": "privateKey", "PrivateKeyFile": "id_ed25519" },
          "DefaultUser": "deploy",
          "Users": { "deploy": "{{mode}}" },
          "Platform": { "OsFamily": "debian" }
        }
        """;
    }

    private static SshCommandService CreateSshCommandService()
    {
        return new SshCommandService(
            CommandProcessingProviderCatalog.CreateDefault(),
            new FakeSshCommandRunner());
    }

    private sealed class FakeSshCommandRunner : ISshCommandRunner
    {
        public Task<SshCommandResult> ExecuteAsync(
            SshCommandRequest request,
            CancellationToken cancellationToken = default)
        {
            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(new SshCommandResult(
                request.CommandName,
                request.CommandText,
                0,
                "fake output",
                string.Empty,
                now,
                now,
                false));
        }
    }

    private sealed class FakeHostApplicationLifetime : IHostApplicationLifetime
    {
        private readonly CancellationTokenSource _applicationStarted = new();
        private readonly CancellationTokenSource _applicationStopping = new();
        private readonly CancellationTokenSource _applicationStopped = new();

        public CancellationToken ApplicationStarted => _applicationStarted.Token;

        public CancellationToken ApplicationStopping => _applicationStopping.Token;

        public CancellationToken ApplicationStopped => _applicationStopped.Token;

        public bool StopRequested { get; private set; }

        public void StopApplication()
        {
            StopRequested = true;
            _applicationStopping.Cancel();
            _applicationStopped.Cancel();
        }
    }

    private sealed record EnvPutRequest(string ProfileName, string Key);
}

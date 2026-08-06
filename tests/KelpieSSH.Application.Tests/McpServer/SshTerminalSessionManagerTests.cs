using FluentAssertions;
using KelpieMCPServer;
using KelpieSSH.Application.Ssh;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace KelpieSSH.Application.Tests.McpServer;

public sealed class SshTerminalSessionManagerTests
{
    [Fact]
    public async Task CloseAsync_AfterLastSession_ShouldAllowRepeatedOpenAndClose()
    {
        var profile = new SshConnectionProfile
        {
            Name = "vps01",
            Host = "example.invalid",
            UserName = "tester",
            PrivateKeyPath = "test-key",
            OsFamily = "debian",
            PackageManager = "apt",
        };
        var factory = new FakeInteractiveShellSessionFactory();
        await using var manager = new SshTerminalSessionManager(
            new SshConnectionProfileCatalog([profile]),
            factory);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var opened = await manager.OpenAsync("vps01");
            var closed = await manager.CloseAsync(opened.Handle);

            closed.Closed.Should().BeTrue();
            closed.Error.Should().BeEmpty();
        }

        factory.CreatedSessions.Should().HaveCount(3);
        factory.CreatedSessions.Should().OnlyContain(session => session.IsDisposed);
    }

    [Fact]
    public async Task CloseAsync_AfterLastSession_ShouldKeepHttpHostRunning()
    {
        var profile = new SshConnectionProfile
        {
            Name = "vps01",
            Host = "example.invalid",
            UserName = "tester",
            PrivateKeyPath = "test-key",
            OsFamily = "debian",
            PackageManager = "apt",
        };
        await using var manager = new SshTerminalSessionManager(
            new SshConnectionProfileCatalog([profile]),
            new FakeInteractiveShellSessionFactory());
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var app = builder.Build();
        app.MapGet("/health", () => Results.Ok());
        await app.StartAsync();
        var addresses = app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>();
        var address = addresses!.Addresses.Single();
        using var client = new HttpClient { BaseAddress = new Uri(address) };

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var opened = await manager.OpenAsync("vps01");
            await manager.CloseAsync(opened.Handle);
            var response = await client.GetAsync("/health");

            response.EnsureSuccessStatusCode();
            app.Lifetime.ApplicationStopping.IsCancellationRequested.Should().BeFalse();
        }

        await app.StopAsync();
    }

    private sealed class FakeInteractiveShellSessionFactory : IInteractiveShellSessionFactory
    {
        public List<FakeInteractiveShellSession> CreatedSessions { get; } = [];

        public IInteractiveShellSession Create(SshConnectionProfile profile)
        {
            var session = new FakeInteractiveShellSession();
            CreatedSessions.Add(session);
            return session;
        }
    }

    private sealed class FakeInteractiveShellSession : IInteractiveShellSession
    {
        public bool IsConnected { get; private set; }

        public bool IsDisposed { get; private set; }

        public Task<string> ConnectAsync(
            int columns,
            int rows,
            int pixelWidth,
            int pixelHeight,
            CancellationToken cancellationToken = default)
        {
            IsConnected = true;
            return Task.FromResult(string.Empty);
        }

        public Task WriteAsync(string input, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<string> ReadAvailableOutputAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(string.Empty);
        }

        public ValueTask DisposeAsync()
        {
            IsConnected = false;
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }
}

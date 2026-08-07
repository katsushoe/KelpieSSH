using FluentAssertions;
using KelpieMCPServer;
using Microsoft.Extensions.Logging.Abstractions;

namespace KelpieSSH.Application.Tests.McpServer;

[Collection(NamedPipeTestCollection.Name)]
public sealed class ControlPipeInstanceGuardTests
{
    [Fact]
    public async Task StartAsync_WhenMutexIsOwned_ShouldRejectSecondInstance()
    {
        var options = new KelpieServerControlOptions("KelpieTest." + Guid.NewGuid().ToString("N"));
        using var first = CreateGuard(options);
        using var second = CreateGuard(options);
        await first.StartAsync(CancellationToken.None);

        var action = () => second.StartAsync(CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already running*");
        await first.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_AfterOwnerStops_ShouldAcquireMutexAgain()
    {
        var options = new KelpieServerControlOptions("KelpieTest." + Guid.NewGuid().ToString("N"));
        using (var first = CreateGuard(options))
        {
            await first.StartAsync(CancellationToken.None);
            await first.StopAsync(CancellationToken.None);
        }

        using var next = CreateGuard(options);
        var action = () => next.StartAsync(CancellationToken.None);

        await action.Should().NotThrowAsync();
        await next.StopAsync(CancellationToken.None);
    }

    private static ControlPipeInstanceGuard CreateGuard(KelpieServerControlOptions options)
    {
        return new ControlPipeInstanceGuard(
            options,
            NullLogger<ControlPipeInstanceGuard>.Instance);
    }
}

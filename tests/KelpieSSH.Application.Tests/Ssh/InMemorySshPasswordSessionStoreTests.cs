using FluentAssertions;
using KelpieSSH.Application.Ssh;

namespace KelpieSSH.Application.Tests.Ssh;

public sealed class InMemorySshPasswordSessionStoreTests
{
    [Fact]
    public async Task GetPasswordAsync_ShouldReturnStoredPassword()
    {
        var store = new InMemorySshPasswordSessionStore();

        store.SetPasswordSession("vps01", "kelpie:vps01", "secret");

        var password = await store.GetPasswordAsync("KELPIE:VPS01");

        password.Should().Be("secret");
        var session = store.ListSessions().Should().ContainSingle().Subject;
        session.Handle.Should().StartWith("ssh-");
        session.ProfileName.Should().Be("vps01");
        session.SecretName.Should().Be("kelpie:vps01");
        session.Kind.Should().Be("password");
        session.StartedAtUtc.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ClearPassword_ShouldRemoveStoredPassword()
    {
        var store = new InMemorySshPasswordSessionStore();
        store.SetPassword("kelpie:vps01", "secret");

        var removed = store.ClearPassword("kelpie:vps01");

        removed.Should().BeTrue();
        var password = await store.GetPasswordAsync("kelpie:vps01");
        password.Should().BeNull();
        store.ListSessions().Should().BeEmpty();
    }

    [Fact]
    public async Task ClearSession_ShouldRemoveStoredPasswordByHandle()
    {
        var store = new InMemorySshPasswordSessionStore();
        store.SetPasswordSession("vps01", "kelpie:vps01", "secret");
        var handle = store.ListSessions().Should().ContainSingle().Subject.Handle;

        var removed = store.ClearSession(handle);

        removed.Should().BeTrue();
        var password = await store.GetPasswordAsync("kelpie:vps01");
        password.Should().BeNull();
        store.ListSessions().Should().BeEmpty();
    }

    [Fact]
    public void OpenInteractiveSession_ShouldAddInteractiveSession()
    {
        var store = new InMemorySshPasswordSessionStore();

        var opened = store.OpenInteractiveSession("vps01");

        opened.Handle.Should().StartWith("ssh-");
        opened.ProfileName.Should().Be("vps01");
        opened.SecretName.Should().Be(opened.Handle);
        opened.Kind.Should().Be("interactive");
        opened.StartedAtUtc.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        store.ListSessions().Should().ContainSingle().Which.Should().Be(opened);
    }

    [Fact]
    public void ClearSession_ShouldRemoveInteractiveSessionByHandle()
    {
        var store = new InMemorySshPasswordSessionStore();
        var opened = store.OpenInteractiveSession("vps01");

        var removed = store.ClearSession(opened.Handle);

        removed.Should().BeTrue();
        store.ListSessions().Should().BeEmpty();
    }

    [Fact]
    public void SetPassword_ShouldRejectEmptyPassword()
    {
        var store = new InMemorySshPasswordSessionStore();

        var action = () => store.SetPassword("kelpie:vps01", string.Empty);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("SSH password is required.");
    }
}

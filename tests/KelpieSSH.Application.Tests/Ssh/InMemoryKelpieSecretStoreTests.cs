using System.Text;
using FluentAssertions;
using KelpieSSH.Application.Ssh;

namespace KelpieSSH.Application.Tests.Ssh;

public sealed class InMemoryKelpieSecretStoreTests
{
    [Fact]
    public void Put_ShouldStoreMetadataWithoutReturningContentInList()
    {
        var store = new InMemoryKelpieSecretStore();
        var content = Encoding.UTF8.GetBytes("TOKEN=secret\n");

        var info = store.Put("prod-web-env", content, TimeSpan.FromMinutes(10));

        info.Name.Should().Be("prod-web-env");
        info.Size.Should().Be(content.Length);
        store.List().Should().ContainSingle().Which.Name.Should().Be("prod-web-env");
    }

    [Fact]
    public void TryGetContentBase64_ShouldReturnSecretContentOnlyByName()
    {
        var store = new InMemoryKelpieSecretStore();
        store.Put("prod-web-env", Encoding.UTF8.GetBytes("TOKEN=secret\n"), TimeSpan.FromMinutes(10));

        var found = store.TryGetContentBase64("prod-web-env", out var contentBase64, out var info);

        found.Should().BeTrue();
        Encoding.UTF8.GetString(Convert.FromBase64String(contentBase64)).Should().Be("TOKEN=secret\n");
        info!.Name.Should().Be("prod-web-env");
    }

    [Fact]
    public void Forget_ShouldRemoveSecret()
    {
        var store = new InMemoryKelpieSecretStore();
        store.Put("prod-web-env", Encoding.UTF8.GetBytes("TOKEN=secret\n"), TimeSpan.FromMinutes(10));

        store.Forget("prod-web-env").Should().BeTrue();
        store.TryGetContentBase64("prod-web-env", out _, out _).Should().BeFalse();
    }
}

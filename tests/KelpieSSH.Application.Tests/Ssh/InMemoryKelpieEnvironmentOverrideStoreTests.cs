using FluentAssertions;
using KelpieSSH.Application.Ssh;

namespace KelpieSSH.Application.Tests.Ssh;

public sealed class InMemoryKelpieEnvironmentOverrideStoreTests
{
    [Fact]
    public void Put_ShouldStoreMetadataWithoutExposingValue()
    {
        var store = new InMemoryKelpieEnvironmentOverrideStore();

        var info = store.Put("vps01", "APP_ENV", "production");

        info.ProfileName.Should().Be("vps01");
        info.Key.Should().Be("APP_ENV");
        info.ValueLength.Should().Be("production".Length);
        store.List("vps01").Should().ContainSingle()
            .Which.Should().Be(info);
    }

    [Fact]
    public void GetValues_ShouldReturnValuesForSelectedProfileOnly()
    {
        var store = new InMemoryKelpieEnvironmentOverrideStore();
        store.Put("vps01", "APP_ENV", "production");
        store.Put("vps02", "APP_ENV", "staging");

        var values = store.GetValues("vps01");

        values.Should().ContainSingle();
        values.Should().Contain("APP_ENV", "production");
    }

    [Fact]
    public void Clear_ShouldRemoveOnlySelectedProfile()
    {
        var store = new InMemoryKelpieEnvironmentOverrideStore();
        store.Put("vps01", "APP_ENV", "production");
        store.Put("vps01", "DEPLOY_TOKEN", "secret");
        store.Put("vps02", "APP_ENV", "staging");

        var removed = store.Clear("vps01");

        removed.Should().Be(2);
        store.List("vps01").Should().BeEmpty();
        store.List("vps02").Should().ContainSingle();
    }

    [Fact]
    public void Put_ShouldRejectNewlineValue()
    {
        var store = new InMemoryKelpieEnvironmentOverrideStore();

        var action = () => store.Put("vps01", "APP_ENV", "line1\nline2");

        action.Should().Throw<ArgumentException>()
            .WithMessage("Environment value must not contain newline characters.*");
    }
}

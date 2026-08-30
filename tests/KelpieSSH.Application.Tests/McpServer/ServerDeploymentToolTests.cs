using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using KelpieMCPServer;
using KelpieSSH.Application.Ssh;
using NSubstitute;

namespace KelpieSSH.Application.Tests.McpServer;

public sealed class ServerDeploymentToolTests
{
    [Fact]
    public void ProviderCapabilities_ReturnsCredentialAndRollbackContract()
    {
        var result = KelpieTools.GetProviderCapabilities();

        result.CredentialsManagedByProvider.Should().BeTrue();
        result.SupportsRollback.Should().BeTrue();
        result.Operations.Should().Contain("deploy_verify");
    }

    [Fact]
    public void PrepareDeployment_WhenRepeated_ReturnsSameDeployment()
    {
        var catalog = CreateCatalog();
        var transfers = new WebBulkTransferStore();
        var deployments = new ServerDeploymentStore();

        var first = KelpieTools.PrepareDeployment(
            catalog, transfers, deployments, "deploy-1", "test", "/release/app.bin");
        var second = KelpieTools.PrepareDeployment(
            catalog, transfers, deployments, "deploy-1", "test", "/release/app.bin");

        first.Success.Should().BeTrue();
        second.Success.Should().BeTrue();
        second.Deployment.TransferId.Should().Be(first.Deployment.TransferId);
        second.Deployment.State.Should().Be(ServerDeploymentState.Prepared);
    }

    [Fact]
    public void LoopbackProfileSample_LoadsWithRestrictedDeploymentRoot()
    {
        var root = FindRepositoryRoot();
        var path = Path.Combine(root, "config_samples", "servers", "moyai-deploy-loopback.json");

        var profile = SshConnectionProfileFileLoader.LoadFile(path);

        profile.Host.Should().Be("127.0.0.1");
        profile.Port.Should().Be(2222);
        profile.WebPublicSites.Should().ContainSingle();
        profile.WebPublicSites.Single().RootPath.Should().Be("/tmp/kelpie-deploy-test");
    }

    [Fact]
    public async Task UploadDeployment_WhenHashMatches_BecomesUploaded()
    {
        var path = Path.Combine(Path.GetTempPath(), $"kelpie-deploy-{Guid.NewGuid():N}.bin");
        try
        {
            var bytes = Encoding.UTF8.GetBytes("artifact");
            await File.WriteAllBytesAsync(path, bytes);
            var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            var transfers = new WebBulkTransferStore();
            var deployments = new ServerDeploymentStore();
            KelpieTools.PrepareDeployment(
                CreateCatalog(), transfers, deployments, "deploy-2", "test", "/release/app.bin");

            var result = await KelpieTools.UploadDeploymentAsync(
                transfers, deployments, "deploy-2", path, hash);

            result.Success.Should().BeTrue();
            result.Deployment.State.Should().Be(ServerDeploymentState.Uploaded);
            result.Deployment.Sha256.Should().Be(hash);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task UploadDeployment_WhenHashDiffers_ReturnsStableError()
    {
        var path = Path.Combine(Path.GetTempPath(), $"kelpie-deploy-{Guid.NewGuid():N}.bin");
        try
        {
            await File.WriteAllTextAsync(path, "artifact");
            var transfers = new WebBulkTransferStore();
            var deployments = new ServerDeploymentStore();
            KelpieTools.PrepareDeployment(
                CreateCatalog(), transfers, deployments, "deploy-3", "test", "/release/app.bin");

            var result = await KelpieTools.UploadDeploymentAsync(
                transfers, deployments, "deploy-3", path, new string('0', 64));

            result.Success.Should().BeFalse();
            result.Deployment.Error!.Code.Should().Be("artifact-hash-mismatch");
            result.Deployment.Error.Retryable.Should().BeFalse();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task DeploymentStages_WithLoopbackProvider_CompleteAndRemainIdempotent()
    {
        var path = Path.Combine(Path.GetTempPath(), $"kelpie-deploy-{Guid.NewGuid():N}.bin");
        try
        {
            var bytes = Encoding.UTF8.GetBytes("artifact");
            await File.WriteAllBytesAsync(path, bytes);
            var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            var context = CreateDeploymentContext(hash);

            KelpieTools.PrepareDeployment(
                context.Catalog, context.Transfers, context.Deployments,
                "deploy-flow", "test", "/release/app.bin");
            await KelpieTools.UploadDeploymentAsync(
                context.Transfers, context.Deployments, "deploy-flow", path, hash);
            var activated = await KelpieTools.ActivateDeploymentAsync(
                context.Service, context.Catalog, context.Provider,
                context.Transfers, context.Deployments, "deploy-flow");
            var verified = await KelpieTools.VerifyDeploymentAsync(
                context.Service, context.Catalog, context.Provider,
                context.Deployments, "deploy-flow");
            var repeatedVerify = await KelpieTools.VerifyDeploymentAsync(
                context.Service, context.Catalog, context.Provider,
                context.Deployments, "deploy-flow");
            var cleaned = await KelpieTools.CleanupDeploymentAsync(
                context.Service, context.Catalog, context.Provider,
                context.Transfers, context.Deployments, "deploy-flow");

            activated.Deployment.State.Should().Be(ServerDeploymentState.Activated);
            verified.Deployment.State.Should().Be(ServerDeploymentState.Verified);
            repeatedVerify.Deployment.Should().Be(verified.Deployment);
            cleaned.Success.Should().BeTrue();
            cleaned.Deployment.State.Should().Be(ServerDeploymentState.Cleaned);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RollbackDeployment_WhenProviderFails_ReturnsStableRetryableError()
    {
        var path = Path.Combine(Path.GetTempPath(), $"kelpie-deploy-{Guid.NewGuid():N}.bin");
        try
        {
            var bytes = Encoding.UTF8.GetBytes("artifact");
            await File.WriteAllBytesAsync(path, bytes);
            var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            var context = CreateDeploymentContext(hash, rollbackError: "injected rollback failure");

            KelpieTools.PrepareDeployment(
                context.Catalog, context.Transfers, context.Deployments,
                "deploy-rollback", "test", "/release/app.bin");
            await KelpieTools.UploadDeploymentAsync(
                context.Transfers, context.Deployments, "deploy-rollback", path, hash);
            await KelpieTools.ActivateDeploymentAsync(
                context.Service, context.Catalog, context.Provider,
                context.Transfers, context.Deployments, "deploy-rollback");

            var result = await KelpieTools.RollbackDeploymentAsync(
                context.Service, context.Catalog, context.Provider,
                context.Transfers, context.Deployments, "deploy-rollback");

            result.Success.Should().BeFalse();
            result.Deployment.Error!.Code.Should().Be("rollback-failed");
            result.Deployment.Error.Retryable.Should().BeTrue();
            result.Deployment.State.Should().Be(ServerDeploymentState.Activated);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RollbackDeployment_WhenProviderSucceeds_BecomesRolledBack()
    {
        var path = Path.Combine(Path.GetTempPath(), $"kelpie-deploy-{Guid.NewGuid():N}.bin");
        try
        {
            var bytes = Encoding.UTF8.GetBytes("artifact");
            await File.WriteAllBytesAsync(path, bytes);
            var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            var context = CreateDeploymentContext(hash);

            KelpieTools.PrepareDeployment(
                context.Catalog, context.Transfers, context.Deployments,
                "deploy-rollback-ok", "test", "/release/app.bin");
            await KelpieTools.UploadDeploymentAsync(
                context.Transfers, context.Deployments, "deploy-rollback-ok", path, hash);
            await KelpieTools.ActivateDeploymentAsync(
                context.Service, context.Catalog, context.Provider,
                context.Transfers, context.Deployments, "deploy-rollback-ok");

            var result = await KelpieTools.RollbackDeploymentAsync(
                context.Service, context.Catalog, context.Provider,
                context.Transfers, context.Deployments, "deploy-rollback-ok");

            result.Success.Should().BeTrue();
            result.Deployment.State.Should().Be(ServerDeploymentState.RolledBack);
            result.Deployment.Error.Should().BeNull();
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static DeploymentContext CreateDeploymentContext(string hash, string? rollbackError = null)
    {
        var provider = Substitute.For<IWebPublicFileProvider>();
        provider.CheckWriteAsync(
                Arg.Any<SshCommandService>(), Arg.Any<SshConnectionProfile>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), true, Arg.Any<CancellationToken>())
            .Returns(new WebPublicFileWriteCheckResult(
                "default", "test", "/release/app.bin", "/srv/release/app.bin",
                false, true, true, "confirmation", "application/octet-stream", null, []));
        provider.WriteBulkAsync(
                Arg.Any<SshCommandService>(), Arg.Any<SshConnectionProfile>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<long>(),
                Arg.Any<string>(), Arg.Any<IReadOnlyList<WebBulkTransferFile>>(), Arg.Any<CancellationToken>())
            .Returns(call => new WebBulkTransferResult(
                "default", call.ArgAt<string>(3), true,
                [new WebBulkTransferFileResult(
                    "/release/app.bin", "/srv/release/app.bin", true, false,
                    "artifact"u8.Length, hash, string.Empty)], []));
        provider.HashAsync(
                Arg.Any<SshCommandService>(), Arg.Any<SshConnectionProfile>(),
                Arg.Any<string>(), Arg.Any<string>(), "sha256", Arg.Any<CancellationToken>())
            .Returns(new WebPublicFileHashResult(
                "test", "default", "/release/app.bin", "/srv/release/app.bin",
                "sha256", hash, "artifact"u8.Length, "tester", "tester", "0644", false, [], null));
        provider.CommitBulkAsync(
                Arg.Any<SshCommandService>(), Arg.Any<SshConnectionProfile>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => new WebBulkTransferResult("default", call.ArgAt<string>(3), true, [], []));
        provider.RollbackBulkAsync(
                Arg.Any<SshCommandService>(), Arg.Any<SshConnectionProfile>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => new WebBulkTransferResult(
                "default", call.ArgAt<string>(3), rollbackError is null, [], [], rollbackError));

        var runner = Substitute.For<ISshCommandRunner>();
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        return new DeploymentContext(
            CreateCatalog(), provider, service, new WebBulkTransferStore(), new ServerDeploymentStore());
    }

    private static SshConnectionProfileCatalog CreateCatalog() =>
        new([
            new SshConnectionProfile
            {
                Name = "test",
                Host = "127.0.0.1",
                UserName = "tester",
                AuthenticationMethod = "privateKey",
                PrivateKeyPath = "test-key",
                OsFamily = "debian",
                PackageManager = "apt",
            },
        ]);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "KelpieSSH.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate the repository root.");
    }

    private sealed record DeploymentContext(
        SshConnectionProfileCatalog Catalog,
        IWebPublicFileProvider Provider,
        SshCommandService Service,
        WebBulkTransferStore Transfers,
        ServerDeploymentStore Deployments);
}

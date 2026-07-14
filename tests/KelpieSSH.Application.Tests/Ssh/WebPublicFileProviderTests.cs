using System.Text;
using FluentAssertions;
using KelpieSSH.Application.Ssh;
using Microsoft.Extensions.Logging;

namespace KelpieSSH.Application.Tests.Ssh;

public sealed class WebPublicFileProviderTests
{
    [Fact]
    public async Task ListAsync_ShouldListDefaultSiteDirectory()
    {
        var profile = CreateProfile();
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: """{"resolvedPath":"/var/www/html","exists":true,"entries":[{"name":"index.html","path":"/index.html","resolvedPath":"/var/www/html/index.html","type":"file","size":128,"mode":"644","owner":"nginx","group":"nginx","lastModified":"2026-06-15T00:00:00Z","depth":0,"isSymlink":false}],"truncated":false}""",
                StandardError: string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var result = await provider.ListAsync(service, profile, "default", "/", maxDepth: 0, limit: 100);

        result.SiteKey.Should().Be("default");
        result.DisplayName.Should().Be("Default Web Site");
        result.Path.Should().Be("/");
        result.ResolvedPath.Should().Be("/var/www/html");
        result.Exists.Should().BeTrue();
        result.Truncated.Should().BeFalse();
        result.Entries.Should().ContainSingle().Which.Name.Should().Be("index.html");
        result.Error.Should().BeNull();
        runner.LastRequest!.CommandName.Should().Be("web_public_file_list_internal");
        DecodeArgument(runner.LastRequest, "siteRootBase64").Should().Be("/var/www/html");
        DecodeArgument(runner.LastRequest, "pathBase64").Should().Be("/");
        runner.LastRequest.Arguments["maxDepth"].Should().Be("0");
        runner.LastRequest.Arguments["limit"].Should().Be("100");
    }

    [Fact]
    public async Task StatAsync_ShouldReturnMetadata()
    {
        var profile = CreateProfile();
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: """{"resolvedPath":"/var/www/html/index.html","exists":true,"type":"file","size":128,"mode":"644","owner":"nginx","group":"nginx","lastModified":"2026-06-15T00:00:00Z","isSymlink":false}""",
                StandardError: string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var result = await provider.StatAsync(service, profile, "default", "/index.html");

        result.Exists.Should().BeTrue();
        result.Type.Should().Be("file");
        result.Size.Should().Be(128);
        result.Mode.Should().Be("644");
        result.Owner.Should().Be("nginx");
        result.Group.Should().Be("nginx");
        result.Error.Should().BeNull();
        runner.LastRequest!.CommandName.Should().Be("web_public_file_stat_internal");
        DecodeArgument(runner.LastRequest, "pathBase64").Should().Be("/index.html");
    }

    [Fact]
    public async Task HashAsync_ShouldReturnValidatedMetadataWithoutContent()
    {
        const string hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string secretFixture = "fixture-secret-content";
        var logger = new TestLogger<WebPublicFileProvider>();
        var profile = CreateProfile();
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: $$"""{"resolvedPath":"/var/www/html/index.html","algorithm":"sha256","hash":"{{hash}}","size":22,"owner":"nginx","group":"nginx","mode":"640","isSymlink":false,"errorCode":null}""",
                StandardError: string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider(logger);

        var result = await provider.HashAsync(service, profile, "default", "/index.html");

        result.ProfileName.Should().Be("vps01");
        result.Algorithm.Should().Be("sha256");
        result.Hash.Should().Be(hash);
        result.Size.Should().Be(22);
        result.Owner.Should().Be("nginx");
        result.Group.Should().Be("nginx");
        result.Mode.Should().Be("640");
        result.IsSymlink.Should().BeFalse();
        result.Error.Should().BeNull();
        runner.LastRequest!.CommandName.Should().Be("web_public_file_hash_internal");
        runner.LastRequest.Arguments["maxBytes"].Should().Be((5 * 1024 * 1024).ToString());
        runner.LastRequest.CommandText.Should().NotContain(secretFixture);
        result.ToString().Should().NotContain(secretFixture);
        logger.Entries.Should().OnlyContain(entry => !entry.Message.Contains(secretFixture, StringComparison.Ordinal));
        logger.Entries.Should().OnlyContain(entry => !entry.Message.Contains(Convert.ToBase64String(Encoding.UTF8.GetBytes(secretFixture)), StringComparison.Ordinal));
    }

    [Fact]
    public async Task HashAsync_ShouldRejectWriteOnlyAllowedFileWithoutCallingProvider()
    {
        var site = CreateSite([new WebPublicFileRule("*.html", AllowedRootAccess.Write)]);
        var profile = CreateProfile(webPublicSites: [site]);
        var runner = new FakeSshCommandRunner([]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var result = await provider.HashAsync(service, profile, "default", "/index.html");

        result.Hash.Should().BeNull();
        result.Error!.Code.Should().Be("file-not-allowed");
        runner.LastRequest.Should().BeNull();
    }

    [Theory]
    [InlineData("md5")]
    [InlineData("sha512")]
    public async Task HashAsync_ShouldRejectUnsupportedAlgorithm(string algorithm)
    {
        var runner = new FakeSshCommandRunner([]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var result = await provider.HashAsync(service, CreateProfile(), "default", "/index.html", algorithm);

        result.Error!.Code.Should().Be("algorithm-not-supported");
        runner.LastRequest.Should().BeNull();
    }

    [Theory]
    [InlineData("file-not-found")]
    [InlineData("file-too-large")]
    [InlineData("file-type-not-supported")]
    [InlineData("file-changed-during-read")]
    public async Task HashAsync_ShouldReturnSafeProviderError(string errorCode)
    {
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput($$"""{"errorCode":"{{errorCode}}"}""", string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var result = await provider.HashAsync(service, CreateProfile(), "default", "/index.html");

        result.Hash.Should().BeNull();
        result.Error!.Code.Should().Be(errorCode);
    }

    [Fact]
    public async Task HashAsync_ShouldRejectInvalidProviderResponse()
    {
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput("fixture-secret-content", "raw-secret-error"),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var result = await provider.HashAsync(service, CreateProfile(), "default", "/index.html");

        result.Hash.Should().BeNull();
        result.Error!.Code.Should().Be("invalid-provider-response");
        result.ToString().Should().NotContain("fixture-secret-content");
        result.ToString().Should().NotContain("raw-secret-error");
    }

    [Fact]
    public async Task HashAsync_ShouldReturnRemoteTimeoutWithoutProviderOutput()
    {
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput("fixture-secret-content", "raw-secret-error", ExitCode: -1, TimedOut: true),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var result = await provider.HashAsync(service, CreateProfile(), "default", "/index.html");

        result.Error!.Code.Should().Be("remote-timeout");
        result.ToString().Should().NotContain("fixture-secret-content");
        result.ToString().Should().NotContain("raw-secret-error");
    }

    [Fact]
    public async Task CheckWriteAsync_ShouldReturnConfirmationWithoutWriting()
    {
        var profile = CreateProfile(KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: """{"resolvedPath":"/var/www/html/my_dir/sample.html","exists":false,"canWrite":true,"reason":null}""",
                StandardError: string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var result = await provider.CheckWriteAsync(
            service,
            profile,
            "default",
            "/my_dir/sample.html",
            contentType: "text/html");

        result.CanWrite.Should().BeTrue();
        result.RequiresConfirmation.Should().BeTrue();
        result.Confirmation.Should().Be("web_file_write:default:/my_dir/sample.html");
        result.ContentType.Should().Be("text/html");
        result.Error.Should().BeNull();
        runner.LastRequest!.CommandName.Should().Be("web_public_file_check_write_internal");
        DecodeArgument(runner.LastRequest, "pathBase64").Should().Be("/my_dir/sample.html");
        runner.LastRequest.Arguments["createDirectories"].Should().Be("1");
    }

    [Fact]
    public async Task CheckWriteAsync_ShouldExplainMissingPhpWritePermission()
    {
        var profile = CreateProfile(KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner([]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var result = await provider.CheckWriteAsync(
            service,
            profile,
            "default",
            "/index.php",
            contentType: "text/html");

        result.CanWrite.Should().BeFalse();
        result.Error.Should().Be("Requested file extension is denied.");
        result.ReasonCode.Should().Be("WritableExecutableExtensionMissing");
        result.Guidance.Should().Contain("WritableExecutableExtensions");
        result.Guidance.Should().Contain(".php");
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task CheckWriteAsync_ShouldExplainRemotePermissionDenied()
    {
        var profile = CreateProfile(KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: """{"resolvedPath":"/var/www/html/index.html","exists":true,"canWrite":false,"reason":"Permission denied"}""",
                StandardError: string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var result = await provider.CheckWriteAsync(
            service,
            profile,
            "default",
            "/index.html",
            contentType: "text/html");

        result.CanWrite.Should().BeFalse();
        result.Reason.Should().Be("Permission denied");
        result.ReasonCode.Should().Be("RemoteFileSystemPermissionDenied");
        result.Guidance.Should().Contain("owner");
        result.Guidance.Should().Contain("mode");
    }

    [Fact]
    public async Task CheckSecretWriteAsync_ShouldRequireExplicitAllowedFileRule()
    {
        var profile = CreateProfile(
            KelpiePolicyMode.Expert,
            [CreateSite([new WebPublicFileRule(".html", AllowedRootAccess.Read | AllowedRootAccess.Write)])]);
        var runner = new FakeSshCommandRunner([]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var result = await provider.CheckSecretWriteAsync(
            service,
            profile,
            "default",
            "/.env",
            "prod-web-env");

        result.CanWrite.Should().BeFalse();
        result.Error.Should().Be("Secret file writes require an explicit writable AllowedFiles rule.");
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task CheckSecretWriteAsync_ShouldReturnSecretConfirmation()
    {
        var profile = CreateProfile(
            KelpiePolicyMode.Expert,
            [CreateSite([new WebPublicFileRule(".env*", AllowedRootAccess.Write)])]);
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: """{"resolvedPath":"/var/www/html/.env.production","exists":false,"canWrite":true,"reason":null}""",
                StandardError: string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var result = await provider.CheckSecretWriteAsync(
            service,
            profile,
            "default",
            "/.env.production",
            "prod-web-env");

        result.CanWrite.Should().BeTrue();
        result.Confirmation.Should().Be("web_secret_file_write:default:/.env.production:prod-web-env");
        result.ContentType.Should().Be("text/plain");
        result.Error.Should().BeNull();
        runner.LastRequest!.CommandName.Should().Be("web_public_file_check_write_internal");
    }

    [Fact]
    public async Task ReadFileAsync_ShouldReadDefaultSiteFile()
    {
        var profile = CreateProfile();
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: """{"resolvedPath":"/var/www/html/my_dir/sample.html","exists":true,"contentBase64":"PGgxPk9LPC9oMT4=","size":11,"lastModified":"2026-06-14T00:00:00Z"}""",
                StandardError: string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var result = await provider.ReadFileAsync(service, profile, "default", "/my_dir/sample.html");

        result.SiteKey.Should().Be("default");
        result.DisplayName.Should().Be("Default Web Site");
        result.Path.Should().Be("/my_dir/sample.html");
        result.ResolvedPath.Should().Be("/var/www/html/my_dir/sample.html");
        result.Exists.Should().BeTrue();
        result.ContentBase64.Should().Be("PGgxPk9LPC9oMT4=");
        result.ContentType.Should().Be("text/html");
        result.Size.Should().Be(11);
        result.LastModified.Should().Be("2026-06-14T00:00:00Z");
        result.Error.Should().BeNull();
        runner.LastRequest!.CommandName.Should().Be("web_public_file_read_internal");
        DecodeArgument(runner.LastRequest, "siteRootBase64").Should().Be("/var/www/html");
        DecodeArgument(runner.LastRequest, "pathBase64").Should().Be("/my_dir/sample.html");
    }

    [Fact]
    public async Task WriteSecretFileAsync_ShouldWriteExplicitSecretFileWithoutReturningContent()
    {
        var profile = CreateProfile(
            KelpiePolicyMode.Expert,
            [CreateSite([new WebPublicFileRule(".env*", AllowedRootAccess.Write)])]);
        var contentBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("TOKEN=secret\n"));
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: """{"resolvedPath":"/var/www/html/.env","written":true,"created":true,"overwritten":false,"size":13}""",
                StandardError: string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var result = await provider.WriteSecretFileAsync(
            service,
            profile,
            "default",
            "/.env",
            contentBase64,
            contentType: null);

        result.Written.Should().BeTrue();
        result.Size.Should().Be(13);
        result.Warnings.Should().Contain("Secret content was not returned.");
        result.Error.Should().BeNull();
        runner.LastRequest!.CommandName.Should().Be("web_public_file_write_internal");
        runner.LastRequest.Arguments.Should().NotContainKey("contentBase64");
        runner.LastRequest.CommandText.Should().NotContain(contentBase64);
        runner.LastRequest.StandardInput.Should().Be(contentBase64);
    }

    [Fact]
    public async Task SearchNameAsync_ShouldFilterEntriesBySafeGlob()
    {
        var profile = CreateProfile();
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: """{"resolvedPath":"/var/www/html","exists":true,"entries":[{"name":"index.html","path":"/index.html","resolvedPath":"/var/www/html/index.html","type":"file","size":128,"mode":"644","owner":"nginx","group":"nginx","lastModified":null,"depth":0,"isSymlink":false},{"name":"app.css","path":"/app.css","resolvedPath":"/var/www/html/app.css","type":"file","size":64,"mode":"644","owner":"nginx","group":"nginx","lastModified":null,"depth":0,"isSymlink":false}],"truncated":false}""",
                StandardError: string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var result = await provider.SearchNameAsync(
            service,
            profile,
            "default",
            "/",
            "*.html",
            maxDepth: 2,
            limit: 50);

        result.Entries.Should().ContainSingle().Which.Path.Should().Be("/index.html");
        result.Error.Should().BeNull();
        runner.LastRequest!.CommandName.Should().Be("web_public_file_list_internal");
    }

    [Fact]
    public async Task SearchTextAsync_ShouldSearchReadableTextFiles()
    {
        var profile = CreateProfile();
        var contentBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("alpha\nneedle here\nomega\n"));
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: """{"resolvedPath":"/var/www/html","exists":true,"entries":[{"name":"index.html","path":"/index.html","resolvedPath":"/var/www/html/index.html","type":"file","size":24,"mode":"644","owner":"nginx","group":"nginx","lastModified":null,"depth":0,"isSymlink":false},{"name":"logo.png","path":"/logo.png","resolvedPath":"/var/www/html/logo.png","type":"file","size":4,"mode":"644","owner":"nginx","group":"nginx","lastModified":null,"depth":0,"isSymlink":false}],"truncated":false}""",
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: $$"""{"resolvedPath":"/var/www/html/index.html","exists":true,"contentBase64":"{{contentBase64}}","size":24,"lastModified":"2026-06-14T00:00:00Z"}""",
                StandardError: string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var result = await provider.SearchTextAsync(
            service,
            profile,
            "default",
            "needle",
            "/",
            maxDepth: 2,
            limit: 10);

        result.Matches.Should().ContainSingle().Which.LineNumber.Should().Be(2);
        result.Matches.Single().Path.Should().Be("/index.html");
        result.Error.Should().BeNull();
        runner.Requests.Select(request => request.CommandName)
            .Should().Equal("web_public_file_list_internal", "web_public_file_read_internal");
    }

    [Fact]
    public async Task SearchTextAsync_ShouldRejectControlCharacterQuery()
    {
        var profile = CreateProfile();
        var runner = new FakeSshCommandRunner([]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var result = await provider.SearchTextAsync(service, profile, "default", "bad\nquery");

        result.Error.Should().Be("Query must be 1 to 128 printable characters without control characters.");
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task SearchNameAsync_ShouldRejectPathPattern()
    {
        var profile = CreateProfile();
        var runner = new FakeSshCommandRunner([]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var result = await provider.SearchNameAsync(
            service,
            profile,
            "default",
            "/",
            "../*.html");

        result.Error.Should().Be("Pattern must be a safe file-name glob without path separators.");
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task CheckPermissionsAsync_ShouldReturnPermissionConfirmationsWithoutChanging()
    {
        var profile = CreateProfile();
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: """{"resolvedPath":"/var/www/html/my_dir","exists":true,"type":"directory","size":128,"mode":"755","owner":"nginx","group":"nginx","lastModified":null,"isSymlink":false}""",
                StandardError: string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var result = await provider.CheckPermissionsAsync(
            service,
            profile,
            "default",
            "/my_dir",
            owner: "deploy",
            group: "www-data",
            mode: "775",
            recursive: true);

        result.Exists.Should().BeTrue();
        result.CanChangeOwner.Should().BeTrue();
        result.CanChangeMode.Should().BeTrue();
        result.OwnerConfirmation.Should().Be("web_change_owner_recursive:default:/my_dir:deploy:www-data");
        result.ModeConfirmation.Should().Be("web_change_mode_recursive:default:/my_dir:775");
        result.CurrentOwner.Should().Be("nginx");
        result.CurrentMode.Should().Be("755");
        result.Error.Should().BeNull();
        runner.LastRequest!.CommandName.Should().Be("web_public_file_stat_internal");
    }

    [Fact]
    public async Task CheckPermissionsAsync_ShouldRejectWorldWritableMode()
    {
        var profile = CreateProfile();
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: """{"resolvedPath":"/var/www/html/my_dir","exists":true,"type":"directory","size":128,"mode":"755","owner":"nginx","group":"nginx","lastModified":null,"isSymlink":false}""",
                StandardError: string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var result = await provider.CheckPermissionsAsync(
            service,
            profile,
            "default",
            "/my_dir",
            owner: "deploy",
            group: "www-data",
            mode: "777");

        result.CanChangeOwner.Should().BeTrue();
        result.CanChangeMode.Should().BeFalse();
        result.ModeConfirmation.Should().BeEmpty();
        result.Reason.Should().Be("Mode must not be world-writable.");
    }

    [Fact]
    public async Task ReadFileAsync_ShouldReturnMissingFile()
    {
        var profile = CreateProfile();
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: """{"resolvedPath":"/var/www/html/my_dir/sample.html","exists":false}""",
                StandardError: string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var result = await provider.ReadFileAsync(service, profile, "default", "/my_dir/sample.html");

        result.Exists.Should().BeFalse();
        result.ContentBase64.Should().BeNull();
        result.ResolvedPath.Should().Be("/var/www/html/my_dir/sample.html");
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task ReadHeadAsync_ShouldUseBoundedSliceCommand()
    {
        var profile = CreateProfile();
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: """{"resolvedPath":"/var/www/html/app.txt","exists":true,"contentBase64":"b25lCg==","size":4,"sourceSize":20,"lastModified":"2026-06-14T00:00:00Z"}""",
                StandardError: string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var result = await provider.ReadHeadAsync(service, profile, "default", "/app.txt", maxBytes: 128, maxLines: 1);

        result.Exists.Should().BeTrue();
        result.ContentBase64.Should().Be("b25lCg==");
        result.Warnings.Should().Contain("Returned head slice is bounded by maxBytes=128 and maxLines=1.");
        runner.LastRequest!.CommandName.Should().Be("web_public_file_slice_internal");
        runner.LastRequest.Arguments["mode"].Should().Be("head");
        runner.LastRequest.Arguments["maxBytes"].Should().Be("128");
        runner.LastRequest.Arguments["maxLines"].Should().Be("1");
    }

    [Fact]
    public async Task ReadTailAsync_ShouldUseBoundedSliceCommand()
    {
        var profile = CreateProfile();
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: """{"resolvedPath":"/var/www/html/app.txt","exists":true,"contentBase64":"dHdvCg==","size":4,"sourceSize":20,"lastModified":"2026-06-14T00:00:00Z"}""",
                StandardError: string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var result = await provider.ReadTailAsync(service, profile, "default", "/app.txt", maxBytes: 128, maxLines: 1);

        result.Exists.Should().BeTrue();
        result.ContentBase64.Should().Be("dHdvCg==");
        runner.LastRequest!.CommandName.Should().Be("web_public_file_slice_internal");
        runner.LastRequest.Arguments["mode"].Should().Be("tail");
    }

    [Fact]
    public async Task WriteFileAsync_ShouldWriteImageContent()
    {
        var profile = CreateProfile(KelpiePolicyMode.Expert);
        var imageBase64 = Convert.ToBase64String([0x89, 0x50, 0x4E, 0x47]);
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: """{"resolvedPath":"/var/www/html/my_dir/logo.png","written":true,"created":true,"overwritten":false,"size":4}""",
                StandardError: string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var result = await provider.WriteFileAsync(
            service,
            profile,
            "default",
            "/my_dir/logo.png",
            imageBase64,
            encoding: null,
            contentType: "image/png");

        result.Written.Should().BeTrue();
        result.Created.Should().BeTrue();
        result.Overwritten.Should().BeFalse();
        result.ContentType.Should().Be("image/png");
        result.Size.Should().Be(4);
        result.Error.Should().BeNull();
        runner.LastRequest!.CommandName.Should().Be("web_public_file_write_internal");
        runner.LastRequest.Arguments.Should().NotContainKey("contentBase64");
        runner.LastRequest.CommandText.Should().NotContain(imageBase64);
        runner.LastRequest.StandardInput.Should().Be(imageBase64);
    }

    [Fact]
    public async Task WriteFileAsync_ShouldUseAtomicPermissionedWriteWhenOwnerGroupModeAreSpecified()
    {
        var profile = CreateProfile(KelpiePolicyMode.Expert);
        var contentBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("<h1>OK</h1>"));
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: """{"resolvedPath":"/var/www/html/my_dir/sample.html","written":true,"created":true,"overwritten":false,"size":11,"owner":"www-data","group":"www-data","mode":"775"}""",
                StandardError: string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var result = await provider.WriteFileAsync(
            service,
            profile,
            "default",
            "/my_dir/sample.html",
            contentBase64,
            encoding: "utf-8",
            contentType: "text/html",
            owner: "www-data:www-data",
            mode: "775");

        result.Written.Should().BeTrue();
        result.Owner.Should().Be("www-data");
        result.Group.Should().Be("www-data");
        result.Mode.Should().Be("775");
        result.Error.Should().BeNull();
        runner.LastRequest!.CommandName.Should().Be("web_public_file_write_with_permissions_internal");
        runner.LastRequest.Arguments.Should().NotContainKey("contentBase64");
        runner.LastRequest.CommandText.Should().NotContain(contentBase64);
        runner.LastRequest.StandardInput.Should().Be(contentBase64);
        DecodeArgument(runner.LastRequest, "ownerBase64").Should().Be("www-data:www-data");
        DecodeArgument(runner.LastRequest, "modeBase64").Should().Be("775");
    }

    [Fact]
    public async Task WriteFileAsync_ShouldAllowOwnerWithoutMode()
    {
        var profile = CreateProfile(KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: """{"resolvedPath":"/var/www/html/my_dir/sample.html","written":true,"created":true,"overwritten":false,"size":1,"owner":"www-data","group":"www-data","mode":"644"}""",
                StandardError: string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var result = await provider.WriteFileAsync(
            service,
            profile,
            "default",
            "/my_dir/sample.html",
            "YQ==",
            encoding: "utf-8",
            contentType: "text/html",
            owner: "www-data:www-data");

        result.Written.Should().BeTrue();
        result.Owner.Should().Be("www-data");
        result.Group.Should().Be("www-data");
        result.Mode.Should().Be("644");
        runner.LastRequest!.CommandName.Should().Be("web_public_file_write_with_permissions_internal");
        DecodeArgument(runner.LastRequest, "ownerBase64").Should().Be("www-data:www-data");
        DecodeArgument(runner.LastRequest, "modeBase64").Should().Be("\u001fKELPIE_NONE\u001f");
    }

    [Fact]
    public async Task WriteFileAsync_ShouldAllowModeWithoutOwner()
    {
        var profile = CreateProfile(KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: """{"resolvedPath":"/var/www/html/my_dir/sample.html","written":true,"created":true,"overwritten":false,"size":1,"owner":"","group":"","mode":"775"}""",
                StandardError: string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var result = await provider.WriteFileAsync(
            service,
            profile,
            "default",
            "/my_dir/sample.html",
            "YQ==",
            encoding: "utf-8",
            contentType: "text/html",
            mode: "775");

        result.Written.Should().BeTrue();
        result.Mode.Should().Be("775");
        runner.LastRequest!.CommandName.Should().Be("web_public_file_write_with_permissions_internal");
        DecodeArgument(runner.LastRequest, "ownerBase64").Should().Be("\u001fKELPIE_NONE\u001f");
        DecodeArgument(runner.LastRequest, "modeBase64").Should().Be("775");
    }

    [Fact]
    public async Task ReadFileAsync_ShouldReadZipContent()
    {
        var profile = CreateProfile();
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: """{"resolvedPath":"/var/www/html/downloads/site.zip","exists":true,"contentBase64":"UEsDBA==","size":4,"lastModified":"2026-06-14T00:00:00Z"}""",
                StandardError: string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var result = await provider.ReadFileAsync(service, profile, "default", "/downloads/site.zip");

        result.Exists.Should().BeTrue();
        result.ContentBase64.Should().Be("UEsDBA==");
        result.ContentType.Should().Be("application/zip");
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task WriteFileAsync_ShouldWriteGzipContent()
    {
        var profile = CreateProfile(KelpiePolicyMode.Expert);
        var gzipBase64 = Convert.ToBase64String([0x1F, 0x8B, 0x08, 0x00]);
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: """{"resolvedPath":"/var/www/html/assets/app.js.gz","written":true,"created":true,"overwritten":false,"size":4}""",
                StandardError: string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var result = await provider.WriteFileAsync(
            service,
            profile,
            "default",
            "/assets/app.js.gz",
            gzipBase64,
            encoding: null,
            contentType: null);

        result.Written.Should().BeTrue();
        result.ContentType.Should().Be("application/gzip");
        result.Size.Should().Be(4);
        result.Error.Should().BeNull();
        runner.LastRequest!.Arguments.Should().NotContainKey("contentBase64");
        runner.LastRequest.CommandText.Should().NotContain(gzipBase64);
        runner.LastRequest.StandardInput.Should().Be(gzipBase64);
    }

    [Fact]
    public async Task WriteFileAsync_ShouldAllowConfiguredZipRule()
    {
        var profile = CreateProfile(
            KelpiePolicyMode.Expert,
            [
                CreateSite([
                    new WebPublicFileRule(".zip", AllowedRootAccess.Read | AllowedRootAccess.Write),
                ]),
            ]);
        var zipBase64 = Convert.ToBase64String([0x50, 0x4B, 0x03, 0x04]);
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: """{"resolvedPath":"/var/www/html/downloads/site.zip","written":true,"created":true,"overwritten":false,"size":4}""",
                StandardError: string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var result = await provider.WriteFileAsync(
            service,
            profile,
            "default",
            "/downloads/site.zip",
            zipBase64,
            encoding: null,
            contentType: null);

        result.Written.Should().BeTrue();
        result.ContentType.Should().Be("application/zip");
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task ReadFileAsync_ShouldAllowConfiguredExeReadOnlyRule()
    {
        var profile = CreateProfile(
            KelpiePolicyMode.Safe,
            [
                CreateSite([
                    new WebPublicFileRule(".exe", AllowedRootAccess.Read),
                ]),
            ]);
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: """{"resolvedPath":"/var/www/html/downloads/tool.exe","exists":true,"contentBase64":"TVo=","size":2,"lastModified":"2026-06-14T00:00:00Z"}""",
                StandardError: string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var result = await provider.ReadFileAsync(service, profile, "default", "/downloads/tool.exe");

        result.Exists.Should().BeTrue();
        result.ContentBase64.Should().Be("TVo=");
        result.ContentType.Should().Be("application/octet-stream");
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task WriteFileAsync_ShouldRejectConfiguredExeReadOnlyRule()
    {
        var profile = CreateProfile(
            KelpiePolicyMode.Expert,
            [
                CreateSite([
                    new WebPublicFileRule(".exe", AllowedRootAccess.Read),
                ]),
            ]);
        var runner = new FakeSshCommandRunner([]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var result = await provider.WriteFileAsync(
            service,
            profile,
            "default",
            "/downloads/tool.exe",
            "TVo=",
            encoding: null,
            contentType: null);

        result.Error.Should().Be("Requested file is not writable by AllowedFiles.");
        result.Written.Should().BeFalse();
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task WriteFileAsync_ShouldRejectUnmatchedFileWhenAllowedFilesIsConfigured()
    {
        var profile = CreateProfile(
            KelpiePolicyMode.Expert,
            [
                CreateSite([
                    new WebPublicFileRule("*.zip", AllowedRootAccess.Read | AllowedRootAccess.Write),
                ]),
            ]);
        var runner = new FakeSshCommandRunner([]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var result = await provider.WriteFileAsync(
            service,
            profile,
            "default",
            "/downloads/site.html",
            "YQ==",
            encoding: "utf-8",
            contentType: "text/html");

        result.Error.Should().Be("Requested file is not allowed by AllowedFiles.");
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task WriteFileAsync_ShouldRejectReadOnlyContentTypeRule()
    {
        var profile = CreateProfile(
            KelpiePolicyMode.Expert,
            [
                CreateSite(
                    [
                        new WebPublicFileRule(".png", AllowedRootAccess.Read | AllowedRootAccess.Write),
                    ],
                    [
                        new WebPublicContentTypeRule("image/png", AllowedRootAccess.Read),
                    ]),
            ]);
        var runner = new FakeSshCommandRunner([]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var result = await provider.WriteFileAsync(
            service,
            profile,
            "default",
            "/images/logo.png",
            Convert.ToBase64String([0x89, 0x50, 0x4E, 0x47]),
            encoding: null,
            contentType: "image/png");

        result.Error.Should().Be("Content type is not writable: image/png");
        result.Written.Should().BeFalse();
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task WriteFileAsync_ShouldRejectTraversal()
    {
        var profile = CreateProfile(KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner([]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var result = await provider.WriteFileAsync(
            service,
            profile,
            "default",
            "/../secret.html",
            "YQ==",
            encoding: "utf-8",
            contentType: "text/html");

        result.Error.Should().Be("Requested path must be an absolute site-relative path without traversal.");
        result.Written.Should().BeFalse();
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task WriteFileAsync_ShouldRejectPhp()
    {
        var profile = CreateProfile(KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner([]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var result = await provider.WriteFileAsync(
            service,
            profile,
            "default",
            "/index.php",
            Convert.ToBase64String(Encoding.UTF8.GetBytes("<?php echo 1;")),
            encoding: "utf-8",
            contentType: "text/html");

        result.Error.Should().Be("Requested file extension is denied.");
        result.ReasonCode.Should().Be("WritableExecutableExtensionMissing");
        result.Guidance.Should().Contain("WritableExecutableExtensions");
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task WriteFileAsync_ShouldAllowConfiguredWritableExecutableExtension()
    {
        var profile = CreateProfile(
            KelpiePolicyMode.Expert,
            [
                CreateSite(
                    allowedFiles: [],
                    writableExecutableExtensions: [".php"]),
            ]);
        var logger = new TestLogger<WebPublicFileProvider>();
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: """{"resolvedPath":"/var/www/html/index.php","written":true,"created":true,"overwritten":false,"size":13}""",
                StandardError: string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider(logger);

        var result = await provider.WriteFileAsync(
            service,
            profile,
            "default",
            "/index.php",
            Convert.ToBase64String(Encoding.UTF8.GetBytes("<?php echo 1;")),
            encoding: "utf-8",
            contentType: "text/html");

        result.Written.Should().BeTrue();
        result.Error.Should().BeNull();
        runner.LastRequest.Should().NotBeNull();
        logger.Entries.Should().Contain(entry =>
            entry.Level == LogLevel.Warning
            && entry.Message.Contains("executable web file written under explicit profile permission", StringComparison.Ordinal)
            && entry.Message.Contains("vps01", StringComparison.Ordinal)
            && entry.Message.Contains("default", StringComparison.Ordinal)
            && entry.Message.Contains("/index.php", StringComparison.Ordinal)
            && entry.Message.Contains(".php", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WriteFileAsync_ShouldKeepRejectingUnlistedExecutableExtension()
    {
        var profile = CreateProfile(
            KelpiePolicyMode.Expert,
            [
                CreateSite(
                    allowedFiles: [],
                    writableExecutableExtensions: [".php"]),
            ]);
        var runner = new FakeSshCommandRunner([]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var result = await provider.WriteFileAsync(
            service,
            profile,
            "default",
            "/run.sh",
            Convert.ToBase64String(Encoding.UTF8.GetBytes("#!/bin/sh")),
            encoding: "utf-8",
            contentType: "text/plain");

        result.Error.Should().Be("Requested file extension is denied.");
        result.Written.Should().BeFalse();
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task WriteFileAsync_ShouldKeepRejectingPhpForOtherSiteAndProfile()
    {
        var profile = CreateProfile(
            KelpiePolicyMode.Expert,
            [
                CreateSite(
                    allowedFiles: [],
                    writableExecutableExtensions: [".php"]),
                CreateSite(
                    allowedFiles: [],
                    siteKey: "admin",
                    rootPath: "/var/www/admin"),
            ]);
        var otherProfile = CreateProfile(KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner([]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var siteResult = await provider.WriteFileAsync(
            service,
            profile,
            "admin",
            "/index.php",
            Convert.ToBase64String(Encoding.UTF8.GetBytes("<?php echo 1;")),
            encoding: "utf-8",
            contentType: "text/html");
        var profileResult = await provider.WriteFileAsync(
            service,
            otherProfile,
            "default",
            "/index.php",
            Convert.ToBase64String(Encoding.UTF8.GetBytes("<?php echo 1;")),
            encoding: "utf-8",
            contentType: "text/html");

        siteResult.Error.Should().Be("Requested file extension is denied.");
        profileResult.Error.Should().Be("Requested file extension is denied.");
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task ReadFileAsync_ShouldKeepRejectingWritableExecutableExtension()
    {
        var profile = CreateProfile(
            KelpiePolicyMode.Safe,
            [
                CreateSite(
                    allowedFiles: [],
                    writableExecutableExtensions: [".php"]),
            ]);
        var runner = new FakeSshCommandRunner([]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var result = await provider.ReadFileAsync(service, profile, "default", "/index.php");

        result.Error.Should().Be("Requested file extension is denied.");
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task WriteFileAsync_ShouldKeepRejectingSecretFileWithWritableExecutableExtension()
    {
        var profile = CreateProfile(
            KelpiePolicyMode.Expert,
            [
                CreateSite(
                    allowedFiles: [],
                    writableExecutableExtensions: [".php"]),
            ]);
        var runner = new FakeSshCommandRunner([]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var result = await provider.WriteFileAsync(
            service,
            profile,
            "default",
            "/.htpasswd",
            Convert.ToBase64String(Encoding.UTF8.GetBytes("secret")),
            encoding: "utf-8",
            contentType: "text/plain");

        result.Error.Should().Be("Requested path is denied by web public file safety rules.");
        result.Written.Should().BeFalse();
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task WriteFileAsync_ShouldRejectInvalidBase64()
    {
        var profile = CreateProfile(KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner([]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var result = await provider.WriteFileAsync(
            service,
            profile,
            "default",
            "/my_dir/sample.html",
            "not-base64",
            encoding: "utf-8",
            contentType: "text/html");

        result.Error.Should().Be("ContentBase64 is not valid Base64.");
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task ChangeOwnerAsync_ShouldChangeSiteRootOwner()
    {
        var profile = CreateProfile(KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: """{"resolvedPath":"/var/www/html","changed":true,"owner":"deploy","group":"www-data","mode":"775"}""",
                StandardError: string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var result = await provider.ChangeOwnerAsync(
            service,
            profile,
            "default",
            "/",
            "deploy",
            "www-data");

        result.Changed.Should().BeTrue();
        result.ResolvedPath.Should().Be("/var/www/html");
        result.Owner.Should().Be("deploy");
        result.Group.Should().Be("www-data");
        result.Mode.Should().Be("775");
        result.Error.Should().BeNull();
        runner.LastRequest!.CommandName.Should().Be("web_change_owner_internal");
        DecodeArgument(runner.LastRequest, "siteRootBase64").Should().Be("/var/www/html");
        DecodeArgument(runner.LastRequest, "pathBase64").Should().Be("/");
        DecodeArgument(runner.LastRequest, "ownerBase64").Should().Be("deploy");
        DecodeArgument(runner.LastRequest, "groupBase64").Should().Be("www-data");
        runner.LastRequest.Arguments["recursive"].Should().Be("0");
    }

    [Fact]
    public async Task ChangeOwnerAsync_ShouldPassRecursiveFlag()
    {
        var profile = CreateProfile(KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: """{"resolvedPath":"/var/www/html/assets","changed":true,"owner":"deploy","group":"www-data","mode":"775"}""",
                StandardError: string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var result = await provider.ChangeOwnerAsync(
            service,
            profile,
            "default",
            "/assets",
            "deploy",
            "www-data",
            recursive: true);

        result.Changed.Should().BeTrue();
        result.Warnings.Should().ContainSingle().Which.Should().Be("Recursive permission change skips symbolic links.");
        runner.LastRequest!.Arguments["recursive"].Should().Be("1");
    }

    [Fact]
    public async Task ChangeModeAsync_ShouldChangeSiteRootMode()
    {
        var profile = CreateProfile(KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: """{"resolvedPath":"/var/www/html","changed":true,"owner":"","group":"","mode":"775"}""",
                StandardError: string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var result = await provider.ChangeModeAsync(
            service,
            profile,
            "default",
            "/",
            "775");

        result.Changed.Should().BeTrue();
        result.ResolvedPath.Should().Be("/var/www/html");
        result.Mode.Should().Be("775");
        result.Error.Should().BeNull();
        runner.LastRequest!.CommandName.Should().Be("web_change_mode_internal");
        runner.LastRequest.CommandText.Should().StartWith("sudo -n /usr/local/libexec/kelpie/kelpie-web-permission-helper change-mode ");
        runner.LastRequest.Arguments["recursive"].Should().Be("0");
    }

    [Fact]
    public async Task ChangeModeAsync_ShouldPassRecursiveFlag()
    {
        var profile = CreateProfile(KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: """{"resolvedPath":"/var/www/html/assets","changed":true,"owner":"","group":"","mode":"775"}""",
                StandardError: string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var result = await provider.ChangeModeAsync(
            service,
            profile,
            "default",
            "/assets",
            "775",
            recursive: true);

        result.Changed.Should().BeTrue();
        result.Warnings.Should().ContainSingle().Which.Should().Be("Recursive permission change skips symbolic links.");
        runner.LastRequest!.Arguments["recursive"].Should().Be("1");
    }

    [Fact]
    public async Task ChangeModeAsync_ShouldRejectWorldWritableMode()
    {
        var profile = CreateProfile(KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner([]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var result = await provider.ChangeModeAsync(
            service,
            profile,
            "default",
            "/",
            "777");

        result.Error.Should().Be("Mode must not be world-writable.");
        result.Changed.Should().BeFalse();
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task ChangeOwnerAsync_ShouldRejectRootOwner()
    {
        var profile = CreateProfile(KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner([]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new WebPublicFileProvider();

        var result = await provider.ChangeOwnerAsync(
            service,
            profile,
            "default",
            "/",
            "root",
            "www-data");

        result.Error.Should().Be("Owner must not be root.");
        result.Changed.Should().BeFalse();
        runner.LastRequest.Should().BeNull();
    }

    private static string DecodeArgument(SshCommandRequest request, string name)
    {
        return Encoding.UTF8.GetString(Convert.FromBase64String(request.Arguments[name]));
    }

    private static SshConnectionProfile CreateProfile(
        KelpiePolicyMode mode = KelpiePolicyMode.Safe,
        IReadOnlyCollection<WebPublicSite>? webPublicSites = null)
    {
        return new SshConnectionProfile
        {
            Name = "vps01",
            Host = "example.invalid",
            UserName = "deploy",
            PrivateKeyPath = "id_ed25519",
            OsFamily = "debian",
            PackageManager = "apt",
            Mode = mode,
            Capabilities = PolicySet.Empty,
            WebPublicSites = webPublicSites ?? [],
        };
    }

    private static WebPublicSite CreateSite(
        IReadOnlyCollection<WebPublicFileRule> allowedFiles,
        IReadOnlyCollection<WebPublicContentTypeRule>? allowedContentTypes = null,
        IReadOnlyCollection<string>? writableExecutableExtensions = null,
        string siteKey = "default",
        string rootPath = "/var/www/html")
    {
        return new WebPublicSite
        {
            SiteKey = siteKey,
            DisplayName = "Default Web Site",
            RootPath = rootPath,
            AllowedFiles = allowedFiles,
            AllowedContentTypes = allowedContentTypes ?? [],
            WritableExecutableExtensions = writableExecutableExtensions ?? [],
        };
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<TestLogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new TestLogEntry(logLevel, formatter(state, exception)));
        }
    }

    private sealed record TestLogEntry(LogLevel Level, string Message);

    private sealed class FakeSshCommandRunner : ISshCommandRunner
    {
        private readonly Queue<FakeSshCommandOutput> _outputs;
        private readonly List<SshCommandRequest> _requests = [];

        public FakeSshCommandRunner(IEnumerable<FakeSshCommandOutput> outputs)
        {
            _outputs = new Queue<FakeSshCommandOutput>(outputs);
        }

        public SshCommandRequest? LastRequest { get; private set; }

        public IReadOnlyList<SshCommandRequest> Requests => _requests;

        public Task<SshCommandResult> ExecuteAsync(
            SshCommandRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            _requests.Add(request);
            var output = _outputs.Dequeue();
            return Task.FromResult(new SshCommandResult(
                request.CommandName,
                request.CommandText,
                output.ExitCode,
                output.StandardOutput,
                output.StandardError,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                output.TimedOut));
        }
    }

    private sealed record FakeSshCommandOutput(
        string StandardOutput,
        string StandardError,
        int ExitCode = 0,
        bool TimedOut = false);
}

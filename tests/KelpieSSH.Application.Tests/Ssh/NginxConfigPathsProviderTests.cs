using System.Text.Json;
using FluentAssertions;
using KelpieSSH.Application.Ssh;
using System.Text;

namespace KelpieSSH.Application.Tests.Ssh;

public sealed class NginxConfigPathsProviderTests
{
    [Fact]
    public void ExtractConfPath_ShouldReadNginxVersionConfigureArgument()
    {
        const string output = "nginx version: nginx/1.24.0\nconfigure arguments: --prefix=/usr/share/nginx --conf-path=/etc/nginx/nginx.conf --with-http_ssl_module";

        var result = NginxConfigPathsProvider.ExtractConfPath(output);

        result.Should().Be("/etc/nginx/nginx.conf");
    }

    [Fact]
    public void ExtractIncludePatterns_ShouldReadIncludeDirectives()
    {
        const string configText = """
            user nginx;
            include /etc/nginx/conf.d/*.conf;
            include /etc/nginx/default.d/*.conf;
            # include /ignored/*.conf;
            """;

        var result = NginxConfigPathsProvider.ExtractIncludePatterns(configText);

        result.Should().Equal(
        [
            "/etc/nginx/conf.d/*.conf",
            "/etc/nginx/default.d/*.conf",
        ]);
    }

    [Fact]
    public async Task GetConfigPathsAsync_ShouldReturnMainConfigFromNginxVersion()
    {
        var profile = CreateProfile();
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: string.Empty,
                StandardError: "configure arguments: --conf-path=/etc/nginx/nginx.conf --pid-path=/run/nginx.pid"),
            new FakeSshCommandOutput(
                StandardOutput: "include /etc/nginx/conf.d/*.conf;\n",
                StandardError: string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new NginxConfigPathsProvider();

        var result = await provider.GetConfigPathsAsync(service, profile);

        result.ServiceKey.Should().Be("nginx");
        result.DisplayName.Should().Be("Nginx");
        result.MainConfig.Should().Be("/etc/nginx/nginx.conf");
        result.ConfigFiles.Should().Equal("/etc/nginx/nginx.conf");
        result.IncludePatterns.Should().Equal("/etc/nginx/conf.d/*.conf");
        result.Warnings.Should().BeEmpty();
        runner.Requests[0].CommandName.Should().Be("service_config_nginx_version");
        runner.Requests[0].CommandText.Should().Be("nginx -V");
        runner.Requests[1].CommandName.Should().Be("service_config_nginx_read_config");
    }

    [Fact]
    public async Task ReadConfigFileAsync_ShouldReadMainConfigWhenPathIsOmitted()
    {
        var profile = CreateProfile();
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: string.Empty,
                StandardError: "configure arguments: --conf-path=/etc/nginx/nginx.conf"),
            new FakeSshCommandOutput(
                StandardOutput: "include /etc/nginx/conf.d/*.conf;\n",
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: "user nginx;\nworker_processes auto;\n",
                StandardError: string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new NginxConfigPathsProvider();

        var result = await provider.ReadConfigFileAsync(service, profile);

        result.ServiceKey.Should().Be("nginx");
        result.DisplayName.Should().Be("Nginx");
        result.Path.Should().Be("/etc/nginx/nginx.conf");
        result.Content.Should().Contain("worker_processes auto;");
        result.Encoding.Should().Be("utf-8");
        result.Truncated.Should().BeFalse();
        result.Error.Should().BeNull();
        runner.Requests.Should().HaveCount(3);
        runner.LastRequest!.CommandName.Should().Be("service_config_nginx_read_config");
        DecodeArgument(runner.LastRequest, "pathBase64").Should().Be("/etc/nginx/nginx.conf");
        DecodeArgument(runner.LastRequest, "allowedPathsBase64").Should().Be("/etc/nginx/nginx.conf");
        DecodeArgument(runner.LastRequest, "allowedDirsBase64").Should().Be("/etc/nginx/conf.d");
    }

    [Fact]
    public async Task ReadConfigFileAsync_ShouldLetRemoteCanonicalCheckRejectPathOutsideProviderConfigFiles()
    {
        var profile = CreateProfile();
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: string.Empty,
                StandardError: "configure arguments: --conf-path=/etc/nginx/nginx.conf"),
            new FakeSshCommandOutput(
                StandardOutput: "include /etc/nginx/conf.d/*.conf;\n",
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: string.Empty,
                StandardError: "ERROR: path is not an allowed service config file",
                ExitCode: 1),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new NginxConfigPathsProvider();

        var result = await provider.ReadConfigFileAsync(service, profile, "/etc/passwd");

        result.Error.Should().Be("Requested path is not a provider-approved configuration file.");
        result.Content.Should().BeEmpty();
        runner.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task ReadConfigFileAsync_ShouldRejectUnsafeRelativeTraversal()
    {
        var profile = CreateProfile();
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: string.Empty,
                StandardError: "configure arguments: --conf-path=/etc/nginx/nginx.conf"),
            new FakeSshCommandOutput(
                StandardOutput: "include /etc/nginx/conf.d/*.conf;\n",
                StandardError: string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new NginxConfigPathsProvider();

        var result = await provider.ReadConfigFileAsync(service, profile, "/etc/nginx/../passwd");

        result.Error.Should().Be("Requested path must be a safe absolute configuration path.");
        runner.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task ReadConfigFileAsync_ShouldReturnTruncatedWarning()
    {
        var profile = CreateProfile();
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: string.Empty,
                StandardError: "configure arguments: --conf-path=/etc/nginx/nginx.conf"),
            new FakeSshCommandOutput(
                StandardOutput: "include /etc/nginx/conf.d/*.conf;\n",
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: "user nginx;",
                StandardError: "KELPIE_TRUNCATED=1\n"),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new NginxConfigPathsProvider();

        var result = await provider.ReadConfigFileAsync(service, profile, "/etc/nginx/nginx.conf");

        result.Truncated.Should().BeTrue();
        result.Warnings.Should().Contain("Content was truncated by the maximum read size.");
    }

    [Fact]
    public async Task ReadConfigFileAsync_ShouldAllowProviderApprovedIncludePath()
    {
        var profile = CreateProfile();
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: string.Empty,
                StandardError: "configure arguments: --conf-path=/etc/nginx/nginx.conf"),
            new FakeSshCommandOutput(
                StandardOutput: "include /etc/nginx/conf.d/*.conf;\n",
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: "server { listen 8080; }\n",
                StandardError: string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new NginxConfigPathsProvider();

        var result = await provider.ReadConfigFileAsync(service, profile, "/etc/nginx/conf.d/kelpie-test.conf");

        result.Error.Should().BeNull();
        result.Content.Should().Contain("listen 8080");
        DecodeArgument(runner.LastRequest!, "allowedDirsBase64").Should().Be("/etc/nginx/conf.d");
    }

    [Fact]
    public async Task WriteConfigFileAsync_ShouldWriteProviderApprovedIncludePath()
    {
        var profile = CreateProfile(KelpiePolicyMode.Expert);
        const string originalContent = """
            server {
                server_name old.example.invalid;
            }

            """;
        const string updatedContent = """
            server {
                server_name localhost;
            }

            """;
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: string.Empty,
                StandardError: "configure arguments: --conf-path=/etc/nginx/nginx.conf"),
            new FakeSshCommandOutput(
                StandardOutput: "include /etc/nginx/conf.d/*.conf;\n",
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: originalContent,
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: Encoding.UTF8.GetByteCount(NormalizeLf(updatedContent)).ToString(System.Globalization.CultureInfo.InvariantCulture),
                StandardError: string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new NginxConfigPathsProvider();

        var result = await provider.WriteConfigFileAsync(
            service,
            profile,
            "/etc/nginx/conf.d/kelpie-test.conf",
            "server.server_name",
            "replace",
            "localhost");

        result.Error.Should().BeNull();
        result.Path.Should().Be("/etc/nginx/conf.d/kelpie-test.conf");
        result.Encoding.Should().Be("utf-8");
        result.BytesWritten.Should().Be(Encoding.UTF8.GetByteCount(NormalizeLf(updatedContent)));
        runner.LastRequest!.CommandName.Should().Be("service_config_nginx_helper_apply");
        DecodeArgument(runner.LastRequest, "pathBase64").Should().Be("/etc/nginx/conf.d/kelpie-test.conf");
        runner.LastRequest.Arguments.Should().NotContainKey("contentBase64");
        DecodeStandardInput(runner.LastRequest).Should().Be(NormalizeLf(updatedContent));
    }

    [Fact]
    public async Task WriteConfigFileAsync_ShouldInsertProviderApprovedLineTarget()
    {
        var profile = CreateProfile(KelpiePolicyMode.Expert);
        const string originalContent = """
            events {}

            """;
        const string updatedContent = """
            user www-data;
            events {}

            """;
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: string.Empty,
                StandardError: "configure arguments: --conf-path=/etc/nginx/nginx.conf"),
            new FakeSshCommandOutput(
                StandardOutput: "include /etc/nginx/conf.d/*.conf;\n",
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: originalContent,
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: Encoding.UTF8.GetByteCount(NormalizeLf(updatedContent)).ToString(System.Globalization.CultureInfo.InvariantCulture),
                StandardError: string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new NginxConfigPathsProvider();

        var result = await provider.WriteConfigFileAsync(
            service,
            profile,
            "/etc/nginx/nginx.conf",
            "/etc/nginx/nginx.conf:1",
            "insert",
            "user:www-data");

        result.Error.Should().BeNull();
        result.BytesWritten.Should().Be(Encoding.UTF8.GetByteCount(NormalizeLf(updatedContent)));
        DecodeStandardInput(runner.LastRequest!).Should().Be(NormalizeLf(updatedContent));
    }

    [Fact]
    public async Task WriteConfigFileAsync_ShouldReplaceIndexedDirectiveTarget()
    {
        var profile = CreateProfile(KelpiePolicyMode.Expert);
        const string originalContent = """
            server {
                server_name one.example.invalid;
            }

            server {
                server_name two.example.invalid;
            }

            server {
                server_name three.example.invalid;
            }

            """;
        const string updatedContent = """
            server {
                server_name one.example.invalid;
            }

            server {
                server_name two.example.invalid;
            }

            server {
                server_name localhost;
            }

            """;
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: string.Empty,
                StandardError: "configure arguments: --conf-path=/etc/nginx/nginx.conf"),
            new FakeSshCommandOutput(
                StandardOutput: "include /etc/nginx/conf.d/*.conf;\n",
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: originalContent,
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: Encoding.UTF8.GetByteCount(NormalizeLf(updatedContent)).ToString(System.Globalization.CultureInfo.InvariantCulture),
                StandardError: string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new NginxConfigPathsProvider();

        var result = await provider.WriteConfigFileAsync(
            service,
            profile,
            "/etc/nginx/conf.d/kelpie-test.conf",
            "server.server_name[2]",
            "replace",
            "localhost");

        result.Error.Should().BeNull();
        result.BytesWritten.Should().Be(Encoding.UTF8.GetByteCount(NormalizeLf(updatedContent)));
        DecodeStandardInput(runner.LastRequest!).Should().Be(NormalizeLf(updatedContent));
    }

    [Fact]
    public async Task WriteConfigFileAsync_ShouldRejectIndexedDirectiveTargetOutsideMatches()
    {
        var profile = CreateProfile(KelpiePolicyMode.Expert);
        const string originalContent = """
            server {
                server_name one.example.invalid;
            }

            server {
                server_name two.example.invalid;
            }

            """;
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: string.Empty,
                StandardError: "configure arguments: --conf-path=/etc/nginx/nginx.conf"),
            new FakeSshCommandOutput(
                StandardOutput: "include /etc/nginx/conf.d/*.conf;\n",
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: originalContent,
                StandardError: string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new NginxConfigPathsProvider();

        var result = await provider.WriteConfigFileAsync(
            service,
            profile,
            "/etc/nginx/conf.d/kelpie-test.conf",
            "server.server_name[2]",
            "replace",
            "localhost");

        result.Error.Should().Be("TargetKey index did not match any editable Nginx directive.");
        result.BytesWritten.Should().Be(0);
        runner.LastRequest!.CommandName.Should().Be("service_config_nginx_read_config");
        runner.Requests.Should().HaveCount(3);
    }

    [Fact]
    public async Task WriteConfigFileAsync_ShouldDeleteProviderApprovedDirective()
    {
        var profile = CreateProfile(KelpiePolicyMode.Expert);
        const string originalContent = """
            server {
                listen 80;
                server_name old.example.invalid;
            }

            """;
        const string updatedContent = """
            server {
                listen 80;
            }

            """;
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: string.Empty,
                StandardError: "configure arguments: --conf-path=/etc/nginx/nginx.conf"),
            new FakeSshCommandOutput(
                StandardOutput: "include /etc/nginx/conf.d/*.conf;\n",
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: originalContent,
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: Encoding.UTF8.GetByteCount(NormalizeLf(updatedContent)).ToString(System.Globalization.CultureInfo.InvariantCulture),
                StandardError: string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new NginxConfigPathsProvider();

        var result = await provider.WriteConfigFileAsync(
            service,
            profile,
            "/etc/nginx/conf.d/kelpie-test.conf",
            "server.server_name",
            "delete");

        result.Error.Should().BeNull();
        result.BytesWritten.Should().Be(Encoding.UTF8.GetByteCount(NormalizeLf(updatedContent)));
        DecodeStandardInput(runner.LastRequest!).Should().Be(NormalizeLf(updatedContent));
    }

    [Fact]
    public async Task WriteConfigFileAsync_ShouldRejectPathOutsideProviderRules()
    {
        var profile = CreateProfile(KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: string.Empty,
                StandardError: "configure arguments: --conf-path=/etc/nginx/nginx.conf"),
            new FakeSshCommandOutput(
                StandardOutput: "include /etc/nginx/conf.d/*.conf;\n",
                StandardError: string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new NginxConfigPathsProvider();

        var result = await provider.WriteConfigFileAsync(
            service,
            profile,
            "/etc/passwd",
            "server.server_name",
            "replace",
            "localhost");

        result.Error.Should().Be("Requested path is not a provider-approved configuration file.");
        result.BytesWritten.Should().Be(0);
        runner.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task RollbackConfigFileAsync_ShouldRestoreProviderApprovedPath()
    {
        var profile = CreateProfile(KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: string.Empty,
                StandardError: "configure arguments: --conf-path=/etc/nginx/nginx.conf"),
            new FakeSshCommandOutput(
                StandardOutput: "include /etc/nginx/conf.d/*.conf;\n",
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: "128",
                StandardError: string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new NginxConfigPathsProvider();

        var result = await provider.RollbackConfigFileAsync(
            service,
            profile,
            "/etc/nginx/conf.d/kelpie-test.conf");

        result.Error.Should().BeNull();
        result.Changed.Should().BeTrue();
        result.Path.Should().Be("/etc/nginx/conf.d/kelpie-test.conf");
        result.BackupPath.Should().Be("/etc/nginx/conf.d/kelpie-test.conf.kelpiebakup");
        runner.LastRequest!.CommandName.Should().Be("service_config_nginx_helper_rollback");
        DecodeArgument(runner.LastRequest, "pathBase64").Should().Be("/etc/nginx/conf.d/kelpie-test.conf");
    }

    [Fact]
    public async Task CommitConfigFileAsync_ShouldRemoveProviderApprovedBackup()
    {
        var profile = CreateProfile(KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: string.Empty,
                StandardError: "configure arguments: --conf-path=/etc/nginx/nginx.conf"),
            new FakeSshCommandOutput(
                StandardOutput: "include /etc/nginx/conf.d/*.conf;\n",
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: "1",
                StandardError: string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new NginxConfigPathsProvider();

        var result = await provider.CommitConfigFileAsync(
            service,
            profile,
            "/etc/nginx/conf.d/kelpie-test.conf");

        result.Error.Should().BeNull();
        result.Changed.Should().BeTrue();
        result.BackupPath.Should().Be("/etc/nginx/conf.d/kelpie-test.conf.kelpiebakup");
        runner.LastRequest!.CommandName.Should().Be("service_config_nginx_helper_commit");
        DecodeArgument(runner.LastRequest, "pathBase64").Should().Be("/etc/nginx/conf.d/kelpie-test.conf");
    }

    [Fact]
    public async Task EnablePhpAsync_ShouldInsertIndexAndPhpLocationThenTestAndCommit()
    {
        var profile = CreateProfile(KelpiePolicyMode.Expert);
        const string originalContent = """
            server {
                listen 80;
                root /var/www/html;
                index index.html index.htm;

                location / {
                    try_files $uri $uri/ =404;
                }
            }

            """;
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: string.Empty,
                StandardError: "configure arguments: --conf-path=/etc/nginx/nginx.conf"),
            new FakeSshCommandOutput(
                StandardOutput: "include /etc/nginx/conf.d/*.conf;\n",
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: originalContent,
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: "256",
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: "/etc/nginx/sites-enabled/default\n",
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: string.Empty,
                StandardError: "nginx: configuration file /etc/nginx/nginx.conf test is successful\n"),
            new FakeSshCommandOutput(
                StandardOutput: string.Empty,
                StandardError: "configure arguments: --conf-path=/etc/nginx/nginx.conf"),
            new FakeSshCommandOutput(
                StandardOutput: "include /etc/nginx/conf.d/*.conf;\n",
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: "1",
                StandardError: string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new NginxConfigPathsProvider();

        var result = await provider.EnablePhpAsync(
            service,
            profile,
            "default",
            "/run/php/php8.3-fpm.sock",
            ".php");

        result.Error.Should().BeNull();
        result.Path.Should().Be("/etc/nginx/conf.d/default.conf");
        result.Changed.Should().BeTrue();
        result.Tested.Should().BeTrue();
        result.Committed.Should().BeTrue();
        result.RolledBack.Should().BeFalse();
        runner.Requests.Select(request => request.CommandName).Should().ContainInOrder(
            "service_config_nginx_helper_apply",
            "service_config_nginx_test_config",
            "service_config_nginx_helper_commit");
        var writtenContent = DecodeStandardInput(
            runner.Requests.Single(request => request.CommandName == "service_config_nginx_helper_apply"));
        writtenContent.Should().Contain("index index.php index.html index.htm;");
        writtenContent.Should().Contain("listen 80 default_server;");
        writtenContent.Should().Contain("location ~ \\.php$");
        writtenContent.Should().Contain("include snippets/fastcgi-php.conf;");
        writtenContent.Should().Contain("fastcgi_pass unix:/run/php/php8.3-fpm.sock;");
        writtenContent.Should().Contain("try_files $uri $uri/ =404;");
    }

    [Fact]
    public async Task EnablePhpAsync_ShouldPreferHttpSiteIncludeOverModulesInclude()
    {
        var profile = CreateProfile(KelpiePolicyMode.Expert);
        const string originalContent = """
            server {
                listen 80;
                root /var/www/html;
            }

            """;
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: string.Empty,
                StandardError: "configure arguments: --conf-path=/etc/nginx/nginx.conf"),
            new FakeSshCommandOutput(
                StandardOutput: """
                    include /etc/nginx/modules-enabled/*.conf;
                    events {}
                    http {
                        include /etc/nginx/mime.types;
                        include /etc/nginx/conf.d/*.conf;
                        include /etc/nginx/sites-enabled/*;
                    }

                    """,
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: originalContent,
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: "256",
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: "/etc/nginx/sites-enabled/default\n",
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: string.Empty,
                StandardError: "nginx: configuration file /etc/nginx/nginx.conf test is successful\n"),
            new FakeSshCommandOutput(
                StandardOutput: string.Empty,
                StandardError: "configure arguments: --conf-path=/etc/nginx/nginx.conf"),
            new FakeSshCommandOutput(
                StandardOutput: "include /etc/nginx/conf.d/*.conf;\ninclude /etc/nginx/sites-enabled/*;\n",
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: "1",
                StandardError: string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new NginxConfigPathsProvider();

        var result = await provider.EnablePhpAsync(
            service,
            profile,
            "default",
            "/run/php/php8.3-fpm.sock",
            ".php");

        result.Error.Should().BeNull();
        result.Path.Should().Be("/etc/nginx/conf.d/default.conf");
        runner.Requests.Select(request => request.CommandName)
            .Should().Contain("service_config_nginx_helper_apply");
        DecodeArgument(
            runner.Requests.Single(request => request.CommandName == "service_config_nginx_read_config" && DecodeArgument(request, "pathBase64").EndsWith("default.conf", StringComparison.Ordinal)),
            "pathBase64").Should().Be("/etc/nginx/conf.d/default.conf");
    }

    [Fact]
    public async Task EnablePhpAsync_ShouldCreateFixedSiteConfigWhenTargetFileDoesNotExist()
    {
        var profile = CreateProfile(KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: string.Empty,
                StandardError: "configure arguments: --conf-path=/etc/nginx/nginx.conf"),
            new FakeSshCommandOutput(
                StandardOutput: "include /etc/nginx/conf.d/*.conf;\n",
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: string.Empty,
                StandardError: "ERROR: config path is not a regular file",
                ExitCode: 1),
            new FakeSshCommandOutput(
                StandardOutput: "256",
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: string.Empty,
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: string.Empty,
                StandardError: "nginx: configuration file /etc/nginx/nginx.conf test is successful\n"),
            new FakeSshCommandOutput(
                StandardOutput: string.Empty,
                StandardError: "configure arguments: --conf-path=/etc/nginx/nginx.conf"),
            new FakeSshCommandOutput(
                StandardOutput: "include /etc/nginx/conf.d/*.conf;\n",
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: "1",
                StandardError: string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new NginxConfigPathsProvider();

        var result = await provider.EnablePhpAsync(
            service,
            profile,
            "default",
            "/run/php/php8.3-fpm.sock",
            ".php");

        result.Error.Should().BeNull();
        result.Changed.Should().BeTrue();
        result.Committed.Should().BeTrue();
        result.Warnings.Should().Contain("Nginx site configuration file did not exist; generated a fixed default server block.");
        var writtenContent = DecodeStandardInput(
            runner.Requests.Single(request => request.CommandName == "service_config_nginx_helper_apply"));
        writtenContent.Should().Contain("server_name _;");
        writtenContent.Should().Contain("root /var/www/html;");
        writtenContent.Should().Contain("listen 80 default_server;");
        writtenContent.Should().Contain("index index.php index.html index.htm;");
        writtenContent.Should().Contain("location ~ \\.php$");
        writtenContent.Should().Contain("fastcgi_pass unix:/run/php/php8.3-fpm.sock;");
    }

    [Fact]
    public async Task EnablePhpAsync_ShouldBeIdempotentWhenTemplateAlreadyExists()
    {
        var profile = CreateProfile(KelpiePolicyMode.Expert);
        const string existingContent = """
            server {
                listen 80 default_server;
                root /var/www/html;
                index index.php index.html index.htm;

                location ~ \.php$ {
                    include snippets/fastcgi-php.conf;
                    fastcgi_pass unix:/run/php/php8.3-fpm.sock;
                }
            }

            """;
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: string.Empty,
                StandardError: "configure arguments: --conf-path=/etc/nginx/nginx.conf"),
            new FakeSshCommandOutput(
                StandardOutput: "include /etc/nginx/conf.d/*.conf;\n",
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: existingContent,
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: string.Empty,
                StandardError: string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new NginxConfigPathsProvider();

        var result = await provider.EnablePhpAsync(
            service,
            profile,
            "default",
            "/run/php/php8.3-fpm.sock",
            ".php");

        result.Error.Should().BeNull();
        result.Changed.Should().BeFalse();
        result.Tested.Should().BeFalse();
        runner.Requests.Select(request => request.CommandName)
            .Should().NotContain("service_config_nginx_test_config");
        runner.Requests.Select(request => request.CommandName)
            .Should().Contain("service_config_nginx_helper_apply");
    }

    [Fact]
    public async Task EnablePhpAsync_ShouldDisableConflictingDefaultServerSiteEvenWhenPhpTemplateExists()
    {
        var profile = CreateProfile(KelpiePolicyMode.Expert);
        const string existingContent = """
            server {
                listen 80 default_server;
                root /var/www/html;
                index index.php index.html index.htm;

                location ~ \.php$ {
                    include snippets/fastcgi-php.conf;
                    fastcgi_pass unix:/run/php/php8.3-fpm.sock;
                }
            }

            """;
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: string.Empty,
                StandardError: "configure arguments: --conf-path=/etc/nginx/nginx.conf"),
            new FakeSshCommandOutput(
                StandardOutput: "include /etc/nginx/conf.d/*.conf;\ninclude /etc/nginx/sites-enabled/*;\n",
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: existingContent,
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: "/etc/nginx/sites-enabled/default\n",
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: string.Empty,
                StandardError: "nginx: configuration file /etc/nginx/nginx.conf test is successful\n"),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new NginxConfigPathsProvider();

        var result = await provider.EnablePhpAsync(
            service,
            profile,
            "default",
            "/run/php/php8.3-fpm.sock",
            ".php");

        result.Error.Should().BeNull();
        result.Changed.Should().BeTrue();
        result.Tested.Should().BeTrue();
        result.Committed.Should().BeTrue();
        result.BytesWritten.Should().Be(0);
        result.Warnings.Should().Contain("Disabled 1 conflicting Nginx default_server site link(s).");
        runner.Requests.Select(request => request.CommandName).Should().ContainInOrder(
            "service_config_nginx_helper_apply",
            "service_config_nginx_test_config");
        runner.Requests.Select(request => request.CommandName)
            .Should().Contain("service_config_nginx_helper_plan");
        runner.Requests.Select(request => request.CommandName)
            .Should().Contain("service_config_nginx_helper_commit");
    }

    [Fact]
    public async Task EnablePhpAsync_ShouldRejectUnsafeSocketPathBeforeRemoteCommands()
    {
        var profile = CreateProfile(KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner([]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new NginxConfigPathsProvider();

        var result = await provider.EnablePhpAsync(
            service,
            profile,
            "default",
            "/tmp/php-fpm.sock",
            ".php");

        result.Error.Should().Be("PHP-FPM socketPath is invalid.");
        runner.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task EnablePhpAsync_ShouldRollbackWhenNginxTestFails()
    {
        var profile = CreateProfile(KelpiePolicyMode.Expert);
        const string originalContent = """
            server {
                listen 80;
                root /var/www/html;
            }

            """;
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: string.Empty,
                StandardError: "configure arguments: --conf-path=/etc/nginx/nginx.conf"),
            new FakeSshCommandOutput(
                StandardOutput: "include /etc/nginx/conf.d/*.conf;\n",
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: originalContent,
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: "256",
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: "/etc/nginx/sites-enabled/default\n",
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: string.Empty,
                StandardError: "nginx: [emerg] invalid test config\n",
                ExitCode: 1),
            new FakeSshCommandOutput(
                StandardOutput: string.Empty,
                StandardError: "configure arguments: --conf-path=/etc/nginx/nginx.conf"),
            new FakeSshCommandOutput(
                StandardOutput: "include /etc/nginx/conf.d/*.conf;\n",
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: "128",
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: "/etc/nginx/sites-enabled/default\n",
                StandardError: string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new NginxConfigPathsProvider();

        var result = await provider.EnablePhpAsync(
            service,
            profile,
            "default",
            "/run/php/php8.3-fpm.sock",
            ".php");

        result.Error.Should().Contain("Nginx config test failed");
        result.Changed.Should().BeTrue();
        result.Tested.Should().BeTrue();
        result.RolledBack.Should().BeTrue();
        result.Committed.Should().BeFalse();
        runner.Requests.Select(request => request.CommandName).Should().ContainInOrder(
            "service_config_nginx_helper_apply",
            "service_config_nginx_test_config",
            "service_config_nginx_helper_rollback");
        runner.Requests.Single(request => request.CommandName == "service_config_nginx_helper_rollback")
            .StandardInput.Should().Contain("transactionId");
    }

    [Fact]
    public async Task ReadLogfileAsync_ShouldReadProviderApprovedAccessLog()
    {
        var profile = CreateProfile();
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: "127.0.0.1 - - [14/Jun/2026:10:00:00 +0000] \"GET / HTTP/1.1\" 200 12\n",
                StandardError: string.Empty),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new NginxConfigPathsProvider();

        var result = await provider.ReadLogfileAsync(service, profile, "access", sinceMinutes: 10, lines: 500);

        result.ServiceKey.Should().Be("nginx");
        result.DisplayName.Should().Be("Nginx");
        result.LogKey.Should().Be("access");
        result.Path.Should().Be("/var/log/nginx/access.log");
        result.Content.Should().Contain("GET / HTTP/1.1");
        result.Encoding.Should().Be("utf-8");
        result.Truncated.Should().BeFalse();
        result.Error.Should().BeNull();
        runner.LastRequest!.CommandName.Should().Be("service_logfile_nginx_read");
        DecodeArgument(runner.LastRequest, "pathBase64").Should().Be("/var/log/nginx/access.log");
        DecodeArgument(runner.LastRequest, "allowedPathsBase64").Should().Be("/var/log/nginx/access.log\n/var/log/nginx/error.log");
        runner.LastRequest.Arguments["lines"].Should().Be("500");
        runner.LastRequest.Arguments["sinceMinutes"].Should().Be("10");
    }

    [Fact]
    public async Task ReadLogfileAsync_ShouldRejectUnsupportedLogKey()
    {
        var profile = CreateProfile();
        var runner = new FakeSshCommandRunner([]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new NginxConfigPathsProvider();

        var result = await provider.ReadLogfileAsync(service, profile, "passwd");

        result.Error.Should().Be("Unsupported Nginx logKey: passwd");
        result.Content.Should().BeEmpty();
        runner.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadLogfileAsync_ShouldClampFilterArguments()
    {
        var profile = CreateProfile();
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: "ok\n",
                StandardError: "KELPIE_TRUNCATED=1\nKELPIE_SINCE_FILTER_PARTIAL=1\n"),
        ]);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var provider = new NginxConfigPathsProvider();

        var result = await provider.ReadLogfileAsync(service, profile, "error", sinceMinutes: 99999, lines: 99999);

        result.Truncated.Should().BeTrue();
        result.Warnings.Should().Contain("Content was truncated by the maximum read size.");
        result.Warnings.Should().Contain("Some log lines did not have a recognized timestamp and were excluded by sinceMinutes.");
        runner.LastRequest!.Arguments["lines"].Should().Be("5000");
        runner.LastRequest.Arguments["sinceMinutes"].Should().Be("1440");
    }

    private static string DecodeArgument(SshCommandRequest request, string name)
    {
        if (request.CommandName.StartsWith("service_config_nginx_helper_", StringComparison.Ordinal))
        {
            using var data = JsonDocument.Parse(request.StandardInput!);
            return data.RootElement.GetProperty("target").GetString()!;
        }
        return Encoding.UTF8.GetString(Convert.FromBase64String(request.Arguments[name]));
    }

    private static string DecodeStandardInput(SshCommandRequest request)
    {
        request.StandardInput.Should().NotBeNull();
        using var data = JsonDocument.Parse(request.StandardInput!);
        return Encoding.UTF8.GetString(Convert.FromBase64String(data.RootElement.GetProperty("contentBase64").GetString()!));
    }

    private static string NormalizeLf(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    }

    private static SshConnectionProfile CreateProfile(KelpiePolicyMode mode = KelpiePolicyMode.Safe)
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
        };
    }

    private sealed class FakeSshCommandRunner : ISshCommandRunner
    {
        private readonly Queue<FakeSshCommandOutput> _outputs;

        public FakeSshCommandRunner(IEnumerable<FakeSshCommandOutput> outputs)
        {
            _outputs = new Queue<FakeSshCommandOutput>(outputs);
        }

        public SshCommandRequest? LastRequest { get; private set; }

        public IReadOnlyList<SshCommandRequest> Requests => _requests;

        private readonly List<SshCommandRequest> _requests = [];
        private bool _writesFile;
        private int _changedLinks;
        private int _bytes;
        private string _transaction = "0123456789abcdef0123456789abcdef";

        public Task<SshCommandResult> ExecuteAsync(
            SshCommandRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            _requests.Add(request);
            var output = request.CommandName.StartsWith("service_config_nginx_helper_", StringComparison.Ordinal)
                ? HelperReply(request) : _outputs.Dequeue();
            return Task.FromResult(new SshCommandResult(
                request.CommandName,
                request.CommandText,
                output.ExitCode,
                output.StandardOutput,
                output.StandardError,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                TimedOut: false));
        }
        private FakeSshCommandOutput HelperReply(SshCommandRequest request)
        {
            using var data = JsonDocument.Parse(request.StandardInput!);
            var value = data.RootElement;
            var operation = request.CommandName["service_config_nginx_helper_".Length..];
            var state = "planned";
            var error = string.Empty;
            var code = 0;
            if (operation == "plan")
            {
                _transaction = value.GetProperty("requestId").GetString()!;
                _writesFile = value.TryGetProperty("contentBase64", out var content) && content.ValueKind == JsonValueKind.String;
                _bytes = _writesFile ? Convert.FromBase64String(content.GetString()!).Length : 0;
            }
            else if (operation == "apply")
            {
                if (_writesFile)
                {
                    var write = _outputs.Dequeue();
                    code = write.ExitCode;
                    error = write.StandardError;
                }
                if (code == 0 && value.GetProperty("disableDefaultSites").GetBoolean())
                {
                    var links = _outputs.Dequeue();
                    code = links.ExitCode;
                    error = links.StandardError;
                    _changedLinks = links.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
                }
                state = code == 0 ? (_writesFile || _changedLinks > 0 ? "applied" : "no-op") : "recovery-required";
            }
            else if (operation is "commit" or "rollback")
            {
                if (_outputs.Count > 0)
                {
                    var action = _outputs.Dequeue();
                    code = action.ExitCode;
                    error = action.StandardError;
                }
                if (operation == "rollback" && _writesFile && _changedLinks > 0 && _outputs.Count > 0)
                {
                    var links = _outputs.Dequeue();
                    code = links.ExitCode;
                    error = links.StandardError;
                }
                state = operation == "commit" ? "committed" : "rolled-back";
            }
            else if (operation == "status") state = "applied";
            else if (operation == "check") state = "allowed";
            return new FakeSshCommandOutput(JsonSerializer.Serialize(new
            {
                protocol = 1, requestId = value.GetProperty("requestId").GetString(), transactionId = _transaction,
                ok = code == 0, state, configChanged = state != "no-op" && _writesFile,
                linksChanged = state == "no-op" ? 0 : _changedLinks, bytesWritten = state == "no-op" ? 0 : _bytes,
                intentDigest = new string('a', 64), originalHash = new string('b', 64), currentHash = new string('c', 64),
                recoverable = state == "applied", error,
            }), string.Empty, code);
        }
    }

    private sealed record FakeSshCommandOutput(
        string StandardOutput,
        string StandardError,
        int ExitCode = 0);
}

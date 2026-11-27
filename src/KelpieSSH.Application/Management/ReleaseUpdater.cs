using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

namespace KelpieSSH.Application.Management;

/// <summary>A verified Windows MSI offered by the trusted release API.</summary>
public sealed record MsiRelease(Version Version, Uri DownloadUrl, string Sha256, long Size);

/// <summary>Bounded release discovery and SHA-256 verified download; never executes installers.</summary>
public sealed class ReleaseUpdater(HttpClient client)
{
    public const long MaximumInstallerBytes = 512L * 1024 * 1024;
    private const int MaximumMetadataBytes = 1024 * 1024;

    /// <summary>Finds a newer stable MSI through a bounded trusted HTTPS API response.</summary>
    public async Task<MsiRelease?> CheckAsync(string apiUrl, Version currentVersion, CancellationToken cancellationToken)
    {
        new TrayPreferences { ReleaseApiUrl = apiUrl }.Validate();
        using var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
        request.Headers.UserAgent.ParseAdd("KelpieSSH-Updater/1.0");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        RequireHttps(response.RequestMessage?.RequestUri ?? request.RequestUri!);
        if (response.Content.Headers.ContentLength > MaximumMetadataBytes) throw new InvalidDataException("Release metadata exceeds the limit.");
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var bytes = new MemoryStream();
        await CopyBoundedAsync(input, bytes, MaximumMetadataBytes, cancellationToken);
        var release = Select(bytes.ToArray(), currentVersion);
        var source = new Uri(apiUrl);
        var segments = source.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (release is not null && source.Host.Equals("api.github.com", StringComparison.OrdinalIgnoreCase)
            && segments.Length >= 3 && segments[0] == "repos"
            && (!release.DownloadUrl.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
                || !release.DownloadUrl.AbsolutePath.StartsWith("/" + segments[1] + "/" + segments[2] + "/releases/download/", StringComparison.Ordinal)))
            throw new InvalidDataException("The download is outside the selected GitHub repository.");
        return release;
    }

    /// <summary>Validates release metadata and selects exactly one compatible newer MSI.</summary>
    public static MsiRelease? Select(ReadOnlyMemory<byte> metadata, Version currentVersion)
    {
        try { return SelectCore(metadata, currentVersion); }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        { throw new InvalidDataException("Release metadata is invalid.", ex); }
    }

    private static MsiRelease? SelectCore(ReadOnlyMemory<byte> metadata, Version currentVersion)
    {
        if (metadata.Length > MaximumMetadataBytes) throw new InvalidDataException("Release metadata exceeds the limit.");
        using var document = JsonDocument.Parse(metadata, new JsonDocumentOptions { MaxDepth = 16 });
        var root = document.RootElement;
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean()) return null;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.StartsWith('v') || tag.StartsWith('V') ? tag[1..] : tag, out var version) || version.Revision < 0)
            throw new InvalidDataException("Release version must have four numeric parts.");
        if (version <= currentVersion) return null;
        var assets = root.GetProperty("assets");
        if (assets.GetArrayLength() > 128) throw new InvalidDataException("Too many release assets.");
        var expected = $"KelpieSSH-{version}-x64.msi";
        var matches = assets.EnumerateArray().Where(a => a.GetProperty("name").GetString() == expected).ToArray();
        if (matches.Length != 1) throw new InvalidDataException("The release must contain exactly one matching x64 MSI.");
        var asset = matches[0];
        var digest = asset.GetProperty("digest").GetString() ?? "";
        if (!digest.StartsWith("sha256:", StringComparison.Ordinal) || digest.Length != 71
            || !digest[7..].All(Uri.IsHexDigit)) throw new InvalidDataException("The MSI SHA-256 digest is required.");
        if (!Uri.TryCreate(asset.GetProperty("browser_download_url").GetString(), UriKind.Absolute, out var uri))
            throw new InvalidDataException("The MSI download URL is invalid.");
        RequireHttps(uri);
        var size = asset.GetProperty("size").GetInt64();
        if (size <= 0 || size > MaximumInstallerBytes) throw new InvalidDataException("The MSI size is invalid.");
        return new(version, uri, digest[7..].ToLowerInvariant(), size);
    }

    /// <summary>Downloads to a new file and removes only its own partial file after failure.</summary>
    public async Task DownloadAsync(MsiRelease release, string destination, CancellationToken cancellationToken)
    {
        RequireHttps(release.DownloadUrl);
        if (release.Size <= 0 || release.Size > MaximumInstallerBytes) throw new InvalidDataException("The MSI size is invalid.");
        using var request = new HttpRequestMessage(HttpMethod.Get, release.DownloadUrl);
        request.Headers.UserAgent.ParseAdd("KelpieSSH-Updater/1.0");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        RequireHttps(response.RequestMessage?.RequestUri ?? request.RequestUri!);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
        var created = false;
        try
        {
            await using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            {
                created = true;
                await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
                await CopyBoundedAsync(input, output, release.Size, cancellationToken);
            }
            await VerifyAsync(destination, release.Sha256, release.Size, cancellationToken);
        }
        catch { if (created && File.Exists(destination)) File.Delete(destination); throw; }
    }

    /// <summary>Verifies the declared size and SHA-256 before installer execution.</summary>
    public static async Task VerifyAsync(string path, string expectedSha256, long expectedSize, CancellationToken cancellationToken)
    {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        if (expectedSize <= 0 || expectedSize > MaximumInstallerBytes || file.Length != expectedSize)
            throw new InvalidDataException("The installer size does not match the release.");
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(file, cancellationToken));
        if (!string.Equals(hash, expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The installer SHA-256 does not match the release.");
    }

    private static void RequireHttps(Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment))
            throw new InvalidDataException("Release requests require HTTPS without credentials or fragments.");
    }

    private static async Task CopyBoundedAsync(Stream input, Stream output, long limit, CancellationToken ct)
    {
        var buffer = new byte[81920]; long total = 0;
        while (true)
        {
            var count = await input.ReadAsync(buffer, ct);
            if (count == 0) break;
            total += count;
            if (total > limit) throw new InvalidDataException("The download exceeds its declared limit.");
            await output.WriteAsync(buffer.AsMemory(0, count), ct);
        }
    }
}

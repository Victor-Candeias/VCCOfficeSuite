using System.Net;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Vcc.Office.CoreShell;

public sealed record SuiteUpdate(string Version, string ArchiveName, Uri ArchiveUrl,
    Uri ChecksumsUrl, string ExpectedSha256, string ReleasePageUrl);
public sealed record SuiteUpdateCheck(string CurrentVersion, SuiteUpdate? Available, string Message);

/// <summary>
/// GitHub Releases updater: checks a fixed official source and stages verified archives.
/// It never executes packages or modifies an installed application.
/// </summary>
public sealed class SuiteUpdateService
{
    private const string ReleasesApi = "https://api.github.com/repos/Victor-Candeias/VCCOfficeSuite/releases/latest";
    private const long MaximumPackageBytes = 1024L * 1024L * 1024L;
    private const int MaximumManifestBytes = 128 * 1024;
    private readonly HttpClient _http;
    private readonly string _stagingRoot;

    public SuiteUpdateService(HttpClient http, string stagingRoot)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _stagingRoot = Path.GetFullPath(stagingRoot ?? throw new ArgumentNullException(nameof(stagingRoot)));
    }

    public static string PlatformId()
    {
        if (OperatingSystem.IsWindows() && RuntimeInformation.OSArchitecture == Architecture.X64) return "win-x64";
        if (OperatingSystem.IsLinux() && RuntimeInformation.OSArchitecture == Architecture.X64) return "linux-x64";
        if (OperatingSystem.IsMacOS() && RuntimeInformation.OSArchitecture is Architecture.X64 or Architecture.Arm64) return "macos";
        return "";
    }

    public static bool IsNewer(string candidate, string current)
    {
        if (!Version.TryParse(candidate.TrimStart('v', 'V').Split('-')[0], out var next) ||
            !Version.TryParse(current.TrimStart('v', 'V').Split('-')[0], out var installed)) return false;
        // Pre-releases are not offered by this stable channel.
        if (candidate.Contains('-', StringComparison.Ordinal)) return false;
        return next > installed;
    }

    public static string? FindSha256(string manifest, string archiveName)
    {
        foreach (var line in manifest.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (trimmed.Length < 66 || !trimmed[..64].All(Uri.IsHexDigit)) continue;
            var name = trimmed[64..].TrimStart(' ', '*').Trim();
            if (string.Equals(name, archiveName, StringComparison.Ordinal))
                return trimmed[..64].ToLowerInvariant();
        }
        return null;
    }

    public async Task<SuiteUpdateCheck> CheckAsync(string currentVersion, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(currentVersion)) throw new ArgumentException("Versão atual em falta.", nameof(currentVersion));
        using var request = new HttpRequestMessage(HttpMethod.Get, ReleasesApi);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("VCCOfficeSuite", "1.0"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return new(currentVersion, null, "Ainda não existe uma release oficial publicada.");
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var json = await JsonDocument.ParseAsync(stream, new JsonDocumentOptions { MaxDepth = 32 }, ct);
        var release = json.RootElement;
        if (release.GetProperty("draft").GetBoolean() || release.GetProperty("prerelease").GetBoolean())
            return new(currentVersion, null, "Não existe atualização estável disponível.");
        var tag = release.GetProperty("tag_name").GetString() ?? "";
        if (!IsNewer(tag, currentVersion))
            return new(currentVersion, null, "A aplicação está atualizada ou não existe versão estável mais recente.");

        var platform = PlatformId();
        if (string.IsNullOrEmpty(platform))
            return new(currentVersion, null, "Atualizações ainda não disponíveis para esta arquitetura.");
        var assets = release.GetProperty("assets").EnumerateArray().Select(a => new
        {
            Name = a.GetProperty("name").GetString() ?? "",
            Url = a.GetProperty("browser_download_url").GetString() ?? ""
        }).ToArray();
        var archive = assets.SingleOrDefault(a =>
            a.Name.StartsWith("VCCOfficeSuite-" + tag + "-", StringComparison.Ordinal) &&
            a.Name.EndsWith("-" + platform + ".zip", StringComparison.Ordinal));
        var checksums = assets.SingleOrDefault(a => a.Name == "SHA256SUMS.txt");
        if (archive is null || checksums is null)
            return new(currentVersion, null, "Esta release não tem os pacotes oficiais esperados.");
        var archiveUri = ValidateReleaseAssetUrl(archive.Url);
        var hashesUri = ValidateReleaseAssetUrl(checksums.Url);
        var releasePage = release.GetProperty("html_url").GetString() ?? "";
        if (!Uri.TryCreate(releasePage, UriKind.Absolute, out var releasePageUri) ||
            releasePageUri.Scheme != Uri.UriSchemeHttps ||
            releasePageUri.Host != "github.com" ||
            !releasePageUri.AbsolutePath.StartsWith("/Victor-Candeias/VCCOfficeSuite/releases/tag/", StringComparison.Ordinal))
            throw new InvalidDataException("Página de release inesperada.");

        using var manifestResponse = await _http.GetAsync(hashesUri, HttpCompletionOption.ResponseHeadersRead, ct);
        manifestResponse.EnsureSuccessStatusCode();
        if (manifestResponse.Content.Headers.ContentLength is > MaximumManifestBytes)
            throw new InvalidDataException("Manifesto SHA-256 demasiado grande.");
        await using var manifestStream = await manifestResponse.Content.ReadAsStreamAsync(ct);
        using var manifestBuffer = new MemoryStream();
        await CopyLimitedAsync(manifestStream, manifestBuffer, MaximumManifestBytes, ct);
        var hash = FindSha256(Encoding.UTF8.GetString(manifestBuffer.ToArray()), archive.Name);
        if (hash is null) throw new InvalidDataException("O SHA-256 do pacote não consta do manifesto.");
        return new(currentVersion,
            new SuiteUpdate(tag, archive.Name, archiveUri, hashesUri, hash, releasePageUri.AbsoluteUri),
            "Nova versão disponível: " + tag);
    }

    private static Uri ValidateReleaseAssetUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || uri.Host != "github.com" ||
            !uri.AbsolutePath.StartsWith("/Victor-Candeias/VCCOfficeSuite/releases/download/", StringComparison.Ordinal) ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
            throw new InvalidDataException("Origem de atualização inesperada.");
        return uri;
    }

    private static async Task CopyLimitedAsync(Stream source, Stream destination, long max, CancellationToken ct)
    {
        var buffer = new byte[81920];
        long count = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, ct)) != 0)
        {
            count += read;
            if (count > max) throw new InvalidDataException("O download excede o tamanho máximo permitido.");
            await destination.WriteAsync(buffer.AsMemory(0, read), ct);
        }
    }

    public async Task<string> StageAsync(SuiteUpdate update, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        if (string.IsNullOrWhiteSpace(update.ArchiveName) ||
            Path.GetFileName(update.ArchiveName) != update.ArchiveName ||
            !update.ArchiveName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
            update.ExpectedSha256.Length != 64 || !update.ExpectedSha256.All(Uri.IsHexDigit))
            throw new InvalidDataException("Pacote ou checksum inválido.");
        // Revalidate URLs even if a caller constructs the update manually.
        ValidateReleaseAssetUrl(update.ArchiveUrl.AbsoluteUri);
        ValidateReleaseAssetUrl(update.ChecksumsUrl.AbsoluteUri);
        Directory.CreateDirectory(_stagingRoot);
        var destination = Path.Combine(_stagingRoot, update.ArchiveName);
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".download";
        try
        {
            using var response = await _http.GetAsync(update.ArchiveUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is > MaximumPackageBytes)
                throw new InvalidDataException("Pacote demasiado grande.");
            await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 81920, useAsync: true))
            await using (var source = await response.Content.ReadAsStreamAsync(ct))
                await CopyLimitedAsync(source, file, MaximumPackageBytes, ct);
            await using var verifyStream = File.OpenRead(temporary);
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(verifyStream, ct));
            if (!string.Equals(actual, update.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("A integridade SHA-256 do pacote não foi confirmada.");
            File.Move(temporary, destination, true);
            return destination;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

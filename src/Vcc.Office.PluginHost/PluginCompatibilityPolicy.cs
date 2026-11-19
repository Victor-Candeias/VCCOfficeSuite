using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Vcc.Office.PluginHost;

/// <summary>Declarative compatibility. Absence of optional fields preserves API v1 plugins.</summary>
public sealed record PluginRequirement(string Id, string? MinVersion = null);
public sealed record PluginPackageMetadata(
    string Id, string Assembly, string EntryType, int ApiVersion,
    string? MinHostVersion = null, string? MaxHostVersion = null,
    PluginRequirement[]? Requires = null);

public static class PluginCompatibilityPolicy
{
    private static readonly Regex IdPattern = new("^[a-z][a-z0-9-]{0,63}$", RegexOptions.CultureInvariant);

    public static PluginPackageMetadata Read(string json)
    {
        var value = JsonSerializer.Deserialize<PluginPackageMetadata>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Manifesto de plugin vazio.");
        if (value.ApiVersion != 1 || string.IsNullOrWhiteSpace(value.Id) ||
            !IdPattern.IsMatch(value.Id) || string.IsNullOrWhiteSpace(value.EntryType) ||
            string.IsNullOrWhiteSpace(value.Assembly) ||
            Path.GetFileName(value.Assembly) != value.Assembly ||
            !value.Assembly.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Manifesto ou contrato API incompatível.");
        if (value.Requires is { Length: > 32 } ||
            (value.Requires ?? []).Any(dependency =>
                dependency is null || string.IsNullOrWhiteSpace(dependency.Id) || !IdPattern.IsMatch(dependency.Id) ||
                dependency.Id.Equals(value.Id, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(dependency.MinVersion) &&
                    !Version.TryParse(dependency.MinVersion, out _))) ||
            (value.Requires ?? []).Select(x => x.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
            (value.Requires?.Length ?? 0))
            throw new InvalidDataException("Dependências de plugin inválidas.");
        if ((!string.IsNullOrWhiteSpace(value.MinHostVersion) &&
                !Version.TryParse(value.MinHostVersion, out _)) ||
            (!string.IsNullOrWhiteSpace(value.MaxHostVersion) &&
                !Version.TryParse(value.MaxHostVersion, out _)))
            throw new InvalidDataException("Versão de host inválida.");
        return value;
    }

    public static void EnsureCompatible(PluginPackageMetadata manifest, Version host,
        IReadOnlyDictionary<string, Version> installed)
    {
        if (!string.IsNullOrWhiteSpace(manifest.MinHostVersion) &&
            host < Version.Parse(manifest.MinHostVersion))
            throw new InvalidDataException(manifest.Id + ": necessita de uma versão mais recente da suite.");
        if (!string.IsNullOrWhiteSpace(manifest.MaxHostVersion) &&
            host > Version.Parse(manifest.MaxHostVersion))
            throw new InvalidDataException(manifest.Id + ": versão da suite não suportada.");
        foreach (var item in manifest.Requires ?? [])
        {
            if (!installed.TryGetValue(item.Id, out var version) ||
                (!string.IsNullOrWhiteSpace(item.MinVersion) && version < Version.Parse(item.MinVersion)))
                throw new InvalidDataException(manifest.Id + ": dependência ausente ou incompatível: " + item.Id);
        }
    }
}

/// <summary>Detached RSA-PSS/SHA-256 package signatures with explicit administrator-trusted PEM keys.</summary>
public static class PluginPackageSignature
{
    public static void Verify(string archivePath, string signatureFile, string trustedKeysDirectory)
    {
        if (!Directory.Exists(trustedKeysDirectory))
            throw new InvalidDataException("Não existem chaves públicas de confiança configuradas.");
        var keys = Directory.EnumerateFiles(trustedKeysDirectory, "*.pem",
            SearchOption.TopDirectoryOnly).ToArray();
        if (keys.Length is 0 or > 32)
            throw new InvalidDataException("Configuração de confiança do editor inválida.");
        byte[] signature;
        try { signature = Convert.FromBase64String(File.ReadAllText(signatureFile).Trim()); }
        catch (FormatException ex) { throw new InvalidDataException("Assinatura do pacote malformada.", ex); }
        using var archive = File.OpenRead(archivePath);
        var hash = SHA256.HashData(archive);
        foreach (var key in keys)
        {
            try
            {
                using var rsa = RSA.Create();
                rsa.ImportFromPem(File.ReadAllText(key));
                if (rsa.VerifyHash(hash, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
                    return;
            }
            catch (CryptographicException) { /* Other configured keys may match. */ }
            catch (ArgumentException) { /* Invalid key: do not accept this signer. */ }
        }
        throw new InvalidDataException("Assinatura digital do pacote não corresponde a um editor autorizado.");
    }
}

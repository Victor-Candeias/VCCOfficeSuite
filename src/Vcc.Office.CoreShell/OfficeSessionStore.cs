using System.Text.Json;

namespace Vcc.Office.CoreShell;

/// <summary>Estado das aplicações: a última usada e as janelas ainda abertas.</summary>
public sealed record OfficeSession(string? LastModuleId, IReadOnlyList<string> OpenModuleIds)
{
    public static OfficeSession Empty { get; } = new(null, []);
}

/// <summary>Recupera apenas aplicações registadas e atualmente disponíveis.</summary>
public static class OfficeSessionPolicy
{
    public static OfficeSession Normalize(OfficeSession? session, OfficeApplicationRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        if (session is null) return OfficeSession.Empty;

        var open = (session.OpenModuleIds ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => registry.Find(id)?.Id)
            .Where(id => id is not null && registry.Find(id)?.IsAvailable == true)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(26)
            .ToArray();

        var last = registry.Find(session.LastModuleId);
        return new(last?.IsAvailable == true ? last.Id : null, open);
    }

    public static OfficeSession Open(OfficeSession session, string moduleId, OfficeApplicationRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(registry);
        var module = registry.Find(moduleId);
        if (module?.IsAvailable != true)
            throw new ArgumentException("O módulo não está disponível.", nameof(moduleId));
        var normalized = Normalize(session, registry);
        return normalized with
        {
            LastModuleId = module.Id,
            OpenModuleIds = normalized.OpenModuleIds
                .Append(module.Id)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
    }

    public static OfficeSession Close(OfficeSession session, string moduleId, OfficeApplicationRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(session);
        var normalized = Normalize(session, registry);
        return normalized with
        {
            OpenModuleIds = normalized.OpenModuleIds
                .Where(id => !string.Equals(id, moduleId, StringComparison.OrdinalIgnoreCase))
                .ToArray()
        };
    }
}

/// <summary>Ficheiro JSON independente do estado do CoreShell, com backup e escrita atómica.</summary>
public sealed class OfficeSessionStore
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public OfficeSessionStore(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("O caminho é obrigatório.", nameof(filePath));
        FilePath = Path.GetFullPath(filePath);
    }

    public string FilePath { get; }

    public OfficeSession Load(OfficeApplicationRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        foreach (var path in new[] { FilePath, FilePath + ".bak" })
        {
            if (!File.Exists(path)) continue;
            try
            {
                var session = JsonSerializer.Deserialize<OfficeSession>(File.ReadAllText(path), Options);
                if (session is not null) return OfficeSessionPolicy.Normalize(session, registry);
            }
            catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
            {
                // Recuperar do backup ou começar com uma sessão vazia.
            }
        }
        return OfficeSession.Empty;
    }

    public void Save(OfficeSession session, OfficeApplicationRegistry registry)
    {
        var normalized = OfficeSessionPolicy.Normalize(session, registry);
        var directory = Path.GetDirectoryName(FilePath)
            ?? throw new InvalidOperationException("Caminho sem diretório.");
        Directory.CreateDirectory(directory);
        var temporary = FilePath + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(normalized, Options));
            if (File.Exists(FilePath)) File.Copy(FilePath, FilePath + ".bak", true);
            File.Move(temporary, FilePath, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}

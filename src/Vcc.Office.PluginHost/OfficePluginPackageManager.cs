using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Vcc.Office.PluginHost;

/// <summary>One verified plugin package operation, applied at the next process startup.</summary>
public sealed record PendingPluginChange(string Id, string Action, string? StagedName);
public sealed record PluginPackageReceipt(string Id, string Action);

/// <summary>
/// Stages trusted ZIP packages for an atomic startup replacement. Never overwrites a loaded DLL.
/// Packages must contain plugin.json and the entry DLL at ZIP root.
/// </summary>
public sealed class OfficePluginPackageManager
{
    private const long MaximumExpandedBytes = 128L * 1024 * 1024;
    private readonly string _root;
    private string UpdatesRoot => Path.Combine(_root, ".vcc-updates");
    private string StagingRoot => Path.Combine(UpdatesRoot, "staged");
    private string QueueFile => Path.Combine(UpdatesRoot, "queue.json");

    public OfficePluginPackageManager(string pluginsRoot)
    {
        if (string.IsNullOrWhiteSpace(pluginsRoot))
            throw new ArgumentException("Diretório dos plugins obrigatório.", nameof(pluginsRoot));
        _root = Path.GetFullPath(pluginsRoot);
    }

    public IReadOnlyList<PendingPluginChange> Pending => ReadQueue();

    public PluginPackageReceipt StageInstall(string archivePath)
    {
        if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
            throw new FileNotFoundException("Pacote ZIP não encontrado.", archivePath);
        string id;
        string assembly;
        using (var archive = ZipFile.OpenRead(archivePath))
        {
            if (archive.Entries.Count is 0 or > 256)
                throw new InvalidDataException("Quantidade de ficheiros do pacote inválida.");
            long total = 0;
            foreach (var entry in archive.Entries)
            {
                var name = entry.FullName.Replace('\\', '/');
                if (name.StartsWith("/", StringComparison.Ordinal) || name.Contains(':') ||
                    name.Split('/').Any(segment => segment is "." or ".."))
                    throw new InvalidDataException("Caminho inseguro no pacote: " + name);
                var unixType = (entry.ExternalAttributes >> 16) & 0xF000;
                if (unixType == 0xA000)
                    throw new InvalidDataException("Links simbólicos não são permitidos em plugins.");
                total = checked(total + entry.Length);
                if (total > MaximumExpandedBytes)
                    throw new InvalidDataException("Pacote demasiado grande.");
            }
            var manifestEntry = archive.GetEntry("plugin.json")
                ?? throw new InvalidDataException("O ZIP não contém plugin.json na raiz.");
            using var reader = new StreamReader(manifestEntry.Open());
            var manifest = JsonSerializer.Deserialize<PackageManifest>(reader.ReadToEnd(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("Manifesto do plugin vazio.");
            id = manifest.Id;
            assembly = manifest.Assembly;
            ValidateId(id);
            if (manifest.ApiVersion != 1 || string.IsNullOrWhiteSpace(manifest.EntryType) ||
                string.IsNullOrWhiteSpace(assembly) || Path.GetFileName(assembly) != assembly ||
                !assembly.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                archive.GetEntry(assembly) is null)
                throw new InvalidDataException("Manifesto, versão ou DLL incompatíveis.");
        }
        Directory.CreateDirectory(StagingRoot);
        var token = Guid.NewGuid().ToString("N");
        var stagedPath = Path.Combine(StagingRoot, token);
        try
        {
            ZipFile.ExtractToDirectory(archivePath, stagedPath);
            if (!File.Exists(Path.Combine(stagedPath, assembly)))
                throw new InvalidDataException("A DLL indicada não existe no pacote.");
            var pending = ReadQueue().ToList();
            var previous = pending.FirstOrDefault(change => change.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            pending.RemoveAll(change => change.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            var action = Directory.Exists(PluginFolder(id)) ? "update" : "install";
            pending.Add(new PendingPluginChange(id, action, token));
            SaveQueue(pending);
            CleanPreviousStage(previous);
            return new PluginPackageReceipt(id, action);
        }
        catch
        {
            if (Directory.Exists(stagedPath)) Directory.Delete(stagedPath, recursive: true);
            throw;
        }
    }

    public void StageRemove(string id)
    {
        ValidateId(id);
        var pending = ReadQueue().ToList();
        var previous = pending.FirstOrDefault(change => change.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (!Directory.Exists(PluginFolder(id)) && previous is null)
            throw new ArgumentException("Plugin não instalado.", nameof(id));
        pending.RemoveAll(change => change.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        pending.Add(new PendingPluginChange(id, "remove", null));
        SaveQueue(pending);
        CleanPreviousStage(previous);
    }

    /// <summary>Called before OfficePluginHost loads any assembly; failures stay queued for retry.</summary>
    public IReadOnlyList<string> ApplyPending()
    {
        var pending = ReadQueue().ToList();
        var errors = new List<string>();
        foreach (var change in pending.ToArray())
        {
            try
            {
                Apply(change);
                pending.Remove(change);
                SaveQueue(pending);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                                       ArgumentException or InvalidDataException)
            {
                errors.Add(change.Id + ": " + ex.Message);
            }
        }
        return errors;
    }

    private void Apply(PendingPluginChange change)
    {
        ValidateId(change.Id);
        var destination = PluginFolder(change.Id);
        var backup = Path.Combine(UpdatesRoot, "backup-" + Guid.NewGuid().ToString("N"));
        if (change.Action == "remove")
        {
            if (Directory.Exists(destination))
            {
                Directory.Move(destination, backup);
                try { Directory.Delete(backup, recursive: true); }
                catch { Directory.Move(backup, destination); throw; }
            }
            return;
        }
        if (change.Action is not ("install" or "update") ||
            !Guid.TryParseExact(change.StagedName, "N", out _))
            throw new InvalidDataException("Operação pendente inválida.");
        var stagedPath = Path.Combine(StagingRoot, change.StagedName!);
        if (!Directory.Exists(stagedPath)) throw new DirectoryNotFoundException("Pacote preparado inexistente.");
        if (Directory.Exists(destination)) Directory.Move(destination, backup);
        try
        {
            Directory.Move(stagedPath, destination);
            if (Directory.Exists(backup)) Directory.Delete(backup, recursive: true);
        }
        catch
        {
            if (Directory.Exists(destination)) Directory.Delete(destination, recursive: true);
            if (Directory.Exists(backup)) Directory.Move(backup, destination);
            throw;
        }
    }

    private List<PendingPluginChange> ReadQueue() => File.Exists(QueueFile)
        ? JsonSerializer.Deserialize<List<PendingPluginChange>>(File.ReadAllText(QueueFile)) ?? []
        : [];

    private void SaveQueue(List<PendingPluginChange> queue)
    {
        Directory.CreateDirectory(UpdatesRoot);
        var temp = QueueFile + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(queue));
            File.Move(temp, QueueFile, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private void CleanPreviousStage(PendingPluginChange? previous)
    {
        if (previous?.StagedName is null || !Guid.TryParseExact(previous.StagedName, "N", out _)) return;
        var old = Path.Combine(StagingRoot, previous.StagedName);
        if (Directory.Exists(old)) Directory.Delete(old, recursive: true);
    }

    private string PluginFolder(string id) => Path.Combine(_root,
        char.ToUpperInvariant(id[0]) + id[1..]);

    private static void ValidateId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) ||
            !Regex.IsMatch(id, "^[a-z][a-z0-9-]{0,63}$", RegexOptions.CultureInvariant))
            throw new InvalidDataException("ID de plugin inseguro ou inválido.");
    }

    private sealed record PackageManifest(string Id, string Assembly, string EntryType, int ApiVersion);
}

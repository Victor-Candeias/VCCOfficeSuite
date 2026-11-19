using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Vcc.Office.PluginHost;

/// <summary>One verified plugin package operation, applied at the next process startup.</summary>
public sealed record PendingPluginChange(string Id, string Action, string? StagedName);
public sealed record PluginPackageReceipt(string Id, string Action);
public sealed record PluginRollbackPoint(string Id, string BackupName);

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
    private string HistoryFile => Path.Combine(UpdatesRoot, "rollback.json");
    private string TrustedKeysDirectory => Path.Combine(UpdatesRoot, "trusted-signers");

    public OfficePluginPackageManager(string pluginsRoot)
    {
        if (string.IsNullOrWhiteSpace(pluginsRoot))
            throw new ArgumentException("Diretório dos plugins obrigatório.", nameof(pluginsRoot));
        _root = Path.GetFullPath(pluginsRoot);
    }

    public IReadOnlyList<PendingPluginChange> Pending => ReadQueue();

    public PluginPackageReceipt StageInstall(string archivePath, bool requireSignature = false)
    {
        byte[]? signedArchiveHash = null;
        var signaturePath = archivePath + ".sig";
        if (requireSignature || File.Exists(signaturePath))
        {
            if (!File.Exists(signaturePath))
                throw new InvalidDataException("É obrigatória a assinatura RSA-PSS do pacote.");
            PluginPackageSignature.Verify(archivePath, signaturePath, TrustedKeysDirectory);
            using var signedStream = File.OpenRead(archivePath);
            signedArchiveHash = System.Security.Cryptography.SHA256.HashData(signedStream);
        }
        if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
            throw new FileNotFoundException("Pacote ZIP não encontrado.", archivePath);
        string id;
        string assembly;
        using (var archive = ZipFile.OpenRead(archivePath))
        {
            if (archive.Entries.Count is 0 or > 256)
                throw new InvalidDataException("Quantidade de ficheiros do pacote inválida.");
            long total = 0;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in archive.Entries)
            {
                var name = entry.FullName.Replace('\\', '/');
                if (name.StartsWith("/", StringComparison.Ordinal) || name.Contains(':') ||
                    name.Split('/').Any(segment => segment is "." or "..") ||
                    !seen.Add(name.TrimEnd('/')))
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
            var manifest = PluginCompatibilityPolicy.Read(reader.ReadToEnd());
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
            if (signedArchiveHash is not null)
            {
                using var after = File.OpenRead(archivePath);
                if (!System.Security.Cryptography.SHA256.HashData(after).SequenceEqual(signedArchiveHash))
                    throw new InvalidDataException("O pacote assinado foi modificado durante a instalação.");
            }
            SealStage(stagedPath, token);
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
        EnsureNoDependants(id);
        pending.Add(new PendingPluginChange(id, "remove", null));
        SaveQueue(pending);
        CleanPreviousStage(previous);
    }

    public IReadOnlyList<PluginRollbackPoint> RollbackPoints => ReadHistory();

    /// <summary>Queues recovery of the last replaced or removed plugin version.</summary>
    public void StageRollback(string id)
    {
        ValidateId(id);
        var point = ReadHistory().FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (point is null || !point.BackupName.StartsWith("backup-", StringComparison.Ordinal) ||
            !Guid.TryParseExact(point.BackupName["backup-".Length..], "N", out _) ||
            !Directory.Exists(Path.Combine(UpdatesRoot, point.BackupName)))
            throw new InvalidOperationException("Não existe uma versão anterior recuperável.");
        var pending = ReadQueue();
        var previous = pending.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        pending.RemoveAll(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        pending.Add(new PendingPluginChange(id, "rollback", null));
        SaveQueue(pending);
        CleanPreviousStage(previous);
    }

    /// <summary>Cancel one queued install/update/remove without changing loaded assemblies.</summary>
    public bool CancelPending(string id)
    {
        ValidateId(id);
        var pending = ReadQueue();
        var queued = pending.FirstOrDefault(change => change.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (queued is null) return false;
        pending.RemoveAll(change => change.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        SaveQueue(pending);
        CleanPreviousStage(queued);
        return true;
    }

    /// <summary>Inventory entries are bounded to the managed staging area and contain no executable paths.</summary>
    public IReadOnlyList<PendingPluginChange> PendingChanges() =>
        ReadQueue().Where(change => change.Action is "install" or "update" or "remove")
            .OrderBy(change => change.Id, StringComparer.OrdinalIgnoreCase).ToArray();

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
                                       ArgumentException or InvalidDataException or InvalidOperationException or
                                       BadImageFormatException)
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
        if (change.Action == "rollback")
        {
            var point = ReadHistory().FirstOrDefault(x => x.Id.Equals(change.Id, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException("Versão anterior indisponível.");
            var source = Path.Combine(UpdatesRoot, point.BackupName);
            if (!Directory.Exists(source)) throw new DirectoryNotFoundException("Backup do plugin em falta.");
            if (Directory.Exists(destination)) Directory.Move(destination, backup);
            try
            {
                Directory.Move(source, destination);
                var history = ReadHistory();
                history.RemoveAll(x => x.Id.Equals(change.Id, StringComparison.OrdinalIgnoreCase));
                if (Directory.Exists(backup))
                    history.Add(new PluginRollbackPoint(change.Id, Path.GetFileName(backup)));
                SaveHistory(history);
            }
            catch
            {
                if (Directory.Exists(destination)) Directory.Move(destination, source);
                if (Directory.Exists(backup)) Directory.Move(backup, destination);
                throw;
            }
            return;
        }
        if (change.Action == "remove")
        {
            EnsureNoDependants(change.Id);
            if (Directory.Exists(destination))
            {
                Directory.Move(destination, backup);
                try { RetainBackup(change.Id, backup); }
                catch
                {
                    if (Directory.Exists(backup)) Directory.Move(backup, destination);
                    throw;
                }
            }
            return;
        }
        if (change.Action is not ("install" or "update") ||
            !Guid.TryParseExact(change.StagedName, "N", out _))
            throw new InvalidDataException("Operação pendente inválida.");
        var stagedPath = Path.Combine(StagingRoot, change.StagedName!);
        if (!Directory.Exists(stagedPath)) throw new DirectoryNotFoundException("Pacote preparado inexistente.");
        VerifyStage(stagedPath, change.StagedName!);
        var manifestPath = Path.Combine(stagedPath, "plugin.json");
        if (!File.Exists(manifestPath)) throw new InvalidDataException("Manifesto do plugin preparado em falta.");
        var metadata = PluginCompatibilityPolicy.Read(File.ReadAllText(manifestPath));
        if (!metadata.Id.Equals(change.Id, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("O pacote preparado mudou de identificador.");
        var dependencies = new Dictionary<string, Version>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in Directory.EnumerateDirectories(_root))
        {
            var otherManifest = Path.Combine(folder, "plugin.json");
            if (!File.Exists(otherManifest)) continue;
            var other = PluginCompatibilityPolicy.Read(File.ReadAllText(otherManifest));
            var path = Path.Combine(folder, other.Assembly);
            if (File.Exists(path))
                dependencies[other.Id] = AssemblyName.GetAssemblyName(path).Version ?? new Version(0, 0);
        }
        PluginCompatibilityPolicy.EnsureCompatible(metadata,
            typeof(OfficePluginPackageManager).Assembly.GetName().Version ?? new Version(1, 0),
            dependencies);
        if (Directory.Exists(destination)) Directory.Move(destination, backup);
        try
        {
            Directory.Move(stagedPath, destination);
            RemoveStageSeal(change.StagedName!);
            if (Directory.Exists(backup)) RetainBackup(change.Id, backup);
        }
        catch
        {
            if (Directory.Exists(destination)) Directory.Delete(destination, recursive: true);
            if (Directory.Exists(backup)) Directory.Move(backup, destination);
            throw;
        }
    }

    private string SealPath(string token) => Path.Combine(StagingRoot, token + ".seal.json");

    private static Dictionary<string, string> StageHashes(string stage)
    {
        var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(stage, "*", SearchOption.AllDirectories))
        {
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("O staging não permite links simbólicos.");
            var relative = Path.GetRelativePath(stage, file).Replace(Path.DirectorySeparatorChar, '/');
            using var stream = File.OpenRead(file);
            hashes.Add(relative, Convert.ToHexString(SHA256.HashData(stream)));
        }
        return hashes;
    }

    private void SealStage(string stage, string token)
    {
        var seal = SealPath(token);
        File.WriteAllText(seal, JsonSerializer.Serialize(StageHashes(stage)));
    }

    private void VerifyStage(string stage, string token)
    {
        var seal = SealPath(token);
        if (!File.Exists(seal)) throw new InvalidDataException("Selo de integridade do staging em falta.");
        var expected = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(seal))
            ?? throw new InvalidDataException("Selo de integridade vazio.");
        var actual = StageHashes(stage);
        if (expected.Count != actual.Count || expected.Any(item =>
                !actual.TryGetValue(item.Key, out var hash) ||
                !hash.Equals(item.Value, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("O plugin preparado foi alterado após a validação.");
    }

    private void RemoveStageSeal(string token)
    {
        if (Guid.TryParseExact(token, "N", out _))
        {
            var seal = SealPath(token);
            if (File.Exists(seal)) File.Delete(seal);
        }
    }

    private void EnsureNoDependants(string id)
    {
        if (!Directory.Exists(_root)) return;
        foreach (var folder in Directory.EnumerateDirectories(_root))
        {
            var path = Path.Combine(folder, "plugin.json");
            if (!File.Exists(path) || Path.GetFileName(folder).Equals(id, StringComparison.OrdinalIgnoreCase))
                continue;
            var other = PluginCompatibilityPolicy.Read(File.ReadAllText(path));
            if ((other.Requires ?? []).Any(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("O plugin " + other.Id + " depende de " + id + ".");
        }
    }

    private void RetainBackup(string id, string backup)
    {
        var history = ReadHistory();
        var previous = history.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        history.RemoveAll(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        history.Add(new PluginRollbackPoint(id, Path.GetFileName(backup)));
        SaveHistory(history);
        if (previous is not null && previous.BackupName != Path.GetFileName(backup))
        {
            var old = Path.Combine(UpdatesRoot, previous.BackupName);
            if (old.StartsWith(UpdatesRoot + Path.DirectorySeparatorChar,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) &&
                Directory.Exists(old))
                Directory.Delete(old, recursive: true);
        }
    }

    private List<PluginRollbackPoint> ReadHistory() =>
        File.Exists(HistoryFile)
            ? JsonSerializer.Deserialize<List<PluginRollbackPoint>>(File.ReadAllText(HistoryFile)) ?? []
            : [];

    private void SaveHistory(List<PluginRollbackPoint> history)
    {
        Directory.CreateDirectory(UpdatesRoot);
        var temp = HistoryFile + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(history));
            File.Move(temp, HistoryFile, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
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
        RemoveStageSeal(previous.StagedName);
    }

    private string PluginFolder(string id) => Path.Combine(_root,
        char.ToUpperInvariant(id[0]) + id[1..]);

    private static void ValidateId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) ||
            !Regex.IsMatch(id, "^[a-z][a-z0-9-]{0,63}$", RegexOptions.CultureInvariant))
            throw new InvalidDataException("ID de plugin inseguro ou inválido.");
    }

}

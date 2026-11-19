using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Vcc.Office.CoreShell;

/// <summary>A validated, offline update transaction. Never stores user documents or application preferences.</summary>
public sealed record PreparedSuiteUpdate(string Version, string InstallDirectory,
    string CandidateDirectory, string ExpectedExecutable, string BackupDirectory, int ParentProcessId);

public sealed record SuiteInstallOutcome(bool Succeeded, bool RolledBack, string Message);

/// <summary>
/// Installs a separately verified release only after the host has exited.
/// The offline worker must run outside InstallDirectory to avoid Windows file locks.
/// </summary>
public sealed class SuiteUpdateInstaller
{
    private const long MaximumExpandedBytes = 2L * 1024 * 1024 * 1024;
    private const int MaximumEntries = 20000;
    public const string PlanName = "update-plan.json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<PreparedSuiteUpdate> PrepareAsync(
        string verifiedArchive, string expectedSha256, string version, string installDirectory,
        int parentProcessId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(version) || !Version.TryParse(version.TrimStart('v', 'V'), out _))
            throw new ArgumentException("Versão inválida.", nameof(version));
        if (parentProcessId <= 0) throw new ArgumentOutOfRangeException(nameof(parentProcessId));
        if (expectedSha256.Length != 64 || !expectedSha256.All(Uri.IsHexDigit))
            throw new InvalidDataException("Hash do arquivo inválido.");
        var root = Path.GetFullPath(installDirectory);
        if (!Directory.Exists(root) || Path.GetPathRoot(root) == root)
            throw new InvalidOperationException("Diretório de instalação inválido.");
        var parent = Directory.GetParent(root)!.FullName;
        var exe = OperatingSystem.IsWindows() ? "VCC Office Suite.exe" : "VCC Office Suite.dll";
        if (string.IsNullOrWhiteSpace(exe))
            throw new InvalidOperationException("Executável da suite indisponível.");
        var archivePath = Path.GetFullPath(verifiedArchive);
        if (!File.Exists(archivePath)) throw new FileNotFoundException("Pacote não encontrado.", archivePath);
        await using (var file = File.OpenRead(archivePath))
        {
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(file, cancellationToken));
            if (!hash.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("O pacote deixou de corresponder ao hash verificado.");
        }

        var token = Guid.NewGuid().ToString("N");
        var candidate = Path.Combine(parent, ".vcc-candidate-" + token);
        var backup = Path.Combine(parent, ".vcc-backup-" + token);
        try
        {
            await ExtractVerifiedAsync(archivePath, candidate, cancellationToken);
            // Only installed executable name is trusted, not a path taken from the archive.
            if (!File.Exists(Path.Combine(candidate, exe)))
                throw new InvalidDataException("A release não contém o executável esperado: " + exe);
            return new PreparedSuiteUpdate(version, root, candidate, exe, backup, parentProcessId);
        }
        catch
        {
            if (Directory.Exists(candidate)) Directory.Delete(candidate, true);
            throw;
        }
    }

    /// <summary>
    /// Verifies the complete internal manifest. Disallows duplicate paths, symlinks, traversal,
    /// unlisted files and expanded payloads over 2 GiB before extracting any file.
    /// </summary>
    public static async Task ExtractVerifiedAsync(string zipPath, string candidate, CancellationToken ct = default)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        if (zip.Entries.Count is 0 or > MaximumEntries)
            throw new InvalidDataException("Quantidade de entradas inválida.");
        var entries = zip.Entries.Where(e => !e.FullName.EndsWith('/')).ToArray();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long expanded = 0;
        foreach (var entry in zip.Entries)
        {
            var name = entry.FullName;
            var unixType = (entry.ExternalAttributes >> 16) & 0xF000;
            if (unixType == 0xA000 || name.Contains('\\') || name.Contains(':') ||
                name.StartsWith('/') || name.Split('/').Any(s => s is "" or "." or ".."))
                throw new InvalidDataException("O pacote contém um caminho inseguro.");
            if (!names.Add(name.TrimEnd('/')))
                throw new InvalidDataException("Entradas duplicadas no pacote.");
            expanded = checked(expanded + entry.Length);
            if (expanded > MaximumExpandedBytes)
                throw new InvalidDataException("O pacote excede o limite de expansão.");
        }
        var manifestEntry = zip.GetEntry("SHA256SUMS.txt")
            ?? throw new InvalidDataException("Manifesto de integridade interno em falta.");
        if (manifestEntry.Length > 2 * 1024 * 1024)
            throw new InvalidDataException("Manifesto demasiado grande.");
        string manifest;
        using (var reader = new StreamReader(manifestEntry.Open(), Encoding.UTF8))
            manifest = await reader.ReadToEndAsync(ct);
        var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in manifest.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.TrimEnd('\r');
            if (trimmed.Length < 67 || trimmed[64..66] != "  " ||
                !trimmed[..64].All(Uri.IsHexDigit))
                throw new InvalidDataException("Linha inválida no manifesto.");
            var name = trimmed[66..];
            if (!hashes.TryAdd(name, trimmed[..64]))
                throw new InvalidDataException("Entrada duplicada no manifesto.");
        }
        var content = entries.Where(e => e.FullName != "SHA256SUMS.txt").ToArray();
        if (hashes.Count != content.Length || content.Any(e => !hashes.ContainsKey(e.FullName)))
            throw new InvalidDataException("O manifesto não corresponde ao conteúdo.");
        Directory.CreateDirectory(candidate);
        await File.WriteAllTextAsync(Path.Combine(candidate, "SHA256SUMS.txt"), manifest, ct);
        foreach (var entry in content)
        {
            ct.ThrowIfCancellationRequested();
            var destination = Path.GetFullPath(Path.Combine(candidate, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
            if (!destination.StartsWith(Path.GetFullPath(candidate) + Path.DirectorySeparatorChar,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                throw new InvalidDataException("Ficheiro fora do diretório de destino.");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 81920, useAsync: true))
            await using (var input = entry.Open())
                await input.CopyToAsync(output, ct);
            await using var verify = File.OpenRead(destination);
            var digest = Convert.ToHexString(await SHA256.HashDataAsync(verify, ct));
            if (!digest.Equals(hashes[entry.FullName], StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Hash interno inválido: " + entry.FullName);
        }
    }

    public static async Task SavePlanAsync(PreparedSuiteUpdate plan, string planPath, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        await new AtomicFileStore().WriteAsync(planPath, Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(plan, JsonOptions)), ct);
    }

    public static async Task<PreparedSuiteUpdate> ReadPlanAsync(string path, CancellationToken ct = default)
    {
        var plan = JsonSerializer.Deserialize<PreparedSuiteUpdate>(
            await File.ReadAllTextAsync(path, ct), JsonOptions)
            ?? throw new InvalidDataException("Plano de instalação vazio.");
        if (Path.GetFileName(plan.CandidateDirectory).StartsWith(".vcc-candidate-", StringComparison.Ordinal) == false ||
            Path.GetFileName(plan.BackupDirectory).StartsWith(".vcc-backup-", StringComparison.Ordinal) == false ||
            Path.GetDirectoryName(Path.GetFullPath(plan.CandidateDirectory)) !=
            Path.GetDirectoryName(Path.GetFullPath(plan.InstallDirectory)) ||
            Path.GetDirectoryName(Path.GetFullPath(plan.BackupDirectory)) !=
            Path.GetDirectoryName(Path.GetFullPath(plan.InstallDirectory)) ||
            Path.GetFileName(plan.ExpectedExecutable) != plan.ExpectedExecutable ||
            Path.GetPathRoot(plan.InstallDirectory) == Path.GetFullPath(plan.InstallDirectory))
            throw new InvalidDataException("Plano contém caminhos inválidos.");
        return plan;
    }

    private static void VerifyPreparedFolder(PreparedSuiteUpdate plan)
    {
        var root = Path.GetFullPath(plan.CandidateDirectory);
        var manifestPath = Path.Combine(root, "SHA256SUMS.txt");
        if (!File.Exists(manifestPath)) throw new InvalidDataException("Manifesto do pacote preparado em falta.");
        var expected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadAllLines(manifestPath))
        {
            if (line.Length < 67 || line[64..66] != "  " || !line[..64].All(Uri.IsHexDigit))
                throw new InvalidDataException("Manifesto preparado inválido.");
            var relative = line[66..];
            if (relative.StartsWith('/') || relative.Contains('\\') ||
                relative.Split('/').Any(segment => segment is "." or ".." or ""))
                throw new InvalidDataException("Caminho de manifesto inseguro.");
            if (!expected.TryAdd(relative, line[..64]))
                throw new InvalidDataException("Entrada duplicada.");
        }
        var actualFiles = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(file => !string.Equals(file, manifestPath, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (actualFiles.Length != expected.Count)
            throw new InvalidDataException("O conteúdo preparado difere do manifesto.");
        foreach (var file in actualFiles)
        {
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Links simbólicos não são permitidos.");
            var relative = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
            if (!expected.TryGetValue(relative, out var checksum))
                throw new InvalidDataException("Ficheiro não listado no manifesto.");
            using var stream = File.OpenRead(file);
            var digest = Convert.ToHexString(SHA256.HashData(stream));
            if (!digest.Equals(checksum, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Conteúdo preparado alterado: " + relative);
        }
    }

    /// <summary>Atomic folder swap, with rollback on any failed readiness probe.</summary>
    public static SuiteInstallOutcome CommitOffline(PreparedSuiteUpdate plan, Func<string, bool> readiness)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(readiness);
        if (!Directory.Exists(plan.CandidateDirectory) ||
            !File.Exists(Path.Combine(plan.CandidateDirectory, plan.ExpectedExecutable)) ||
            !Directory.Exists(plan.InstallDirectory) || Directory.Exists(plan.BackupDirectory))
            throw new InvalidOperationException("Os diretórios de instalação não estão prontos.");
        VerifyPreparedFolder(plan);
        Directory.Move(plan.InstallDirectory, plan.BackupDirectory);
        try
        {
            Directory.Move(plan.CandidateDirectory, plan.InstallDirectory);
            if (!readiness(Path.Combine(plan.InstallDirectory, plan.ExpectedExecutable)))
                throw new InvalidOperationException("A nova versão não passou a verificação de arranque.");
            // Keep backup for manual rollback / next successful update; user data lives outside this folder.
            return new(true, false, "Atualização instalada; a versão anterior foi preservada em backup.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            try
            {
                if (Directory.Exists(plan.InstallDirectory))
                {
                    var failed = plan.CandidateDirectory + "-failed";
                    Directory.Move(plan.InstallDirectory, failed);
                }
                Directory.Move(plan.BackupDirectory, plan.InstallDirectory);
            }
            catch (Exception rollback) when (rollback is IOException or UnauthorizedAccessException)
            {
                return new(false, false, "Falhou a atualização e o rollback automático: " + rollback.Message);
            }
            return new(false, true, "Falhou a atualização; versão anterior restaurada: " + ex.Message);
        }
    }

    public static bool ProbeLaunch(string executable)
    {
        try
        {
            var dotnetDll = executable.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
            var info = new ProcessStartInfo(dotnetDll ? "dotnet" : executable)
            {
                UseShellExecute = false, CreateNoWindow = true
            };
            if (dotnetDll) info.ArgumentList.Add(executable);
            info.ArgumentList.Add("--vcc-update-health-check");
            using var process = Process.Start(info);
            if (process is null) return false;
            if (!process.WaitForExit(25000))
            {
                process.Kill(entireProcessTree: true);
                return false;
            }
            return process.ExitCode == 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    public static SuiteInstallOutcome ApplyAfterExit(PreparedSuiteUpdate plan)
    {
        try
        {
            using var parent = Process.GetProcessById(plan.ParentProcessId);
            if (!parent.WaitForExit(120000))
                return new(false, false, "A aplicação não terminou; instalação cancelada.");
        }
        catch (ArgumentException) { /* Already exited. */ }
        return CommitOffline(plan, ProbeLaunch);
    }
}

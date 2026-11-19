using System.Security.Cryptography;

namespace Vcc.Office.CoreShell;

/// <summary>
/// Opt-in bridge to a folder already synchronized by a cloud storage client.
/// Does not own credentials or perform remote authentication.
/// </summary>
public sealed class CloudSyncFolderService
{
    public string Folder { get; }

    public CloudSyncFolderService(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
            throw new ArgumentException("A pasta sincronizada é obrigatória.", nameof(folder));
        Folder = Path.GetFullPath(folder);
        if (!Directory.Exists(Folder))
            throw new DirectoryNotFoundException("A pasta sincronizada não está disponível.");
    }

    public async Task<string> ExportCopyAsync(string source, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("Ficheiro obrigatório.", nameof(source));
        var path = Path.GetFullPath(source);
        if (!File.Exists(path)) throw new FileNotFoundException("O ficheiro não existe.", path);
        var name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(name)) throw new InvalidDataException("Nome inválido.");
        if (string.Equals(Path.GetDirectoryName(path)?.TrimEnd(Path.DirectorySeparatorChar),
                Folder.TrimEnd(Path.DirectorySeparatorChar), OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidOperationException("O ficheiro já se encontra na pasta de sincronização.");

        // Never overwrite a cloud document; concurrent operations use CreateNew and a unique name.
        var destination = Path.Combine(Folder, Guid.NewGuid().ToString("N") + "-" + name);
        try
        {
            await using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            await using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 81920, FileOptions.Asynchronous))
                await input.CopyToAsync(output, ct);
            return destination;
        }
        catch
        {
            if (File.Exists(destination)) File.Delete(destination);
            throw;
        }
    }

    public IReadOnlyList<string> ListLocalFiles()
    {
        if (!Directory.Exists(Folder)) throw new DirectoryNotFoundException("A pasta sincronizada não está disponível.");
        return Directory.EnumerateFiles(Folder, "*", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vcc.Office.CoreShell;

/// <summary>
/// 
/// </summary>
public sealed class ShellStateStore
{
    /// <summary>
    /// 
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// 
    /// </summary>
    /// <param name="statePath"></param>
    /// <exception cref="ArgumentException"></exception>
    public ShellStateStore(string statePath)
    {
        if (string.IsNullOrWhiteSpace(statePath)) throw new ArgumentException("O caminho do estado é obrigatório.", nameof(statePath));
        StatePath = Path.GetFullPath(statePath);
    }

    /// <summary>
    /// 
    /// </summary>
    public string StatePath { get; }

    /// <summary>
    /// 
    /// </summary>
    public string BackupPath => StatePath + ".bak";

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<ShellState> LoadAsync(CancellationToken cancellationToken = default)
    {
        foreach (var path in new[] { StatePath, BackupPath })
        {
            if (!File.Exists(path)) continue;
            try
            {
                await using var stream = File.OpenRead(path);
                var state = await JsonSerializer.DeserializeAsync<ShellState>(stream, JsonOptions, cancellationToken);
                if (state is not null && ShellStateValidator.Validate(state).IsValid) return state;
            }
            catch (JsonException) { }
            catch (IOException) { }
        }
        return ShellState.Default;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="state"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task SaveAsync(ShellState state, CancellationToken cancellationToken = default)
    {
        var validation = ShellStateValidator.Validate(state);
        if (!validation.IsValid) throw new ArgumentException(string.Join(" ", validation.Errors), nameof(state));

        var directory = Path.GetDirectoryName(StatePath) ?? throw new InvalidOperationException("O caminho não tem diretório.");
        Directory.CreateDirectory(directory);
        var temporaryPath = StatePath + ".tmp";
        await using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
        {
            await JsonSerializer.SerializeAsync(stream, state, JsonOptions, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        if (File.Exists(StatePath)) File.Copy(StatePath, BackupPath, overwrite: true);
        File.Move(temporaryPath, StatePath, overwrite: true);
    }
}

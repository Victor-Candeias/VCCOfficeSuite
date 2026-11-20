using System.Text.Json;

namespace Vcc.Office.CoreShell;

/// <summary>Portable customization preferences only; intentionally excludes documents and session state.</summary>
public sealed record LauncherProfile(
    string Name, string DashboardId, ThemeMode Theme, string[] ApplicationOrder,
    string[] Favorites, string[] Hidden, string CardSize, string VisualDensity,
    bool SidebarCollapsed, string DashboardStyle, bool ShowWelcomeBanner,
    bool CompactNavigation);

public static class LauncherProfilePolicy
{
    public static bool IsValidName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && name.Length <= 48 &&
        name == name.Trim() && name.All(c => !char.IsControl(c));

    public static LauncherProfile Capture(string name, DesktopLauncherState state)
    {
        if (!IsValidName(name)) throw new ArgumentException("Nome do perfil inválido.", nameof(name));
        return new LauncherProfile(name, state.DashboardId, state.Theme,
            LauncherCustomizationPolicy.NormalizeIds(state.ApplicationOrder),
            LauncherCustomizationPolicy.NormalizeIds(state.FavoriteApplications),
            LauncherCustomizationPolicy.NormalizeIds(state.HiddenApplications),
            state.CardSize, state.VisualDensity, state.SidebarCollapsed,
            state.DashboardStyle, state.ShowWelcomeBanner, state.CompactNavigation);
    }

    public static LauncherProfile Validate(LauncherProfile profile, IDashboardRegistry dashboards)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (!IsValidName(profile.Name) || !dashboards.Contains(profile.DashboardId) ||
            !Enum.IsDefined(profile.Theme) ||
            profile.CardSize is not ("small" or "medium" or "large") ||
            profile.VisualDensity is not ("compact" or "comfortable") ||
            profile.DashboardStyle is not ("standard" or "minimal") ||
            (profile.ApplicationOrder?.Length ?? 0) > 256 ||
            (profile.Favorites?.Length ?? 0) > 256 ||
            (profile.Hidden?.Length ?? 0) > 256)
            throw new InvalidDataException("Perfil de personalização inválido.");
        return profile with
        {
            ApplicationOrder = LauncherCustomizationPolicy.NormalizeIds(profile.ApplicationOrder),
            Favorites = LauncherCustomizationPolicy.NormalizeIds(profile.Favorites),
            Hidden = LauncherCustomizationPolicy.NormalizeIds(profile.Hidden)
        };
    }

    public static DesktopLauncherState Apply(LauncherProfile profile, DesktopLauncherState current,
        IDashboardRegistry dashboards)
    {
        profile = Validate(profile, dashboards);
        return current with
        {
            DashboardId = profile.DashboardId, Theme = profile.Theme,
            ApplicationOrder = profile.ApplicationOrder,
            FavoriteApplications = profile.Favorites,
            HiddenApplications = profile.Hidden,
            CardSize = profile.CardSize, VisualDensity = profile.VisualDensity,
            SidebarCollapsed = profile.SidebarCollapsed, DashboardStyle = profile.DashboardStyle,
            ShowWelcomeBanner = profile.ShowWelcomeBanner,
            CompactNavigation = profile.CompactNavigation,
            ActiveProfileName = profile.Name
        };
    }
}

/// <summary>SQLite-backed named profiles with a last-known-good snapshot and strict JSON import.</summary>
public sealed class LauncherProfileStore
{
    private const string Key = "avalonia.launcher.profiles.v1";
    private const string BackupKey = "avalonia.launcher.profiles.backup.v1";
    private readonly CoreDatabase _database;
    private readonly IDashboardRegistry _dashboards;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public LauncherProfileStore(CoreDatabase database, IDashboardRegistry dashboards)
    {
        _database = database;
        _dashboards = dashboards;
    }

    public async Task<IReadOnlyList<LauncherProfile>> LoadAsync(CancellationToken ct = default)
    {
        var raw = await _database.GetSettingAsync(Key, ct);
        if (string.IsNullOrWhiteSpace(raw)) return [];
        try { return ParseCollection(raw); }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            var backup = await _database.GetSettingAsync(BackupKey, ct);
            if (string.IsNullOrWhiteSpace(backup)) return [];
            try { return ParseCollection(backup); }
            catch (Exception backupEx) when (backupEx is JsonException or InvalidDataException) { return []; }
        }
    }

    private IReadOnlyList<LauncherProfile> ParseCollection(string json)
    {
        if (json.Length > 256 * 1024) throw new InvalidDataException("Ficheiro de perfis demasiado grande.");
        var records = JsonSerializer.Deserialize<LauncherProfile[]>(json, Options)
            ?? throw new InvalidDataException("Lista de perfis inválida.");
        if (records.Length > 24 || records.Any(p => p is null))
            throw new InvalidDataException("Quantidade de perfis inválida.");
        var profiles = records.Select(p => LauncherProfilePolicy.Validate(p, _dashboards)).ToArray();
        if (profiles.Select(p => p.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != profiles.Length)
            throw new InvalidDataException("Nomes de perfis repetidos.");
        return profiles;
    }

    public async Task SaveAsync(LauncherProfile profile, CancellationToken ct = default)
    {
        profile = LauncherProfilePolicy.Validate(profile, _dashboards);
        await _gate.WaitAsync(ct);
        try
        {
            var profiles = (await LoadAsync(ct)).ToList();
            profiles.RemoveAll(p => string.Equals(p.Name, profile.Name, StringComparison.OrdinalIgnoreCase));
            if (profiles.Count >= 24) throw new InvalidOperationException("Limite de 24 perfis atingido.");
            profiles.Add(profile);
            await StoreAsync(profiles, ct);
        }
        finally { _gate.Release(); }
    }

    public async Task RemoveAsync(string name, CancellationToken ct = default)
    {
        if (!LauncherProfilePolicy.IsValidName(name)) throw new ArgumentException("Nome de perfil inválido.", nameof(name));
        await _gate.WaitAsync(ct);
        try
        {
            var profiles = (await LoadAsync(ct)).Where(p =>
                !p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToArray();
            await StoreAsync(profiles, ct);
        }
        finally { _gate.Release(); }
    }

    private async Task StoreAsync(IReadOnlyList<LauncherProfile> profiles, CancellationToken ct)
    {
        var previous = await _database.GetSettingAsync(Key, ct);
        if (!string.IsNullOrWhiteSpace(previous))
        {
            try { ParseCollection(previous); await _database.SetSettingAsync(BackupKey, previous, ct); }
            catch (Exception ex) when (ex is JsonException or InvalidDataException) { }
        }
        await _database.SetSettingAsync(Key, JsonSerializer.Serialize(profiles, Options), ct);
    }

    public string Export(LauncherProfile profile) =>
        JsonSerializer.Serialize(LauncherProfilePolicy.Validate(profile, _dashboards),
            new JsonSerializerOptions(Options) { WriteIndented = true });

    public LauncherProfile Import(string json)
    {
        if (json.Length > 64 * 1024) throw new InvalidDataException("Perfil importado demasiado grande.");
        var profile = JsonSerializer.Deserialize<LauncherProfile>(json, Options)
            ?? throw new InvalidDataException("Perfil importado vazio.");
        return LauncherProfilePolicy.Validate(profile, _dashboards);
    }
}

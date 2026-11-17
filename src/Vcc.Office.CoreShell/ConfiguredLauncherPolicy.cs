namespace Vcc.Office.CoreShell;

/// <summary>
/// Descreve atalhos personalizados do Workspace que não pertencem ao catálogo
/// fixo de aplicações. A configuração NÃO constitui um executável instalado.
/// </summary>
public static class ConfiguredLauncherPolicy
{
    public static IReadOnlyList<LauncherItem> CustomItems(
        IEnumerable<LauncherItem> configured,
        OfficeApplicationRegistry registry,
        string? query = null)
    {
        ArgumentNullException.ThrowIfNull(configured);
        ArgumentNullException.ThrowIfNull(registry);

        // "settings" is a built-in CoreShell command, not an app.
        var custom = configured.Where(item =>
            item is not null &&
            !string.Equals(item.Id, "settings", StringComparison.OrdinalIgnoreCase) &&
            registry.Find(item.Id) is null);

        return LauncherPolicy.Query(custom, query)
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }

    public static bool IsBuiltIn(string? id, OfficeApplicationRegistry registry) =>
        id is not null && (registry.Find(id) is not null ||
            string.Equals(id, "settings", StringComparison.OrdinalIgnoreCase));
}

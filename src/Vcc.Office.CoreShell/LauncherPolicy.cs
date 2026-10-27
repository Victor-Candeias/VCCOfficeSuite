namespace Vcc.Office.CoreShell;

/// <summary>
/// Regras de configuração do Launcher, partilhadas pela UI e pelo serviço.
/// </summary>
public static class LauncherPolicy
{
    public static IReadOnlyList<LauncherItem> Query(IEnumerable<LauncherItem> items, string? query)
    {
        ArgumentNullException.ThrowIfNull(items);
        var source = items.ToArray();
        var normalized = query?.Trim();
        if (string.IsNullOrEmpty(normalized)) return source;

        return source.Where(item =>
            item.Id.Contains(normalized, StringComparison.OrdinalIgnoreCase) ||
            item.Title.Contains(normalized, StringComparison.OrdinalIgnoreCase) ||
            item.Description.Contains(normalized, StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    public static ValidationResult Validate(IReadOnlyList<LauncherItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var errors = new List<string>();
        if (items.Count == 0) errors.Add("O Launcher deve conter pelo menos um módulo.");

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Id) || string.IsNullOrWhiteSpace(item.Title))
                errors.Add("Cada módulo deve ter ID e título.");
            if (string.IsNullOrWhiteSpace(item.Description)) errors.Add("Cada módulo deve ter descrição.");
            if (string.IsNullOrWhiteSpace(item.Glyph)) errors.Add("Cada módulo deve ter um identificador visual.");
            if (!string.IsNullOrWhiteSpace(item.Id) && !ids.Add(item.Id))
                errors.Add($"Já existe um módulo com o ID '{item.Id}'.");
        }

        return new ValidationResult(errors);
    }
}

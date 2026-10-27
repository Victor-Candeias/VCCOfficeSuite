namespace Vcc.Office.CoreShell;

/// <summary>
/// Regras de configuração do Ribbon, independentes da camada WPF.
/// </summary>
public static class RibbonPolicy
{
    public static IReadOnlyList<RibbonTab> Query(IReadOnlyList<RibbonTab> tabs, string? query)
    {
        ArgumentNullException.ThrowIfNull(tabs);
        var normalized = query?.Trim();
        if (string.IsNullOrEmpty(normalized)) return tabs;

        return tabs
            .Where(tab => tab.Id.Contains(normalized, StringComparison.OrdinalIgnoreCase) ||
                         tab.Title.Contains(normalized, StringComparison.OrdinalIgnoreCase) ||
                         tab.Commands.Any(command =>
                             command.Id.Contains(normalized, StringComparison.OrdinalIgnoreCase) ||
                             command.Title.Contains(normalized, StringComparison.OrdinalIgnoreCase) ||
                             command.Description.Contains(normalized, StringComparison.OrdinalIgnoreCase)))
            .Select(tab =>
            {
                var tabMatches = tab.Id.Contains(normalized, StringComparison.OrdinalIgnoreCase) ||
                                  tab.Title.Contains(normalized, StringComparison.OrdinalIgnoreCase);
                var commands = tabMatches
                    ? tab.Commands
                    : tab.Commands.Where(command =>
                        command.Id.Contains(normalized, StringComparison.OrdinalIgnoreCase) ||
                        command.Title.Contains(normalized, StringComparison.OrdinalIgnoreCase) ||
                        command.Description.Contains(normalized, StringComparison.OrdinalIgnoreCase)).ToArray();
                return tab with { Commands = commands };
            }).ToArray();
    }

    public static ValidationResult Validate(IReadOnlyList<RibbonTab> tabs)
    {
        ArgumentNullException.ThrowIfNull(tabs);
        var errors = new List<string>();
        if (tabs.Count == 0) errors.Add("O Ribbon deve conter pelo menos um separador.");

        var tabIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var commandIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tab in tabs)
        {
            if (string.IsNullOrWhiteSpace(tab.Id) || string.IsNullOrWhiteSpace(tab.Title))
                errors.Add("Cada separador do Ribbon deve ter ID e título.");
            if (!string.IsNullOrWhiteSpace(tab.Id) && !tabIds.Add(tab.Id))
                errors.Add($"Já existe um separador com o ID '{tab.Id}'.");
            if (tab.Commands.Count == 0) errors.Add($"O separador '{tab.Title}' deve conter pelo menos um comando.");

            foreach (var command in tab.Commands)
            {
                if (string.IsNullOrWhiteSpace(command.Id) || string.IsNullOrWhiteSpace(command.Title))
                    errors.Add("Cada comando do Ribbon deve ter ID e título.");
                if (string.IsNullOrWhiteSpace(command.Glyph)) errors.Add("Cada comando do Ribbon deve ter um ícone.");
                if (!string.IsNullOrWhiteSpace(command.Id) && !commandIds.Add(command.Id))
                    errors.Add($"Já existe um comando com o ID '{command.Id}'.");
            }
        }

        return new ValidationResult(errors);
    }
}

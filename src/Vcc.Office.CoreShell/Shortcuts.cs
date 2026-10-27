namespace Vcc.Office.CoreShell;

/// <summary>
/// Regras comuns para atalhos do Core Shell. A representação é textual para
/// manter o contrato independente da tecnologia de UI e persistível em JSON.
/// </summary>
public static class ShortcutPolicy
{
    private static readonly string[] Modifiers = ["Ctrl", "Alt", "Shift", "Win"];

    public static string Normalize(string gesture)
    {
        if (string.IsNullOrWhiteSpace(gesture)) return string.Empty;

        var parts = gesture.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return string.Empty;

        var normalized = new List<string>(parts.Length);
        foreach (var part in parts)
        {
            var modifier = Modifiers.FirstOrDefault(value => string.Equals(value, part, StringComparison.OrdinalIgnoreCase));
            normalized.Add(modifier ?? part.ToUpperInvariant());
        }

        var key = normalized[^1];
        var modifierParts = Modifiers.Where(modifier => normalized[..^1].Contains(modifier, StringComparer.OrdinalIgnoreCase));
        return string.Join('+', modifierParts.Append(key));
    }

    public static bool IsValid(string gesture)
    {
        var normalized = Normalize(gesture);
        if (normalized.Length == 0) return false;

        var parts = normalized.Split('+');
        if (parts.Length == 1) return false;
        if (!parts[..^1].Any()) return false;
        return parts[^1] is not ("CTRL" or "ALT" or "SHIFT" or "WIN") && parts[^1].Length <= 20;
    }
}

public static class ShortcutCatalog
{
    public static ValidationResult Validate(IReadOnlyDictionary<string, string> shortcuts)
    {
        ArgumentNullException.ThrowIfNull(shortcuts);
        var errors = new List<string>();
        var gestures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in shortcuts)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                errors.Add("Cada atalho deve ter um comando definido.");
                continue;
            }

            var normalized = ShortcutPolicy.Normalize(pair.Value);
            if (!ShortcutPolicy.IsValid(normalized))
            {
                errors.Add($"O atalho de '{pair.Key}' não é válido.");
                continue;
            }

            if (!gestures.Add(normalized)) errors.Add($"O atalho '{normalized}' está atribuído a mais do que um comando.");
        }

        return new ValidationResult(errors);
    }

    public static bool TryResolveCommand(
        IReadOnlyDictionary<string, string> shortcuts,
        string gesture,
        out string? commandId)
    {
        var normalized = ShortcutPolicy.Normalize(gesture);
        var match = shortcuts.FirstOrDefault(pair =>
            string.Equals(ShortcutPolicy.Normalize(pair.Value), normalized, StringComparison.OrdinalIgnoreCase));
        commandId = match.Key;
        return !string.IsNullOrEmpty(commandId);
    }
}

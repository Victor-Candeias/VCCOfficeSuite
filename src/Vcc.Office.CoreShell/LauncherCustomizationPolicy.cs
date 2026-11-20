namespace Vcc.Office.CoreShell;

/// <summary>Deterministic layout decisions, independent from installed DLLs.</summary>
public static class LauncherCustomizationPolicy
{
    public static string[] NormalizeIds(IEnumerable<string>? ids) =>
        (ids ?? []).Where(id => !string.IsNullOrWhiteSpace(id) && id.Length <= 64 &&
            id.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-'))
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(256).ToArray();

    public static IReadOnlyList<T> Sort<T>(IEnumerable<T> modules, string[]? order,
        Func<T, string> idSelector)
    {
        var rank = NormalizeIds(order).Select((id, i) => (id, i))
            .ToDictionary(item => item.id, item => item.i, StringComparer.OrdinalIgnoreCase);
        return modules.OrderBy(item => rank.TryGetValue(idSelector(item), out var i) ? i : int.MaxValue)
            .ThenBy(item => idSelector(item), StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static string[] Move(string[]? order, IEnumerable<string> installedIds,
        string movedId, string targetId)
    {
        var normalized = NormalizeIds(installedIds);
        if (!normalized.Contains(movedId, StringComparer.OrdinalIgnoreCase) ||
            !normalized.Contains(targetId, StringComparer.OrdinalIgnoreCase) || movedId == targetId)
            return NormalizeIds(order);
        var result = Sort(normalized, order, x => x).ToList();
        result.Remove(movedId);
        result.Insert(result.IndexOf(targetId), movedId);
        return result.ToArray();
    }

    public static string[] Toggle(string[]? ids, string id)
    {
        var current = NormalizeIds(ids).ToList();
        if (!NormalizeIds([id]).Any()) return current.ToArray();
        if (!current.Remove(id)) current.Add(id);
        return current.ToArray();
    }
}

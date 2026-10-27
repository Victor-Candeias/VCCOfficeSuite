namespace Vcc.Office.CoreShell;

/// <summary>Preferências de acessibilidade independentes da tecnologia de UI.</summary>
public sealed record AccessibilitySettings(bool HighContrast, bool ReducedMotion);

/// <summary>DTO estável para transportar as preferências de acessibilidade.</summary>
public sealed record AccessibilityPreferencesDto(bool HighContrast, bool ReducedMotion)
{
    public AccessibilitySettings ToModel()
    {
        var validation = AccessibilityPolicy.Validate(this);
        if (!validation.IsValid)
            throw new ArgumentException(string.Join(" ", validation.Errors), nameof(AccessibilityPreferencesDto));
        return new(HighContrast, ReducedMotion);
    }

    public static AccessibilityPreferencesDto FromModel(AccessibilitySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new(settings.HighContrast, settings.ReducedMotion);
    }
}

public static class AccessibilityContract
{
    public const string SectionName = "accessibility";
}

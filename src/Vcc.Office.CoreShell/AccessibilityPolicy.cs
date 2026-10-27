namespace Vcc.Office.CoreShell;

public static class AccessibilityPolicy
{
    public static ValidationResult Validate(AccessibilitySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return ValidationResult.Valid;
    }

    public static ValidationResult Validate(AccessibilityPreferencesDto preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        return Validate(new AccessibilitySettings(preferences.HighContrast, preferences.ReducedMotion));
    }
}

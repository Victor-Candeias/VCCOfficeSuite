namespace Vcc.Office.CoreShell;

public enum NavigationFocusTarget
{
    Launcher,
    Ribbon
}

public sealed record NavigationSettings(NavigationFocusTarget InitialFocus, bool WrapFocus)
{
    public static NavigationSettings Default => new(NavigationFocusTarget.Launcher, false);
}

public static class NavigationPolicy
{
    public static ValidationResult Validate(NavigationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return Enum.IsDefined(settings.InitialFocus)
            ? ValidationResult.Valid
            : new ValidationResult(["O foco inicial de navegação não é válido."]);
    }

    public static ValidationResult Validate(NavigationSettingsDto settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return Validate(new NavigationSettings(settings.InitialFocus, settings.WrapFocus));
    }
}

namespace Vcc.Office.CoreShell;

/// <summary>DTO estável para transportar a configuração de foco da navegação.</summary>
public sealed record NavigationSettingsDto(NavigationFocusTarget InitialFocus, bool WrapFocus)
{
    public NavigationSettings ToModel()
    {
        var validation = NavigationPolicy.Validate(this);
        if (!validation.IsValid)
            throw new ArgumentException(string.Join(" ", validation.Errors), nameof(NavigationSettingsDto));
        return new(InitialFocus, WrapFocus);
    }

    public static NavigationSettingsDto FromModel(NavigationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new(settings.InitialFocus, settings.WrapFocus);
    }
}

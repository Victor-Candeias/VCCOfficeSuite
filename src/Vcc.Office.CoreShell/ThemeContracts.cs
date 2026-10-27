namespace Vcc.Office.CoreShell;

/// <summary>DTO estável para transportar a seleção de tema.</summary>
public sealed record ThemeSelectionDto(string Mode)
{
    public ThemeMode ToModel()
    {
        if (!ThemePolicy.TryParse(Mode, out var mode))
            throw new ArgumentException("O tema selecionado não é válido.", nameof(Mode));
        return mode;
    }

    public static ThemeSelectionDto FromModel(ThemeMode mode)
    {
        var validation = ThemePolicy.Validate(mode);
        if (!validation.IsValid)
            throw new ArgumentException(string.Join(" ", validation.Errors), nameof(mode));
        return new(mode.ToString());
    }
}

public static class ThemeContract
{
    public const int MaxModeLength = 16;
}

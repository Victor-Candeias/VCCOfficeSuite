namespace Vcc.Office.CoreShell;

public static class ThemePolicy
{
    public static ValidationResult Validate(ThemeMode mode)
        => Enum.IsDefined(mode)
            ? ValidationResult.Valid
            : new ValidationResult(["O tema selecionado não é válido."]);

    public static bool TryParse(string? value, out ThemeMode mode)
    {
        if (value is not null && value.Length <= ThemeContract.MaxModeLength &&
            Enum.TryParse(value.Trim(), ignoreCase: true, out mode))
            return Validate(mode).IsValid;

        mode = default;
        return false;
    }

    public static ValidationResult Validate(ThemeSelectionDto selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (string.IsNullOrWhiteSpace(selection.Mode) || selection.Mode.Length > ThemeContract.MaxModeLength)
            return new ValidationResult(["O tema selecionado não é válido."]);
        return TryParse(selection.Mode, out _)
            ? ValidationResult.Valid
            : new ValidationResult(["O tema selecionado não é válido."]);
    }
}

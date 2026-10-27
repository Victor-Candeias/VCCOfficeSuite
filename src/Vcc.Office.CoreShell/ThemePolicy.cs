namespace Vcc.Office.CoreShell;

public static class ThemePolicy
{
    public static ValidationResult Validate(ThemeMode mode)
        => Enum.IsDefined(mode)
            ? ValidationResult.Valid
            : new ValidationResult(["O tema selecionado não é válido."]);
}

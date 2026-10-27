namespace Vcc.Office.CoreShell;

public enum WindowNavigationAction
{
    Maximize,
    Restore,
    Center
}

public static class WindowNavigationPolicy
{
    public static ValidationResult Validate(WindowNavigationAction action)
        => Enum.IsDefined(action)
            ? ValidationResult.Valid
            : new ValidationResult(["A ação de navegação da janela não é válida."]);

    public static WindowLayout Apply(
        WindowLayout current,
        WindowNavigationAction action,
        double workAreaLeft,
        double workAreaTop,
        double workAreaWidth,
        double workAreaHeight)
    {
        var validation = Validate(action);
        if (!validation.IsValid) throw new ArgumentException(string.Join(" ", validation.Errors), nameof(action));
        var normalized = WindowLayoutPolicy.Normalize(current, workAreaLeft, workAreaTop, workAreaWidth, workAreaHeight);
        return action switch
        {
            WindowNavigationAction.Maximize => normalized with { IsMaximized = true },
            WindowNavigationAction.Restore => normalized with { IsMaximized = false },
            WindowNavigationAction.Center => normalized with
            {
                Left = workAreaLeft + (workAreaWidth - normalized.Width) / 2,
                Top = workAreaTop + (workAreaHeight - normalized.Height) / 2,
                IsMaximized = false
            },
            _ => normalized
        };
    }
}

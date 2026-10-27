namespace Vcc.Office.CoreShell;

/// <summary>
/// 
/// </summary>
public enum ThemeMode { Light, Dark, System }

/// <summary>
/// 
/// </summary>
/// <param name="Id"></param>
/// <param name="Title"></param>
/// <param name="Description"></param>
/// <param name="Glyph"></param>
public sealed record LauncherItem(string Id, string Title, string Description, string Glyph);

/// <summary>
/// 
/// </summary>
/// <param name="Width"></param>
/// <param name="Height"></param>
/// <param name="Left"></param>
/// <param name="Top"></param>
/// <param name="IsMaximized"></param>
public sealed record WindowLayout(double Width, double Height, double Left, double Top, bool IsMaximized)
{
    public static WindowLayout Default => new(1280, 800, 0, 0, false);
}

/// <summary>
/// 
/// </summary>
/// <param name="SchemaVersion"></param>
/// <param name="Theme"></param>
/// <param name="Window"></param>
/// <param name="Launcher"></param>
/// <param name="Shortcuts"></param>
/// <param name="HighContrast"></param>
/// <param name="ReducedMotion"></param>
/// <param name="LastSection"></param>
public sealed record ShellState(
    string SchemaVersion,
    ThemeMode Theme,
    WindowLayout Window,
    IReadOnlyList<LauncherItem> Launcher,
    IReadOnlyDictionary<string, string> Shortcuts,
    bool HighContrast,
    bool ReducedMotion,
    string LastSection)
{
    public static ShellState Default => new(
        "1.0",
        ThemeMode.System,
        WindowLayout.Default,
        [
            new LauncherItem("writer", "VCC Writer", "Documentos e texto estruturado", "W"),
            new LauncherItem("sheets", "VCC Sheets", "Folhas de cálculo e dados", "S"),
            new LauncherItem("slides", "VCC Slides", "Apresentações", "P"),
            new LauncherItem("settings", "Definições", "Configurar o Core Shell", "⚙")
        ],
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["open-launcher"] = "Ctrl+L",
            ["open-settings"] = "Ctrl+,"
        },
        false,
        false,
        "launcher");
}

/// <summary>
/// 
/// </summary>
/// <param name="Errors"></param>
public sealed record ValidationResult(IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
    public static ValidationResult Valid => new([]);
}

/// <summary>
/// 
/// </summary>
public static class ShellStateValidator
{
    public static ValidationResult Validate(ShellState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(state.SchemaVersion)) errors.Add("A versão do esquema é obrigatória.");
        if (state.Window.Width is < 640 or > 10000) errors.Add("A largura da janela deve estar entre 640 e 10000.");
        if (state.Window.Height is < 480 or > 10000) errors.Add("A altura da janela deve estar entre 480 e 10000.");
        if (state.Launcher.Count == 0) errors.Add("O Launcher deve conter pelo menos um módulo.");
        if (state.Launcher.Any(item => string.IsNullOrWhiteSpace(item.Id) || string.IsNullOrWhiteSpace(item.Title)))
            errors.Add("Cada item do Launcher deve ter ID e título.");
        if (state.Shortcuts.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value)))
            errors.Add("Os atalhos devem ter comando e combinação definidos.");
        return new ValidationResult(errors);
    }
}

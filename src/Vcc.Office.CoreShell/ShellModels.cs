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

public sealed record RibbonCommand(string Id, string Title, string Description, string Glyph);

public sealed record RibbonTab(string Id, string Title, IReadOnlyList<RibbonCommand> Commands);

public static class RibbonDefaults
{
    public static IReadOnlyList<RibbonTab> Tabs { get; } =
    [
        new RibbonTab("home", "Início", [
            new RibbonCommand("new", "Novo", "Criar um novo documento", "＋"),
            new RibbonCommand("open", "Abrir", "Abrir um ficheiro local", "↥"),
            new RibbonCommand("save", "Guardar", "Guardar o documento atual", "↓")
        ]),
        new RibbonTab("edit", "Editar", [
            new RibbonCommand("undo", "Desfazer", "Desfazer a última ação", "↶"),
            new RibbonCommand("redo", "Refazer", "Refazer a última ação", "↷")
        ])
    ];
}

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
/// Normaliza layouts persistidos antes de os entregar à camada de UI.
/// Mantém a janela acessível mesmo quando o estado foi criado num monitor
/// que já não está disponível.
/// </summary>
public static class WindowLayoutPolicy
{
    public const double MinimumWidth = 640;
    public const double MinimumHeight = 480;
    public const double MaximumWidth = 10000;
    public const double MaximumHeight = 10000;

    public static WindowLayout Normalize(
        WindowLayout layout,
        double workAreaLeft = 0,
        double workAreaTop = 0,
        double workAreaWidth = 1920,
        double workAreaHeight = 1080)
    {
        var width = ClampFinite(layout.Width, WindowLayout.Default.Width, MinimumWidth, MaximumWidth);
        var height = ClampFinite(layout.Height, WindowLayout.Default.Height, MinimumHeight, MaximumHeight);
        var areaWidth = ClampFinite(workAreaWidth, width, MinimumWidth, MaximumWidth);
        var areaHeight = ClampFinite(workAreaHeight, height, MinimumHeight, MaximumHeight);
        var left = FiniteOrDefault(layout.Left, workAreaLeft);
        var top = FiniteOrDefault(layout.Top, workAreaTop);

        // Keep at least 48px of the normal window on the selected work area.
        left = Math.Clamp(left, workAreaLeft - width + 48, workAreaLeft + areaWidth - 48);
        top = Math.Clamp(top, workAreaTop - height + 48, workAreaTop + areaHeight - 48);

        return new WindowLayout(width, height, left, top, layout.IsMaximized);
    }

    private static double ClampFinite(double value, double fallback, double minimum, double maximum)
        => double.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;

    private static double FiniteOrDefault(double value, double fallback)
        => double.IsFinite(value) ? value : fallback;
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
    public IReadOnlyList<RibbonTab> Ribbon { get; init; } = RibbonDefaults.Tabs;

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
        if (!double.IsFinite(state.Window.Width) || state.Window.Width is < WindowLayoutPolicy.MinimumWidth or > WindowLayoutPolicy.MaximumWidth)
            errors.Add("A largura da janela deve estar entre 640 e 10000.");
        if (!double.IsFinite(state.Window.Height) || state.Window.Height is < WindowLayoutPolicy.MinimumHeight or > WindowLayoutPolicy.MaximumHeight)
            errors.Add("A altura da janela deve estar entre 480 e 10000.");
        if (!double.IsFinite(state.Window.Left) || !double.IsFinite(state.Window.Top))
            errors.Add("A posição da janela deve ser finita.");
        if (state.Launcher.Count == 0) errors.Add("O Launcher deve conter pelo menos um módulo.");
        if (state.Launcher.Any(item => string.IsNullOrWhiteSpace(item.Id) || string.IsNullOrWhiteSpace(item.Title)))
            errors.Add("Cada item do Launcher deve ter ID e título.");
        if (state.Ribbon is null || state.Ribbon.Count == 0) errors.Add("O Ribbon deve conter pelo menos um separador.");
        else
        {
            if (state.Ribbon.Any(tab => string.IsNullOrWhiteSpace(tab.Id) || string.IsNullOrWhiteSpace(tab.Title)))
                errors.Add("Cada separador do Ribbon deve ter ID e título.");
            if (state.Ribbon.SelectMany(tab => tab.Commands).Any(command =>
                    string.IsNullOrWhiteSpace(command.Id) || string.IsNullOrWhiteSpace(command.Title)))
                errors.Add("Cada comando do Ribbon deve ter ID e título.");
        }
        if (state.Shortcuts.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value)))
            errors.Add("Os atalhos devem ter comando e combinação definidos.");
        return new ValidationResult(errors);
    }
}

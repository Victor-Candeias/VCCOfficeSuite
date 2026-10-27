namespace Vcc.Office.CoreShell;

/// <summary>
/// 
/// </summary>
public sealed class CoreShellService
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="stateStore"></param>
    public CoreShellService(ShellStateStore stateStore) => StateStore = stateStore;

    /// <summary>
    /// 
    /// </summary>
    public ShellStateStore StateStore { get; }

    /// <summary>
    /// 
    /// </summary>
    public ShellState State { get; private set; } = ShellState.Default;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task InitialiseAsync(CancellationToken cancellationToken = default)
        => State = await StateStore.LoadAsync(cancellationToken);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="update"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public async Task UpdateAsync(Func<ShellState, ShellState> update, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        var next = update(State);
        var validation = ShellStateValidator.Validate(next);
        if (!validation.IsValid) throw new ArgumentException(string.Join(" ", validation.Errors));
        await StateStore.SaveAsync(next, cancellationToken);
        State = next;
    }

    /// <summary>
    /// Substitui a configuração do Launcher depois de validar todos os módulos
    /// e persiste a alteração de forma atómica.
    /// </summary>
    public Task ConfigureLauncherAsync(
        IEnumerable<LauncherItem> items,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        var launcher = items.ToArray();
        var validation = LauncherPolicy.Validate(launcher);
        if (!validation.IsValid) throw new ArgumentException(string.Join(" ", validation.Errors), nameof(items));
        return UpdateAsync(state => state with { Launcher = launcher, LastSection = "launcher" }, cancellationToken);
    }

    /// <summary>
    /// Guarda a configuração da janela depois de passar pela validação do estado.
    /// </summary>
    public Task ConfigureWindowAsync(
        WindowLayout layout,
        CancellationToken cancellationToken = default)
        => UpdateAsync(state => state with { Window = layout, LastSection = "window" }, cancellationToken);

    /// <summary>
    /// Substitui a configuração do Ribbon depois de validar separadores e comandos.
    /// </summary>
    public Task ConfigureRibbonAsync(
        IEnumerable<RibbonTab> tabs,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tabs);
        var ribbon = tabs.ToArray();
        var validation = RibbonPolicy.Validate(ribbon);
        if (!validation.IsValid) throw new ArgumentException(string.Join(" ", validation.Errors), nameof(tabs));
        return UpdateAsync(state => state with { Ribbon = ribbon, LastSection = "ribbon" }, cancellationToken);
    }

    /// <summary>
    /// Guarda o mapa de atalhos depois de validar combinações e conflitos.
    /// </summary>
    public Task ConfigureShortcutsAsync(
        IReadOnlyDictionary<string, string> shortcuts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(shortcuts);
        var copy = new Dictionary<string, string>(shortcuts, StringComparer.OrdinalIgnoreCase);
        var validation = ShortcutCatalog.Validate(copy);
        if (!validation.IsValid) throw new ArgumentException(string.Join(" ", validation.Errors), nameof(shortcuts));
        return UpdateAsync(state => state with { Shortcuts = copy, LastSection = "shortcuts" }, cancellationToken);
    }

    /// <summary>
    /// Persiste o modo de tema selecionado pelo utilizador.
    /// </summary>
    public Task ConfigureThemeAsync(
        ThemeMode theme,
        CancellationToken cancellationToken = default)
    {
        var validation = ThemePolicy.Validate(theme);
        if (!validation.IsValid) throw new ArgumentException(string.Join(" ", validation.Errors), nameof(theme));
        return UpdateAsync(state => state with { Theme = theme, LastSection = "theme" }, cancellationToken);
    }
}

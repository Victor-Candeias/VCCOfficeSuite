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

    /// <summary>Valida e persiste módulos recebidos pelo contrato do Launcher.</summary>
    public Task ConfigureLauncherAsync(
        IEnumerable<LauncherItemDto> items,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        var launcher = items.Select(item => item is null
            ? throw new ArgumentException("Cada módulo do Launcher é obrigatório.", nameof(items))
            : item.ToModel()).ToArray();
        return ConfigureLauncherAsync(launcher, cancellationToken);
    }

    /// <summary>Regista o módulo consultado no Launcher depois de o validar.</summary>
    public Task NavigateLauncherAsync(string itemId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            throw new ArgumentException("Indique o módulo do Launcher.", nameof(itemId));
        var item = State.Launcher.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, itemId.Trim(), StringComparison.OrdinalIgnoreCase));
        if (item is null) throw new ArgumentException("O módulo selecionado não existe no Launcher.", nameof(itemId));
        return UpdateAsync(state => state with { LastSection = $"launcher:{item.Id}" }, cancellationToken);
    }

    /// <summary>
    /// Guarda a configuração da janela depois de passar pela validação do estado.
    /// </summary>
    public Task ConfigureWindowAsync(
        WindowLayout layout,
        CancellationToken cancellationToken = default)
        => UpdateAsync(state => state with { Window = layout, LastSection = "window" }, cancellationToken);

    /// <summary>Valida e persiste o layout recebido pelo contrato da janela.</summary>
    public Task ConfigureWindowAsync(
        WindowLayoutDto layout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(layout);
        return ConfigureWindowAsync(layout.ToModel(), cancellationToken);
    }

    /// <summary>Executa e persiste uma ação de navegação da janela.</summary>
    public Task NavigateWindowAsync(
        WindowNavigationAction action,
        double workAreaLeft,
        double workAreaTop,
        double workAreaWidth,
        double workAreaHeight,
        CancellationToken cancellationToken = default)
    {
        var layout = WindowNavigationPolicy.Apply(State.Window, action,
            workAreaLeft, workAreaTop, workAreaWidth, workAreaHeight);
        return UpdateAsync(state => state with { Window = layout, LastSection = $"window:{action}" }, cancellationToken);
    }

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

    /// <summary>Valida e persiste separadores recebidos pelo contrato do Ribbon.</summary>
    public Task ConfigureRibbonAsync(
        IEnumerable<RibbonTabDto> tabs,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tabs);
        var ribbon = tabs.Select(tab => tab is null
            ? throw new ArgumentException("Cada separador do Ribbon é obrigatório.", nameof(tabs))
            : tab.ToModel()).ToArray();
        return ConfigureRibbonAsync(ribbon, cancellationToken);
    }

    /// <summary>Regista o separador ou comando consultado no Ribbon.</summary>
    public Task NavigateRibbonAsync(string tabId, string? commandId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tabId))
            throw new ArgumentException("Indique o separador do Ribbon.", nameof(tabId));
        var tab = State.Ribbon.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, tabId.Trim(), StringComparison.OrdinalIgnoreCase));
        if (tab is null) throw new ArgumentException("O separador selecionado não existe no Ribbon.", nameof(tabId));
        if (!string.IsNullOrWhiteSpace(commandId) && !tab.Commands.Any(command =>
                string.Equals(command.Id, commandId.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("O comando selecionado não existe no separador.", nameof(commandId));
        var suffix = string.IsNullOrWhiteSpace(commandId) ? tab.Id : $"{tab.Id}:{commandId.Trim()}";
        return UpdateAsync(state => state with { LastSection = $"ribbon:{suffix}" }, cancellationToken);
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

    /// <summary>Persiste as preferências de acessibilidade.</summary>
    public Task ConfigureAccessibilityAsync(
        bool highContrast,
        bool reducedMotion,
        CancellationToken cancellationToken = default)
        => ConfigureAccessibilityAsync(new AccessibilityPreferencesDto(highContrast, reducedMotion), cancellationToken);

    /// <summary>Valida e persiste um contrato de preferências de acessibilidade.</summary>
    public Task ConfigureAccessibilityAsync(
        AccessibilityPreferencesDto preferences,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        var settings = preferences.ToModel();
        return UpdateAsync(state => state with
        {
            HighContrast = settings.HighContrast,
            ReducedMotion = settings.ReducedMotion,
            LastSection = AccessibilityContract.SectionName
        }, cancellationToken);
    }

    /// <summary>Persiste as opções de foco e ciclo da navegação.</summary>
    public Task ConfigureNavigationAsync(
        NavigationSettings navigation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        var validation = NavigationPolicy.Validate(navigation);
        if (!validation.IsValid) throw new ArgumentException(string.Join(" ", validation.Errors), nameof(navigation));
        return UpdateAsync(state => state with { Navigation = navigation, LastSection = "navigation" }, cancellationToken);
    }

    /// <summary>Valida e persiste a configuração de navegação recebida por DTO.</summary>
    public Task ConfigureNavigationAsync(
        NavigationSettingsDto navigation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        return ConfigureNavigationAsync(navigation.ToModel(), cancellationToken);
    }
}

using Vcc.Office.CoreShell;

/// <summary>
var tests = new (string Name, Func<Task> Run)[]
{
    ("estado predefinido é válido", () => AssertValid(ShellState.Default)),
    ("Ribbon predefinido é válido", ValidatesDefaultRibbon),
    ("estado inválido é rejeitado", RejectsInvalidState),
    ("layout de janela é normalizado", NormalizesWindowLayout),
    ("estado é guardado e recuperado", PersistsAndRecoversState),
    ("estado corrompido recupera do backup", RecoversFromBackup)
    ,("atalhos são normalizados e resolvidos", ResolvesShortcuts)
    ,("atalhos duplicados são rejeitados", RejectsConflictingShortcuts)
    ,("Launcher valida e persiste configuração", ConfiguresLauncher)
    ,("janela valida e persiste configuração", ConfiguresWindow)
    ,("Ribbon valida e persiste configuração", ConfiguresRibbon)
    ,("atalhos configuram e persistem", ConfiguresShortcuts)
    ,("tema configura e persiste", ConfiguresTheme)
    ,("acessibilidade configura e persiste", ConfiguresAccessibility)
    ,("navegação configura e persiste", ConfiguresNavigation)
    ,("Launcher é consultado e navegado", QueriesAndNavigatesLauncher)
    ,("janelas são consultadas e navegadas", NavigatesWindows)
    ,("Ribbon é consultado e navegado", QueriesAndNavigatesRibbon)
};

foreach (var test in tests)
{
    try { await test.Run(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}"); return 1; }
}

return 0;

/// <summary>
static Task AssertValid(ShellState state)
{
    if (!ShellStateValidator.Validate(state).IsValid) throw new InvalidOperationException("O estado predefinido não é válido.");
    return Task.CompletedTask;
}

static Task RejectsInvalidState()
{
    var invalid = ShellState.Default with { Window = new WindowLayout(100, 100, 0, 0, false) };
    if (ShellStateValidator.Validate(invalid).IsValid) throw new InvalidOperationException("Um estado inválido foi aceite.");
    return Task.CompletedTask;
}

static Task ValidatesDefaultRibbon()
{
    if (ShellState.Default.Ribbon.Count != 2 || ShellState.Default.Ribbon.SelectMany(tab => tab.Commands).Count() != 5)
        throw new InvalidOperationException("O Ribbon predefinido não contém os separadores e comandos esperados.");
    if (!ShellStateValidator.Validate(ShellState.Default).IsValid)
        throw new InvalidOperationException("O Ribbon predefinido não é válido.");
    return Task.CompletedTask;
}

static Task ResolvesShortcuts()
{
    var shortcuts = new Dictionary<string, string>
    {
        ["open-launcher"] = "ctrl + l",
        ["open-settings"] = "Ctrl+,"
    };

    if (ShortcutPolicy.Normalize(shortcuts["open-launcher"]) != "Ctrl+L")
        throw new InvalidOperationException("A normalização do atalho falhou.");
    if (!ShortcutCatalog.TryResolveCommand(shortcuts, "CTRL+L", out var command) || command != "open-launcher")
        throw new InvalidOperationException("O comando do atalho não foi resolvido.");
    if (!ShellStateValidator.Validate(ShellState.Default with { Shortcuts = shortcuts }).IsValid)
        throw new InvalidOperationException("Um conjunto válido de atalhos foi rejeitado.");
    return Task.CompletedTask;
}

static Task RejectsConflictingShortcuts()
{
    var state = ShellState.Default with
    {
        Shortcuts = new Dictionary<string, string>
        {
            ["first"] = "Ctrl+K",
            ["second"] = "ctrl + k"
        }
    };

    if (ShellStateValidator.Validate(state).IsValid)
        throw new InvalidOperationException("Atalhos conflitantes foram aceites.");
    return Task.CompletedTask;
}

static async Task ConfiguresLauncher()
{
    var directory = CreateTestDirectory();
    var store = new ShellStateStore(Path.Combine(directory, "shell.json"));
    var service = new CoreShellService(store);
    var item = new LauncherItem("calendar", "VCC Calendar", "Calendário local", "C");
    await service.ConfigureLauncherAsync(ShellState.Default.Launcher.Append(item));

    var loaded = await store.LoadAsync();
    if (loaded.Launcher.Count != 5 || loaded.Launcher[^1] != item || loaded.LastSection != "launcher")
        throw new InvalidOperationException("A configuração do Launcher não foi persistida.");

    try { await service.ConfigureLauncherAsync(ShellState.Default.Launcher.Append(item with { Id = "writer" })); }
    catch (ArgumentException) { Directory.Delete(directory, recursive: true); return; }
    throw new InvalidOperationException("Um ID duplicado de Launcher foi aceite.");
}

static async Task ConfiguresWindow()
{
    var directory = CreateTestDirectory();
    var store = new ShellStateStore(Path.Combine(directory, "shell.json"));
    var service = new CoreShellService(store);
    var layout = new WindowLayout(1024, 720, 100, 80, true);
    await service.ConfigureWindowAsync(layout);

    var loaded = await store.LoadAsync();
    if (loaded.Window != layout || loaded.LastSection != "window")
        throw new InvalidOperationException("A configuração da janela não foi persistida.");

    try { await service.ConfigureWindowAsync(new WindowLayout(100, 100, 0, 0, false)); }
    catch (ArgumentException) { Directory.Delete(directory, recursive: true); return; }
    throw new InvalidOperationException("Uma dimensão inválida de janela foi aceite.");
}

static async Task ConfiguresRibbon()
{
    var directory = CreateTestDirectory();
    var store = new ShellStateStore(Path.Combine(directory, "shell.json"));
    var service = new CoreShellService(store);
    var tab = new RibbonTab("review", "Revisão", [new RibbonCommand("comment", "Comentário", "Adicionar comentário", "C")]);
    await service.ConfigureRibbonAsync(ShellState.Default.Ribbon.Append(tab));

    var loaded = await store.LoadAsync();
    var loadedTab = loaded.Ribbon[^1];
    if (loaded.Ribbon.Count != 3 || loadedTab.Id != tab.Id || loadedTab.Title != tab.Title ||
        loadedTab.Commands.Count != 1 || loadedTab.Commands[0].Id != tab.Commands[0].Id || loaded.LastSection != "ribbon")
        throw new InvalidOperationException("A configuração do Ribbon não foi persistida.");

    try { await service.ConfigureRibbonAsync(ShellState.Default.Ribbon.Append(tab with { Id = "home" })); }
    catch (ArgumentException) { Directory.Delete(directory, recursive: true); return; }
    throw new InvalidOperationException("Um ID duplicado de Ribbon foi aceite.");
}

static async Task ConfiguresShortcuts()
{
    var directory = CreateTestDirectory();
    var store = new ShellStateStore(Path.Combine(directory, "shell.json"));
    var service = new CoreShellService(store);
    var shortcuts = new Dictionary<string, string>(ShellState.Default.Shortcuts, StringComparer.OrdinalIgnoreCase)
    {
        ["open-launcher"] = "Ctrl+Shift+L"
    };
    await service.ConfigureShortcutsAsync(shortcuts);

    var loaded = await store.LoadAsync();
    if (loaded.Shortcuts["open-launcher"] != "Ctrl+Shift+L" || loaded.LastSection != "shortcuts")
        throw new InvalidOperationException("A configuração dos atalhos não foi persistida.");

    try
    {
        await service.ConfigureShortcutsAsync(new Dictionary<string, string>
        {
            ["first"] = "Ctrl+K",
            ["second"] = "Ctrl+K"
        });
    }
    catch (ArgumentException) { Directory.Delete(directory, recursive: true); return; }
    throw new InvalidOperationException("Atalhos conflitantes foram aceites.");
}

static async Task ConfiguresTheme()
{
    var directory = CreateTestDirectory();
    var store = new ShellStateStore(Path.Combine(directory, "shell.json"));
    var service = new CoreShellService(store);
    await service.ConfigureThemeAsync(ThemeMode.Dark);

    var loaded = await store.LoadAsync();
    if (loaded.Theme != ThemeMode.Dark || loaded.LastSection != "theme")
        throw new InvalidOperationException("A configuração do tema não foi persistida.");

    try { await service.ConfigureThemeAsync((ThemeMode)99); }
    catch (ArgumentException) { Directory.Delete(directory, recursive: true); return; }
    throw new InvalidOperationException("Um modo de tema inválido foi aceite.");
}

static async Task ConfiguresAccessibility()
{
    var directory = CreateTestDirectory();
    var store = new ShellStateStore(Path.Combine(directory, "shell.json"));
    var service = new CoreShellService(store);
    await service.ConfigureAccessibilityAsync(highContrast: true, reducedMotion: true);

    var loaded = await store.LoadAsync();
    if (!loaded.HighContrast || !loaded.ReducedMotion || loaded.LastSection != "accessibility")
        throw new InvalidOperationException("As preferências de acessibilidade não foram persistidas.");
    if (!ShellStateValidator.Validate(loaded).IsValid)
        throw new InvalidOperationException("O estado com acessibilidade configurada não é válido.");
    Directory.Delete(directory, recursive: true);
}

static async Task ConfiguresNavigation()
{
    var directory = CreateTestDirectory();
    var store = new ShellStateStore(Path.Combine(directory, "shell.json"));
    var service = new CoreShellService(store);
    await service.ConfigureNavigationAsync(new NavigationSettings(NavigationFocusTarget.Ribbon, true));

    var loaded = await store.LoadAsync();
    if (loaded.Navigation.InitialFocus != NavigationFocusTarget.Ribbon || !loaded.Navigation.WrapFocus ||
        loaded.LastSection != "navigation")
        throw new InvalidOperationException("As preferências de navegação não foram persistidas.");

    try { await service.ConfigureNavigationAsync(new NavigationSettings((NavigationFocusTarget)99, false)); }
    catch (ArgumentException) { Directory.Delete(directory, recursive: true); return; }
    throw new InvalidOperationException("Um foco inicial de navegação inválido foi aceite.");
}

static async Task QueriesAndNavigatesLauncher()
{
    var items = ShellState.Default.Launcher;
    if (LauncherPolicy.Query(items, "texto").Count != 1 ||
        LauncherPolicy.Query(items, "VCC").Count != 3 ||
        LauncherPolicy.Query(items, "inexistente").Count != 0)
        throw new InvalidOperationException("A consulta do Launcher devolveu resultados incorretos.");

    var directory = CreateTestDirectory();
    var service = new CoreShellService(new ShellStateStore(Path.Combine(directory, "shell.json")));
    await service.InitialiseAsync();
    await service.NavigateLauncherAsync("writer");
    if (service.State.LastSection != "launcher:writer")
        throw new InvalidOperationException("A navegação do Launcher não foi persistida.");

    try { await service.NavigateLauncherAsync("missing"); }
    catch (ArgumentException) { Directory.Delete(directory, recursive: true); return; }
    throw new InvalidOperationException("Um módulo inexistente foi aceite pelo Launcher.");
}

static async Task NavigatesWindows()
{
    var centered = WindowNavigationPolicy.Apply(
        new WindowLayout(800, 600, 0, 0, false), WindowNavigationAction.Center, 0, 0, 1920, 1080);
    if (centered.Left != 560 || centered.Top != 240 || centered.IsMaximized)
        throw new InvalidOperationException("A janela não foi centrada corretamente.");

    var directory = CreateTestDirectory();
    var service = new CoreShellService(new ShellStateStore(Path.Combine(directory, "shell.json")));
    await service.InitialiseAsync();
    await service.NavigateWindowAsync(WindowNavigationAction.Maximize, 0, 0, 1920, 1080);
    if (!service.State.Window.IsMaximized || service.State.LastSection != "window:Maximize")
        throw new InvalidOperationException("A maximização da janela não foi persistida.");

    try { await service.NavigateWindowAsync((WindowNavigationAction)99, 0, 0, 1920, 1080); }
    catch (ArgumentException) { Directory.Delete(directory, recursive: true); return; }
    throw new InvalidOperationException("Uma ação inválida de janela foi aceite.");
}

static async Task QueriesAndNavigatesRibbon()
{
    var tabs = ShellState.Default.Ribbon;
    if (RibbonPolicy.Query(tabs, "guardar").Count != 1 ||
        RibbonPolicy.Query(tabs, "edit")[0].Commands.Count != 2 ||
        RibbonPolicy.Query(tabs, "inexistente").Count != 0)
        throw new InvalidOperationException("A consulta do Ribbon devolveu resultados incorretos.");

    var directory = CreateTestDirectory();
    var service = new CoreShellService(new ShellStateStore(Path.Combine(directory, "shell.json")));
    await service.InitialiseAsync();
    await service.NavigateRibbonAsync("home", "save");
    if (service.State.LastSection != "ribbon:home:save")
        throw new InvalidOperationException("A navegação do Ribbon não foi persistida.");

    try { await service.NavigateRibbonAsync("missing"); }
    catch (ArgumentException) { Directory.Delete(directory, recursive: true); return; }
    throw new InvalidOperationException("Um separador inexistente foi aceite pelo Ribbon.");
}

static Task NormalizesWindowLayout()
{
    var normalized = WindowLayoutPolicy.Normalize(
        new WindowLayout(100, double.NaN, -2000, 2000, true),
        workAreaLeft: 0,
        workAreaTop: 0,
        workAreaWidth: 1920,
        workAreaHeight: 1080);

    if (normalized.Width != WindowLayoutPolicy.MinimumWidth || normalized.Height != WindowLayout.Default.Height)
        throw new InvalidOperationException("As dimensões inválidas não foram normalizadas.");
    if (normalized.Left < -normalized.Width + 48 || normalized.Top > 1080 - 48)
        throw new InvalidOperationException("A posição normalizada deixou a janela inacessível.");
    if (!normalized.IsMaximized) throw new InvalidOperationException("O estado maximizado não foi preservado.");
    return Task.CompletedTask;
}

static async Task PersistsAndRecoversState()
{
    var directory = CreateTestDirectory();
    var store = new ShellStateStore(Path.Combine(directory, "shell.json"));
    var state = ShellState.Default with { Theme = ThemeMode.Dark, LastSection = "settings" };
    await store.SaveAsync(state);
    var loaded = await store.LoadAsync();
    if (loaded.Theme != ThemeMode.Dark || loaded.LastSection != "settings" || loaded.Ribbon.Count != 2)
        throw new InvalidOperationException("O estado recuperado não corresponde ao guardado.");
    Directory.Delete(directory, recursive: true);
}

static async Task RecoversFromBackup()
{
    var directory = CreateTestDirectory();
    var store = new ShellStateStore(Path.Combine(directory, "shell.json"));
    await store.SaveAsync(ShellState.Default with { Theme = ThemeMode.Light });
    await store.SaveAsync(ShellState.Default with { Theme = ThemeMode.Dark });
    await File.WriteAllTextAsync(store.StatePath, "{ invalid json");
    var loaded = await store.LoadAsync();
    if (loaded.Theme != ThemeMode.Light) throw new InvalidOperationException("O backup não foi usado após corrupção.");
    Directory.Delete(directory, recursive: true);
}

static string CreateTestDirectory()
{
    var path = Path.Combine(Path.GetTempPath(), "vcc-office-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(path);
    return path;
}

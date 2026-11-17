using Vcc.Office.CoreShell;
using System.Text;

/// <summary>
var tests = new (string Name, Func<Task> Run)[]
{
    ("estado predefinido é válido", () => AssertValid(ShellState.Default)),
    ("VCC-0815 registo de 26 módulos e disponibilidade", ValidatesOfficeApplicationRegistry),
    ("VCC-0816 sessão de aplicações: persistência, recuperação e filtros", ValidatesOfficeSessionRecovery),
    ("VCC-0817 documentos recentes, favoritos, filtros e remoção", TestsRecentDocuments),
    ("VCC-0818 sessão do Launcher Avalonia: persistência e recuperação", TestsDesktopLauncherState),
    ("VCC-0820 importa preferências WPF de dashboard sem alterar a origem", TestsLegacyDashboardPreferences),
    ("dashboards resolvem arranque e validação", ValidatesDashboardPreferences),
    ("Ribbon predefinido é válido", ValidatesDefaultRibbon),
    ("estado inválido é rejeitado", RejectsInvalidState),
    ("layout de janela é normalizado", NormalizesWindowLayout),
    ("estado é guardado e recuperado", PersistsAndRecoversState),
    ("estado corrompido recupera do backup", RecoversFromBackup)
    ,("atalhos são normalizados e resolvidos", ResolvesShortcuts)
    ,("atalhos duplicados são rejeitados", RejectsConflictingShortcuts)
    ,("Launcher valida e persiste configuração", ConfiguresLauncher)
    ,("contrato DTO do Launcher é convertido e validado", ValidatesLauncherContract)
    ,("contrato DTO da janela é convertido e validado", ValidatesWindowContract)
    ,("contrato DTO do Ribbon é convertido e validado", ValidatesRibbonContract)
    ,("contrato DTO de atalhos é convertido e validado", ValidatesShortcutContract)
    ,("contrato DTO de tema é convertido e validado", ValidatesThemeContract)
    ,("contrato DTO de acessibilidade é convertido e validado", ValidatesAccessibilityContract)
    ,("contrato DTO de navegação é convertido e validado", ValidatesNavigationContract)
    ,("janela valida e persiste configuração", ConfiguresWindow)
    ,("Ribbon valida e persiste configuração", ConfiguresRibbon)
    ,("atalhos configuram e persistem", ConfiguresShortcuts)
    ,("tema configura e persiste", ConfiguresTheme)
    ,("acessibilidade configura e persiste", ConfiguresAccessibility)
    ,("navegação configura e persiste", ConfiguresNavigation)
    ,("Launcher é consultado e navegado", QueriesAndNavigatesLauncher)
    ,("VCC-0820.7 módulos personalizados são filtrados e pesquisados sem simular instalação", TestsConfiguredLauncherPolicy)
    ,("janelas são consultadas e navegadas", NavigatesWindows)
    ,("Ribbon é consultado e navegado", QueriesAndNavigatesRibbon)
    ,("SQLite aplica migrações e persiste definições", PersistsDatabaseSetting)
    ,("ficheiros são escritos atomicamente", WritesAtomicFiles)
    ,("autosave cria snapshot recuperável", CreatesRecoverySnapshot)
    ,("Core Integration valida e consulta contratos", ValidatesCoreIntegration)
    ,("WebView2 bridge aplica origem e método autorizados", ValidatesBridgeSecurity)
    ,("Core Integration persiste estado completo", PersistsCoreIntegration)
    ,("Core Integration recupera estado e expõe estados de interface", LoadsCoreIntegration)
    ,("Core Delivery valida CI/CD, instalação, atualização e testes", ValidatesCoreDelivery)
    ,("telemetria local remove dados sensíveis", ProtectsTelemetry)
    ,("licenças são validadas e consultadas", ValidatesLicensing)
    ,("Writer cria, edita, navega e persiste documentos", ExercisesWriter)
    ,("Writer exporta PDF e preserva acessibilidade", ExportsWriter)
    ,("Writer DOCX: ida e volta Unicode, parágrafos e XML", RoundTripsWriterDocx)
    ,("Sheets calcula, filtra, persiste e exporta CSV", ExercisesSheets)
    ,("Sheets seleciona, recalcula, copia e limpa intervalos", ExercisesSheetRanges)
    ,("Sheets exporta PDF e Slides cria, apresenta e persiste", ExercisesSlides)
    ,("Slides aplica transições e exporta pacotes; Mail compõe, envia e pesquisa", ExercisesSlidesAndMail)
    ,("VCC-0820 Slides adiciona diapositivos, edita títulos e recupera estado", TestsSlidesAvaloniaWorkflow)
    ,("VCC-0820 WPF e Avalonia partilham estado Workspace sem migração destrutiva", TestsLegacyWorkspaceParity)
    ,("Data cria tabelas, importa CSV, consulta e exporta", ExercisesData)
    ,("Data guarda formulários e executa relatórios", ExercisesDataFormsAndReports)
    ,("Avalonia reutiliza Core Data SQLite entre sessões", ExercisesAvaloniaDataCore)
};

foreach (var test in tests)
{
    try { await test.Run(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}"); return 1; }
}

return 0;

/// <summary>

static Task ValidatesDashboardPreferences()
{
    var registry = new DashboardRegistry();
    if (registry.GetAll().Count != 3) throw new InvalidOperationException("O catálogo deve ter três dashboards.");
    var defaults = DashboardPreferences.Default;
    if (DashboardPolicy.ResolveStartup(defaults, registry) != DashboardIds.Modern)
        throw new InvalidOperationException("No primeiro arranque deve abrir o dashboard moderno.");

    var selected = DashboardPolicy.Select(defaults, DashboardIds.Grid, registry);
    if (DashboardPolicy.ResolveStartup(selected, registry) != DashboardIds.Grid)
        throw new InvalidOperationException("O modo last não recordou o dashboard utilizado.");
    var alwaysPreferred = selected with { StartupMode = DashboardStartupMode.Preferred };
    if (DashboardPolicy.ResolveStartup(alwaysPreferred, registry) != DashboardIds.Modern)
        throw new InvalidOperationException("O modo preferred não foi respeitado.");

    var invalid = selected with { LastDashboardId = "removed", PreferredDashboardId = "removed" };
    if (DashboardPolicy.ResolveStartup(invalid, registry) != DashboardIds.Modern)
        throw new InvalidOperationException("O fallback moderno não foi aplicado.");
    var normalized = DashboardPolicy.ValidateOrDefault(invalid, registry);
    if (normalized.LastDashboardId is not null || normalized.PreferredDashboardId != DashboardIds.Modern)
        throw new InvalidOperationException("As referências inválidas não foram normalizadas.");

    try { DashboardPolicy.Select(selected, "../invalid", registry); }
    catch (ArgumentException) { return Task.CompletedTask; }
    throw new InvalidOperationException("Foi aceite um dashboard não registado.");
}

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

    await service.ConfigureLauncherAsync(ShellState.Default.Launcher.Select(LauncherItemDto.FromModel));
    loaded = await store.LoadAsync();
    if (loaded.Launcher.Count != ShellState.Default.Launcher.Count)
        throw new InvalidOperationException("O contrato DTO do Launcher não foi persistido.");

    try { await service.ConfigureLauncherAsync(ShellState.Default.Launcher.Append(item with { Id = "writer" })); }
    catch (ArgumentException) { Directory.Delete(directory, recursive: true); return; }
    throw new InvalidOperationException("Um ID duplicado de Launcher foi aceite.");
}

static Task ValidatesLauncherContract()
{
    var dto = new LauncherItemDto("calendar", "VCC Calendar", "Calendário local", "C");
    var model = dto.ToModel();
    if (model != dto.ToModel() || LauncherItemDto.FromModel(model) != dto)
        throw new InvalidOperationException("A conversão do DTO do Launcher perdeu dados.");
    if (!LauncherPolicy.Validate([dto]).IsValid)
        throw new InvalidOperationException("Um DTO válido do Launcher foi rejeitado.");

    var tooLongTitle = dto with { Title = new string('x', LauncherContract.MaxTitleLength + 1) };
    if (LauncherPolicy.Validate([tooLongTitle]).IsValid)
        throw new InvalidOperationException("Um título acima do limite foi aceite.");

    var padded = dto with { Id = " calendar " };
    if (LauncherPolicy.Validate([padded, dto]).IsValid)
        throw new InvalidOperationException("IDs equivalentes com espaços foram aceites.");
    return Task.CompletedTask;
}

static Task ValidatesWindowContract()
{
    var dto = new WindowLayoutDto(1024, 720, 100, 80, true);
    var model = dto.ToModel();
    if (WindowLayoutDto.FromModel(model) != dto)
        throw new InvalidOperationException("A conversão do DTO da janela perdeu dados.");
    if (!WindowLayoutPolicy.Validate(model).IsValid)
        throw new InvalidOperationException("Um layout válido foi rejeitado.");

    var invalid = dto with { Width = WindowLayoutPolicy.MinimumWidth - 1 };
    if (WindowLayoutPolicy.Validate(invalid.ToModel()).IsValid)
        throw new InvalidOperationException("Uma largura abaixo do limite foi aceite.");

    var nonFinite = dto with { Top = double.NaN };
    if (WindowLayoutPolicy.Validate(nonFinite.ToModel()).IsValid)
        throw new InvalidOperationException("Uma posição não finita foi aceite.");
    return Task.CompletedTask;
}

static Task ValidatesRibbonContract()
{
    var command = new RibbonCommandDto("comment", "Comentário", "Adicionar comentário", "C");
    var dto = new RibbonTabDto("review", "Revisão", [command]);
    var model = dto.ToModel();
    var roundTrip = RibbonTabDto.FromModel(model);
    if (roundTrip.Id != dto.Id || roundTrip.Title != dto.Title ||
        roundTrip.Commands.Count != 1 || roundTrip.Commands[0] != command)
        throw new InvalidOperationException("A conversão do DTO do Ribbon perdeu dados.");
    if (!RibbonPolicy.Validate([dto]).IsValid)
        throw new InvalidOperationException("Um DTO válido do Ribbon foi rejeitado.");

    var tooLong = dto with { Title = new string('x', RibbonContract.MaxTitleLength + 1) };
    if (RibbonPolicy.Validate([tooLong]).IsValid)
        throw new InvalidOperationException("Um título de separador acima do limite foi aceite.");

    var duplicatedCommand = dto with
    {
        Commands = [command, command with { Title = "Outra ação" }]
    };
    if (RibbonPolicy.Validate([duplicatedCommand]).IsValid)
        throw new InvalidOperationException("Comandos com o mesmo ID foram aceites.");
    return Task.CompletedTask;
}

static Task ValidatesShortcutContract()
{
    var dto = new ShortcutBindingDto("open-launcher", "ctrl + shift + l");
    if (ShortcutBindingDto.FromPair(dto.ToPair()) != dto)
        throw new InvalidOperationException("A conversão do DTO de atalho perdeu dados.");
    if (!ShortcutCatalog.Validate([dto]).IsValid)
        throw new InvalidOperationException("Um DTO de atalho válido foi rejeitado.");
    if (ShortcutPolicy.Normalize(dto.Gesture) != "Ctrl+Shift+L")
        throw new InvalidOperationException("A normalização do DTO de atalho falhou.");

    if (ShortcutPolicy.IsValid("Ctrl+Unknown+K"))
        throw new InvalidOperationException("Um token desconhecido foi aceite no atalho.");
    if (ShortcutCatalog.Validate([dto, dto with { Gesture = "Alt+L" }]).IsValid)
        throw new InvalidOperationException("Comandos repetidos no DTO foram aceites.");
    return Task.CompletedTask;
}

static Task ValidatesThemeContract()
{
    var dto = new ThemeSelectionDto("dark");
    if (!ThemePolicy.Validate(dto).IsValid || dto.ToModel() != ThemeMode.Dark)
        throw new InvalidOperationException("Um DTO de tema válido foi rejeitado.");
    if (ThemeSelectionDto.FromModel(ThemeMode.Light).Mode != "Light")
        throw new InvalidOperationException("A conversão do tema não preservou o modo.");
    if (ThemePolicy.Validate(new ThemeSelectionDto("unknown")).IsValid)
        throw new InvalidOperationException("Um tema desconhecido foi aceite.");
    try { _ = new ThemeSelectionDto("unknown").ToModel(); }
    catch (ArgumentException) { return Task.CompletedTask; }
    throw new InvalidOperationException("A conversão de tema inválido não falhou.");
}

static Task ValidatesAccessibilityContract()
{
    var dto = new AccessibilityPreferencesDto(true, false);
    var model = dto.ToModel();
    if (AccessibilityPreferencesDto.FromModel(model) != dto || !model.HighContrast || model.ReducedMotion)
        throw new InvalidOperationException("A conversão do DTO de acessibilidade perdeu dados.");
    if (!AccessibilityPolicy.Validate(dto).IsValid)
        throw new InvalidOperationException("Um DTO de acessibilidade válido foi rejeitado.");

    try { _ = AccessibilityPolicy.Validate((AccessibilityPreferencesDto)null!); }
    catch (ArgumentNullException) { return Task.CompletedTask; }
    throw new InvalidOperationException("Um DTO de acessibilidade nulo foi aceite.");
}

static Task ValidatesNavigationContract()
{
    var dto = new NavigationSettingsDto(NavigationFocusTarget.Ribbon, true);
    if (NavigationSettingsDto.FromModel(dto.ToModel()) != dto || !NavigationPolicy.Validate(dto).IsValid)
        throw new InvalidOperationException("A conversão do DTO de navegação falhou.");

    if (NavigationPolicy.Validate(new NavigationSettingsDto((NavigationFocusTarget)99, false)).IsValid)
        throw new InvalidOperationException("Um foco inicial inválido foi aceite pelo DTO.");
    try { _ = new NavigationSettingsDto((NavigationFocusTarget)99, false).ToModel(); }
    catch (ArgumentException) { return Task.CompletedTask; }
    throw new InvalidOperationException("A conversão de navegação inválida não falhou.");
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

    await service.ConfigureWindowAsync(WindowLayoutDto.FromModel(layout));
    loaded = await store.LoadAsync();
    if (loaded.Window != layout)
        throw new InvalidOperationException("O contrato DTO da janela não foi persistido.");

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

    await service.ConfigureRibbonAsync(ShellState.Default.Ribbon.Select(RibbonTabDto.FromModel));
    loaded = await store.LoadAsync();
    if (loaded.Ribbon.Count != ShellState.Default.Ribbon.Count)
        throw new InvalidOperationException("O contrato DTO do Ribbon não foi persistido.");

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
    await service.ConfigureAccessibilityAsync(new AccessibilityPreferencesDto(false, true));
    loaded = await store.LoadAsync();
    if (loaded.HighContrast || !loaded.ReducedMotion || loaded.LastSection != AccessibilityContract.SectionName)
        throw new InvalidOperationException("O DTO de acessibilidade não foi persistido.");
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

    await service.ConfigureNavigationAsync(new NavigationSettingsDto(NavigationFocusTarget.Launcher, false));
    loaded = await store.LoadAsync();
    if (loaded.Navigation != NavigationSettings.Default)
        throw new InvalidOperationException("O contrato DTO de navegação não foi persistido.");

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

static async Task PersistsDatabaseSetting()
{
    var directory = CreateTestDirectory();
    var database = new CoreDatabase(new StoragePaths(directory));
    await database.InitialiseAsync();
    await database.SetSettingAsync("theme", "{\"mode\":\"dark\"}");
    if (await database.GetSettingAsync("theme") != "{\"mode\":\"dark\"}")
        throw new InvalidOperationException("A definição não foi persistida na base SQLite.");
    await database.InitialiseAsync();
    if (!File.Exists(database.DatabasePath)) throw new InvalidOperationException("A base SQLite não foi criada.");
    Directory.Delete(directory, recursive: true);
}

static async Task WritesAtomicFiles()
{
    var directory = CreateTestDirectory();
    var path = Path.Combine(directory, "nested", "document.bin");
    var files = new AtomicFileStore();
    await files.WriteAsync(path, "conteúdo local"u8.ToArray());
    var content = await files.ReadAsync(path);
    if (Encoding.UTF8.GetString(content) != "conteúdo local" || File.Exists(path + ".tmp"))
        throw new InvalidOperationException("A gravação atómica não produziu o ficheiro esperado.");
    Directory.Delete(directory, recursive: true);
}

static async Task CreatesRecoverySnapshot()
{
    var directory = CreateTestDirectory();
    var recovery = new RecoveryStore(new StoragePaths(directory));
    var session = await recovery.SaveSnapshotAsync("Writer", "documento.vcc", "rascunho"u8.ToArray());
    var restored = await recovery.ReadSnapshotAsync(session);
    if (session.Status != "pending" || Encoding.UTF8.GetString(restored) != "rascunho")
        throw new InvalidOperationException("O snapshot de recuperação não pode ser restaurado.");
    Directory.Delete(directory, recursive: true);
}

static async Task ValidatesCoreIntegration()
{
    var service = new CoreIntegrationService();
    await service.ConfigurePluginAsync(new PluginContractDto("writer", "VCC Writer", "1.0", "Vcc.Writer"));
    await service.ConfigureEventAsync(new EventDefinitionDto("document.saved", "Documento guardado", "object"));
    if (service.Query<PluginContract>("writer").Count != 1 || service.Query<EventDefinition>("saved").Count != 1)
        throw new InvalidOperationException("A consulta de Core Integration não devolveu os contratos esperados.");
    try { await service.ConfigurePluginAsync(new PluginContractDto("writer", "Duplicado", "1.0", "Vcc.Writer")); }
    catch (ArgumentException) { return; }
    throw new InvalidOperationException("Um ID duplicado foi aceite.");
}

static Task ValidatesBridgeSecurity()
{
    var service = new CoreIntegrationService();
    service.ConfigureBridgeAsync(new WebViewBridgeDto("shell", "https://office.local", ["open", "save"])).GetAwaiter().GetResult();
    if (!service.HandleBridgeRequest(new BridgeRequest("shell", "https://office.local", "save", "{}" )).Accepted ||
        service.HandleBridgeRequest(new BridgeRequest("shell", "https://evil.local", "save", "{}")).Accepted ||
        service.HandleBridgeRequest(new BridgeRequest("shell", "https://office.local", "delete", "{}")).Accepted)
        throw new InvalidOperationException("A política do bridge aceitou uma origem ou método não autorizado.");
    return Task.CompletedTask;
}

static async Task PersistsCoreIntegration()
{
    var directory = CreateTestDirectory();
    var service = new CoreIntegrationService(Path.Combine(directory, "integration.json"));
    await service.ConfigureWorkspaceAsync(new WorkspaceDefinitionDto("default", "Principal", directory));
    await service.ConfigurePermissionAsync(new PermissionGrantDto("writer-save", "writer", "document", ["save"]));
    await service.SaveAsync();
    var json = await File.ReadAllTextAsync(Path.Combine(directory, "integration.json"));
    if (!json.Contains("writer-save", StringComparison.Ordinal) || !json.Contains("default", StringComparison.Ordinal))
        throw new InvalidOperationException("O estado de Core Integration não foi persistido.");
    if (!service.HasPermission("writer", "document", "save")) throw new InvalidOperationException("A permissão persistida não foi reconhecida.");
    Directory.Delete(directory, recursive: true);
}

static async Task LoadsCoreIntegration()
{
    var directory = CreateTestDirectory();
    var path = Path.Combine(directory, "integration.json");
    var original = new CoreIntegrationService(path);
    await original.ConfigurePluginAsync(new PluginContractDto("writer", "VCC Writer", "1.0", "Vcc.Writer"));
    var restored = new CoreIntegrationService(path);
    await restored.LoadAsync();
    if (restored.Query<PluginContract>().Count != 1 || restored.GetState("writer") != IntegrationItemState.Ready || restored.GetState("missing") != IntegrationItemState.Draft)
        throw new InvalidOperationException("O estado de Core Integration não foi recuperado corretamente.");
    Directory.Delete(directory, recursive: true);
}

static async Task ValidatesCoreDelivery()
{
    var directory = CreateTestDirectory();
    var service = new CoreDeliveryService(Path.Combine(directory, "delivery.json"));
    await service.ConfigureCiCdAsync(new CiCdDefinitionDto("build", "Build local", "dotnet build", directory));
    await service.ConfigureInstallationAsync(new InstallationDefinitionDto("install", "VCC Office", "1.0", directory));
    await service.ConfigureUpdateAsync(new UpdateDefinitionDto("update", "VCC Office", "1.0", "1.1", "offline-cache"));
    await service.ConfigureTestAsync(new TestDefinitionDto("smoke", "Smoke", "dotnet test", directory));
    if (service.GetState("build") != DeliveryItemState.Ready) throw new InvalidOperationException("O estado do pipeline CI/CD não ficou pronto.");
    try { await service.ConfigureCiCdAsync(new CiCdDefinitionDto("bad", "", "", "relative")); }
    catch (ArgumentException) { Directory.Delete(directory, recursive: true); return; }
    throw new InvalidOperationException("Uma definição de CI/CD inválida foi aceite.");
}

static async Task ProtectsTelemetry()
{
    var directory = CreateTestDirectory();
    var service = new CoreDeliveryService(Path.Combine(directory, "delivery.json"));
    await service.RecordTelemetryAsync("build.finished", new Dictionary<string, string>
    {
        ["duration_ms"] = "12", ["token"] = "must-not-be-written", ["result"] = "success"
    });
    var json = await File.ReadAllTextAsync(Path.Combine(directory, "delivery.json"));
    if (json.Contains("must-not-be-written", StringComparison.Ordinal) || !json.Contains("build.finished", StringComparison.Ordinal))
        throw new InvalidOperationException("A telemetria local não filtrou dados sensíveis corretamente.");
    Directory.Delete(directory, recursive: true);
}

static async Task ValidatesLicensing()
{
    var directory = CreateTestDirectory();
    var service = new CoreDeliveryService(Path.Combine(directory, "delivery.json"));
    await service.ConfigureLicenseAsync(new LicenseGrantDto("writer-license", "VCC Writer", "local-user", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)), ["edit", "export-pdf"]));
    if (!service.HasLicense("VCC Writer", "local-user", "export-pdf") || service.Query<LicenseGrant>("writer-license").Count != 1)
        throw new InvalidOperationException("A licença não foi consultada ou validada corretamente.");
    Directory.Delete(directory, recursive: true);
}

static async Task ExercisesWriter()
{
    var directory = CreateTestDirectory();
    var service = new VccWriterService(Path.Combine(directory, "writer.json"));
    await service.CreateAsync("doc-1", "Documento de teste");
    await service.SetRichTextAsync("doc-1", "Título\nConteúdo local");
    await service.AddStyleAsync("doc-1", new WriterStyle("Heading", "Aptos Display", 18, true));
    await service.AddTableAsync("doc-1", new WriterTable("table-1", ["Campo", "Valor"], [["Estado", "Pronto"]]));
    await service.AddImageAsync("doc-1", new WriterImage("image-1", "logo.png", "Logótipo VCC"));
    await service.AddReviewAsync("doc-1", new WriterReviewNote("review-1", 1, "Rever texto"));
    if (service.GetState("doc-1") != WriterDocumentState.Ready || service.Query("Conteúdo").Count != 1 || service.Navigate("doc-1").Tables.Count != 1)
        throw new InvalidOperationException("O fluxo de edição e navegação do Writer falhou.");
    var restored = new VccWriterService(Path.Combine(directory, "writer.json"));
    await restored.LoadAsync();
    if (restored.Documents.Count != 1 || !restored.Documents[0].AccessibilityEnabled) throw new InvalidOperationException("O documento Writer não foi recuperado.");
    Directory.Delete(directory, recursive: true);
}

static async Task ExportsWriter()
{
    var directory = CreateTestDirectory();
    var service = new VccWriterService();
    await service.CreateAsync("doc-1", "PDF local");
    await service.SetRichTextAsync("doc-1", "Conteúdo exportado");
    await service.SetAccessibilityAsync("doc-1", true);
    var pdf = Path.Combine(directory, "document.pdf");
    await service.ExportPdfAsync("doc-1", pdf);
    var bytes = await File.ReadAllBytesAsync(pdf);
    if (Encoding.ASCII.GetString(bytes, 0, 8) != "%PDF-1.4") throw new InvalidOperationException("A exportação PDF não produziu um documento PDF.");
    Directory.Delete(directory, recursive: true);
}

static async Task ExercisesSheets()
{
    var directory = CreateTestDirectory();
    var service = new VccSheetsService(Path.Combine(directory, "sheets.json"));
    await service.CreateAsync("sheet-1", "Dados");
    await service.SetValueAsync("sheet-1", "A1", "10");
    await service.SetValueAsync("sheet-1", "A2", "20");
    await service.SetFormulaAsync("sheet-1", "A3", "=SUM(A1:A2)");
    await service.SetFormatAsync("sheet-1", "A3", "0.00");
    await service.AddTableAsync("sheet-1", new SheetTable("table-1", new SheetRange(new SheetCellAddress(1, 1), new SheetCellAddress(3, 1))));
    await service.SaveAsync();
    if (service.GetValue("sheet-1", "A3") != "30" || service.Filter("sheet-1", new SheetRange(new SheetCellAddress(1, 1), new SheetCellAddress(3, 1)), "20").Count != 1)
        throw new InvalidOperationException("O cálculo ou filtro do Sheets falhou.");
    var csv = Path.Combine(directory, "dados.csv");
    await service.ExportCsvAsync("sheet-1", csv);
    if (!File.ReadAllText(csv).Contains("30", StringComparison.Ordinal)) throw new InvalidOperationException("A exportação CSV falhou.");
    var restored = new VccSheetsService(Path.Combine(directory, "sheets.json"));
    await restored.LoadAsync();
    if (restored.GetValue("sheet-1", "A3") != "30") throw new InvalidOperationException("A persistência do Sheets falhou.");
    Directory.Delete(directory, recursive: true);
}

static async Task ExercisesSheetRanges()
{
    var service = new VccSheetsService();
    await service.CreateAsync("sheet-1", "Operações");
    await service.SetValueAsync("sheet-1", "A1", "5");
    await service.SetFormulaAsync("sheet-1", "B1", "=A1+7");
    var selection = service.Select("sheet-1", new SheetRange(new SheetCellAddress(1, 1), new SheetCellAddress(1, 2)), new SheetCellAddress(1, 2));
    if (selection.ActiveCell.ToString() != "B1" || service.Recalculate("sheet-1")["B1"] != "12")
        throw new InvalidOperationException("A seleção ou o recálculo do intervalo falhou.");
    await service.CopyRangeAsync("sheet-1", selection.Range, new SheetCellAddress(2, 1));
    if (service.GetValue("sheet-1", "B2") != "12") throw new InvalidOperationException("A cópia do intervalo falhou.");
    await service.ClearRangeAsync("sheet-1", new SheetRange(new SheetCellAddress(2, 1), new SheetCellAddress(2, 2)));
    if (service.GetValue("sheet-1", "B2") != "") throw new InvalidOperationException("A limpeza do intervalo falhou.");
    try { service.Select("sheet-1", new SheetRange(new SheetCellAddress(2, 2), new SheetCellAddress(1, 1))); }
    catch (ArgumentException) { return; }
    throw new InvalidOperationException("Um intervalo invertido foi aceite.");
}

static async Task ExercisesSlides()
{
    var directory = CreateTestDirectory();
    var service = new VccSlidesService(Path.Combine(directory, "slides.json"));
    await service.CreateAsync("deck-1", "Apresentação VCC");
    await service.AddObjectAsync("deck-1", 0, new SlideObject("title", SlideObjectKind.Text, 24, 24, 640, 80, "Olá VCC"));
    await service.SetNotesAsync("deck-1", 0, "Notas do apresentador");
    await service.SetThemeAsync("deck-1", new SlideTheme("Escuro", "#111827", "#F9FAFB"));
    await service.NavigatePresentationAsync("deck-1", 0);
    var html = Path.Combine(directory, "slides.html");
    await service.ExportHtmlAsync("deck-1", html);
    await service.SaveAsync();
    var restored = new VccSlidesService(Path.Combine(directory, "slides.json"));
    await restored.LoadAsync();
    if (!File.ReadAllText(html).Contains("Ol&#225; VCC", StringComparison.Ordinal) || restored.Navigate("deck-1").Slides[0].Notes != "Notas do apresentador")
        throw new InvalidOperationException("A apresentação não foi exportada ou persistida corretamente.");
    var pdfService = new VccSheetsService();
    await pdfService.CreateAsync("sheet-1", "Dados");
    await pdfService.SetValueAsync("sheet-1", "A1", "42");
    var pdf = Path.Combine(directory, "dados.pdf");
    await pdfService.ExportPdfAsync("sheet-1", pdf);
    if (!Encoding.ASCII.GetString(await File.ReadAllBytesAsync(pdf), 0, 8).Equals("%PDF-1.4", StringComparison.Ordinal)) throw new InvalidOperationException("A exportação PDF do Sheets falhou.");
    Directory.Delete(directory, recursive: true);
}

static async Task ExercisesSlidesAndMail()
{
    var directory = CreateTestDirectory();
    var slides = new VccSlidesService(Path.Combine(directory, "slides.json"));
    await slides.CreateAsync("deck-1", "Demo");
    await slides.SetTransitionAsync("deck-1", 0, new SlideTransition("push", 700));
    var pptx = Path.Combine(directory, "demo.pptx");
    await slides.ExportPptxAsync("deck-1", pptx);
    if (!File.Exists(pptx) || new FileInfo(pptx).Length == 0) throw new InvalidOperationException("A exportação PPTX falhou.");
    var mail = new VccMailService(Path.Combine(directory, "mail.json"));
    await mail.ConfigureAccountAsync(new MailAccount("local", "user@example.com", "Utilizador", "imap.local", "smtp.local"));
    await mail.ComposeAsync(new MailMessage("message-1", "local", "user@example.com", ["team@example.com"], "Assunto", "Mensagem offline"));
    await mail.AddAttachmentAsync("message-1", new MailAttachment("file-1", "nota.txt", 12, "text/plain", "nota.txt"));
    await mail.SendAsync("message-1");
    if (mail.Search("offline", MailFolder.Sent).Count != 1 || !mail.Open("message-1").IsRead) throw new InvalidOperationException("O fluxo local de Mail falhou.");
    var restored = new VccMailService(Path.Combine(directory, "mail.json"));
    await restored.LoadAsync();
    if (restored.Messages.Count != 1 || restored.Messages[0].Attachments?.Count != 1) throw new InvalidOperationException("A persistência de Mail falhou.");
    Directory.Delete(directory, recursive: true);
}

static async Task ExercisesData()
{
    var directory = CreateTestDirectory();
    try
    {
        var service = new VccDataService(new CoreDatabase(StoragePaths.ForLocalApplication(directory)));
        await service.CreateTableAsync(new DataTableDefinition("contacts", "Contactos", [new("id", "INTEGER", false), new("name", "TEXT", false)]));
        await service.InsertRowAsync("contacts", new Dictionary<string, object?> { ["id"] = 1, ["name"] = "Ana, VCC" });
        if (await service.ImportCsvAsync("contacts", "id,name\n2,Bruno\n") != 1) throw new InvalidOperationException("A importação CSV falhou.");
        var result = await service.QueryAsync("SELECT id, name FROM contacts ORDER BY id");
        if (result.Rows.Count != 2 || result.Rows[1]["name"]?.ToString() != "Bruno") throw new InvalidOperationException("A consulta de dados falhou.");
        if (! (await service.ExportCsvAsync("SELECT name FROM contacts ORDER BY id")).Contains("Ana, VCC", StringComparison.Ordinal)) throw new InvalidOperationException("A exportação CSV falhou.");
        await service.CreateRelationAsync(new DataRelationDefinition("contact_self", "contacts", "id", "contacts", "id"));
    }
    finally { Directory.Delete(directory, true); }
}

static async Task ExercisesDataFormsAndReports()
{
    var directory = CreateTestDirectory();
    try
    {
        var service = new VccDataService(new CoreDatabase(StoragePaths.ForLocalApplication(directory)));
        await service.CreateTableAsync(new DataTableDefinition("sales", "Vendas", [new("id", "INTEGER", false), new("total", "REAL", false), new("customer", "TEXT", false)]));
        var form = new DataFormDefinition("sale_form", "sales", "Nova venda", [new("id", "ID", "INTEGER"), new("total", "Total", "REAL", true), new("customer", "Cliente", "TEXT", true)]);
        await service.CreateFormAsync(form);
        var stored = (await service.ListFormsAsync()).Single();
        await service.SubmitFormAsync(stored, new Dictionary<string, object?> { ["id"] = 1, ["total"] = 12.5, ["customer"] = "Ana" });
        await service.CreateReportAsync(new DataReportDefinition("sales_report", "Vendas", "SELECT customer, total FROM sales ORDER BY id"));
        var result = await service.RunReportAsync((await service.ListReportsAsync()).Single());
        if (result.Rows.Count != 1 || result.Rows[0]["customer"]?.ToString() != "Ana") throw new InvalidOperationException("O relatório não devolveu os dados esperados.");
        await AssertThrows<ArgumentException>(() => service.SubmitFormAsync(stored, new Dictionary<string, object?> { ["id"] = 2, ["total"] = null, ["customer"] = "" }));
    }
    finally { Directory.Delete(directory, true); }
}

static async Task AssertThrows<TException>(Func<Task> action) where TException : Exception
{
    try { await action(); }
    catch (TException) { return; }
    throw new InvalidOperationException($"Era esperada a exceção {typeof(TException).Name}.");
}

static string CreateTestDirectory()
{
    var path = Path.Combine(Path.GetTempPath(), "vcc-office-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(path);
    return path;
}

static Task ValidatesOfficeApplicationRegistry()
{
    var registry = new OfficeApplicationRegistry();
    var all = registry.GetAll();
    if (all.Count != 26 || all.Select(m => m.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 26)
        throw new Exception("O catálogo deve conter 26 módulos únicos.");
    var manager = new OfficeApplicationManager(registry);
    if (!manager.TryResolve("DATA", out var data) || data?.Id != "data")
        throw new Exception("VCC Data deve abrir no Launcher.");
    if (manager.TryResolve("writer", out _) || manager.TryResolve("desconhecido", out _))
        throw new Exception("Módulos sem interface não podem ser anunciados como disponíveis.");
    registry.Register(new OfficeModule("writer", "VCC Writer", true));
    if (!manager.TryResolve("writer", out _))
        throw new Exception("O registo dinâmico deve permitir disponibilizar futuros módulos.");
    try
    {
        registry.Register(new OfficeModule("unknown", "Inexistente", true));
        throw new Exception("Foi aceite um módulo desconhecido.");
    }
    catch (ArgumentException) { }
    return Task.CompletedTask;
}


static Task ValidatesOfficeSessionRecovery()
{
    var directory = CreateTestDirectory();
    try
    {
        var registry = new OfficeApplicationRegistry();
        var store = new OfficeSessionStore(Path.Combine(directory, "application-session.json"));
        if (store.Load(registry).OpenModuleIds.Count != 0)
            throw new Exception("Uma primeira execução deve começar sem janelas de módulos.");

        var session = OfficeSessionPolicy.Open(OfficeSession.Empty, "DATA", registry);
        session = OfficeSessionPolicy.Open(session, "data", registry);
        if (session.OpenModuleIds.Count != 1 || session.LastModuleId != "data")
            throw new Exception("O mesmo módulo não pode originar sessões duplicadas.");
        store.Save(session, registry);
        var restored = store.Load(registry);
        if (restored.LastModuleId != "data" || restored.OpenModuleIds.Single() != "data")
            throw new Exception("Não foi possível recuperar a última aplicação aberta.");

        var closed = OfficeSessionPolicy.Close(restored, "DATA", registry);
        store.Save(closed, registry);
        if (store.Load(registry).OpenModuleIds.Count != 0 ||
            store.Load(registry).LastModuleId != "data")
            throw new Exception("Uma janela fechada não deve reabrir, mas a última aplicação deve ser recordada.");

        registry.Register(new OfficeModule("writer", "VCC Writer", true));
        var multi = OfficeSessionPolicy.Open(closed, "writer", registry);
        multi = OfficeSessionPolicy.Open(multi, "data", registry);
        store.Save(multi, registry);
        var newRegistry = new OfficeApplicationRegistry();
        var available = store.Load(newRegistry);
        if (available.OpenModuleIds.Count != 1 || available.OpenModuleIds[0] != "data")
            throw new Exception("Módulos que deixaram de estar disponíveis não devem ser restaurados.");

        // Um JSON danificado deve recuperar a versão anterior guardada no backup.
        File.WriteAllText(store.FilePath, "{ inválido");
        var recovered = store.Load(newRegistry);
        if (recovered.OpenModuleIds.Count != 0 || recovered.LastModuleId != "data")
            throw new Exception("A sessão não foi recuperada do backup.");

        try
        {
            OfficeSessionPolicy.Open(OfficeSession.Empty, "slides", newRegistry);
            throw new Exception("Um editor não implementado não pode entrar na sessão.");
        }
        catch (ArgumentException) { }
    }
    finally { Directory.Delete(directory, true); }
    return Task.CompletedTask;
}

static async Task TestsRecentDocuments()
{
    var root = CreateTestDirectory();
    try
    {
        var database = new CoreDatabase(StoragePaths.ForLocalApplication(root));
        var documents = new RecentDocumentsService(database);
        var docx = Path.Combine(root, "Relatório.docx");
        var xlsx = Path.Combine(root, "Tabela.xlsx");
        File.WriteAllText(docx, "documento de teste");
        File.WriteAllText(xlsx, "tabela de teste");

        if (RecentDocumentsService.ResolveModule(docx) != "writer" ||
            RecentDocumentsService.ResolveModule(xlsx) != "sheets" ||
            RecentDocumentsService.ResolveModule("teste.ext") is not null)
            throw new Exception("Associações de formatos incorretas.");

        await documents.RegisterAsync(docx);
        await documents.RegisterAsync(xlsx);
        await documents.RegisterAsync(docx);
        var recent = await documents.QueryAsync();
        if (recent.Count != 2 || recent[0].FileName != "Relatório.docx")
            throw new Exception("Recentes não devem duplicar ficheiros e devem estar ordenados.");
        if ((await documents.QueryAsync(moduleId: "sheets")).Count != 1 ||
            (await documents.QueryAsync(search: "relatório")).Count != 1)
            throw new Exception("Filtros por aplicação e pesquisa falharam.");

        await documents.SetFavoriteAsync(xlsx, true);
        if ((await documents.QueryAsync(favoritesOnly: true)).Single().FullPath != xlsx)
            throw new Exception("O favorito não foi guardado.");
        var reopened = new RecentDocumentsService(new CoreDatabase(StoragePaths.ForLocalApplication(root)));
        if ((await reopened.QueryAsync(favoritesOnly: true)).Count != 1)
            throw new Exception("A persistência SQLite não sobreviveu à nova instância.");

        File.Delete(xlsx);
        if ((await reopened.QueryAsync(favoritesOnly: true)).Single().Exists)
            throw new Exception("Deveria ser sinalizado um ficheiro eliminado.");

        await reopened.RemoveAsync(xlsx);
        if ((await reopened.QueryAsync(favoritesOnly: true)).Count != 0)
            throw new Exception("O ficheiro não foi removido do catálogo.");

        await AssertThrows<FileNotFoundException>(() => reopened.RegisterAsync(xlsx));
    }
    finally { Directory.Delete(root, true); }
}

static async Task TestsDesktopLauncherState()
{
    var root = CreateTestDirectory();
    try
    {
        var database = new CoreDatabase(StoragePaths.ForLocalApplication(root));
        var dashboards = new DashboardRegistry();
        var modules = new OfficeApplicationRegistry();
        var store = new DesktopLauncherStateStore(database, dashboards, modules);
        var initial = await store.LoadAsync();
        if (initial != DesktopLauncherState.Default)
            throw new Exception("O Launcher Avalonia deve começar com preferências predefinidas.");

        var path = Path.Combine(root, "Exemplo.docx");
        File.WriteAllText(path, "Documento de teste");
        var selected = new DesktopLauncherState(
            DashboardIds.Grid, ThemeMode.Dark, "WRITER", path);
        await store.SaveAsync(selected);

        var recovered = await new DesktopLauncherStateStore(
            new CoreDatabase(StoragePaths.ForLocalApplication(root)), dashboards, modules).LoadAsync();
        if (recovered.DashboardId != DashboardIds.Grid ||
            recovered.Theme != ThemeMode.Dark ||
            recovered.LastSelectedModuleId != "writer" ||
            recovered.LastOpenedDocumentPath != path)
            throw new Exception("A sessão Avalonia não recuperou dashboard, tema, módulo e último documento.");

        // Every dashboard and theme mode must roundtrip through the shared SQLite store.
        foreach (var dashboard in new[] { DashboardIds.Modern, DashboardIds.Classic, DashboardIds.Grid })
        {
            foreach (var theme in new[] { ThemeMode.System, ThemeMode.Light, ThemeMode.Dark })
            {
                await store.SaveAsync(new DesktopLauncherState(dashboard, theme, "data", path));
                var persisted = await new DesktopLauncherStateStore(
                    new CoreDatabase(StoragePaths.ForLocalApplication(root)), dashboards, modules).LoadAsync();
                if (persisted.DashboardId != dashboard || persisted.Theme != theme ||
                    persisted.LastSelectedModuleId != "data")
                    throw new Exception("Falha ao recuperar uma combinação dashboard/tema.");
            }
        }

        // Um módulo pode estar registado mas ainda não ter interface Avalonia:
        // recordar a seleção não implica abrir uma janela fictícia.
        var invalid = new DesktopLauncherState(
            "desconhecido", (ThemeMode)999, "modulo-inexistente", " ");
        await store.SaveAsync(invalid);
        var normalized = await store.LoadAsync();
        if (normalized.DashboardId != DashboardIds.Modern ||
            normalized.Theme != ThemeMode.System ||
            normalized.LastSelectedModuleId is not null ||
            normalized.LastOpenedDocumentPath is not null)
            throw new Exception("O estado inválido do Launcher deve ser normalizado.");

        await database.SetSettingAsync("avalonia.launcher.session.v1", "{corrompido");
        if (await store.LoadAsync() != DesktopLauncherState.Default)
            throw new Exception("Um JSON de sessão corrompido deve recuperar valores predefinidos.");

        // Sessões e documentos partilham CoreDatabase sem alterar documentos locais.
        var docs = new RecentDocumentsService(database);
        await docs.RegisterAsync(path);
        await docs.SetFavoriteAsync(path, true);
        if ((await docs.QueryAsync(favoritesOnly: true)).Single().FullPath != path ||
            File.ReadAllText(path) != "Documento de teste")
            throw new Exception("A migração não pode afetar o conteúdo dos documentos.");
    }
    finally
    {
        Directory.Delete(root, true);
    }
}

static async Task ExercisesAvaloniaDataCore()
{
    var directory = CreateTestDirectory();
    try
    {
        var paths = StoragePaths.ForLocalApplication(directory);
        var first = new VccDataService(new CoreDatabase(paths));
        await first.CreateTableAsync(new DataTableDefinition(
            "avalonia_clients", "Clientes Avalonia",
            [new("code", "INTEGER", false), new("name", "TEXT", false)]));
        await first.InsertRowAsync("avalonia_clients",
            new Dictionary<string, object?> { ["code"] = 1, ["name"] = "Teste" });
        var second = new VccDataService(new CoreDatabase(paths));
        var tables = await second.ListTablesAsync();
        if (!tables.Any(t => t.Name == "avalonia_clients" && t.Columns.Count == 2))
            throw new Exception("As tabelas não persistiram no Core partilhado.");

        var preview = await second.QueryAsync("SELECT code, name FROM avalonia_clients ORDER BY code");
        if (preview.Rows.Count != 1 ||
            Convert.ToString(preview.Rows[0]["name"]) != "Teste")
            throw new Exception("A consulta do Data após reabrir a sessão falhou.");

        await second.CreateFormAsync(new DataFormDefinition(
            "avalonia_form", "avalonia_clients", "Novo cliente",
            [new("code", "Código", "INTEGER", true), new("name", "Nome", "TEXT", true)]));
        var form = (await second.ListFormsAsync()).Single(f => f.Name == "avalonia_form");
        await second.SubmitFormAsync(form,
            new Dictionary<string, object?> { ["code"] = 2L, ["name"] = "Outro" });
        await second.CreateReportAsync(new DataReportDefinition(
            "avalonia_report", "Clientes", "SELECT code, name FROM avalonia_clients ORDER BY code"));
        var report = (await second.ListReportsAsync()).Single(r => r.Name == "avalonia_report");
        if ((await second.RunReportAsync(report)).Rows.Count != 2)
            throw new Exception("O formulário/relatório partilhados não foram persistidos.");
        var exported = await second.ExportCsvAsync("SELECT code, name FROM avalonia_clients ORDER BY code");
        if (!exported.Contains("Outro", StringComparison.Ordinal))
            throw new Exception("A exportação CSV do Data falhou.");

        try
        {
            await second.QueryAsync("DELETE FROM avalonia_clients");
            throw new Exception("A consulta de escrita devia ser recusada.");
        }
        catch (ArgumentException) { }
    }
    finally { Directory.Delete(directory, true); }
}

static async Task TestsSlidesAvaloniaWorkflow()
{
    var directory = CreateTestDirectory();
    try
    {
        var path = Path.Combine(directory, "slides-avalonia.json");
        var service = new VccSlidesService(path);
        var deck = await service.CreateAsync("slides-0820", "Verificação Avalonia");
        if (deck.Slides.Count != 1) throw new Exception("A apresentação inicial deve ter um diapositivo.");
        await service.SetSlideTitleAsync(deck.Id, 0, "Capa atualizada");
        var next = await service.AddSlideAsync(deck.Id, "Agenda");
        await service.SetNotesAsync(deck.Id, 1, "Notas do segundo diapositivo");
        await service.AddObjectAsync(deck.Id, 1, new SlideObject("texto-1", SlideObjectKind.Text, 15, 30, 420, 80, "Agenda"));
        if (next.Slides.Count != 2 || service.Navigate(deck.Id).Slides[0].Title != "Capa atualizada")
            throw new Exception("Criação e edição dos diapositivos falharam.");
        var recovered = new VccSlidesService(path);
        await recovered.LoadAsync();
        var loaded = recovered.Navigate(deck.Id);
        if (loaded.Slides.Count != 2 || loaded.CurrentSlide != 1 ||
            loaded.Slides[1].Notes != "Notas do segundo diapositivo" ||
            loaded.Slides[1].Objects.Single().Content != "Agenda")
            throw new Exception("O estado dos Slides não foi preservado.");
    }
    finally { Directory.Delete(directory, recursive: true); }
}

static async Task RoundTripsWriterDocx()
{
    var directory = CreateTestDirectory();
    try
    {
        var path = Path.Combine(directory, "writer-roundtrip.docx");
        var service = new VccWriterService();
        await service.CreateAsync("docx-a", "Teste DOCX");
        await service.SetRichTextAsync("docx-a", "Olá, mundo!\n\nÁrvore & ciência <2026>\nÚltima linha");
        await service.ExportDocxAsync("docx-a", path);
        using (var archive = System.IO.Compression.ZipFile.OpenRead(path))
        {
            if (archive.GetEntry("[Content_Types].xml") is null ||
                archive.GetEntry("_rels/.rels") is null ||
                archive.GetEntry("word/document.xml") is null)
                throw new InvalidOperationException("Ficheiro DOCX sem estrutura OpenXML obrigatória.");
        }
        var imported = await new VccWriterService().ImportDocxAsync("docx-b", "Recuperado", path);
        var original = service.Navigate("docx-a");
        if (!original.Paragraphs.Select(p => p.Text).SequenceEqual(imported.Paragraphs.Select(p => p.Text)))
            throw new InvalidOperationException("Parágrafos Unicode não sobreviveram ao DOCX.");
    }
    finally { Directory.Delete(directory, recursive: true); }
}

static async Task TestsLegacyWorkspaceParity()
{
    var directory = CreateTestDirectory();
    try
    {
        var path = Path.Combine(directory, "VCC Office", "shell-state.json");
        var legacy = new CoreShellService(new ShellStateStore(path));
        await legacy.InitialiseAsync();
        await legacy.ConfigureRibbonAsync([.. legacy.State.Ribbon,
            new RibbonTab("custom", "Personalizado", [
                new RibbonCommand("print", "Imprimir", "Personalizado", "P")])]);
        await legacy.ConfigureShortcutsAsync(new Dictionary<string, string>(legacy.State.Shortcuts)
        {
            ["print"] = "Ctrl+Shift+P"
        });
        await legacy.ConfigureNavigationAsync(new NavigationSettings(NavigationFocusTarget.Ribbon, true));
        await legacy.ConfigureAccessibilityAsync(highContrast: true, reducedMotion: true);
        await legacy.ConfigureWindowAsync(new WindowLayout(1024, 768, 70, 80, false));
        var avalonia = new CoreShellService(new ShellStateStore(path));
        await avalonia.InitialiseAsync();
        if (!avalonia.State.HighContrast || !avalonia.State.ReducedMotion ||
            avalonia.State.Navigation.InitialFocus != NavigationFocusTarget.Ribbon ||
            !avalonia.State.Navigation.WrapFocus ||
            avalonia.State.Window.Width != 1024 ||
            !avalonia.State.Ribbon.Any(tab => tab.Id == "custom") ||
            !ShortcutCatalog.TryResolveCommand(avalonia.State.Shortcuts, "Ctrl+Shift+P", out var cmd) ||
            cmd != "print")
            throw new Exception("O estado do Workspace WPF não foi recuperado pela versão Avalonia.");
        if (File.ReadAllText(path).Contains("System.Windows", StringComparison.Ordinal))
            throw new Exception("O estado persistido contém dependências de WPF.");
    }
    finally { Directory.Delete(directory, recursive: true); }
}

static async Task TestsLegacyDashboardPreferences()
{
    var root = CreateTestDirectory();
    try
    {
        var legacyPath = Path.Combine(root, "legacy-dashboards.sqlite");
        var cs = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
        {
            DataSource = legacyPath,
            Pooling = false
        };
        using (var connection = new Microsoft.Data.Sqlite.SqliteConnection(cs.ToString()))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE DashboardSettings (
                    UserId TEXT PRIMARY KEY, PreferredDashboardId TEXT NOT NULL,
                    LastDashboardId TEXT, StartupMode TEXT NOT NULL, Theme TEXT NOT NULL);
                INSERT INTO DashboardSettings (UserId,PreferredDashboardId,LastDashboardId,StartupMode,Theme)
                VALUES ('test-user','grid','classic','preferred','dark');
                """;
            command.ExecuteNonQuery();
        }
        var dashboards = new DashboardRegistry();
        var registry = new OfficeApplicationRegistry();
        var database = new CoreDatabase(new StoragePaths(Path.Combine(root, "avalonia")));
        var stateStore = new DesktopLauncherStateStore(database, dashboards, registry,
            legacyPath, "test-user");
        var imported = await stateStore.LoadAsync();
        if (imported.DashboardId != DashboardIds.Classic ||
            imported.PreferredDashboardId != DashboardIds.Grid ||
            imported.StartupMode != DashboardStartupMode.Preferred ||
            imported.Theme != ThemeMode.Dark ||
            DesktopLauncherStatePolicy.ResolveStartup(imported, dashboards) != DashboardIds.Grid)
            throw new InvalidOperationException("A migração das preferências WPF alterou a seleção inicial.");
        await stateStore.SaveAsync(imported with { StartupMode = DashboardStartupMode.Last });
        var recovered = await stateStore.LoadAsync();
        if (DesktopLauncherStatePolicy.ResolveStartup(recovered, dashboards) != DashboardIds.Classic)
            throw new InvalidOperationException("O modo último dashboard não foi respeitado.");
        using (var connection = new Microsoft.Data.Sqlite.SqliteConnection(cs.ToString()))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT StartupMode || ':' || Theme FROM DashboardSettings WHERE UserId='test-user'";
            if ((string?)command.ExecuteScalar() != "preferred:dark")
                throw new InvalidOperationException("As preferências WPF originais foram alteradas.");
        }
    }
    finally { Directory.Delete(root, recursive: true); }
}

static async Task TestsConfiguredLauncherPolicy()
{
    var directory = CreateTestDirectory();
    try
    {
        var shell = new CoreShellService(new ShellStateStore(
            Path.Combine(directory, "shell-state.json")));
        await shell.InitialiseAsync();
        await shell.ConfigureLauncherAsync([
            .. shell.State.Launcher,
            new LauncherItem("vcc-tasks", "VCC Tasks", "Gestão de tarefas", "✓"),
            new LauncherItem("vcc-inventory", "Inventário", "Controlo de stock", "□")
        ]);

        var restored = new CoreShellService(new ShellStateStore(
            Path.Combine(directory, "shell-state.json")));
        await restored.InitialiseAsync();
        var registry = new OfficeApplicationRegistry();
        var custom = ConfiguredLauncherPolicy.CustomItems(restored.State.Launcher, registry);
        if (custom.Count != 2 || custom[0].Id != "vcc-tasks" ||
            custom[1].Id != "vcc-inventory")
            throw new InvalidOperationException("Os módulos personalizados não foram recuperados em ordem.");

        var filtered = ConfiguredLauncherPolicy.CustomItems(
            restored.State.Launcher, registry, "TAREFAS");
        if (filtered.Count != 1 || filtered[0].Id != "vcc-tasks")
            throw new InvalidOperationException("Pesquisa por descrição falhou.");

        if (!ConfiguredLauncherPolicy.IsBuiltIn("WRITER", registry) ||
            !ConfiguredLauncherPolicy.IsBuiltIn("settings", registry) ||
            ConfiguredLauncherPolicy.IsBuiltIn("vcc-tasks", registry))
            throw new InvalidOperationException("A distinção entre módulos internos e personalizados falhou.");

        await restored.NavigateLauncherAsync("vcc-tasks");
        if (restored.State.LastSection != "launcher:vcc-tasks")
            throw new InvalidOperationException("A seleção do módulo personalizado não foi persistida.");
    }
    finally { Directory.Delete(directory, recursive: true); }
}

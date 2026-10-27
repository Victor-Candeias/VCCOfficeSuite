using Vcc.Office.CoreShell;
using System.Text;

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

static string CreateTestDirectory()
{
    var path = Path.Combine(Path.GetTempPath(), "vcc-office-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(path);
    return path;
}

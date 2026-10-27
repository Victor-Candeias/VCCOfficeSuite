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

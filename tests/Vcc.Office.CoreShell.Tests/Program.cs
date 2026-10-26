using Vcc.Office.CoreShell;

var tests = new (string Name, Func<Task> Run)[]
{
    ("estado predefinido é válido", () => AssertValid(ShellState.Default)),
    ("estado inválido é rejeitado", RejectsInvalidState),
    ("estado é guardado e recuperado", PersistsAndRecoversState),
    ("estado corrompido recupera do backup", RecoversFromBackup)
};

foreach (var test in tests)
{
    try { await test.Run(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}"); return 1; }
}

return 0;

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

static async Task PersistsAndRecoversState()
{
    var directory = CreateTestDirectory();
    var store = new ShellStateStore(Path.Combine(directory, "shell.json"));
    var state = ShellState.Default with { Theme = ThemeMode.Dark, LastSection = "settings" };
    await store.SaveAsync(state);
    var loaded = await store.LoadAsync();
    if (loaded.Theme != ThemeMode.Dark || loaded.LastSection != "settings") throw new InvalidOperationException("O estado recuperado não corresponde ao guardado.");
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

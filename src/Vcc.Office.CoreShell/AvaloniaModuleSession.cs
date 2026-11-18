namespace Vcc.Office.CoreShell;

/// <summary>
/// Partilha o contrato de sessões WPF, mas preserva o ficheiro original ao
/// importar a última sessão para um ficheiro próprio da interface Avalonia.
/// </summary>
public static class AvaloniaModuleSession
{
    /// <summary>
    /// Session availability comes from the discovered plugin registry and the
    /// currently available built-in applications, never hardcoded module names.
    /// </summary>
    public static OfficeApplicationRegistry AvailableModules(IEnumerable<OfficeModule> available)
    {
        ArgumentNullException.ThrowIfNull(available);
        var registry = new OfficeApplicationRegistry();
        foreach (var module in available.Where(module => module.IsAvailable))
            registry.RegisterPlugin(module);
        return registry;
    }

    /// <summary>
    /// A sessão Avalonia tem precedência. Se ainda não existir, importa a
    /// sessão WPF em modo leitura e nunca reescreve o ficheiro de origem.
    /// </summary>
    public static OfficeSession Load(
        OfficeSessionStore avalonia,
        OfficeSessionStore legacy,
        OfficeApplicationRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(avalonia);
        ArgumentNullException.ThrowIfNull(legacy);
        ArgumentNullException.ThrowIfNull(registry);
        var source = File.Exists(avalonia.FilePath) ||
                     File.Exists(avalonia.FilePath + ".bak") ? avalonia : legacy;
        return source.Load(registry);
    }
}
